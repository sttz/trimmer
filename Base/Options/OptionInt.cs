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
public abstract class OptionInt : Option, IOption<int>
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

	public int Value { get; set; }

	public int? MinValue { get; set; }
	public int? MaxValue { get; set; }

	public int Parse(string input)
	{
		if (input.Length == 0)
			input = DefaultValue ?? string.Empty;

		int result;
		if (int.TryParse(input, out result)) {
			if (MinValue != null && MaxValue != null) {
				return Math.Min(Math.Max((int)MinValue, result), (int)MaxValue);
			} else {
				return result;
			}
		} else {
			return 0;
		}
	}

	public override void Load(string input)
	{
		Value = Parse(input);
	}

	public string Save(int input)
	{
		return input.ToString();
	}

	public override string Save()
	{
		return Save(Value);
	}
}

}

