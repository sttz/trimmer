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
			DefaultValue = "Export/%Target%";
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

    override public BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, bool includedInBuild, RuntimeProfile profile)
    {
        options.options |= Value;
        options.locationPathName = GetChild<OptionBuildPath>().Value;
        
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