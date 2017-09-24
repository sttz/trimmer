using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
#endif

namespace sttz.Workbench {

/// <summary>
/// Base implementation of IOption.
/// </summary>
public abstract class Option : IOption
{
	// -------- Implement / Override in Sub-Classes --------

	/// <summary>
	/// Overwrite this method to set up your option class (don't do it in the constructor).
	/// </summary>
	protected abstract void Configure();

	#if UNITY_EDITOR

	/// <summary>
	/// Prefix for the per-option compilation defines.
	/// </summary>
	public const string DEFINE_PREFIX = "OPTION_";

	public bool BuildOnly { get; private set; }

	public virtual void PostprocessScene(Scene scene, bool isBuild, bool includedInBuild, Profile profile)
	{
		// NOP
	}

	public int PostprocessOrder { get; protected set; }

	public virtual void PreprocessBuild(BuildTarget target, string path, bool includedInBuild, Profile profile)
	{
		// NOP
	}

	public virtual void PostprocessBuild(BuildTarget target, string path, bool includedInBuild, Profile profile)
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

	/// <summary>
	/// </summary>
	public abstract string Name { get; }

	public IOption Parent {
		get {
			return _parent;
		}
		set {
			_parent = value;
			if (Parent == null) {
				Path = Name;
			} else {
				var own = Name;
				if (IsVariant && !IsDefaultVariant) {
					own = VariantParameter;
				}
				Path = Parent.Path + "/" + own;
 			}
		}
	}
	private IOption _parent;

	/// <summary>
	/// The path to this option, given by the name of the root option and
	/// the parmeters of its variants or names of its children, separated by "/".
	/// </summary>
	public string Path { get; private set; }

	/// <summary>
	/// The default value of the option.
	/// </summary>
	public string DefaultValue {
		get {
			return _defaultValue;
		}
		set {
			_defaultValue = value;
		}
	}
	private string _defaultValue = string.Empty;
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

	// -------- Init --------

	public Option()
	{
		Parent = null;

		#if UNITY_EDITOR
		BuildOnly = GetType().GetCustomAttributes(typeof(BuildOnlyAttribute), true).Length > 0;
		#endif
		
		Configure();
		
		if (IsVariant) {
			IsDefaultVariant = true;
			VariantParameter = VariantDefaultParameter;
		}

		CreateChildren();
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

	/// <summary>
	/// Add a new variant option.
	/// </summary>
	public IOption AddVariant(string parameter)
	{
		Assert.IsTrue(IsVariant, "Invalid call to AddVariant, option is not variant.");
		Assert.IsTrue(IsDefaultVariant, "Invalid call to AddVariant, option is not the default variant.");

		Assert.IsNotNull(parameter);
		Assert.IsFalse(string.Equals(parameter, VariantDefaultParameter, StringComparison.OrdinalIgnoreCase), "Cannot add variant with default parameter.");
		Assert.IsTrue(variants == null || !variants.ContainsKey(parameter), "Variant with paramter already exists.");

		var instance = (Option)Activator.CreateInstance(GetType());
		instance.Parent = this;
		instance.VariantParameter = parameter;
		instance.IsDefaultVariant = false;

		if (variants == null)
			variants = new Dictionary<string, IOption>(StringComparer.OrdinalIgnoreCase);
		variants[parameter] = instance;

		return instance;
	}

	public IOption GetVariant(string parameter)
	{
		Assert.IsTrue(IsVariant, "Invalid call to GetVariant, option is not variant.");
		Assert.IsTrue(IsDefaultVariant, "Invalid call to GetVariant, option is not the default variant.");

		if (string.Equals(parameter, VariantDefaultParameter, StringComparison.OrdinalIgnoreCase))
			return this;

		IOption variant = null;
		if (variants == null 
				|| !variants.TryGetValue(parameter, out variant)) {
			variant = AddVariant(parameter);
		}

		return variant;
	}

	/// <summary>
	/// Remove a variant.
	/// </summary>
	/// <remarks>
	/// Variants are only available on a <c>IsVariant</c> option and also only
	/// on the <c>IsDefaultVariant</c> instance that acts as the container for
	/// the other variants.
	/// </remarks>
	public void RemoveVariant(IOption option)
	{
		Assert.IsTrue(IsVariant, "Invalid call to RemoveVariant, option is not variant.");
		Assert.IsTrue(IsDefaultVariant, "Invalid call to RemoveVariant, option is not the default variant.");

		Assert.IsTrue(variants != null || variants.ContainsValue(option), "Invalid call to RemoveVariant, option is not a variant of this instance.");

		variants.Remove(option.VariantParameter);
		option.Parent = null;
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
			child.Parent = this;
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

	public string Category { get; protected set; }

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
			pathToBuiltProject = System.IO.Path.GetDirectoryName(pathToBuiltProject);
		}

		foreach (var pathTemplate in desc.deployPaths) {
			var path = pathTemplate.Replace("{Product}", PlayerSettings.productName);
			path = System.IO.Path.Combine(pathToBuiltProject, path);

			if (!Directory.Exists(path)) {
				Debug.Log("Plugin path does not exist: " + path);
				continue;
			}

			foreach (var entry in Directory.GetFileSystemEntries(path)) {
				var extension = System.IO.Path.GetExtension(entry);
				if (!desc.extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
					Debug.Log("Extension does not match: " + entry);
					continue;
				}

				var fileName = System.IO.Path.GetFileNameWithoutExtension(entry);
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

