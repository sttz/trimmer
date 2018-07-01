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