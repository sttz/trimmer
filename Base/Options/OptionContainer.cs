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
/// Option that only acts as a container.
/// </summary>
/// <remarks>
/// The container Option doesn't show a GUI, ignores any input value 
/// and always saves itself as an empty string.
/// </remarks>
public abstract class OptionContainer : Option
{
    #if UNITY_EDITOR
    public override string EditGUI(string input)
    {
        GUILayout.FlexibleSpace();
        return input;
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
