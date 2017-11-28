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

#if TR_OptionTrimmerServer || UNITY_EDITOR

using System;
using System.Collections.Generic;
using sttz.Trimmer.BaseOptions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace sttz.Trimmer.Options
{

/// <summary>
/// Option to include <see cref="TrimmerServer"/> in a build to configure
/// it over the network.
/// </summary>
[Capabilities(OptionCapabilities.CanIncludeOption)]
public class OptionTrimmerServer : OptionToggle
{
    protected override void Configure()
    {
        Category = "Configuration";
        DefaultValue = false;
    }

    public override void Apply()
    {
        base.Apply();
        
        var host = OptionHelper.GetSingleton<TrimmerServerHost>(Value);
        if (host != null) {
            host.enabled = Value;
            host.serverPort = GetChild<OptionServerPort>().Value;
            host.isDiscoverable = GetChild<OptionDiscoverable>().Value;
        }
    }

    #if UNITY_EDITOR
    override public bool ShouldIncludeOnlyFeature()
    {
        return Value;
    }

    override public void PostprocessScene(Scene scene, OptionInclusion inclusion)
    {
        base.PostprocessScene(scene, inclusion);

        var host = OptionHelper.InjectFeature<TrimmerServerHost>(scene, inclusion);
        if (host != null) {
            host.enabled = Value;
            host.serverPort = GetChild<OptionServerPort>().Value;
            host.isDiscoverable = GetChild<OptionDiscoverable>().Value;
        }
    }

    override public void GetScriptingDefineSymbols(OptionInclusion inclusion, HashSet<string> symbols)
    {
        base.GetScriptingDefineSymbols(inclusion, symbols);

        if (inclusion != OptionInclusion.Remove) {
            symbols.Add("TRIMMER_SERVER");
        }
    }
    #endif

    public class OptionServerPort : OptionInt
    {
        override protected void Configure()
        {
            DefaultValue = 21076;
        }
    }

    public class OptionDiscoverable : OptionToggle
    {
        override protected void Configure()
        {
            DefaultValue = true;
        }
    }
}

}
#endif