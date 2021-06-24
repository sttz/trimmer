//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using sttz.Trimmer.BaseOptions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.iOS.Xcode;
using UnityEditor.Build.Reporting;

namespace sttz.Trimmer.Options
{

/// <summary>
/// Option to manage the version of your build and access 
/// version info at runtime.
/// </summary>
/// <remarks>
/// Unity's project version management is fragmented and differs greatly
/// between platforms. This option attempts to unify them and provide
/// additional features.
/// 
/// The version provided by this Option is based on the semantic 
/// versioning specification. Given some platform's limitations (especially
/// iOS and Android - see below), prerelease tagging is not supported
/// and the build number can only be an increasing positive integer.
/// 
/// The Option does not rely on additional files but instead uses Unity's
/// existing settings. It does make <see cref="Version"/> available as 
/// an additional API to access a build's version with additional details
/// like build number and version control details (commit / branch) that
/// are not available in Unity.
/// 
/// In Build Profiles, the option allows to turn toggle it on/off and to
/// override the version or build number if you need to version some 
/// builds differently from others. It also allows to toggle the inclusion
/// of version control data in the build in case you prefer not to include
/// it in release builds.
/// 
/// Two additional options can be set inside the Option script itself:
/// <see cref="shareBuildNumber"/> and <see cref="incrementBuildNumber"/>,
/// to set if there should be a global build number across platforms or
/// if each platform should have their own and to set if the Option
/// automatically increments the build number on every build.
/// 
/// In case <see cref="shareBuildNumber"/> is enabled, the Option checks
/// all build numbers and uses the largest and then applies the incremented
/// build number to all platforms. In case you want to change the build
/// number, you can edit any platform's build number if you want to 
/// increase it but have to edit all platforms in case you want to lower it.
/// 
/// The Option currently only supports Git for the version control information
/// and requires that the git command line is on the PATH.
/// 
/// Documentation:
/// https://developer.android.com/studio/publish/versioning.html
/// https://developer.apple.com/library/content/technotes/tn2420/_index.html
/// </remarks>
[Capabilities(OptionCapabilities.ConfiguresBuild | OptionCapabilities.CanPlayInEditor | OptionCapabilities.HasAssociatedFeature)]
public class OptionVersion : OptionContainer
{
    // ------ Configuration ------

    /// <summary>
    /// When enabled, the build numbers between iOS/Android/MacAppStore are synchronized
    /// and used for other platforms as well. When not enabled, those platforms will have
    /// independent build numbers and all other platforms will have 0.
    /// </summary>
    public bool shareBuildNumber = true;

    /// <summary>
    /// Automatically increment build number for each build.
    /// </summary>
    public bool incrementBuildNumber = true;

    // ------ Option ------

    override protected void Configure()
    {
        Category = "General";
    }

    /// <summary>
    /// If non-empty, this version will be used instead of `PlayerSettings.bundleVersion`
    /// </summary>
    public class OptionOverrideVersion : OptionString { }

    /// <summary>
    /// If non-empty, this build number will be used instead of the shared build number.
    /// </summary>
    public class OptionOverrideBuild : OptionString { }

    /// <summary>
    /// When enabled, version control information will be included.
    /// </summary>
    public class OptionVersionControl : OptionToggle
    {
        override protected void Configure()
        {
            DefaultValue = true;
        }
    }

    /// <summary>
    /// Apply the version to the product (app bundle or exe)
    /// </summary>
    /// <remarks>
    /// Updating the app bundle version requires the Unity iOS support to be installed,
    /// the exe updating requires python with the pefile installed.
    /// </remarks>
    public class OptionApplyToProduct : OptionToggle
    {
        override public void PostprocessBuild(BuildReport report, OptionInclusion inclusion)
        {
            base.PostprocessBuild(report, inclusion);

            if (!inclusion.HasFlag(OptionInclusion.Feature) || !Value)
                return;

            var platform = report.summary.platform;
            if (platform == BuildTarget.StandaloneOSX) {
                UpdateVersionMac(report.summary.outputPath, Version.ProjectVersion);
            } else if (platform == BuildTarget.StandaloneWindows || platform == BuildTarget.StandaloneWindows64) {
                UpdateVersionWindows(report.summary.outputPath, Version.ProjectVersion);
            }
        }

        public static bool UpdateVersionMac(string path, Version version)
        {
            var infoPlistPath = System.IO.Path.Combine(path, "Contents/Info.plist");
            if (!File.Exists(infoPlistPath)) {
                Debug.LogError("OptionVersion: Info plist not found at path: " + infoPlistPath);
                return false;
            }

            var doc = new PlistDocument();
            doc.ReadFromFile(infoPlistPath);

            doc.root.SetString("CFBundleGetInfoString", string.Format(
                "{0} {1} ({2})",
                Application.productName, version.MajorMinorPatch, version.build
            ));

            doc.WriteToFile(infoPlistPath);
            return true;
        }

        const string PythonUpdateWindows = @"
import sys

try:
    import pefile
except ImportError, e:
    print 'ERROR: Required Python package pefile not installed: ' + str(e)
    sys.exit(20)

path = sys.argv[1]
major = sys.argv[2]
minor = sys.argv[3]
patch = sys.argv[4]
build = sys.argv[5]

version = '{0}.{1}.{2}'.format(major, minor, patch)
major_int = int(major)
minor_int = int(minor)
patch_int = int(patch)
build_int = int(build)

pe = pefile.PE(path)

oldFileVersion = pe.FileInfo[0].StringTable[0].entries['FileVersion']
pe.FileInfo[0].StringTable[0].entries['FileVersion'] = build.ljust(len(oldFileVersion))

oldProductVersion = pe.FileInfo[0].StringTable[0].entries['ProductVersion']
pe.FileInfo[0].StringTable[0].entries['ProductVersion'] = version.ljust(len(oldFileVersion))

ms = (major_int << 16) | (minor_int & 0x0000ffff)
ls = (patch_int << 16) | (build_int & 0x0000ffff)

pe.VS_FIXEDFILEINFO.FileVersionMS = ms
pe.VS_FIXEDFILEINFO.FileVersionLS = ls

pe.VS_FIXEDFILEINFO.ProductVersionMS = ms
pe.VS_FIXEDFILEINFO.ProductVersionLS = ls

pe.write(filename=path)
";

        public static bool UpdateVersionWindows(string path, Version version)
        {
            var args = string.Format(
                "- '{0}' {1} {2} {3} {4}",
                path, version.major, version.minor, version.patch, version.build
            );
            string output, error;
            var exitcode = OptionHelper.RunScript("python", args, PythonUpdateWindows, out output, out error);
            if (exitcode != 0) {
                Debug.LogError("OptionVersion: Failed to run python to update version: " + output);
                return false;
            }
            return true;
        }
    }

    override public void Apply()
    {
        base.Apply();

        // In the editor we don't need the container and set the version directly
        Version.ProjectVersion = DetermineProjectVersion(EditorUserBuildSettings.activeBuildTarget);
    }

    override public void PreprocessBuild(BuildReport report, OptionInclusion inclusion)
    {
        base.PreprocessBuild(report, inclusion);

        if (!inclusion.HasFlag(OptionInclusion.Feature)) return;

        Version.ProjectVersion = DetermineProjectVersion(report.summary.platform);

        if (incrementBuildNumber) {
            Version.ProjectVersion = IncrementBuildNumber(Version.ProjectVersion, report.summary.platform);
        }
    }

    override public void PostprocessScene(Scene scene, OptionInclusion inclusion)
    {
        base.PostprocessScene(scene, inclusion);

        var script = OptionHelper.InjectFeature<VersionContainer>(scene, OptionInclusion.Feature);
        if (script != null) {
            script.version = Version.ProjectVersion;
        }
    }

    Version DetermineProjectVersion(BuildTarget target)
    {
        // Get version string from either the override or PlayerSettings.bundleVersion
        string versionString;
        
        var overrideVersion = GetChild<OptionOverrideVersion>().Value;
        if (!string.IsNullOrEmpty(overrideVersion)) {
            versionString = overrideVersion;
        } else {
            versionString = PlayerSettings.bundleVersion;
            if (string.IsNullOrEmpty(versionString)) {
                Debug.LogError("OptionVersion: Please configure the version in the player settings.");
                return default(Version);
            }
        }

        // -- Parse version
        string error;
        var version = Version.Parse(versionString, out error);
        if (error != null) {
            Debug.LogError(error);
            return default(Version);
        }

        if (!version.IsDefined) {
            Debug.LogWarning("OptionVersion: No valid version defined (" + version.MajorMinorPatch + ")");
        }

        // -- Parse build
        string numberString;
        var overrideBuild = GetChild<OptionOverrideBuild>().Value;
        if (!string.IsNullOrEmpty(overrideBuild)) {
            // Build number has been overridden
            if (!int.TryParse(overrideBuild, out version.build) || version.build < 0) {
                Debug.LogError("OptionVersion: Override build not a positive number: " + overrideBuild);
            }
        
        } else if (shareBuildNumber) {
            // Look for highest build number across build target groups, increment it
            // and then apply it back to all groups
            if (GetBuildNumber == null || SetBuildNumber == null) {
                Debug.LogError("OptionVersion: Could not find internal GetBuildNumber method on PlayerSettings.");
                return default(Version);
            }

            var buildNumbers = new List<int>();
            foreach (BuildTargetGroup targetGroup in System.Enum.GetValues(typeof(BuildTargetGroup))) {
                numberString = (string)GetBuildNumber.Invoke(null, new object[] { targetGroup });
                int number;
                if (!int.TryParse(numberString, out number) || number < 0) {
                    Debug.LogError("OptionVersion: " + targetGroup + " build number should be a positive integer (" + numberString  + ")");
                } else {
                    buildNumbers.Add(number);
                }
            }

            var android = PlayerSettings.Android.bundleVersionCode;
            if (android < 0) {
                Debug.LogError("OptionVersion: Android bundle version code should be a positive integer.");
            } else {
                buildNumbers.Add(android);
            }

            // Use the highest defined build number
            version.build = buildNumbers.Max();
        
        } else {
            // Only increment the number for the current build target group
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (targetGroup == BuildTargetGroup.Android) {
                version.build = PlayerSettings.Android.bundleVersionCode;
            } else {
                if (GetBuildNumber == null || SetBuildNumber == null) {
                    Debug.LogError("OptionVersion: Could not find internal GetBuildNumber method on PlayerSettings.");
                    return default(Version);
                }

                numberString = (string)GetBuildNumber.Invoke(null, new object[] { targetGroup });
                var number = 0;
                if (!int.TryParse(numberString, out number) || number < 0) {
                    Debug.LogError("OptionVersion: " + targetGroup + " build number should be a positive integer (" + numberString  + ")");
                }

                version.build = number;
            }
        }

        // -- Version Control
        if (GetChild<OptionVersionControl>().Value) {
            // -- Git
            if (System.IO.Directory.Exists(".git")) {
                OptionHelper.RunScript("git", "rev-parse --verify HEAD", out version.commit);
                if (version.commit != null) {
                    version.commit = version.commit.Trim();
                    if (version.commit.Length == 0) {
                        version.commit = null;
                    }
                }

                OptionHelper.RunScript("git", "rev-parse --abbrev-ref --verify HEAD", out version.branch);
                if (version.branch != null) {
                    version.branch = version.branch.Trim();
                    if (version.branch.Length == 0) {
                        version.branch = null;
                    }
                }
            }
        }

        return version;
    }

    Version IncrementBuildNumber(Version version, BuildTarget target)
    {
        version.build++;

        string numberString;
        var optionOverrideBuild = GetChild<OptionOverrideBuild>();
        if (!string.IsNullOrEmpty(optionOverrideBuild.Value)) {
            // Build number has been overridden, increment override
            optionOverrideBuild.Value = version.build.ToString();
        
        } else if (shareBuildNumber) {
            // Increment all build numbers together
            numberString = version.build.ToString();
            foreach (BuildTargetGroup targetGroup in System.Enum.GetValues(typeof(BuildTargetGroup))) {
                var value = (string)GetBuildNumber.Invoke(null, new object[] { targetGroup });
                if (!string.IsNullOrEmpty(value) && value != "0") {
                    SetBuildNumber.Invoke(null, new object[] { targetGroup, numberString });
                }
            }

            PlayerSettings.Android.bundleVersionCode = version.build;
        
        } else {
            // Only increment the number for the current build target group
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (targetGroup == BuildTargetGroup.Android) {
                PlayerSettings.Android.bundleVersionCode = version.build;
            } else {
                SetBuildNumber.Invoke(null, new object[] { targetGroup, version.build.ToString() });
            }
        }

        return version;
    }

    static MethodInfo GetBuildNumber {
        get {
            if (_getBuildNumber == null) {
                // internal static extern string GetBuildNumber(BuildTargetGroup targetGroup);
                _getBuildNumber = typeof(PlayerSettings).GetMethod(
                    "GetBuildNumber", 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );
                if (_getBuildNumber == null) {
                    Debug.LogError("OptionVersion: Could not find internal GetBuildNumber method on PlayerSettings.");
                }
            }
            return _getBuildNumber;
        }
    }
    static MethodInfo _getBuildNumber;

    static MethodInfo SetBuildNumber {
        get {
            if (_setBuildNumber == null) {
                // internal static extern void SetBuildNumber(BuildTargetGroup targetGroup, string buildNumber);
                _setBuildNumber = typeof(PlayerSettings).GetMethod(
                    "SetBuildNumber", 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );
                if (_setBuildNumber == null) {
                    Debug.LogError("OptionVersion: Could not find internal SetBuildNumber method on PlayerSettings.");
                }
            }
            return _setBuildNumber;
        }
    }
    static MethodInfo _setBuildNumber;
}

}

#endif
