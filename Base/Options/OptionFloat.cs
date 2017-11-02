#if !NO_WORKBENCH || UNITY_EDITOR

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
public abstract class OptionFloat : Option<float>
{
	#if UNITY_EDITOR
	public override string EditGUI(string input)
	{
		if (MinValue != null && MaxValue != null) {
			return Save(EditorGUILayout.Slider(Parse(input), (float)MinValue, (float)MaxValue));
		} else {
			return Save(EditorGUILayout.FloatField(Parse(input)));
		}
	}
	#endif

	public float? MinValue { get; set; }
	public float? MaxValue { get; set; }

	override public float Parse(string input)
	{
		if (string.IsNullOrEmpty(input))
			return DefaultValue;

		float result;
		if (float.TryParse(input, out result)) {
			if (MinValue != null && MaxValue != null) {
				return Math.Min(Math.Max((float)MinValue, result), (float)MaxValue);
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

	override public string Save(float input)
	{
		return input.ToString("R");
	}

	override public string Save()
	{
		return Save(Value);
	}
}

}

#endif
