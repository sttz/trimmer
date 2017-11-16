using System;
using System.Collections.Generic;
using sttz.Trimmer.Extensions;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
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

    public abstract ValueStore Store { get; }
    public abstract void SaveIfNeeded();
	public abstract Recursion.RecursionType GetRecursionType();
    public abstract IEnumerable<Option> GetAllOptions();
    public abstract void EditOption(string path, Option option, ValueStore.Node node);
}

}