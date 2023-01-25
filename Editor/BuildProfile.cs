//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;
using sttz.Trimmer.Extensions;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Build Profiles configure builds. They define which Options are
/// included, how the Options are configured and give Options a 
/// chance to influence the build process.
/// </summary>
/// <remarks>
/// A project can contain multiple Build Profiles and each profile
/// can build multiple platforms at once. A Build Profile contains
/// the configuration values for all Options and configures if an
/// Option or its associated feature are included in the build.
/// 
/// The <see cref="EditorProfile"/> is used to configure the project
/// when playing in the editor.
/// 
/// In the build, <see cref="Options.OptionPrompt"/> and 
/// <see cref="Options.OptionIniFile"/> can be used to change the 
/// included Options' configuration.
/// 
/// See the <see cref="BuildManager"/> for methods to build profiles.
/// </remarks>
[CreateAssetMenu(fileName = "Build Profile.asset", menuName = "Trimmer/Build Profile")]
[HelpURL("https://sttz.ch/trimmer/manual/using_trimmer.html")]
public class BuildProfile : EditableProfile, IEditorProfile
{
    // -------- Static --------

    /// <summary>
    /// Enumeration of all build profiles in the current project.
    /// </summary>
    public static IEnumerable<BuildProfile> AllBuildProfiles {
        get {
            if (_buildProfiles == null || _buildProfiles.Any(p => p == null)) {
                _buildProfiles = null;

                var profiles = new List<BuildProfile>();
                var guids = AssetDatabase.FindAssets("t:BuildProfile");
                foreach (var guid in guids) {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var profile = AssetDatabase.LoadAssetAtPath(path, typeof(BuildProfile));
                    profiles.Add((BuildProfile)profile);
                }

                // Assign _buildProfiles only here because LoadAssetAtPath will cause
                // the newly loaded profile's OnEnable to be called, which will check
                // to invalidate _buildProfiles.
                _buildProfiles = profiles;
            }
            return _buildProfiles;
        }
    }
    private static List<BuildProfile> _buildProfiles;

    /// <summary>
    /// Look for a Build Profile by its name (case insensitive).
    /// </summary>
    public static BuildProfile Find(string name)
    {
        return AllBuildProfiles
            .Where(p => p.name.EqualsIgnoringCase(name))
            .FirstOrDefault();
    }

    static BuildTarget[] activeBuildTarget;

    /// <summary>
    /// Option needs to have one of these capabilities to be 
    /// displayed in Build Profiles.
    /// </summary>
    const OptionCapabilities requiredCapabilities = (
        OptionCapabilities.HasAssociatedFeature
        | OptionCapabilities.CanIncludeOption
        | OptionCapabilities.ConfiguresBuild
    );

    // ------ Build Targets ------

    [SerializeField] List<BuildTarget> _buildTargets;

    /// <summary>
    /// The build targets this profile will create builds for.
    /// </summary>
    /// <remarks>
    /// If the profile doesn't define any targets, this method
    /// will return the active build target.
    /// </remarks>
    public IEnumerable<BuildTarget> BuildTargets {
        get {
            if (_buildTargets != null && _buildTargets.Count > 0) {
                return _buildTargets;
            } else {
                if (activeBuildTarget == null 
                        || activeBuildTarget[0] != EditorUserBuildSettings.activeBuildTarget) {
                    activeBuildTarget = new BuildTarget[] {
                        EditorUserBuildSettings.activeBuildTarget
                    };
                }
                return activeBuildTarget;
            }
        }
    }

    /// <summary>
    /// Returns wether the profile has no explicit build targets set
    /// and builds the active build target instead.
    /// </summary>
    public bool UsesActiveBuildTarget()
    {
        return (_buildTargets == null || _buildTargets.Count == 0);
    }

    /// <summary>
    /// Add a build target to the profile.
    /// </summary>
    public void AddBuildTarget(BuildTarget target)
    {
        if (_buildTargets == null) {
            _buildTargets = new List<BuildTarget>();
        } else if (_buildTargets.Contains(target)) {
            return;
        }

        _buildTargets.Add(target);
        EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Remove a build target form the profile.
    /// </summary>
    /// <remarks>
    /// > [!NOTE]
    /// > If the profile has no build targets set, it will build the
    /// > active build target.
    /// </remarks>
    public void RemoveBuildTarget(BuildTarget target)
    {
        if (_buildTargets == null) return;
        _buildTargets.Remove(target);
        EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Get the path to the last build of this profile for the given target.
    /// </summary>
    /// <remarks>
    /// Build paths are only saved on the instance and are lost once
    /// Unity does a domain reload or restarts.
    /// </remarks>
    /// <returns>The path or null if no path is recorded.</returns>
    public string GetLastBuildPath(BuildTarget target)
    {
        return PlayerPrefs.GetString(GetLastBuildPathSettingName(target));
    }

    public void SetLastBuildPath(BuildTarget target, string path)
    {
        PlayerPrefs.SetString(GetLastBuildPathSettingName(target), path);
    }

    private string GetLastBuildPathSettingName(BuildTarget target)
    {
        return $"{target.ToString()}:{OptionHelper.GetAssetGUID(this)}:lastBuildPath";
    }

    // ------ Context Menu ------

    [ContextMenu("Activate Profile")]
    public void ActivateProfile()
    {
        EditorProfile.Instance.ActiveProfile = this;
    }

    // ------ Build Profile ------

    /// <summary>
    /// Check if an Option should be included in builds of this profile.
    /// </summary>
    public OptionInclusion GetInclusionOf(Option option, BuildTarget target)
    {
        var node = store.GetRoot(option.Name);
        if (node == null) {
            return OptionInclusion.Remove;
        } else {
            if (!option.IsAvailable(target))
                return OptionInclusion.Remove;
            
            var inclusion = node.Inclusion;
            if ((option.Capabilities & OptionCapabilities.CanIncludeOption) == 0) {
                inclusion &= ~OptionInclusion.Option;
            }
            if ((option.Capabilities & OptionCapabilities.HasAssociatedFeature) == 0) {
                inclusion &= ~OptionInclusion.Feature;
            } else if (inclusion == OptionInclusion.Feature && !option.ShouldIncludeOnlyFeature()) {
                inclusion &= ~OptionInclusion.Feature;
            }

            if ((option.Capabilities & OptionCapabilities.ConfiguresBuild) != 0) {
                inclusion |= OptionInclusion.Build;
            }
            
            return inclusion;
        }
    }

    // ------ Editable Profile ------

    /// <summary>
    /// Profile used to edit build profiles.
    /// </summary>
    private class EditBuildProfile : RuntimeProfile
    {
        public EditBuildProfile(IEditorProfile profile, ValueStore store) : base(store)
        {
            foreach (var option in this) {
                option.SetEditorProfile(profile);
            }
        }

        protected override bool ShouldCreateOption(Type optionType)
        {
            var caps = optionType.GetOptionCapabilities();
            return (caps & requiredCapabilities) != 0;
        }
    }

    /// <summary>
    /// Edit profile used when there's no active build profile.
    /// </summary>
    public static RuntimeProfile EmptyEditProfile {
        get {
            if (_emptyProfile == null) {
                _emptyProfile = new EditBuildProfile(null, new ValueStore());
            }
            return _emptyProfile;
        }
    }
    static EditBuildProfile _emptyProfile;

    [SerializeField] ValueStore store = new ValueStore();

    public override ValueStore Store {
        get {
            return store;
        }
    }

    EditBuildProfile editProfile;

    public override RuntimeProfile EditProfile {
        get {
            if (editProfile == null) {
                editProfile = new EditBuildProfile(this, store);
            }
            return editProfile;
        }
    }

    public override void SaveToStore()
    {
        EditProfile.SaveToStore(clear: false);
    }

    public override void SaveIfNeeded()
    {
        // Unity overrides the == operator and this will be true if the profile
        // has been destroyed
        if (this == null) return;

        // Make sure changes to the store get serialized
        if (store.IsDirty(true)) {
            EditorUtility.SetDirty(this);
        }
    }

    public override void EditOption(Option option)
    {
        try {
            if (option.EditGUI()) {
                Option.changed = true;
            }
        } catch (Exception e) {
            EditorGUILayout.HelpBox($"Error showing the Option GUI:\n{e.Message}", MessageType.Error);
        }
    }

    // -------- Internals --------

    void OnEnable()
    {
        // Invalidate AllBuildProfiles when a new one is created
        if (_buildProfiles != null && !_buildProfiles.Contains(this)) {
            _buildProfiles = null;
        }
    }
}

}

