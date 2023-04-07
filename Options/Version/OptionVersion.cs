//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using sttz.Trimmer.BaseOptions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;

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
    public class OptionOverrideBuild : OptionInt
    {
        override protected void Configure()
        {
            DefaultValue = -1;
        }
    }

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
    /// How to share the build number across platforms.
    /// </summary>
    public enum BuildNumberSharing
    {
        /// <summary>
        /// Do not share build number, only update the current platform.
        /// </summary>
        None,
        /// <summary>
        /// Share the build number across all platforms in the profile.
        /// </summary>
        InProfile,
        /// <summary>
        /// Share the build number across all platforms in the project.
        /// </summary>
        InProject,
    }

    /// <summary>
    /// Use the highest build number across multiple platforms when incrementing.
    /// </summary>
    public class OptionShareBuildNumber : OptionEnum<BuildNumberSharing>
    {
        override protected void Configure()
        {
            DefaultValue = BuildNumberSharing.None;
        }
    }

    /// <summary>
    /// Increment the build number on every build.
    /// </summary>
    /// <remarks>
    /// Automatic build number incrementing will increment the build
    /// number per target, even if those targets are built in a 
    /// single profile.
    /// </remarks>
    public class OptionIncrementBuildNumber : OptionToggle
    {
        override protected void Configure()
        {
            DefaultValue = true;
        }

        public override bool EditGUI()
        {
            var changed = false;
            EditorGUILayout.BeginHorizontal();
            {
                changed = base.EditGUI();

                if (EditorProfile != null 
                        && EditorProfile.BuildTargets.Any() 
                        && GUILayout.Button("Increment Now", EditorStyles.miniButton)) {
                    var parent = (OptionVersion)Parent;
                    var messages = new List<string>();
                    if (parent.GetChild<OptionShareBuildNumber>().Value == BuildNumberSharing.None) {
                        // Build numbers aren't shared, increment for each platform in the profile
                        foreach (var target in EditorProfile.BuildTargets) {
                            var version = parent.DetermineProjectVersion(target);
                            version = parent.IncrementBuildNumber(version, target);
                            messages.Add($"Incremented {target} build number to {version.build}");
                        }
                    } else {
                        // Build numbers are shared, only increment once
                        var firstTarget = EditorProfile.BuildTargets.First();
                        var version = parent.DetermineProjectVersion(firstTarget);
                        version = parent.IncrementBuildNumber(version, firstTarget);
                        messages.Add($"Incremented shared build number to {version.build}");
                    }
                    EditorUtility.DisplayDialog(
                        "Increment Build Number", 
                        string.Join("\n", messages),
                        "OK"
                    );
                }
            }
            EditorGUILayout.EndHorizontal();
            return changed;
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

        if (GetChild<OptionIncrementBuildNumber>().Value) {
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
        var sharing = GetChild<OptionShareBuildNumber>().Value;
        var overrideBuild = GetChild<OptionOverrideBuild>().Value;
        if (overrideBuild >= 0) {
            // Build number has been overridden
            version.build = overrideBuild;
        
        } else if (sharing != BuildNumberSharing.None) {
            // Look for highest build number across build target groups, increment it
            // and then apply it back to all groups
            if (!LoadBuildNumberMethods()) {
                Debug.LogError("OptionVersion: Could not find internal GetBuildNumber method on PlayerSettings.");
                return default(Version);
            }

            var buildNumbers = new List<int>();
            foreach (var targetGroup in GetSharedTargets(sharing)) {
                var numberString = GetBuildNumber(targetGroup);
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
                if (!LoadBuildNumberMethods()) {
                    Debug.LogError("OptionVersion: Could not find internal GetBuildNumber method on PlayerSettings.");
                    return default(Version);
                }

                var numberString = GetBuildNumber(targetGroup);
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
        var sharing = GetChild<OptionShareBuildNumber>().Value;
        if (optionOverrideBuild.Value >= 0) {
            // Build number has been overridden, increment override
            optionOverrideBuild.Value = version.build;
        
        } else if (sharing != BuildNumberSharing.None) {
            // Increment all build numbers together
            numberString = version.build.ToString();
            foreach (var targetGroup in GetSharedTargets(sharing)) {
                var value = GetBuildNumber(targetGroup);
                if (!string.IsNullOrEmpty(value)) {
                    SetBuildNumber(targetGroup, numberString);
                }
            }

            PlayerSettings.Android.bundleVersionCode = version.build;
        
        } else {
            // Only increment the number for the current build target group
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (targetGroup == BuildTargetGroup.Android) {
                PlayerSettings.Android.bundleVersionCode = version.build;
            } else {
                SetBuildNumber(targetGroup, version.build.ToString());
            }
        }

        return version;
    }

    IEnumerable<BuildTargetGroup> GetSharedTargets(BuildNumberSharing sharing)
    {
        var targets = new HashSet<BuildTargetGroup>();
        if (sharing == BuildNumberSharing.InProject) {
            foreach (BuildTargetGroup targetGroup in System.Enum.GetValues(typeof(BuildTargetGroup))) {
                if (targetGroup == BuildTargetGroup.Unknown) continue;
            #if UNITY_2021_2_OR_NEWER
                try {
                    var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
                    targets.Add(targetGroup);
                } catch {
                    // ignore
                }
            #else
                targets.Add(targetGroup);
            #endif
            }
        } else if (sharing == BuildNumberSharing.InProfile) {
            foreach (var profileTarget in EditorProfile.BuildTargets) {
                targets.Add(BuildPipeline.GetBuildTargetGroup(profileTarget));
            }
        } else {
            throw new System.Exception($"GetSharedTargets: Unexpected sharing value: {sharing}");
        }
        return targets;
    }

    static bool LoadBuildNumberMethods()
    {
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

        return (_getBuildNumber != null && _setBuildNumber != null);
    }

    static string GetBuildNumber(BuildTargetGroup targetGroup)
    {
        if (_getBuildNumber == null)
            return null;

    #if UNITY_2021_2_OR_NEWER
        var targetName = NamedBuildTarget.FromBuildTargetGroup(targetGroup).TargetName;
        return (string)_getBuildNumber.Invoke(null, new object[] { targetName });
    #else
        return (string)_getBuildNumber.Invoke(null, new object[] { targetGroup });
    #endif
    }
    static MethodInfo _getBuildNumber;

    static void SetBuildNumber(BuildTargetGroup targetGroup, string buildNumber)
    {
        if (_setBuildNumber == null)
            return;

    #if UNITY_2021_2_OR_NEWER
        var targetName = NamedBuildTarget.FromBuildTargetGroup(targetGroup).TargetName;
        _setBuildNumber.Invoke(null, new object[] { targetName, buildNumber });
    #else
        _setBuildNumber.Invoke(null, new object[] { targetGroup, buildNumber });
    #endif
    }
    static MethodInfo _setBuildNumber;
}

}

#endif
