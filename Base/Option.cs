// Workaround for docfx documentation building
#if !UNITY_5 && !UNITY_2017 && !UNITY_2018
#define UNITY_EDITOR
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using sttz.Workbench.Extensions;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
#endif

namespace sttz.Workbench {

// TODO: Add OnEnable / OnDisable?
// TODO: Name defaults to class name

/// <summary>
/// Base class for individual Workebnch options.
/// </summary>
/// <remarks>
/// Options are the basic building blocks to integrate your project 
/// into Workbench. Workbench detects all <see cref="IOption"/> classes
/// in your project, so there's no additional configuration necessary
/// besides adding the option source files to your project.
/// 
/// Each option has a value, which you can edit in the editor and which
/// can also be changed in the player using <see cref="RuntimeProfile"/>.
/// The runtime profile is only a script API, use the bundled options to
/// change option values in the player using configuration files 
/// (<see cref="Options.OptionIniFile"/>) or using a simple GUI 
/// (<see cref="Options.OptionPrompt"/>).
/// 
/// Options can model more complicated data than simple values in two ways:
/// * <b>Variant options</b> allow to have multiple instances of the same
///   option type that differ by their <see cref="IOption.VariantParameter"/>,
///   e.g. to have a volume option, which can control multiple channels.
/// * <b>Child options</b> allow options to group multiple different values
///   together.
/// 
/// Child and variant options can be nested, with the only limitation that
/// variant options cannot be directly nested (but a variant option can
/// have a variant child option).
/// 
/// Most of the time, you want to extend one of the typed base classes
/// that fit the type of option you want to create:
/// * <see cref="BaseOptions.OptionAsset{TUnity}" />
/// * <see cref="BaseOptions.OptionEnum{TEnum}" />
/// * <see cref="BaseOptions.OptionFloat" />
/// * <see cref="BaseOptions.OptionInt" />
/// * <see cref="BaseOptions.OptionString" />
/// * <see cref="BaseOptions.OptionToggle" />
/// 
/// 
/// </remarks>
public abstract class Option : IOption
{
	// -------- Implement / Override in Sub-Classes --------

	/// <summary>
	/// Overwrite this method to set up your option class (don't do it in the constructor).
	/// </summary>
	protected abstract void Configure();

	/// <summary>
	/// Prefix for the per-option scripting defines.
	/// </summary>
	/// <remarks>
	/// By default, Options will have the same name as their class. If the class
	/// name starts with `DEFINE_PREFIX`, the prefix will be removed and later
	/// re-appended to create the scripting define symbol. This way, the scripting
	/// define symbols will match the class name. If you set an Option's name to 
	/// something else that doesn't start with the prefix, it's scripting define
	/// symbol will have the prefix prepended.
	/// 
	/// e.g.
	/// Class Name -> Option Name -> Scripting Define Symbol
	/// OptionExample -> Example -> OptionExample
	/// NonDefaultExample -> NonDefaultExample -> OptionNonDefaultExample
	/// </remarks>
	public const string DEFINE_PREFIX = "Option";

	#if UNITY_EDITOR

	/// <summary>
	/// Wether the Option is build-only.
	/// </summary>
	/// <remarks>
	/// Build-only options only apply to the build process.
	/// They're not loaded at runtime in the editor or player
	/// and their <c>Apply</c> method is only called at build-time.
	/// </remarks>
	public bool BuildOnly { get; private set; }
	public bool EditorOnly { get; private set; }

	/// <summary>
	/// Override this method if you want to hide the option in some scenarios.
	/// </summary>
	public virtual bool IsAvailable(IEnumerable<BuildTarget> targets)
	{
		return true;
	}

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
	/// method is called. Lower values are called first.<br/>
	/// Note that this only orders the options between themselves, this does
	/// not affect other <c>PostProcessBuild</c> callbacks.
	/// </remarks>
	public int PostprocessOrder { get; protected set; }

	/// <summary>
	/// Prepare the build options.
	/// </summary>
	/// <remarks>
	/// This allows to change the build options, set a build path, add/remove
	/// scenes or do anything else to set up the build process.<br/>
	/// Note that this method won't be called for default Unity builds
	/// triggered from the build settings window or the menu.
	/// </remarks>
	public virtual BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, bool includedInBuild, RuntimeProfile profile)
	{
		return options;
	}

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
	public string Name { get; protected set; }

	public IOption Parent {
		get {
			return _parent;
		}
		set {
			if (_parent == value) return;

			_parent = value;
			
			InvalidatePathRecursive();
		}
	}
	private IOption _parent;

	/// <summary>
	/// The path to this option. The path consists of option names separated by «/»
	/// and variants separated by «:» and their parameter.
	/// </summary>
	public string Path {
		get {
			if (_path == null) {
				_path = GetPathRecursive(this);
			}
			return _path;
		}
	}
	protected string _path;

	protected string GetPathRecursive(IOption current)
	{
		if (current.IsVariant && !current.IsDefaultVariant) {
			if (current.Parent != null) {
				return GetPathRecursive(current.Parent) + ":" + current.VariantParameter;
			} else {
				throw new Exception("A non-default variant needs to have a parent.");
			}
		} else {
			if (current.Parent != null) {
				return GetPathRecursive(current.Parent) + "/" + current.Name;
			} else {
				return current.Name;
			}
		}
	}

	public void InvalidatePathRecursive()
	{
		_path = null;

		foreach (var child in Children) {
			child.InvalidatePathRecursive();
		}
		foreach (var variant in Variants) {
			variant.InvalidatePathRecursive();
		}
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
	/// Control the order options get applied (lower values get applied first).
	/// </summary>
	public int ApplyOrder { get; protected set; }

	/// <summary>
	/// Apply the option to the current environment.
	/// </summary>
	/// <remarks>
	/// This method gets called when playing in the editor or at runtime,
	/// on load and then every time the option's value changes.<br/>
	/// Options that have the [ExecuteInEditMode] attribute set will also
	/// get called at edit-time.
	/// </remarks>
	public virtual void Apply()
	{
		if (variants != null) {
			foreach (var variant in variants) {
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
		Name = GetType().Name;
		if (Name.StartsWith(DEFINE_PREFIX)) {
			Name = Name.Substring(DEFINE_PREFIX.Length);
		}

		Parent = null;

		#if UNITY_EDITOR
		BuildOnly = GetType().GetCustomAttributes(typeof(BuildOnlyAttribute), true).Length > 0;
		EditorOnly = GetType().GetCustomAttributes(typeof(EditorOnlyAttribute), true).Length > 0;
		#endif
		
		Configure();
		
		if (IsVariant) {
			IsDefaultVariant = true;
			VariantParameter = VariantDefaultParameter;
			if (string.IsNullOrEmpty(VariantDefaultParameter)) {
				if (IsArrayVariant) {
					VariantDefaultParameter = "0";
				} else {
					VariantDefaultParameter = "Default";
				}
			}
		}

		CreateChildren();
	}

	// -------- Variants --------

	/// <summary>
	/// Wether the option accepts variants.
	/// </summary>
	public bool IsVariant { get; protected set; }
	/// <summary>
	/// Treat the variants as an array.
	/// </summary>
	/// <remarks>
	/// Array variants' parameters cannot be edited and are 
	/// assigned automatically to numeric values.
	/// </remarks>
	public bool IsArrayVariant { get; protected set; }
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
	/// <remarks>
	/// Variant options can have an arbitrary number instances, each with
	/// a different variant parameter to distinguish them. Variant options are
	/// created on-demand when a new paramter appears. However, the one
	/// instance using the <c>VariantDefaultParameter</c> is guaranteed 
	/// to always exist.
	/// </remarks>
	public bool IsDefaultVariant { get; set; }

	private List<IOption> variants;

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
				return variants;
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
		Assert.IsTrue(variants == null || variants.Find(v => v.VariantParameter.EqualsIgnoringCase(parameter)) == null, "Variant with paramter already exists.");

		var instance = (Option)Activator.CreateInstance(GetType());
		instance.Parent = this;
		instance.VariantParameter = parameter;
		instance.IsDefaultVariant = false;

		if (variants == null)
			variants = new List<IOption>();
		variants.Add(instance);

		if (IsArrayVariant) {
			RenumberArrayVariants();
		}

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
	public IOption GetVariant(string parameter, bool create = true)
	{
		Assert.IsTrue(IsVariant, "Invalid call to GetVariant, option is not variant.");
		Assert.IsTrue(IsDefaultVariant, "Invalid call to GetVariant, option is not the default variant.");

		if (string.Equals(parameter, VariantDefaultParameter, StringComparison.OrdinalIgnoreCase))
			return this;

		if (!create && variants == null)
			return null;

		IOption variant = null;
		if (variants != null) {
			variant = variants.Find(v => v.VariantParameter.EqualsIgnoringCase(parameter));
		}
		
		if (create && variant == null) {
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

		Assert.IsTrue(variants != null && variants.Contains(option), "Invalid call to RemoveVariant, option is not a variant of this instance.");

		variants.Remove(option);
		option.Parent = null;

		if (IsArrayVariant) {
			RenumberArrayVariants();
		}
	}

	/// <summary>
	/// Ensures parameters in array variants are all numbers and sequential.
	/// </summary>
	protected void RenumberArrayVariants()
	{
		Assert.IsTrue(IsVariant, "Invalid call to RenumberArrayVariants, option is not variant.");
		Assert.IsTrue(IsDefaultVariant, "Invalid call to RenumberArrayVariants, option is not the default variant.");
		Assert.IsTrue(IsArrayVariant, "Invalid call to RenumberArrayVariants, option is not variant.");

		// Default variant is always 0
		VariantParameter = "0";

		// First order parameters using natural sort, then assign sequential indices
		var comparer = NumericStringComparer.Instance;
		variants.Sort((a, b) => comparer.Compare(a.VariantParameter, b.VariantParameter));
		for (int i = 0; i < variants.Count; i++) {
			variants[i].VariantParameter = (i + 1).ToString();
		}
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

		IOption child;
		children.TryGetValue(name, out child);
		return child;
	}

	/// <summary>
	/// Get a child option instance by its name.
	/// </summary>
	public TOption GetChild<TOption>() where TOption : IOption
	{
		if (children != null) {
			foreach (var child in children.Values) {
				if (child is TOption)
					return (TOption)child;
			}
		}

		return default(TOption);
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

	// TODO: Move somewhere else?

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

