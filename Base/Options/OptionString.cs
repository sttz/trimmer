//
// Trimmer Framework for Unity - https://sttz.ch/trimmer
// Copyright © 2017 Adrian Stutz
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
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
