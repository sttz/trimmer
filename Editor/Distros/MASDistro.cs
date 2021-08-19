//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Prepare a Mac build for the Mac App Store.
/// </summary>
/// <remarks>
/// This distribution makes the necessary modifications for Mac App Store builds
/// and uploads it using altool.
/// 
/// The distribution will take these steps:
/// * Add additional languages to the Info.plist (see <see cref="languages"/>)
/// * Update the copyright in the Info.plist (optional, <see cref="copyright"/>)
/// * Link additional frameworks (required for Game Center, <see cref="linkFrameworks"/>)
/// * Copy the provisioning profile to `Contents/embedded.provisionprofile`
/// * Sign the plugins and application with the given entitlements (see <see cref="entitlements"/> and <see cref="appSignIdentity"/>)
/// * Create a pkg installer and sign it (see <see cref="installerSignIdentity"/>)
/// 
/// Use XCode the create your developer and distribution identities and the Apple Developer Portal
/// to create the provisioning profiles. One way to create an entitlements file is to create an
/// empty dummy project in XCode and then to configure its capabilities accordingly.
/// 
/// The distribution can be used to create Mac App Store builds for testing:
/// * Set the <see cref="appSignIdentity"/> to your developer identity (not the 3rd party mac developer one)
/// * Leave the <see cref="installerSignIdentity"/> blank to skip generating the pkg
/// * Set the provisioning profile to a development profile
/// </remarks>
[CreateAssetMenu(fileName = "MAS Distro.asset", menuName = "Trimmer/Mac App Store", order = 100)]
public class MASDistro : DistroBase
{
    /// <summary>
    /// The identity to sign the app with.
    /// </summary>
    public string appSignIdentity;
    /// <summary>
    /// The identity to sign the installer with.
    /// </summary>
    public string installerSignIdentity;
    /// <summary>
    /// The entitlements file.
    /// </summary>
    public DefaultAsset entitlements;
    /// <summary>
    /// The provisioning profile.
    /// </summary>
    public DefaultAsset provisioningProfile;
    /// <summary>
    /// Copyright to set in the Info.plist (empty = no change).
    /// </summary>
    public string copyright;
    /// <summary>
    /// Comma-separated list of ISO-639 language codes to add to the Info.plist.
    /// </summary>
    public string languages;
    /// <summary>
    /// Additional frameworks the binary should be linked with.
    /// </summary>
    public string[] linkFrameworks;
    /// <summary>
    /// Path to the optool binary (only required for linking frameworks).
    /// (see https://github.com/alexzielenski/optool)
    /// </summary>
    public string optoolPath;

    /// <summary>
    /// App Store Connect login.
    /// </summary>
    [Keychain(keychainService)] public Login ascLogin;
    const string keychainService = "MASDistro";
    /// <summary>
    /// App Store Connect provider ID (only required if account is part of multiple teams).
    /// </summary>
    public string ascProvider;

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        var hasMacBuild = false;
        foreach (var buildPath in buildPaths) {
            if (buildPath.target != BuildTarget.StandaloneOSX)
                continue;

            hasMacBuild = true;
            await Process(buildPath.path, task);
        }

        if (!hasMacBuild)
            throw new Exception("MASDistro: No macOS build in profiles");
    }

    protected async Task Process(string path, TaskToken task)
    {
        // Check settings
        if (string.IsNullOrEmpty(appSignIdentity))
            throw new Exception("MASDistro: App sign identity not set.");

        if (entitlements == null)
            throw new Exception("MASDistro: Entitlements file not set.");

        if (provisioningProfile == null)
            throw new Exception("MASDistro: Provisioning profile not set.");

        if (linkFrameworks != null && linkFrameworks.Length > 0 && !File.Exists(optoolPath))
            throw new Exception("MASDistro: optool path not set for linking frameworks.");

        var plistPath = Path.Combine(path, "Contents/Info.plist");
        if (!File.Exists(plistPath))
            throw new Exception("MASDistro: Info.plist file not found at path: " + plistPath);

        task.Report(0, 3);

        var doc = new PlistDocument();
        doc.ReadFromFile(plistPath);

        // Edit Info.plist
        if (!string.IsNullOrEmpty(copyright) || !string.IsNullOrEmpty(languages)) {
            if (!string.IsNullOrEmpty(copyright)) {
                doc.root.SetString("NSHumanReadableCopyright", string.Format(copyright, System.DateTime.Now.Year));
            }

            if (!string.IsNullOrEmpty(languages)) {
                var parts = languages.Split(',');

                var array = doc.root.CreateArray("CFBundleLocalizations");
                foreach (var part in parts) {
                    array.AddString(part.Trim());
                }
            }

            doc.WriteToFile(plistPath);
        }

        // Link frameworks
        if (linkFrameworks != null && linkFrameworks.Length > 0) {
            task.Report(0, description: "Linking frameworks");

            var binaryPath = Path.Combine(path, "Contents/MacOS");
            binaryPath = Path.Combine(binaryPath, doc.root["CFBundleExecutable"].AsString());

            foreach (var framework in linkFrameworks) {
                var frameworkBinaryPath = FindFramework(framework);
                if (frameworkBinaryPath == null)
                    throw new Exception("MASDistro: Could not locate framework: " + framework);

                var otoolargs = string.Format(
                    "install -c weak -p '{0}' -t '{1}'",
                    frameworkBinaryPath, binaryPath
                );
                await Execute(new ExecutionArgs(optoolPath, otoolargs), task);
            }
        }

        // Copy provisioning profile
        var profilePath = AssetDatabase.GetAssetPath(provisioningProfile);
        var embeddedPath = Path.Combine(path, "Contents/embedded.provisionprofile");
        File.Copy(profilePath, embeddedPath, true);

        // Sign plugins
        var plugins = Path.Combine(path, "Contents/Plugins");
        if (Directory.Exists(plugins)) {
            task.Report(0, description: "Signing plugins");
            await Sign(Directory.GetFiles(plugins, "*.dylib", SearchOption.AllDirectories), task);
            await Sign(Directory.GetFiles(plugins, "*.bundle", SearchOption.AllDirectories), task);
            await Sign(Directory.GetDirectories(plugins, "*.bundle", SearchOption.TopDirectoryOnly), task);
        }

        // Sign application
        task.Report(1, description: "Singing app");
        var entitlementsPath = AssetDatabase.GetAssetPath(entitlements);
        await Sign(path, task, entitlementsPath);

        // Create installer
        var pkgPath = Path.ChangeExtension(path, ".pkg");
        if (!string.IsNullOrEmpty(installerSignIdentity)) {
            task.Report(1, description: "Creating installer");
            var args = string.Format(
                "--component '{0}' /Applications --sign '{1}' '{2}'",
                path, installerSignIdentity, pkgPath
            );
            await Execute(new ExecutionArgs("productbuild", args), task);
        }

        // Upload to App Store
        if (!string.IsNullOrEmpty(ascLogin.User)) {
            task.Report(2, description: "Uploading to App Store Connect");
            await Upload(pkgPath, task);
        }
    }

    protected string FindFramework(string input)
    {
        if (File.Exists(input)) {
            return input;
        }

        if (!Directory.Exists(input)) {
            input = Path.Combine("/System/Library/Frameworks", input);
            if (!Directory.Exists(input)) {
                return null;
            }
        }

        var name = Path.GetFileNameWithoutExtension(input);
        input = Path.Combine(input, "Versions/Current");
        input = Path.Combine(input, name);

        return input;
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

    protected async Task Upload(string path, TaskToken task)
    {
        var asc = "";
        if (!string.IsNullOrEmpty(ascProvider)) {
            asc = $" --asc-provider '{ascProvider}'";
        }

        var args = $"altool"
            + $" --upload-app"
            + $" --type osx"
            + $" --username '{ascLogin.User}'"
            + asc
            + $" --file '{path}'";

        await Execute(new ExecutionArgs("xcrun", args) { 
            input = ascLogin.GetPassword(keychainService) + "\n"
        }, task);
    }
}

}
