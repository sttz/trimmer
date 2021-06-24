//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if UNITY_EDITOR

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using sttz.Trimmer.Extensions;
using sttz.Trimmer.BaseOptions;
using UnityEditor.Build.Reporting;

namespace sttz.Trimmer.Options
{

[Capabilities(OptionCapabilities.ConfiguresBuild)]
public class OptionBuildSettings : OptionEnum<BuildOptions>
{
    protected override void Configure()
    {
        Category = "Build";
        IsMask = true;
    }

    public class OptionBuildPath : OptionString
    {
        protected override void Configure()
        {
            DefaultValue = "Export/%Target%/%ProductName%";
        }
    }

    public class OptionScenes : OptionAsset<SceneAsset>
    {
        protected override void Configure()
        {
            Variance = OptionVariance.Array;
            VariantDefaultParameter = "0";
        }
    }

    public class OptionSaveBuildInfo : OptionToggle
    {
        protected override void Configure()
        {
            DefaultValue = true;
        }
    }

#if UNITY_2019_4_OR_NEWER
    public class OptionAppendIfPossible : OptionToggle
    {
        protected override void Configure()
        {
            DefaultValue = true;
        }

        public override BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, OptionInclusion inclusion)
        {
            if (Value) {
                var canAppend = BuildPipeline.BuildCanBeAppended(options.target, options.locationPathName);
                if (canAppend == CanAppendBuild.Yes) {
                    Debug.Log($"Trimmer: Appending build is possible, adding option AcceptExternalModificationsToPlayer");
                    options.options |= BuildOptions.AcceptExternalModificationsToPlayer;
                } else {
                    Debug.Log($"Trimmer: Appending build is not possible ({canAppend})");
                }
            }

            return base.PrepareBuild(options, inclusion);
        }
    }
#endif

    /// <summary>
    /// Replace special variables in a ini file path.
    /// </summary>
    public string ExpandPath(string path, BuildPlayerOptions options)
    {
        path = path.ReplaceCaseInsensitive("%Target%", options.target.ToString());
        path = path.ReplaceCaseInsensitive("%Group%", options.targetGroup.ToString());
        path = path.ReplaceCaseInsensitive("%Version%", Application.version);
        path = path.ReplaceCaseInsensitive("%ProductName%", Application.productName);
        path = path.ReplaceCaseInsensitive("%CompanyName%", Application.companyName);
        path = path.ReplaceCaseInsensitive("%UnityVersion%", Application.unityVersion);
        path = path.ReplaceCaseInsensitive("%Development%", (options.options & BuildOptions.Development) > 0 ? "dev" : "");
        return path;
    }

    override public BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, OptionInclusion inclusion)
    {
        options.options |= Value;
        options.locationPathName = ExpandPath(GetChild<OptionBuildPath>().Value, options);

        var scenes = GetChild<OptionScenes>();
        if (scenes.Value != null || scenes.Variants.Any()) {
            var paths = new List<string>();
            if (scenes.Value != null) {
                paths.Add(AssetDatabase.GetAssetPath(scenes.Value));
            }
            foreach (OptionScenes variant in scenes.Variants) {
                if (variant.Value != null) {
                    paths.Add(AssetDatabase.GetAssetPath(variant.Value));
                }
            }
            if (paths.Count > 0) {
                options.scenes = paths.ToArray();
            }
        }

        return base.PrepareBuild(options, inclusion);
    }

    override public void PostprocessBuild(BuildReport report, OptionInclusion inclusion)
    {
        base.PostprocessBuild(report, inclusion);

        if (GetChild<OptionSaveBuildInfo>().Value) {
            if (BuildInfo.Current == null) {
                Debug.LogWarning("Save build info: BuildInfo.Current not set");
            } else {
                var infoPath = OptionHelper.GetBuildBasePath(report.summary.outputPath);
                infoPath = System.IO.Path.Combine(infoPath, BuildInfo.DEFAULT_NAME);

                var json = BuildInfo.Current.ToJson();
                File.WriteAllText(infoPath, json);
            }
        }
    }

    static BuildOptions[] optionFlags;
    static string[] optionNames;

    /// <summary>
    /// Unity's EnumMaskPopup doesn't work with enums where the flags are not
    /// neatly sorted and without gaps, so it doesn't work with BuildOptions.
    /// We implement a custom menu here to work around this and can also hide
    /// some obsolete options (which have been set to 0) and sort them alphabetically.
    /// </summary>
    public override bool EditGUI()
    {
        if (optionFlags == null || optionNames == null) {
            optionFlags = (BuildOptions[])Enum.GetValues(typeof(BuildOptions));
            optionNames = Enum.GetNames(typeof(BuildOptions));
            Array.Sort(optionNames, optionFlags);
        }

        string name = "Multiple...";

        // Unity defines some deprecated flags as 0 as well (e.g. 
        // StripDebugSymbols or CompressTextures), which can get
        // C# confused and pick the wrong one, so hardcode 0 == None.
        if (Value == 0) {
            name = "None";

        // Check if mask is power of two, which means only one flag is set
        } else if ((Value & (Value - 1)) == 0) {
            var index = Array.IndexOf(optionFlags, Value);
            if (index >= 0 && index < optionNames.Length) {
                name = optionNames[index];
            }
        }

        if (GUILayout.Button(name, "MiniPullDown")) {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Clear Options"), false, () => {
                Value = 0;
                Option.changed = true;
            });
            menu.AddSeparator("");
            for (int i = 0; i < optionFlags.Length && i < optionNames.Length; i++) {
                var flag = optionFlags[i];
                if ((int)flag == 0) continue;
                var selected = (Value & flag) == flag;
                menu.AddItem(new GUIContent(optionNames[i]), selected, () => {
                    Value ^= flag;
                    Option.changed = true;
                });
            }
            menu.ShowAsContext();
        }

        // The value is changed in the GenericMenu callbacks,
        // which set Option.changed to true manually instead of returning true here.
        return false;
    }
}

}
#endif
