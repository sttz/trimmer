using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench.BaseOptions
{

/// <summary>
/// Option base class with a string as value.
/// </summary>
public abstract class OptionString : Option<string>
{
	#if UNITY_EDITOR
	public override string EditGUI(GUIContent label, string input)
	{
		return Save(EditorGUILayout.DelayedTextField(label, Parse(input)));
	}
	#endif

	public OptionString() : base()
	{
		DefaultValue = string.Empty;
	}

	override public string Parse(string input)
	{
		if (string.IsNullOrEmpty(input))
			return DefaultValue;

		return input;
	}

	override public void Load(string input)
	{
		Value = input;
	}

	override public string Save(string input)
	{
		return input;
	}

	override public string Save()
	{
		return Value;
	}
}

}

