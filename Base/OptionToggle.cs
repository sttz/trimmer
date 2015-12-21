using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

/// <summary>
/// Option base class with a boolean as value.
/// </summary>
public abstract class OptionToggle : Option, IOption<bool>
{
	#if UNITY_EDITOR
	public override string EditGUI(GUIContent label, string input)
	{
		return Save(EditorGUILayout.Toggle(label, Parse(input)));
	}
	#endif

	private static string[] trueStrings = new string[] {
		"yes", "true", "y", "1"
	};

	public bool Value { get; set; }

	public bool Parse(string input)
	{
		if (input.Length == 0)
			input = DefaultIniValue ?? string.Empty;

		return Array.FindIndex(trueStrings, s => s.Equals(input, StringComparison.OrdinalIgnoreCase)) >= 0;
	}

	public override void Load(string input)
	{
		Value = Parse(input);
	}

	public string Save(bool input)
	{
		return input ? "yes" : "no";
	}

	public override string Save()
	{
		return Save(Value);
	}
}

}

