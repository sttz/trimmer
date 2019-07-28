//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if TR_OptionPrompt || UNITY_EDITOR

using System;
using sttz.Trimmer.BaseOptions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace sttz.Trimmer.Options
{

[Capabilities(OptionCapabilities.PresetDefault)]
public class OptionPrompt : OptionToggle
{
    protected override void Configure()
    {
        Category = "Configuration";
    }

    public override void Apply()
    {
        base.Apply();
        
        var prompt = OptionHelper.GetSingleton<Prompt>(Value);
        if (prompt != null) {
            prompt.enabled = Value;
            prompt.activationSequence = GetChild<OptionPromptActivation>().Value;
            prompt.fontSize = GetChild<OptionPromptFontSize>().Value;
            prompt.position = GetChild<OptionPromptPosition>().Value;
        }
    }

    #if UNITY_EDITOR
    override public void PostprocessScene(Scene scene, OptionInclusion inclusion)
    {
        base.PostprocessScene(scene, inclusion);

        var prompt = OptionHelper.InjectFeature<Prompt>(scene, inclusion);
        if (prompt != null) {
            prompt.activationSequence = GetChild<OptionPromptActivation>().Value;
            prompt.fontSize = GetChild<OptionPromptFontSize>().Value;
            prompt.position = GetChild<OptionPromptPosition>().Value;
        }
    }
    #endif

    public class OptionPromptFontSize : OptionInt { }

    public class OptionPromptPosition : OptionEnum<Prompt.Position> { }

    public class OptionPromptActivation : OptionString
    {
        protected override void Configure()
        {
            DefaultValue = "O-O-O";
        }
    }
}

}
#endif
