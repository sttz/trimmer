#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace sttz.Trimmer.Options {

/// <summary>
/// Helper class to post-process the Xcode projects Unity generates for iOS and macOS.
/// </summary>
/// <remarks>
/// This mainly handles differences between how the Xcode project is generated
/// on iOS and macOS. It also handles getting or creating the info or entitlements
/// files.
/// </remarks>
public class XcodePostprocessor
{
    // -------- Helpers --------

    /// <summary>
    /// Find the pbxproj file in the given Unity build path.
    /// </summary>
    /// <returns>The path to the pbxproj file or null if no project could be found</returns>
    public static string FindProject(string outputPath)
    {
        // iOS Xcode project layout:
        // ./Unity-iPhone.xcodeproj/project.pbxproj
        // ./Info.plist

        // macOS Xcode project layout:
        // ./PRODUCT_NAME.xcodeproj/project.pbxproj
        // ./PRODUCT_NAME/Info.plist

        // On iOS, the project name is hardcoded to "Unity-iPhone.xcodeproj"
        var projectDirectory = Path.Combine(outputPath, "Unity-iPhone.xcodeproj");
        if (!Directory.Exists(projectDirectory)) {
            // On macOS, the project name is "PRODUCT_NAME.xcodeproj"
            // As a general fallback, we look for any "*.xcodeproj" directory
            var projects = Directory.GetDirectories(outputPath, "*.xcodeproj");
            if (projects.Length == 0) {
                return null;
            } else if (projects.Length > 1) {
                Debug.LogWarning($"FindXcodeProject: Found multiple xcodeproj in build directory ({string.Join(", ", projects)})");
                return null;
            }

            projectDirectory = projects[0];
        }

        var pbxproj = Path.Combine(projectDirectory, "project.pbxproj");
        if (!File.Exists(pbxproj))
            return null;

        return pbxproj;
    }

    /// <summary>
    /// Convert a Xcode project relative path to a Unity project relative or absolute path.
    /// </summary>
    /// <param name="outputPath">The build output path.</param>
    /// <param name="projectPath">The project path to convert</param>
    public static string XcodeToUnityPath(string outputPath, string projectPath)
    {
        return Path.Combine(outputPath, projectPath);
    }

    /// <summary>
    /// Convert a Unity project relative or absolute path to a Xcode project relative path.
    /// This also converts backslashes to forward slashes.
    /// </summary>
    /// <param name="outputPath">The build output path.</param>
    /// <param name="unityPath">The Unity project relative or absolute path</param>
    public static string UnityToXcodePath(string outputPath, string unityPath)
    {
        var fullPath = Path.GetFullPath(unityPath);
        var relativePath = Path.GetRelativePath(outputPath, fullPath);
        return relativePath.Replace('\\', '/');
    }

    /// <summary>
    /// Get the main target Guid in the Xcode project.
    /// </summary>
    /// <param name="project">The loaded PBXProject instance of the project</param>
    /// <returns>The guid or null if the target couldn't be found</returns>
    public static string FindMainTargetGuid(PBXProject project, string productName)
    {
        // iOS main target is always called "Unity-iPhone"
        // but we can't call TargetGuidByName("Unity-iPhone") because that throws
        // and forces us to use GetUnityMainTargetGuid.
        var targetGuid = project.GetUnityMainTargetGuid();
        if (targetGuid != null)
            return targetGuid;

        // macOS main target is named based on product name
        targetGuid = project.TargetGuidByName(productName);
        if (targetGuid != null)
            return targetGuid;

        return null;
    }

    /// <summary>
    /// Get the path to the Info.plist file of the main target in the given Xcode project.
    /// </summary>
    /// <param name="project">The loaded PBXProject instance of the project</param>
    /// <returns>The Info.plist file path (relative to Xcode project) 
    /// or null if it couldn't be found.</returns>
    public static string FindInfoPlistFile(PBXProject project, string targetGuid)
    {
        return project.GetBuildPropertyForAnyConfig(targetGuid, "INFOPLIST_FILE");
    }

    /// <summary>
    /// Get the path to the entitlements file of the main target in the given Xcode project.
    /// </summary>
    /// <param name="project">The loaded PBXProject instance of the project</param>
    /// <returns>The path to the entitlements file path (relative to Xcode project) 
    /// or null if one couldn't be found</returns>
    public static string FindEntitlementsFile(PBXProject project, string targetGuid)
    {
        return project.GetEntitlementFilePathForTarget(targetGuid);
    }

    /// <summary>
    /// Get the default entitlements file path, relative to the Xcode project.
    /// </summary>
    /// <param name="outputPath">The path to the build output</param>
    /// <param name="productName">The build product name</param>
    public static string GetDefaultEntitlementsFilePath(string outputPath, string productName)
    {
        var fileName = $"{productName}.entitlements";

        var basePath = Path.Combine(outputPath, "Unity-iPhone");
        if (Directory.Exists(basePath)) {
            return "Unity-iPhone/" + fileName;
        }

        basePath = Path.Combine(outputPath, productName);
        if (Directory.Exists(basePath)) {
            return productName + "/" + fileName;
        }

        return fileName;
    }

    // -------- Post-Processor --------

    string outputPath;
    string productName;

    string projectPath;
    PBXProject project;
    string mainTargetGuid;

    string infoPlistPath;
    PlistDocument infoPlist;

    string entitlementsPath;
    PlistDocument entitlements;

    /// <summary>
    /// Create a new post-processor with the given build output path.
    /// </summary>
    /// <param name="outputPath">The output path as given to Unity or as 
    /// set on <see cref="UnityEditor.Build.Reporting.BuildSummary.outputPath"/></param>
    /// <param name="productName">The project product name (defaults to <see cref="Application.productName"/>)</param>
    public XcodePostprocessor(string outputPath, string productName = null)
    {
        if (!Directory.Exists(outputPath))
            throw new Exception($"XcodePostprocessor: Output path does not exist ({outputPath})");
        
        projectPath = FindProject(outputPath);
        if (projectPath == null)
            throw new Exception($"XcodePostprocessor: Could not find Xcode project at output path ({outputPath})");

        if (productName == null)
            productName = Application.productName;

        this.outputPath = outputPath;
        this.productName = productName;

        project = new PBXProject();
        project.ReadFromFile(projectPath);

        mainTargetGuid = FindMainTargetGuid(project, mainTargetGuid);
        if (mainTargetGuid == null)
            throw new Exception($"XcodePostprocessor: Could not find main target Guid in Xcode project ({outputPath})");
    }

    /// <summary>
    /// The <see cref="PBXProject"/> instance.
    /// </summary>
    public PBXProject Project => project;

    /// <summary>
    /// Guid of the main target.
    /// </summary>
    public string MainTargetGuid => mainTargetGuid;

    /// <summary>
    /// The path to the Info.plist file, set after <see cref="GetOrCrateInfoPlist"/> is called.
    /// </summary>
    public string InfoPlistPath => infoPlistPath;
    /// <summary>
    /// The path to the entitlements file, set after <see cref="GetOrCreateEntitlements"/> is called.
    /// </summary>
    public string EntitlementsPath => entitlementsPath;

    /// <summary>
    /// Get the Info.plist instance of the main target of the Xcode project.
    /// Create a new plist instance and new file if the Info.plist doesn't exist.
    /// </summary>
    public PlistDocument GetOrCrateInfoPlist()
    {
        if (infoPlist != null)
            return infoPlist;

        var infoPlistPathRelative = FindInfoPlistFile(project, mainTargetGuid);
        if (infoPlistPathRelative == null)
            throw new Exception($"XcodePostprocessor: Could not get Info.plist path from Xcode project ({outputPath})");

        infoPlist = new PlistDocument();

        infoPlistPath = XcodeToUnityPath(outputPath, infoPlistPathRelative);
        if (File.Exists(infoPlistPath)) {
            infoPlist.ReadFromFile(infoPlistPath);
        } else {
            infoPlist.Create();
        }

        return infoPlist;
    }

    /// <summary>
    /// Get the entitlements plist instance of the main target of the Xcode project.
    /// Create a new plist instance and new file if the entitlements plist doesn't exist.
    /// </summary>
    public PlistDocument GetOrCreateEntitlements()
    {
        if (entitlements != null)
            return entitlements;

        var entitlementsPathRelative = FindEntitlementsFile(project, mainTargetGuid);
        if (entitlementsPathRelative == null) {
            entitlementsPathRelative = GetDefaultEntitlementsFilePath(outputPath, productName);
            project.AddFile(entitlementsPathRelative, entitlementsPathRelative);
            project.SetBuildProperty(mainTargetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsPathRelative);
        }

        entitlements = new PlistDocument();

        entitlementsPath = XcodeToUnityPath(outputPath, entitlementsPathRelative);
        if (File.Exists(entitlementsPath)) {
            entitlements.ReadFromFile(entitlementsPath);
        } else {
            entitlements.Create();
        }

        return entitlements;
    }

    /// <summary>
    /// Save the changes to the project and optionally Info.plist / entitlements,
    /// if they were accessed.
    /// </summary>
    public void Save()
    {
        project.WriteToFile(projectPath);

        if (infoPlist != null) {
            infoPlist.WriteToFile(infoPlistPath);
        }

        if (entitlements != null) {
            entitlements.WriteToFile(entitlementsPath);
        }
    }
}

}

#endif
