//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if TR_OptionTrimmerServer || UNITY_EDITOR

using System.Collections.Generic;
using sttz.Trimmer.BaseOptions;
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
            if (Value && host.Server == null) {
                host.CreateServer(
                    GetChild<OptionServerPort>().Value, 
                    GetChild<OptionDiscoverable>().Value,
                    GetChild<OptionIPV6>().Value
                );
            }
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
            host.ipv6 = GetChild<OptionIPV6>().Value;
            host.isDiscoverable = GetChild<OptionDiscoverable>().Value;
        }
    }

    override public void GetScriptingDefineSymbols(OptionInclusion inclusion, HashSet<string> symbols)
    {
        base.GetScriptingDefineSymbols(inclusion, symbols);

        if (inclusion.HasFlag(OptionInclusion.Option)) {
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

    public class OptionIPV6 : OptionToggle
    {
        override protected void Configure()
        {
            DefaultValue = false;
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
