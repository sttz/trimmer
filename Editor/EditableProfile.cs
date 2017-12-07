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

using System;
using System.Collections.Generic;
using sttz.Trimmer.Extensions;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Base class for <see cref="EditorProfile"/> and <see cref="BuildProfile"/> to
/// make sharing common editor code easier.
/// </summary>
public abstract class EditableProfile : ScriptableObject
{
    /// <summary>
    /// The store backing the profile.
    /// </summary>
    public abstract ValueStore Store { get; }

    /// <summary>
    /// The runtime profile used to inspect the profile in the editor.
    /// </summary>
    public abstract RuntimeProfile EditProfile { get; }

    /// <summary>
    /// Callback to save the profile if it has been changed.
    /// </summary>
    public abstract void SaveIfNeeded();

    /// <summary>
    /// Show the edit GUI for an Option.
    /// </summary>
    /// <param name="option">The Option to be edited</param>
    public abstract void EditOption(Option option);

    // ------ Context Menu ------

    [ContextMenu("Copy As Ini File")]
    public void CopyAsIniFile()
    {
        EditorGUIUtility.systemCopyBuffer = IniAdapter.Save(Store);
    }

    [ContextMenu("Paste From Ini File")]
    public void PasteFromIniFile()
    {
        Undo.RecordObject(this, "Paste From Ini File");
        IniAdapter.Load(Store, EditorGUIUtility.systemCopyBuffer);
    }
}

}