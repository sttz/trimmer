using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench.BaseOptions
{

/// <summary>
/// Option base class with a boolean as value.
/// </summary>
public abstract class OptionToggle : Option<bool>
{
	#if UNITY_EDITOR
	public override string EditGUI(string input)
	{
		return Save(EditorGUILayout.Toggle(Parse(input)));
	}
	#endif

	private static string[] trueStrings = new string[] {
		"yes", "true", "y", "1"
	};

	override public bool Parse(string input)
	{
		if (string.IsNullOrEmpty(input))
			return DefaultValue;

		return Array.FindIndex(trueStrings, s => s.Equals(input, StringComparison.OrdinalIgnoreCase)) >= 0;
	}

	override public void Load(string input)
	{
		Value = Parse(input);
	}

	override public string Save(bool input)
	{
		return input ? "yes" : "no";
	}

	override public string Save()
	{
		return Save(Value);
	}
}

}

