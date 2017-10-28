using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench.BaseOptions
{

/// <summary>
/// Option base class with a float as value.
/// </summary>
public abstract class OptionFloat : Option, IOption<float>
{
	#if UNITY_EDITOR
	public override string EditGUI(GUIContent label, string input)
	{
		if (MinValue != null && MaxValue != null) {
			return Save(EditorGUILayout.Slider(label, Parse(input), (float)MinValue, (float)MaxValue));
		} else {
			return Save(EditorGUILayout.FloatField(label, Parse(input)));
		}
	}
	#endif

	public float Value { get; set; }

	public float? MinValue { get; set; }
	public float? MaxValue { get; set; }

	public float Parse(string input)
	{
		if (input.Length == 0)
			input = DefaultValue ?? string.Empty;

		float result;
		if (float.TryParse(input, out result)) {
			if (MinValue != null && MaxValue != null) {
				return Math.Min(Math.Max((float)MinValue, result), (float)MaxValue);
			} else {
				return result;
			}
		} else {
			return 0f;
		}
	}

	public override void Load(string input)
	{
		Value = Parse(input);
	}

	public string Save(float input)
	{
		return input.ToString("R");
	}

	public override string Save()
	{
		return Save(Value);
	}
}

}

