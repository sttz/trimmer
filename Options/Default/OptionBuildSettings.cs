#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

namespace sttz.Workbench
{

// BuildOptions
// Path
// Scenes

[BuildOnly]
public class OptionBuildSettings : OptionEnum<BuildOptions>
{
	public override string Name { get { return "BuildSettings"; } }

	protected override void Configure()
	{
        Category = "Build";
        IsMask = true;
		DefaultValue = "";
	}

	public class OptionBuildPath : OptionString
	{
		public override string Name { get { return "BuildPath"; } }

		protected override void Configure()
		{
			DefaultValue = "Export/%Target%/%ProductName%";
		}
	}

    public class OptionScenes : OptionAsset<SceneAsset>
	{
		public override string Name { get { return "Scenes"; } }

		protected override void Configure()
		{
            IsVariant = true;
            IsArrayVariant = true;
			DefaultValue = "";
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

    override public BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, bool includedInBuild, RuntimeProfile profile)
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
}

}
#endif