#if WB_OptionPrompt || UNITY_EDITOR
using System;
using sttz.Workbench.BaseOptions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace sttz.Workbench.Options
{

[Capabilities(OptionCapabilities.PresetWithFeature)]
public class OptionPrompt : OptionToggle
{
	protected override void Configure()
	{
		Category = "Configuration";
	}

	public override void Apply()
	{
		base.Apply();
		
		// Do not create instance when disabled
		if (Prompt.Instance == null && !Value)
			return;

		CreateAndUpdate();
	}

	public void CreateAndUpdate()
	{
		var prompt = Prompt.Instance;

		if (prompt == null) {
			var go = new GameObject("Prompt");
			prompt = go.AddComponent<Prompt>();
		}

		prompt.enabled = Value;
		prompt.activationSequence = GetChild<OptionPromptActivation>().Value;
		prompt.fontSize = GetChild<OptionPromptFontSize>().Value;
		prompt.position = GetChild<OptionPromptPosition>().Value;
	}

	#if UNITY_EDITOR
	override public void PostprocessScene(Scene scene, OptionInclusion inclusion)
	{
		base.PostprocessScene(scene, inclusion);

		// Inject prompt into first scene when only including feature and prompt enabled
		if (inclusion == OptionInclusion.Feature && scene.buildIndex == 0 && Value) {
			CreateAndUpdate();
		}
	}
	#endif

	public class OptionPromptFontSize : OptionInt
	{
		protected override void Configure() { }
	}

	public class OptionPromptPosition : OptionEnum<Prompt.Position>
	{
		protected override void Configure() { }
	}

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