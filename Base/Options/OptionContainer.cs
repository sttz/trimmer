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
/// Option that only acts as a container.
/// </summary>
/// <remarks>
/// The container Option doesn't show a GUI, ignores any input value 
/// and always saves itself as an empty string.
/// </remarks>
public abstract class OptionContainer : Option
{
    #if UNITY_EDITOR
    public override bool EditGUI()
    {
        GUILayout.FlexibleSpace();
        return false;
    }
    #endif

    override public void Load(string input)
    {
        // NOP
    }

    override public string Save()
    {
        return string.Empty;
    }
}

}

#endif
