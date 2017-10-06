#if OPTION_Prompt || UNITY_EDITOR
using System;
using UnityEngine;

namespace sttz.Workbench.Prompt
{

public class OptionPrompt : OptionToggle
{
	public override string Name { get { return "Prompt"; } }

	protected override void Configure()
	{
		DefaultValue = "";
	}

	public override void Apply()
	{
		base.Apply();

		// TODO: Fix this
		var isBuilding = false;
		#if UNITY_EDITOR
		isBuilding = UnityEditor.BuildPipeline.isBuildingPlayer;
		#endif

		if (!isBuilding) {
			var prompt = Prompt.Instance;
			if (prompt == null) {
				if (!Value)
					return;

				var go = new GameObject("Prompt");
				prompt = go.AddComponent<Prompt>();
			}

			prompt.enabled = Value;
			prompt.activationSequence = GetChild<OptionPromptActivation>().Value;
			prompt.fontSize = GetChild<OptionPromptFontSize>().Value;
			prompt.position = GetChild<OptionPromptPosition>().Value;
		}
	}

	public Prompt GetOrCreatePrompt()
	{
		var prompt = Prompt.Instance;
		if (prompt == null) {
			var go = new GameObject("Prompt");
			prompt = go.AddComponent<Prompt>();
		}
		return prompt;
	}

	public class OptionPromptFontSize : OptionInt
	{
		public override string Name { get { return "FontSize"; } }

		protected override void Configure()
		{
			DefaultValue = "";
		}
	}

	public class OptionPromptPosition : OptionEnum<Prompt.Position>
	{
		public override string Name { get { return "Position"; } }

		protected override void Configure()
		{
			DefaultValue = "";
		}
	}

	public class OptionPromptActivation : OptionString
	{
		public override string Name { get { return "Activation"; } }

		protected override void Configure()
		{
			DefaultValue = "O-O-O";
		}
	}
}

}
#endif