#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using sttz.Workbench.Extensions;
using sttz.Workbench.BaseOptions;

namespace sttz.Workbench.Options
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

        return options;
    }

    #if UNITY_EDITOR
    static BuildOptions[] optionValues;
    static Dictionary<int, string> pendingUpdates = new Dictionary<int, string>();

    /// <summary>
    /// Unity's EnumMaskPopup doesn't work with enums where the flags are not
    /// neatly sorted and without gaps, so it doesn't work with BuildOptions.
    /// We implement a custom menu here to work around this and can also hide
    /// some obsolete options (which have been set to 0) and sort them alphabetically.
    /// </summary>
	public override string EditGUI(string input)
	{
        var nextControlID = GUIUtility.GetControlID(FocusType.Passive) + 1;
        if (GUILayout.Button(input, "MiniPullDown")) {
            if (optionValues == null) {
                optionValues = (BuildOptions[])Enum.GetValues(typeof(BuildOptions));
                Array.Sort(optionValues, (a, b) => a.ToString().CompareTo(b.ToString()));
            }

            var options = Parse(input);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Clear Options"), false, () => {
                pendingUpdates[nextControlID] = Save(BuildOptions.None);
            });
            menu.AddSeparator("");
            foreach (var value in optionValues) {
                if ((int)value == 0) continue;
                var selected = (options & value) == value;
                menu.AddItem(new GUIContent(value.ToString()), selected, () => {
                    options ^= value;
                    pendingUpdates[nextControlID] = Save(options);
                });
            }
            menu.ShowAsContext();
        
        } else if (pendingUpdates.ContainsKey(nextControlID)) {
            input = pendingUpdates[nextControlID];
            pendingUpdates.Remove(nextControlID);
        }

		return input;
	}
	#endif
}

}
#endif