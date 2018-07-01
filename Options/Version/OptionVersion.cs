//
// Trimmer Framework for Unity - https://sttz.ch/trimmer
// Copyright © 2017 Adrian Stutz
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using sttz.Trimmer;
using sttz.Trimmer.BaseOptions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace sttz.Trimmer.Options
{

/// <summary>
/// Option to manage the version of your build and access 
/// version info at runtime.
/// </summary>
/// <remarks>
/// Unity's project version manegement is fragmented and differs greatly
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


    override public void Apply()
    {
        base.Apply();

        // In the editor we don't need the container and set the version directly
        Version.ProjectVersion = DetermineProjectVersion(EditorUserBuildSettings.activeBuildTarget);
    }

    override public void PreprocessBuild(BuildTarget target, string path, OptionInclusion inclusion)
    {
        base.PreprocessBuild(target, path, inclusion);

        if (inclusion == OptionInclusion.Remove) return;

        Version.ProjectVersion = DetermineProjectVersion(target);

        if (incrementBuildNumber) {
            Version.ProjectVersion = IncrementBuildNumber(Version.ProjectVersion, target);
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