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

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using sttz.Trimmer.Extensions;
using sttz.Trimmer.BaseOptions;

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


    static BuildOptions[] optionFlags;

    /// <summary>
    /// Unity's EnumMaskPopup doesn't work with enums where the flags are not
    /// neatly sorted and without gaps, so it doesn't work with BuildOptions.
    /// We implement a custom menu here to work around this and can also hide
    /// some obsolete options (which have been set to 0) and sort them alphabetically.
    /// </summary>
    public override bool EditGUI()
    {
        EditorGUI.BeginChangeCheck();

        string name = "Multiple...";

        // Unity defines some deprecated flags as 0 as well (e.g. 
        // StripDebugSymbols or CompressTextures), which can get
        // C# confused and pick the wrong one, so hardcode 0 == None.
        if (Value == 0) {
            name = "None";

        // Check if mask is power of two, which means only one flag is set
        } else if ((Value & (Value - 1)) == 0) {
            name = Value.ToString();
        }

        if (GUILayout.Button(name, "MiniPullDown")) {
            if (optionFlags == null) {
                optionFlags = (BuildOptions[])Enum.GetValues(typeof(BuildOptions));
                Array.Sort(optionFlags, (a, b) => a.ToString().CompareTo(b.ToString()));
            }

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Clear Options"), false, () => {
                Value = 0;
            });
            menu.AddSeparator("");
            foreach (var flag in optionFlags) {
                if ((int)flag == 0) continue;
                var selected = (Value & flag) == flag;
                menu.AddItem(new GUIContent(flag.ToString()), selected, () => {
                    Value ^= flag;
                });
            }
            menu.ShowAsContext();
        }

        return EditorGUI.EndChangeCheck();
    }
}

}
#endif