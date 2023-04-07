//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Sign, upload a macOS build for notarization and staple the ticket.
/// </summary>
/// <remarks>
/// From macOS 10.15 Catalina, it's required to sign and notarize builds
/// to pass Gate Keeper checks and to be able to open them without going
/// through the Open context menu.
/// 
/// This distro uses notarytool to upload builds and wait for the
/// notarization to complete. Credentials are handled by notarytool and
/// stored in a keychain profile (API Key or App-specific password, 
/// including Team ID). Use 'xcrun notarytool store-credentials' to 
/// set up a profile interactively and then set the profile name
/// in the distro options.
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
    /// The entitlements file (optional).
    /// </summary>
    public DefaultAsset entitlements;

    [Header("Notarization")]

    /// <summary>
    /// Wether to notarize or only sign the build.
    /// </summary>
    public bool notraize;

    [Space]

    /// <summary>
    /// They notarytool keychain profile to use.
    /// </summary>
    public string keychainProfile;

    [Space]

    /// <summary>
    /// Tell notarytool to wait for app to be notarized.
    /// </summary>
    public bool waitForCompletion = true;
    /// <summary>
    /// Timeout to wait for notarization to complete.
    /// </summary>
    /// <remarks>
    /// Empty = no timeout.
    /// Duration is an integer followed by an optional suffix: seconds 's' (default), minutes 'm', hours 'h'. Examples: '3600', '60m', '1h'
    /// </remarks>
    public string waitTimeout;

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

        // Check keychain profile
        if (string.IsNullOrEmpty(keychainProfile))
            throw new Exception("NotarizationDistro: No keychain profile set (use 'xcrun notarytool store-credentials' to set it up).");

        task.Report(0, 5, "Checking if app is already notarized");

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
        string entitlementsPath = null;
        if (entitlements != null) {
            entitlementsPath = AssetDatabase.GetAssetPath(entitlements);
        }
        await Sign(path, task, entitlementsPath);

        if (notraize) {
            task.Report(2, description: "Zipping app");

            // Zip app
            var zipPath = path + ".zip";
            await Zip(path, zipPath, task);

            task.Report(3, description: "Notarizing app");

            // Upload for notarization
            string requestUUID = null;
            try {
                requestUUID = await Notarize(zipPath, task);
            } finally {
                // Delete ZIP regardless of upload result
                File.Delete(zipPath);
            }

            if (!string.IsNullOrEmpty(requestUUID)) {
                Debug.Log($"NotarizationDistro: Request UUID is {requestUUID}");
            }

            task.Report(4, description: "Stapling ticket to app");

            // Staple
            await Staple(path, silentError: false, task);
        }
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

    [Serializable]
    public struct NotarytoolResult
    {
        public string id;
        public string status;
        public string message;
    }

    protected async Task<string> Notarize(string path, TaskToken task)
    {
        var wait = "";
        if (waitForCompletion) {
            wait = $" --wait";
            if (!string.IsNullOrEmpty(waitTimeout)) {
                wait += $" --timeout '{waitTimeout}'";
            }
        }

        var args = $"notarytool"
            + $" submit"
            + $" --output-format json"
            + $" --keychain-profile '{keychainProfile}'"
            + wait
            + $" '{path}'";

        string output = "";
        await Execute(new ExecutionArgs("xcrun", args) {
            onOutput = (o) => output +=o
        }, task);

        var result = JsonUtility.FromJson<NotarytoolResult>(output);
        if (result.status != "Accepted")
            throw new Exception($"Notarization failed with status '{result.status}': {result.message} ({result.id})");

        return result.id;
    }

    protected async Task<bool> Staple(string path, bool silentError, TaskToken task)
    {
        var args = $"stapler staple '{path}'";
        var code = await Execute(new ExecutionArgs("xcrun", args) { silentError = silentError }, task);
        return (code == 0);
    }
}

}
