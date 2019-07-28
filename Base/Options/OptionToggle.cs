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
/// Option base class with a boolean as value.
/// </summary>
/// <remarks>
/// Only the strings `yes`, `true`, `y` and `1` (not case sensitive) will be
/// considered as `true`, all other strings as `false`.
/// </remarks>
public abstract class OptionToggle : Option<bool>
{
    #if UNITY_EDITOR
    public override bool EditGUI()
    {
        EditorGUI.BeginChangeCheck();
        Value = EditorGUILayout.Toggle(Value);
        return EditorGUI.EndChangeCheck();
    }
    #endif

    static string[] trueStrings = new string[] {
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

#endif
