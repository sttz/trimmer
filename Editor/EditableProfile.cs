//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Base class for <see cref="EditorProfile"/> and <see cref="BuildProfile"/> to
/// make sharing common editor code easier.
/// </summary>
public abstract class EditableProfile : BatchItem
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
    /// Save the option instance values to the profile's store.
    /// </summary>
    public abstract void SaveToStore();

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
