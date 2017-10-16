using System;
using System.Collections.Generic;
using sttz.Workbench.Extensions;
using UnityEditor;
using UnityEngine;

namespace sttz.Workbench.Editor
{

/// <summary>
/// Base class for <c>EditorProfile</c> and <c>BuildProfile</c>.
/// </summary>
public abstract class EditableProfile : ScriptableObject
{
    /// <summary>
	/// Instances of all options used for editor purposes.
	/// </summary>
	/// <remarks>
	/// These options are used for the editor GUI and should not be
	/// used to change option values.
	/// </remarks>
	public static IEnumerable<IOption> AllOptions {
		get {
			if (_allOptions == null) {
				_allOptions = new List<IOption>();
				foreach (var optionType in RuntimeProfile.AllOptionTypes) {
					_allOptions.Add((IOption)Activator.CreateInstance(optionType));
				}
			}
			return _allOptions;
		}
	}
	private static List<IOption> _allOptions;

    public abstract ValueStore Store { get; }
    public abstract void SaveIfNeeded();
	public abstract Recursion.RecursionType GetRecursionType();
    public abstract IEnumerable<IOption> GetAllOptions();
    public abstract void EditOption(string path, GUIContent label, IOption option, ValueStore.Node node);
}

}