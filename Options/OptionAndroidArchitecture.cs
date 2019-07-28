//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if UNITY_EDITOR && !UNITY_2018_2_OR_NEWER

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
public class OptionAndroidArchitecture : OptionEnum<AndroidTargetDevice>
{
    protected override void Configure()
    {
        Category = "Build";
        SupportedTargets = new BuildTarget[] {
            BuildTarget.Android
        };
    }

    AndroidTargetDevice originalValue;

    public override void PreprocessBuild(BuildTarget target, string path, OptionInclusion inclusion)
    {
        base.PreprocessBuild(target, path, inclusion);

        originalValue = PlayerSettings.Android.targetDevice;
        PlayerSettings.Android.targetDevice = Value;
    }

    public override void PostprocessBuild(BuildTarget target, string path, OptionInclusion inclusion)
    {
        base.PostprocessBuild(target, path, inclusion);

        PlayerSettings.Android.targetDevice = originalValue;
    }
}

}
#endif
