//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Sign, upload a macOS build for notarization and staple the ticket.
/// </summary>
/// <remarks>
/// From macOS 10.15 Catalina it's required to sign and notarize builds
/// to pass Gate Keeper checks and to be able to open them without going
/// through the Open context menu.
/// 
/// If you don't use IL2CPP, the following entitlement is required:
/// com.apple.security.cs.allow-unsigned-executable-memory
/// 
/// If you're using the Steam API, the following entitlements are required:
/// com.apple.security.cs.allow-dyld-environment-variables
/// com.apple.security.cs.disable-library-validation
/// </remarks>
[CreateAssetMenu(fileName = "Notarization Distro.asset", menuName = "Trimmer/Mac Notarization", order = 100)]
public class NotarizationDistro : DistroBase
{
    [Header("Signing")]

    /// <summary>
    /// The identity to sign the app with.
    /// </summary>
    public string appSignIdentity;
    /// <summary>
    /// The entitlements file.
    /// </summary>
    public DefaultAsset entitlements;

    [Header("Notarization")]

    /// <summary>
    /// Id use to identify the product, does not need to match app bundle id.
    /// </summary>
    public string primaryBundleId;
    /// <summary>
    /// App Store Connect login.
    /// </summary>
    [Keychain(keychainService)] public Login ascLogin;
    const string keychainService = "NotarizationDistro";
    /// <summary>
    /// App Store Connect provider ID (only required if account is part of multiple teams).
    /// </summary>
    public string ascProvider;

    /// <summary>
    /// Interval in seconds between request status checks.
    /// </summary>
    const float statusCheckInterval = 30;

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        foreach (var buildPath in buildPaths) {
            if (buildPath.target != BuildTarget.StandaloneOSX)
                continue;
            await Notarize(buildPath, task);
        }
    }

    /// <summary>
    /// Notarize a macOS build.
    /// </summary>
    /// <remarks>
    /// This method will silently ignore non-macOS builds.
    /// </remarks>
    /// <param name="buildPath">Build path</param>
    public async Task NotarizeIfMac(BuildPath buildPath, TaskToken task)
    {
        if (buildPath.target == BuildTarget.StandaloneOSX) {
            var child = task.StartChild("Notarize macOS Build");
            try {
                await Notarize(buildPath, child);
            } finally {
                child.Remove();
            }
        }
    }

    /// <summary>
    /// Notarize a macOS build.
    /// </summary>
    /// <remarks>
    /// This method will throw if the given build is not a macOS build.
    /// </remarks>
    /// <param name="macBuildPath">Path to the app bundle</param>
    public async Task Notarize(BuildPath macBuildPath, TaskToken task)
    {
        if (macBuildPath.target != BuildTarget.StandaloneOSX)
            throw new Exception($"NotarizationDistro: Notarization is only available for macOS builds (got {macBuildPath.target})");

        var path = macBuildPath.path;

        // Check settings
        if (string.IsNullOrEmpty(appSignIdentity))
            throw new Exception("NotarizationDistro: App sign identity not set.");

        if (entitlements == null)
            throw new Exception("NotarizationDistro: Entitlements file not set.");

        // Check User
        if (string.IsNullOrEmpty(ascLogin.User))
            throw new Exception("NotarizationDistro: No App Store Connect user set.");

        if (ascLogin.GetPassword(keychainService) == null)
            throw new Exception("NotarizationDistro: No App Store Connect password found in Keychain.");

        task.Report(0, 6, "Checking if app is already notarized");

        // Try stapling in case the build has already been notarized
        if (await Staple(path, silentError: true, task)) {
            Debug.Log("Build already notarized, nothing more to do...");
            return;
        }

        task.Report(1, description: "Signing app");

        // Sign plugins
        // codesign --deep --force does not resign nested plugins,
        // --force only applies to the main bundle. If we want to
        // resign nested plugins, we have to call codesign for each.
        // This is required for library validation with the hardened runtime.
        var plugins = Path.Combine(path, "Contents/Plugins");
        if (Directory.Exists(plugins)) {
            await Sign(Directory.GetFiles(plugins, "*.dylib", SearchOption.TopDirectoryOnly), task);
            await Sign(Directory.GetFiles(plugins, "*.bundle", SearchOption.TopDirectoryOnly), task);
            await Sign(Directory.GetDirectories(plugins, "*.bundle", SearchOption.TopDirectoryOnly), task);
        }

        // Sign application
        var entitlementsPath = AssetDatabase.GetAssetPath(entitlements);
        await Sign(path, task, entitlementsPath);

        task.Report(2, description: "Zipping app");

        // Zip app
        var zipPath = path + ".zip";
        await Zip(path, zipPath, task);

        task.Report(3, description: "Uploading app");

        // Upload for notarization
        string requestUUID = null;
        try {
            requestUUID = await Upload(zipPath, task);
            if (requestUUID == null)
                throw new Exception("NotarizationDistro: Could not parse request UUID from upload output");
        } finally {
            // Delete ZIP regardless of upload result
            File.Delete(zipPath);
        }

        task.Report(4, description: "Waiting for notarization result");

        // Wait for notarization to complete
        var status = await WaitForCompletion(requestUUID, task);
        if (status != "success")
            throw new Exception($"NotarizationDistro: Got '{status}' notarization status");

        task.Report(5, description: "Stapling ticket to app");

        // Staple
        await Staple(path, silentError: false, task);
    }

    protected async Task Sign(IEnumerable<string> paths, TaskToken task)
    {
        foreach (var path in paths) {
            await Sign(path, task);
        }
    }

    protected async Task Sign(string path, TaskToken task, string entitlementsPath = null)
    {
        // Delete .meta files Unity might have erroneously copied to the build
        // and which will cause the signing to fail.
        // See: https://issuetracker.unity3d.com/issues/macos-standalone-build-contains-meta-files-inside-native-plugin-bundles
        if (Directory.Exists(path)) {
            var metas = Directory.GetFiles(path, "*.meta", SearchOption.AllDirectories);
            foreach (var meta in metas) {
                File.Delete(meta);
            }
        }

        var entitlements = "";
        if (entitlementsPath != null) {
            entitlements = $" --entitlements '{entitlementsPath}'";
        }

        var args = $"--force"
            + $" --deep"
            + $" --timestamp"
            + $" --options=runtime"
            + entitlements
            + $" --sign '{appSignIdentity}'"
            + $" '{path}'";
        await Execute(new ExecutionArgs("codesign", args), task);
    }

    protected async Task Zip(string input, string output, TaskToken task)
    {
        var args = $" -qr"
            + $" '{output}'"
            + $" '{input}'";
        await Execute(new ExecutionArgs("zip", args), task);
    }

    static readonly Regex RequestUUIDRegex = new Regex(@"RequestUUID = ([a-z0-9-]+)");

    protected async Task<string> Upload(string path, TaskToken task)
    {
        var asc = "";
        if (!string.IsNullOrEmpty(ascProvider)) {
            asc = $" --asc-provider '{ascProvider}'";
        }

        var args = $"altool"
            + $" --notarize-app"
            + $" --primary-bundle-id '{primaryBundleId}'"
            + $" --username '{ascLogin.User}'"
            + asc
            + $" --file '{path}'";

        string requestUUID = null;
        await Execute(new ExecutionArgs("xcrun", args) {
            input = ascLogin.GetPassword(keychainService) + "\n", 
            onOutput = (output) => {
                if (requestUUID != null) return;
                var match = RequestUUIDRegex.Match(output);
                if (match.Success) {
                    requestUUID = match.Groups[1].Value;
                }
            }
        }, task);

        return requestUUID;
    }

    static readonly Regex StatusRegex = new Regex(@"Status: ([\w ]+)");
    static readonly Regex LogFileRegex = new Regex(@"LogFileURL: (\S+)");

    protected async Task<string> WaitForCompletion(string requestUUID, TaskToken task)
    {
        string status = null, logFile = null;
        do {
            await Task.Delay(TimeSpan.FromSeconds(statusCheckInterval));

            var args = $"altool"
                + $" --notarization-info '{requestUUID}'"
                + $" --username '{ascLogin.User}'";
            
            status = null;
            await Execute(new ExecutionArgs("xcrun", args) { 
                input =  ascLogin.GetPassword(keychainService) + "\n",
                onOutput = (output) => {
                    if (status == null) {
                        var match = StatusRegex.Match(output);
                        if (match.Success) {
                            status = match.Groups[1].Value;
                        }
                    }
                    if (logFile == null) {
                        var match = LogFileRegex.Match(output);
                        if (match.Success) {
                            logFile = match.Groups[1].Value;
                        }
                    }
                }
            }, task);
        } while (status == "in progress");

        if (logFile != null) {
            Debug.Log("Notarization log file: " + logFile);
        }

        return status;
    }

    protected async Task<bool> Staple(string path, bool silentError, TaskToken task)
    {
        var args = $"stapler staple '{path}'";
        var code = await Execute(new ExecutionArgs("xcrun", args) { silentError = silentError }, task);
        return (code == 0);
    }
}

}
