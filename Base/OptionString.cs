using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

/// <summary>
/// Option base class with a string as value.
/// </summary>
public abstract class OptionString : Option, IOption<string>
{
	#if UNITY_EDITOR
	public override string EditGUI(GUIContent label, string input)
	{
		return Save(EditorGUILayout.TextField(label, Parse(input)));
	}
	#endif

	public string Value { get; set; }

	public string Parse(string input)
	{
		if (input.Length == 0)
			input = DefaultValue ?? string.Empty;

		return input;
	}

	public override void Load(string input)
	{
		Value = input;
	}

	public string Save(string input)
	{
		return input;
	}

	public override string Save()
	{
		return Value;
	}
}

}

