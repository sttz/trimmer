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
/// Option base class with an int as value.
/// </summary>
/// <remarks>
/// If you set <see cref="MinValue"/> and <see cref="MaxValue"/>, the value
/// will be clamped to this range and displayed as a slider in the editor.
/// </remarks>
public abstract class OptionInt : Option<int>
{
    #if UNITY_EDITOR
    public override bool EditGUI()
    {
        EditorGUI.BeginChangeCheck();
        if (MinValue != null && MaxValue != null) {
            Value = EditorGUILayout.IntSlider(Value, (int)MinValue, (int)MaxValue);
        } else {
            Value = EditorGUILayout.IntField(Value);
        }
        return EditorGUI.EndChangeCheck();
    }
    #endif

    /// <summary>
    /// The minimum value of the int (must be set together with <see cref="MaxValue"/>).
    /// </summary>
    public int? MinValue { get; set; }

    /// <summary>
    /// The maximum value of the int (must be set together with <see cref="MinValue"/>).
    /// </summary>
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

#endif
