using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench.BaseOptions
{

/// <summary>
/// Option base class with an int as value.
/// </summary>
public abstract class OptionInt : Option<int>
{
	#if UNITY_EDITOR
	public override string EditGUI(GUIContent label, string input)
	{
		if (MinValue != null && MaxValue != null) {
			return Save(EditorGUILayout.IntSlider(label, Parse(input), (int)MinValue, (int)MaxValue));
		} else {
			return Save(EditorGUILayout.IntField(label, Parse(input)));
		}
	}
	#endif

	public int? MinValue { get; set; }
	public int? MaxValue { get; set; }

	override public int Parse(string input)
	{
		if (string.IsNullOrEmpty(input))
			return DefaultValue;

		int result;
		if (int.TryParse(input, out result)) {
			if (MinValue != null && MaxValue != null) {
				return Math.Min(Math.Max((int)MinValue, result), (int)MaxValue);
			} else {
				return result;
			}
		} else {
			return DefaultValue;
		}
	}

	override public void Load(string input)
	{
		Value = Parse(input);
	}

	override public string Save(int input)
	{
		return input.ToString();
	}

	override public string Save()
	{
		return Save(Value);
	}
}

}

