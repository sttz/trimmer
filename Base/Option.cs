using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
#endif

// TODO: Dfeault DefaultIniValue = ""

namespace sttz.Workbench {

/// <summary>
/// Base implementation of IOption.
/// </summary>
public abstract class Option : IOption
{
	// -------- Implement / Override in Sub-Classes --------

	#if UNITY_EDITOR

	/// <summary>
	/// Prefix for the per-option compilation defines.
	/// </summary>
	public const string DEFINE_PREFIX = "OPTION_";

	public bool BuildOnly { get; protected set; }

	public virtual void Remove()
	{
		// NOP
	}

	public int PostprocessOrder { get; protected set; }

	public virtual void PostprocessBuild(BuildTarget target, string pathToBuiltProject, bool optionRemoved, Profile profile)
	{
		// NOP
	}

	public virtual IEnumerable<string> GetSctiptingDefineSymbols(bool includedInBuild, string parameter, string value)
	{
		if (includedInBuild) {
			return new string[] { DEFINE_PREFIX + Name };
		} else {
			return Enumerable.Empty<string>();
		}
	}

	public abstract string EditGUI(GUIContent label, string input);

	#endif

	public abstract string Name { get; }
	public string DefaultIniValue { get; protected set; }
	public abstract void Load(string input);
	public abstract string Save();

	public virtual void Apply()
	{
		if (variants != null) {
			foreach (var variant in variants.Values) {
				variant.Apply();
			}
		}

		if (children != null) {
			foreach (var child in children.Values) {
				child.Apply();
			}
		}
	}

	// -------- Variants --------

	public bool IsVariant { get; protected set; }
	public string VariantParameter { get; set; }
	public string VariantDefaultParameter { get; protected set; }
	public bool IsDefaultVariant { get; set; }

	private Dictionary<string, IOption> variants;

	public IEnumerable<IOption> Variants {
		get {
			if (variants == null) {
				return Enumerable.Empty<IOption>();
			} else {
				return variants.Values;
			}
		}
	}

	public void AddVariant(IOption variant)
	{
		Assert.IsTrue(IsVariant, "Invalid call to AddVariant, option is not variant.");
		Assert.IsTrue(IsDefaultVariant, "Invalid call to AddVariant, option is not the default variant.");

		Assert.IsNotNull(variant);
		Assert.IsNotNull(variant.VariantParameter, "Variant's parameter is null.");
		Assert.IsTrue(variant.GetType() == GetType(), "Variants must be of the same type.");
		Assert.IsFalse(string.Equals(variant.VariantParameter, VariantDefaultParameter, StringComparison.OrdinalIgnoreCase), "Cannot add variant with default parameter.");
		Assert.IsTrue(variants == null || !variants.ContainsKey(variant.VariantParameter), "Variant with paramter already exists.");

		if (variants == null)
			variants = new Dictionary<string, IOption>(StringComparer.OrdinalIgnoreCase);

		variants[variant.VariantParameter] = variant;
	}

	public IOption GetVariant(string parameter)
	{
		Assert.IsTrue(IsVariant, "Invalid call to GetVariant, option is not variant.");
		Assert.IsTrue(IsDefaultVariant, "Invalid call to GetVariant, option is not the default variant.");

		if (string.Equals(parameter, VariantDefaultParameter, StringComparison.OrdinalIgnoreCase))
			return this;

		IOption variant;
		if (!variants.TryGetValue(parameter, out variant)) {
			variant = (IOption)Activator.CreateInstance(GetType());
			variant.VariantParameter = parameter;
			variants[parameter] = variant;
		}

		return variant;
	}

	// -------- Children --------

	public Type[] ChildOptionTypes { get; protected set; }

	private Dictionary<string, IOption> children;

	public bool HasChildren {
		get {
			return children != null && children.Count > 0;
		}
	}

	public IEnumerable<IOption> Children {
		get {
			if (children == null) {
				return Enumerable.Empty<IOption>();
			} else {
				return children.Values;
			}
		}
	}

	protected void CreateChildren()
	{
		var type = GetType();

		var nested = type.GetNestedTypes(BindingFlags.Public);
		foreach (var nestedType in nested) {
			if (!typeof(IOption).IsAssignableFrom(nestedType))
				continue;

			if (children == null)
				children = new Dictionary<string, IOption>(StringComparer.OrdinalIgnoreCase);

			var child = (IOption)Activator.CreateInstance(nestedType);
			children[child.Name] = child;
		}
	}

	public IOption GetChild(string name)
	{
		if (children == null)
			return null;

		return children[name];
	}

	// -------- Category --------

	private string _category;
	public string Category {
		get {
			return _category ?? DefaultCategory;
		}
		set {
			_category = value;
		}
	}

	public string DefaultCategory { get; protected set; }

	#if UNITY_EDITOR

	// -------- Plugin Removal --------

	class PluginDescription
	{
		public string[] deployPaths;
		public string[] extensions;
	}

	static PluginDescription pluginsOSX = new PluginDescription() {
		deployPaths = new string[] { "Contents/Plugins" },
		extensions = new string[] { ".bundle" }
	};
	static PluginDescription pluginsWindows = new PluginDescription() {
		deployPaths = new string[] {
			"",
			"{Product}_Data/Plugins", 
		},
		extensions = new string[] { ".dll" }
	};
	static PluginDescription pluginsLinux = new PluginDescription() {
		deployPaths = new string[] { 
			"{Product}_Data/Plugins", 
			"{Product}_Data/Plugins/x86", 
			"{Product}_Data/Plugins/x86_64", 
		},
		extensions = new string[] { ".so" }
	};

	static Dictionary<BuildTarget, PluginDescription> pluginDescs
	= new Dictionary<BuildTarget, PluginDescription>() {

		{ BuildTarget.StandaloneOSXIntel, pluginsOSX },
		{ BuildTarget.StandaloneOSXIntel64, pluginsOSX },
		{ BuildTarget.StandaloneOSXUniversal, pluginsOSX },

		{ BuildTarget.StandaloneWindows, pluginsWindows },
		{ BuildTarget.StandaloneWindows64, pluginsWindows },

		{ BuildTarget.StandaloneLinux, pluginsLinux },
		{ BuildTarget.StandaloneLinux64, pluginsLinux },
		{ BuildTarget.StandaloneLinuxUniversal, pluginsLinux },
	};

	/// <summary>
	/// Remove a plugin from a build.
	/// </summary>
	/// <remarks>
	/// This can be used to remove a plugin when an option has been removed and the
	/// plugin is no longer needed. Currently, only OS X, Windows and Linux standalone
	/// builds are supported.
	/// </remarks>
	public static void RemovePluginFromBuild(BuildTarget target, string pathToBuiltProject, Regex pluginNameMatch)
	{
		PluginDescription desc;
		if (!pluginDescs.TryGetValue(target, out desc)) {
			Debug.LogError(string.Format("Build target {0} not supported for plugin removal.", target));
			return;
		}

		if (File.Exists(pathToBuiltProject)) {
			pathToBuiltProject = Path.GetDirectoryName(pathToBuiltProject);
		}

		foreach (var pathTemplate in desc.deployPaths) {
			var path = pathTemplate.Replace("{Product}", PlayerSettings.productName);
			path = Path.Combine(pathToBuiltProject, path);

			if (!Directory.Exists(path)) {
				Debug.Log("Plugin path does not exist: " + path);
				continue;
			}

			foreach (var entry in Directory.GetFileSystemEntries(path)) {
				var extension = Path.GetExtension(entry);
				if (!desc.extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
					Debug.Log("Extension does not match: " + entry);
					continue;
				}

				var fileName = Path.GetFileNameWithoutExtension(entry);
				if (!pluginNameMatch.IsMatch(fileName)) {
					Debug.Log("Name does not match: " + entry);
					continue;
				}

				Debug.Log("Removing plugin: " + entry);
				if (File.Exists(entry))
					File.Delete(entry);
				else
					Directory.Delete(entry, true);
			}
		}
	}

	#endif
}

}

