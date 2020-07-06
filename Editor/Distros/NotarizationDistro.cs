//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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

    protected override IEnumerator DistributeCoroutine(IEnumerable<BuildPath> buildPaths, bool forceBuild)
    {
        foreach (var buildPath in buildPaths) {
            if (buildPath.target != BuildTarget.StandaloneOSX) {
                Debug.Log("NotarizationDistro: Skipping mismatched platform, only macOS is supported: " + buildPath.target);
                continue;
            }
            yield return Notarize(buildPath);
            if (GetSubroutineResult<string>() == null) {
                yield return false; yield break;
            }
        }
        yield return true;
    }

    /// <summary>
    /// Notarize a macOS build.
    /// </summary>
    /// <param name="path">Path to the app bundle</param>
    /// <returns>The string path to the notarized macOS app bundle</returns>
    public IEnumerator Notarize(BuildPath macBuildPath)
    {
        if (macBuildPath.target != BuildTarget.StandaloneOSX) {
            Debug.LogError($"NotarizationDistro: Notarization is only available for macOS builds (got {macBuildPath.target})");
            yield return null; yield break;
        }

        var path = macBuildPath.path;

        // Check settings
        if (string.IsNullOrEmpty(appSignIdentity)) {
            Debug.LogError("NotarizationDistro: App sign identity not set.");
            yield return null; yield break;
        }

        if (entitlements == null) {
            Debug.LogError("NotarizationDistro: Entitlements file not set.");
            yield return null; yield break;
        }

        // Check User
        if (string.IsNullOrEmpty(ascLogin.User)) {
            Debug.LogError("NotarizationDistro: No App Store Connect user set.");
            yield return null; yield break;
        }

        if (ascLogin.GetPassword(keychainService) == null) {
            Debug.LogError("NotarizationDistro: No App Store Connect password found in Keychain.");
            yield return null; yield break;
        }

        Debug.Log("Checking if app is already notarized...");

        // Try stapling in case the build has already been notarized
        yield return Staple(path, false);
        if (GetSubroutineResult<bool>()) {
            Debug.Log("Build already notarized, nothing more to do...");
            yield return path; yield break;
        }

        Debug.Log("Singing app...");

        // Sign plugins
        // codesign --deep --force does not resign nested plugins,
        // --force only applies to the main bundle. If we want to
        // resign nested plugins, we have to call codesign for each.
        // This is required for library validation with the hardened runtime.
        var plugins = Path.Combine(path, "Contents/Plugins");
        if (Directory.Exists(plugins)) {
            yield return SignAll(Directory.GetFiles(plugins, "*.dylib", SearchOption.TopDirectoryOnly));
            if (!GetSubroutineResult<bool>()) {
                yield return false; yield break;
            }

            yield return SignAll(Directory.GetFiles(plugins, "*.bundle", SearchOption.TopDirectoryOnly));
            if (!GetSubroutineResult<bool>()) {
                yield return false; yield break;
            }
        }

        // Sign application
        var entitlementsPath = AssetDatabase.GetAssetPath(entitlements);
        yield return Sign(path, entitlementsPath);
        if (!GetSubroutineResult<bool>()) {
            yield return null; yield break;
        }

        Debug.Log("Signed! Zipping app...");

        // Zip app
        var zipPath = path + ".zip";
        yield return Zip(path, zipPath);
        if (!GetSubroutineResult<bool>()) {
            yield return null; yield break;
        }

        Debug.Log("Zipped! Uploading...");

        // Upload for notarization
        yield return Upload(zipPath);

        // Delete ZIP regardless of upload result
        File.Delete(zipPath);

        var requestUUID = GetSubroutineResult<string>();
        if (requestUUID == null) {
            yield return null; yield break;
        }

        Debug.Log("Uploaded! Waiting for result...");

        // Wait for notarization to complete
        yield return WaitForCompletion(requestUUID);
        var status = GetSubroutineResult<string>();
        if (status != "success") {
            yield return null; yield break;
        }

        Debug.Log("Done! Stapling...");

        // Staple
        yield return Staple(path, true);
        if (!GetSubroutineResult<bool>()) {
            yield return null; yield break;
        }

        Debug.Log("NotarizationDistro: Finished");
        yield return path;
    }

    protected IEnumerator SignAll(IEnumerable<string> paths)
    {
        foreach (var path in paths) {
            yield return Sign(path);
            if (!GetSubroutineResult<bool>()) {
                yield return false; yield break;
            }
        }
        yield return true;
    }

    protected IEnumerator Sign(string path, string entitlementsPath = null)
    {
        // Delete .meta files Unity might have erroneously copied to the build
        // and which will cause the signing to fail.
        // See: https://issuetracker.unity3d.com/issues/macos-standalone-build-contains-meta-files-inside-native-plugin-bundles
        if (Directory.Exists(path)) {
            var metas = Directory.GetFiles(path, "*.meta", SearchOption.AllDirectories);
            foreach (var meta in metas) {
                Debug.Log($"File.Delete({meta})");
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

        yield return Execute("codesign", args);
        if (GetSubroutineResult<int>() != 0) {
            yield return false; yield break;
        }

        yield return true;
    }

    protected IEnumerator Zip(string input, string output)
    {
        var args = $" -qr"
            + $" '{output}'"
            + $" '{input}'";

        yield return Execute("zip", args);
        if (GetSubroutineResult<int>() != 0) {
            yield return false; yield break;
        }

        yield return true;
    }

    static readonly Regex RequestUUIDRegex = new Regex(@"RequestUUID = ([a-z0-9-]+)");

    protected IEnumerator Upload(string path)
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
        yield return Execute("xcrun", args, ascLogin.GetPassword(keychainService) + "\n", (output) => {
            if (requestUUID != null) return;
            var match = RequestUUIDRegex.Match(output);
            if (match.Success) {
                requestUUID = match.Groups[1].Value;
            }
        });
        if (GetSubroutineResult<int>() != 0) {
            yield return null; yield break;
        }
        yield return requestUUID;
    }

    static readonly Regex StatusRegex = new Regex(@"Status: ([\w ]+)");
    static readonly Regex LogFileRegex = new Regex(@"LogFileURL: (\S+)");

    protected IEnumerator WaitForCompletion(string requestUUID)
    {
        string status = null, logFile = null;
        do {
            var startTime = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - startTime < statusCheckInterval) {
                yield return null;
            }

            var args = $"altool"
                + $" --notarization-info '{requestUUID}'"
                + $" --username '{ascLogin.User}'";
            
            status = null;
            yield return Execute("xcrun", args, ascLogin.GetPassword(keychainService) + "\n", (output) => {
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
            });
            if (GetSubroutineResult<int>() != 0) {
                yield return null; yield break;
            }
        } while (status == "in progress");

        if (logFile != null) {
            Debug.Log("Notarization log file: " + logFile);
        }

        yield return status;
    }

    protected IEnumerator Staple(string path, bool logError)
    {
        var args = $"stapler staple '{path}'";

        yield return Execute("xcrun", args, logError: logError);
        if (GetSubroutineResult<int>() != 0) {
            yield return false; yield break;
        }

        yield return true;
    }
}

}
