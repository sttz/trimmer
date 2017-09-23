#if OPTION_Dummy || UNITY_EDITOR
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench.Prompt
{

public class OptionPrompt : OptionToggle
{
	public override string Name { get { return "Prompt"; } }

	public OptionPrompt()
	{
		DefaultValue = "";
		CreateChildren();
	}

	public override void Apply()
	{
		//
	}

#if UNITY_EDITOR

	public override void Remove()
	{
		//
	}

#endif

	public class OptionPromptSize : OptionInt
	{
		public override string Name { get { return "Size"; } }

		public OptionPromptSize()
		{
			DefaultValue = "";
		}
	}

	public class OptionPromptPosition : OptionEnum<Prompt.Position>
	{
		public override string Name { get { return "Position"; } }

		public OptionPromptPosition()
		{
			DefaultValue = "";
		}
	}

	public class OptionPromptActivation : OptionString
	{
		public override string Name { get { return "Activation"; } }

		public OptionPromptActivation()
		{
			DefaultValue = "O-O-O";
		}
	}
}

}
#endif