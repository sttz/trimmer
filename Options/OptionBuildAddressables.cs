//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if UNITY_EDITOR && TRIMMER_ADDRESSABLES

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using sttz.Trimmer.BaseOptions;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEditor;
using UnityEngine.AddressableAssets;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace sttz.Trimmer.Options
{

/// <summary>
/// Option to build Addressables before a player build.
/// </summary>
/// <remarks>
/// The settings, profile id and data builder child options
/// can be left empty for the defaults to be used. Some of 
/// the options can be set to override the defaults, the 
/// option will try to revert any changes it has made.
/// </remarks>
[Capabilities(OptionCapabilities.ConfiguresBuild)]
public class OptionBuildAddressables : OptionToggle
{
    protected override void Configure()
    {
        Category = "Build";
    }

    /// <summary>
    /// Option to set the Addressable settings to use.
    /// </summary>
    /// <remarks>
    /// Defaults to the Addressables default settings object.
    /// </remarks>
    public class OptionSettings : OptionAsset<AddressableAssetSettings>
    {
        protected override void Configure()
        {
            DefaultValue = null;
        }
    }

    /// <summary>
    /// Option to select the Addressables profile (by id).
    /// </summary>
    /// <remarks>
    /// Defaults to the setting's active profile.
    /// </remarks>
    public class OptionProfileId : OptionString
    {
        protected override void Configure()
        {
            DefaultValue = "";
        }
    }

    /// <summary>
    /// Option to select the data builder script.
    /// </summary>
    /// <remarks>
    /// Defaults to the active player data builder. The builder object
    /// will be added to the settings object (the default if <see cref="OptionSettings"/>
    /// is not set) if it does not exist yet.
    /// </remarks>
    public class OptionDataBuilder : OptionAsset<ScriptableObject>
    {
        protected override void Configure()
        {
            DefaultValue = null;
        }
    }
    
    /// <summary>
    /// Option to enable output from <see cref="UnityEngine.AddressableAssets.Addressables.Log"/>
    /// and its cohorts at runtime,
    /// including in player builds.
    /// </summary>
    /// <remarks>
    /// This option sets the <c>ADDRESSABLES_LOG_ALL</c> compilation symbol.
    /// If it's defined by other means, this option will not remove it.
    /// </remarks>
    public class OptionEnableRuntimeLogging : OptionToggle
    {
        const string Symbol = "ADDRESSABLES_LOG_ALL";

        protected override void Configure()
        {
            DefaultValue = false;
        }

        public override void GetScriptingDefineSymbols(OptionInclusion inclusion, HashSet<string> symbols)
        {
            base.GetScriptingDefineSymbols(inclusion, symbols);

            if (Value)
                symbols.Add(Symbol);
        }
    }

    /// <summary>
    /// Option to clear any existing cached data from previous builds
    /// before beginning a new content build.
    /// </summary>
    /// <remarks>
    /// Calls the selected builder's <see cref="IDataBuilder.ClearCachedData"/> method
    /// within the <see cref="PrepareBuild"/> phase,
    /// just before the content build.
    /// Defaults to <see langword="false"/>.
    /// Set this to <see langword="true"/> for a clean build of your assets,
    /// at the cost of a longer build time.
    /// </remarks>
    public class OptionRebuildContent : OptionToggle
    {
        protected override void Configure()
        {
            DefaultValue = false;
        }
    }

    /// <summary>
    /// Option to copy the generated build timeline to the output directory
    /// to simplify analysis later.
    /// </summary>
    /// <remarks>
    /// Addressables exposes a detailed timeline of its build process through
    /// a file located at <c>Library/com.unity.addressables/AddressablesBuildTEP.json</c>.
    /// This file is overwritten with each Addressables build,
    /// which can complicate analysis if your project involves multiple build profiles.
    /// This option, if enabled, will copy the aforementioned file
    /// to the directory given by <see cref="AddressableAssetSettings.RemoteCatalogBuildPath"/>.
    /// </remarks>
    /// <seealso href="https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/BuildProfileLog"/>
    public class OptionCopyBuildTimelineToOutputDirectory : OptionToggle
    {
        protected override void Configure()
        {
            DefaultValue = false;
        }
    }
    
    /// <summary>
    /// Option to copy the generated build layout to the output directory
    /// to simplify analysis later.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Addressables provides a detailed layout of the asset bundles it produces in
    /// a file located at <c>Library/com.unity.addressables/buildlayout.txt</c>.
    /// This file is overwritten with each Addressables build,
    /// which can complicate analysis if your project involves multiple build profiles.
    /// This option, if enabled, will copy the aforementioned file
    /// to the directory given by <see cref="AddressableAssetSettings.RemoteCatalogBuildPath"/>.
    /// </para>
    /// <para>
    /// If build layouts are disabled, this option will do nothing.
    /// </para>
    /// </remarks>
    /// <seealso href="https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/BuildLayoutReport"/>
    public class OptionCopyBuildLayoutToOutputDirectory : OptionToggle
    {
        protected override void Configure()
        {
            DefaultValue = false;
        }
    }

    override public BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, OptionInclusion inclusion)
    {
        options = base.PrepareBuild(options, inclusion);

        if (Value) BuildAddressables();

        return options;
    }

    void BuildAddressables()
    {
        // Original values to restore overrides
        var originalSettings = AddressableAssetSettingsDefaultObject.Settings;
        string originalProfileId = null;
        int originalDataBuilderIndex = -1;

        var settings = originalSettings;
        var result = default(AddressablesPlayerBuildResult);
        try {
            // Apply overrides
            var settingsOption = GetChild<OptionSettings>();
            if (settingsOption.Value != null) {
                settings = AddressableAssetSettingsDefaultObject.Settings = settingsOption.Value;
            }

            if (settings == null) {
                throw new BuildFailedException($"OptionBuildAddressables: No Addressables Asset Settings object set and no default set either.");
            }

            var profileOption = GetChild<OptionProfileId>();
            if (!string.IsNullOrEmpty(profileOption.Value)) {
                originalProfileId = settings.activeProfileId;
                settings.activeProfileId = profileOption.Value;
            }

            var builderOption = GetChild<OptionDataBuilder>();
            if (builderOption.Value != null) {
                var index = settings.DataBuilders.IndexOf(builderOption.Value);
                if (index < 0) {
                    if (!settings.AddDataBuilder((IDataBuilder)builderOption.Value)) {
                        throw new Exception($"OptionBuildAddressables: Failed to add data builder to settings (builder = {builderOption.Value})");
                    }
                    index = settings.DataBuilders.Count - 1;
                }
                originalDataBuilderIndex = settings.ActivePlayerDataBuilderIndex;
                settings.ActivePlayerDataBuilderIndex = index;
            }

            // Clear cached data if requested
            if (GetChild<OptionRebuildContent>() is { Value: true }) {
                settings.ActivePlayerDataBuilder?.ClearCachedData();
            }

            // Build!
            AddressableAssetSettings.BuildPlayerContent(out result);

            // Copy relevant logs to the build directory for easier analysis, if requested
            var localBuildPath = settings.RemoteCatalogBuildPath?.GetValue(settings);
            if (localBuildPath != null) { // little bit of insurance against a malformed settings file
                if (GetChild<OptionCopyBuildTimelineToOutputDirectory>() is { Value: true }) {
                    // Copy the build timeline to the output directory
                    const string BuildTimelineFilename = "AddressablesBuildTEP.json";
                    var buildTimelineSource = IOPath.Combine(Addressables.LibraryPath, BuildTimelineFilename);

                    if (IOFile.Exists(buildTimelineSource)) {
                        var buildTimelineDestination = IOPath.Combine(localBuildPath, BuildTimelineFilename);
                        File.Copy(buildTimelineSource, buildTimelineDestination);
                    }
                }

                if (GetChild<OptionCopyBuildLayoutToOutputDirectory>() is { Value: true }) {
                    // Copy the build layout to the output directory
                    const string BuildLayoutFilename = "buildlayout.txt"; 
                    var buildLayoutSource = IOPath.Combine(Addressables.LibraryPath, BuildLayoutFilename);

                    if (IOFile.Exists(buildLayoutSource)) {
                        var buildLayoutDestination = IOPath.Combine(localBuildPath, BuildLayoutFilename);
                        File.Copy(buildLayoutSource, buildLayoutDestination);
                    }
                }
            }
        } finally {
            // Restore overrides
            if (settings != null) {
                if (originalDataBuilderIndex >= 0) {
                    settings.ActivePlayerDataBuilderIndex = originalDataBuilderIndex;
                }
                if (originalProfileId != null) {
                    settings.activeProfileId = originalProfileId;
                }
            }
            AddressableAssetSettingsDefaultObject.Settings = originalSettings;
        }

        if (!string.IsNullOrEmpty(result.Error)) {
            throw new BuildFailedException("OptionBuildAddressables: Addressables build failed with error:\n" + result.Error);
        }

        Debug.Log($"Built {result.LocationCount} Addressable assets in {result.Duration}s to {result.OutputPath}");
    }
}

}

#endif
