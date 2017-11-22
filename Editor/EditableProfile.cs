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
	/// Instances of all Options, used for editor purposes.
	/// </summary>
	/// <remarks>
	/// These options are used for the editor GUI and should not be
	/// used to change option values.
	/// </remarks>
	public static IEnumerable<Option> AllOptions {
		get {
			if (_allOptions == null) {
				_allOptions = new List<Option>();
				foreach (var optionType in RuntimeProfile.AllOptionTypes) {
					_allOptions.Add((Option)Activator.CreateInstance(optionType));
				}
			}
			return _allOptions;
		}
	}
	private static List<Option> _allOptions;

	/// <summary>
	/// The store backing the profile.
	/// </summary>
	public abstract ValueStore Store { get; }

	/// <summary>
	/// Callback to save the profile if it has been changed.
	/// </summary>
	public abstract void SaveIfNeeded();

	/// <summary>
	/// Callback to determine the recursion type to use by the editor.
	/// </summary>
	public abstract Recursion.RecursionType GetRecursionType();

	/// <summary>
	/// Returns all main Options to be displayed in this profile.
	/// </summary>
	public abstract IEnumerable<Option> GetAllOptions();

	/// <summary>
	/// Show the edit GUI for an Option.
	/// </summary>
	/// <param name="path">Path to the Option in the profile</param>
	/// <param name="option">The shared Option instance</param>
	/// <param name="node">The store node backing the Option (only when available)</param>
	public abstract void EditOption(string path, Option option, ValueStore.Node node);

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