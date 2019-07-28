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
/// Option base class with a string as value.
/// </summary>
public abstract class OptionString : Option<string>
{
    #if UNITY_EDITOR
    public override bool EditGUI()
    {
        EditorGUI.BeginChangeCheck();
        Value = EditorGUILayout.DelayedTextField(Value);
        return EditorGUI.EndChangeCheck();
    }
    #endif

    public OptionString() : base()
    {
        if (DefaultValue == null) {
            DefaultValue = string.Empty;
        }
    }

    override public string Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return DefaultValue;

        return input;
    }

    override public void Load(string input)
    {
        Value = Parse(input);
    }

    override public string Save(string input)
    {
        return input;
    }

    override public string Save()
    {
        return Save(Value);
    }
}

}

#endif
