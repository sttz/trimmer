//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if !NO_TRIMMER || UNITY_EDITOR

using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Trimmer.BaseOptions
{

/// <summary>
/// Option base class with a float as value.
/// </summary>
/// <remarks>
/// If you set <see cref="MinValue"/> and <see cref="MaxValue"/>, the value
/// will be clamped to this range and displayed as a slider in the editor.
/// </remarks>
public abstract class OptionFloat : Option<float>
{
    #if UNITY_EDITOR
    public override bool EditGUI()
    {
        EditorGUI.BeginChangeCheck();
        if (MinValue != null && MaxValue != null) {
            Value = EditorGUILayout.Slider(Value, (float)MinValue, (float)MaxValue);
        } else {
            Value = EditorGUILayout.FloatField(Value);
        }
        return EditorGUI.EndChangeCheck();
    }
    #endif

    /// <summary>
    /// The minimum value of the float (must be set together with <see cref="MaxValue"/>).
    /// </summary>
    public float? MinValue { get; set; }

    /// <summary>
    /// The maximum value of the float (must be set together with <see cref="MinValue"/>).
    /// </summary>
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
