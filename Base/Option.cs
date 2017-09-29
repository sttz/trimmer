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

	/// <summary>
	/// Wether the Option is build-only.
	/// </summary>
	/// <remarks>
	/// Build-only options only apply to the build process.
	/// They're not loaded at runtime in the editor or player
	/// and their <c>Apply</c> method is only called at build-time.
	/// </remarks>
	public bool BuildOnly { get; private set; }

	/// <summary>
	/// Perform removal of option during build.
	/// </summary>
	/// <remarks>
	/// Overwrite this method to do custom processing when the
	/// option is not included in a build. This method gets called
	/// during each built scene's <c>OnPostprocessScene</c>.
	/// </remarks>
	public virtual void PostprocessScene(Scene scene, bool isBuild, bool includedInBuild, RuntimeProfile profile)
	{
		// NOP
	}

	/// <summary>
	/// The priority of the option's <c>PostProcessBuild</c> callback.
	/// </summary>
	/// <remarks>
	/// This determines the order in which all options' <c>PostProcessBuild</c> 
	/// method is called. Lower values are called first.<br>
	/// Note that this only orders the options between themselves, this does
	/// not affect other <c>PostProcessBuild</c> callbacks.
	/// </remarks>
	public int PostprocessOrder { get; protected set; }

	/// <summary>
	/// Do custom processing before the build is started.
	/// </summary>
	/// <param name="target">Build target type</param>
	/// <param name="path">Path to the built project</param>
	/// <param name="includedInBuild">Wether this option should be included in build</param>
	/// <param name="profile">The profile used in the build</param>
	public virtual void PreprocessBuild(BuildTarget target, string path, bool includedInBuild, RuntimeProfile profile)
	{
		// NOP
	}

	/// <summary>
	/// Do custom processing after the build has been completed.
	/// </summary>
	/// <param name="target">Build target type</param>
	/// <param name="path">Path to the built project</param>
	/// <param name="includedInBuild">Wether this option should be included in build</param>
	/// <param name="profile">The profile used in the build</param>
	public virtual void PostprocessBuild(BuildTarget target, string path, bool includedInBuild, RuntimeProfile profile)
	{
		// NOP
	}

	/// <summary>
	/// The scripting define symbols set by this option.
	/// </summary>
	/// <remarks>
	/// This includes <c>DEFINE_PREFIX + Name</c> by default when the
	/// option is included in the build. Overrride this method to add 
	/// custom defines or change when the define is set.
	/// </remarks>
	public virtual IEnumerable<string> GetSctiptingDefineSymbols(bool includedInBuild, string parameter, string value)
	{
		if (includedInBuild) {
			return new string[] { DEFINE_PREFIX + Name };
		} else {
			return Enumerable.Empty<string>();
		}
	}

	/// <summary>
	/// Do the editor GUI to edit this option.
	/// </summary>
	/// <remarks>
	/// Use one of the typed subclasses that already provides a GUI
	/// and/or override this method to customize how the option is
	/// edited.
	/// </remarks>
	public abstract string EditGUI(GUIContent label, string input);

	#endif

	/// <summary>
	/// The name of the option, used in config files, shown in the editor
	/// and used for the scripting define symbol.
	/// </summary>
	/// <returns></returns>
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
	/// Override this method if you want to hide the option in some scenarios.
	/// </summary>
	public virtual bool IsAvailable(IEnumerable<BuildTarget> targets)
	{
		return true;
	}

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

	/// <summary>
	/// Parse and load an input string.
	/// </summary>
	public abstract void Load(string input);
	/// <summary>
	/// Serialize the option's value to a string.
	/// </summary>
	public abstract string Save();

	/// <summary>
	/// Apply the option to the current environment.
	/// </summary>
	/// <remarks>
	/// This method gets called when playing in the editor or at runtime,
	/// on load and then every time the option's value changes.<br>
	/// Options that have the [ExecuteInEditMode] attribute set will also
	/// get called at edit-time.
	/// </remarks>
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

	/// <summary>
	/// Wether the option accepts variants.
	/// </summary>
	public bool IsVariant { get; protected set; }
	/// <summary>
	/// The parameter of a variant option, only valid if <c>IsVariant</c> is <c>true</c>.
	/// </summary>
	/// <remarks>
	/// The variant parameter should not be changed after the option has been initialized.
	/// The system will remove/create new instances for different parameters.
	/// </remarks>
	public string VariantParameter { get; set; }
	/// <summary>
	/// The default value of the parameter of a variant option.
	/// </summary>
	public string VariantDefaultParameter { get; protected set; }
	/// <summary>
	/// Wether this option instance represents the variant with the default parameter.
	/// </summary>
	/// </remarks>
	/// Variant options can have an arbitrary number instances, each with
	/// a different variant parameter to distinguish them. Variant options are
	/// created on-demand when a new paramter appears. However, the one
	/// instance using the <c>VariantDefaultParameter</c> is guaranteed 
	/// to always exist.
	/// </remarks>
	public bool IsDefaultVariant { get; set; }

	private Dictionary<string, IOption> variants;

	/// <summary>
	/// All the variants of this option currently known.
	/// Will return an empty enumerable except for the <c>IsDefaultVariant</c> option.
	/// </summary>
	/// <remarks>
	/// Note that this list of variants won't include the default variant,
	/// which acts as the container for the other variants.
	/// </remarks>
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
	/// <remarks>
	/// Variants can only be added to a <c>IsVariant</c> option and also only
	/// to the <c>IsDefaultVariant</c> instance that acts as the container for
	/// the other variants. The default variant and all other variants must be
	/// of the same type and each variant must have a unique non-null 
	/// <c>VariantParameter</c>.
	/// </remarks>
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

	/// <summary>
	/// Get the variant option for the given parameter.
	/// </summary>
	/// <remarks>
	/// Variants are only available on a <c>IsVariant</c> option and also only
	/// on the <c>IsDefaultVariant</c> instance that acts as the container for
	/// the other variants.
	/// </remarks>
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

	/// <summary>
	/// The types of the child options of this option.
	/// </summary>
	/// <remarks>
	/// Child options are created automatically together with
	/// the main option and allow an option to contain a set 
	/// of values of different types.
	/// </remarks>
	public Type[] ChildOptionTypes { get; protected set; }

	private Dictionary<string, IOption> children;

	/// <summary>
	/// Wether this option has children.
	/// </summary>
	public bool HasChildren {
		get {
			return children != null && children.Count > 0;
		}
	}

	/// <summary>
	/// The child instances of this option.
	/// </summary>
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

	/// <summary>
	/// Get a child option instance by its name.
	/// </summary>
	public IOption GetChild(string name)
	{
		if (children == null)
			return null;

		return children[name];
	}

	// -------- Category --------

	/// <summary>
	/// Category of the option, only used in the editor.
	/// </summary>
	/// <remarks>
	/// The category is used to group options in the editor.
	/// Only the main option can have a category, the value is
	/// ignored for child or variant options.
	/// </remarks>
	public string Category {
		get {
			return _category;
		}
		protected set {
			_category = value;
		}
	}
	string _category = "General";

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

