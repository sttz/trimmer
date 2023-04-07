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
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Build and upload an iOS build to the iOS App Store.
/// </summary>
/// <remarks>
/// This distro archives the project to <see cref="archivesPath"> with a unique
/// file name. It then uploads the build to the iOS app store using Xcode.
/// 
/// The build will use the same settings as when selecting "Archive" in Xcode. Make sure
/// Unity generates an Xcode project that is immediately ready to build.
/// 
/// Uploading will use the default options. Specify a custom export options file
/// to override those defaults. See "xcodebuild --help" for the available options.
/// You need to be authenticated in Xcode and able to upload builds.
/// </remarks>
[CreateAssetMenu(fileName = "iOS Distro.asset", menuName = "Trimmer/iOS", order = 100)]
public class iOSDistro : DistroBase
{
    /// <summary>
    /// Path where generated xcarchives will be stored, relative to the Xcode project.
    /// </summary>
    public string archivesPath = "../Archives";
    /// <summary>
    /// Scheme in the Xcode project to archive and upload.
    /// </summary>
    public string scheme = "Unity-iPhone";
    /// <summary>
    /// Export options plist. If not set, a basic one will be used.
    /// </summary>
    public DefaultAsset exportOptions;
    /// <summary>
    /// Allow Xcode to update provisioning.
    /// </summary>
    public bool allowProvisioningUpdates;
    /// <summary>
    /// Allow Xcode to register new devices on the developer portal.
    /// Only works if <see cref="allowProvisioningUpdates"/> is enabled
    /// as well.
    /// </summary>
    public bool allowProvisioningDeviceRegistration;

    const string DefaultExportOptions = @"
<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>method</key>
    <string>app-store</string>
    <key>destination</key>
    <string>upload</string>
</dict>
</plist>
";

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        var hasTarget = false;
        foreach (var buildPath in buildPaths) {
            if (buildPath.target != BuildTarget.iOS)
                continue;
            
            hasTarget = true;
            await Process(buildPath.path, task);
        }

        if (!hasTarget) {
            throw new Exception($"iOSDistro: No iOS build target in build profiles");
        }
    }

    protected async Task Process(string path, TaskToken task)
    {
        task.Report(0, 2);

        // Create archive
        var projectPath = Path.Combine(path, "Unity-iPhone.xcodeproj");
        if (!Directory.Exists(projectPath)) {
            throw new Exception($"iOSDistro: Could not find Xcode project at path '{projectPath}'");
        }

        var archiveName = $"{scheme}-{PlayerSettings.iOS.buildNumber}-{DateTime.Now.ToString("O")}.xcarchive";
        var archivePath = Path.Combine(archivesPath, archiveName);

        task.Report(0, description: $"Building scheme '{scheme}'");
        await Archive(projectPath, scheme, archivePath, task);

        // Upload archive
        var cleanUpOptions = false;
        string exportOptionsPath = null;
        try {
            if (exportOptions != null) {
                exportOptionsPath = AssetDatabase.GetAssetPath(exportOptions);
            }

            if (string.IsNullOrEmpty(exportOptionsPath)) {
                cleanUpOptions = true;
                exportOptionsPath = Path.GetTempFileName();
                File.WriteAllText(exportOptionsPath, DefaultExportOptions);
            }

            task.Report(1, description: $"Uploading archive");
            await Upload(archivePath, exportOptionsPath, task);
        } finally {
            if (cleanUpOptions) {
                File.Delete(exportOptionsPath);
            }
        }
    }

    protected async Task Archive(string projectPath, string schemeName, string archivePath, TaskToken task)
    {
        var args = $"archive"
            + $" -project '{projectPath}'"
            + $" -archivePath '{archivePath}'"
            + $" -scheme '{schemeName}'"
            + $" -destination 'generic/platform=iOS'";

        if (allowProvisioningUpdates)
            args += " -allowProvisioningUpdates";
        if (allowProvisioningDeviceRegistration)
            args += " -allowProvisioningDeviceRegistration";

        await Execute(new ExecutionArgs("xcodebuild", args), task);
    }

    protected async Task Upload(string archivePath, string exportOptionsPlist, TaskToken task)
    {
        var args = $"-exportArchive"
            + $" -archivePath '{archivePath}'"
            + $" -exportOptionsPlist '{exportOptionsPlist}'";

        if (allowProvisioningUpdates)
            args += " -allowProvisioningUpdates";
        if (allowProvisioningDeviceRegistration)
            args += " -allowProvisioningDeviceRegistration";

        await Execute(new ExecutionArgs("xcodebuild", args), task);
    }
}

}
