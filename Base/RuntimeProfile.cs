#if !NO_TRIMMER || UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using sttz.Trimmer.Extensions;
using UnityEngine;

namespace sttz.Trimmer
{

/// <summary>
/// The Runtime Profile manages Options and their initial values
/// when playing the project in the editor or in a build.
/// </summary>
/// <remarks>
/// The profile can be enumerated to access the individual Options.
/// 
/// In case no Options are included in a build, Trimmer will be completely
/// removed and the RuntimeProfile class will not be available in builds. Use
/// the `NO_TRIMMER` compilation symbol to check if Trimmer will be removed.
/// </remarks>
public class RuntimeProfile : IEnumerable<Option>
{
	// -------- Static --------

	/// <summary>
	/// All available Options types.
	/// </summary>
	/// <remarks>
	/// This property will include all Options in the editor but
	/// only the included Options in builds.
	/// </remarks>
	public static IEnumerable<Type> AllOptionTypes {
		get {
			if (_options == null) {
				_options = new List<Type>();
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
					if (assembly is System.Reflection.Emit.AssemblyBuilder)
						continue;
					_options.AddRange(assembly.GetExportedTypes()
						.Where(t => 
							t.IsClass 
							&& !t.IsAbstract 
							&& !t.IsNested
							&& typeof(Option).IsAssignableFrom(t)
						)
					);
				}
			}
			return _options;
		}
	}
	private static List<Type> _options;

	/// <summary>
	/// Main runtime profile.
	/// </summary>
	/// <remarks>
	/// The main runtime profile is available while playing in the editor,
	/// during building when postprocessing scenes and in the player if
	/// any Options have been included.
	/// </remarks>
	public static RuntimeProfile Main { get; protected set; }

	/// <summary>
	/// Create the main runtime profile with the given value store.
	/// </summary>
	/// <remarks>
	/// This method is automatically called by <see cref="Editor.BuildManager"/>
	/// and <see cref="ProfileContainer"/> and should not be called manually.
	/// </remarks>
	public static void CreateMain(ValueStore store)
	{
		if (Main == null) {
			Main = new RuntimeProfile(store);
		} else {
			Main.Store = store;
		}
	}

	// -------- Profile --------

	/// <summary>
	/// Options sorted by <see cref="Option.ApplyOrder"/>.
	/// </summary>
	protected List<Option> options = new List<Option>();

	/// <summary>
	/// Options by name.
	/// </summary>
	protected Dictionary<string, Option> optionsByName
		= new Dictionary<string, Option>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Values used for this profile.
	/// </summary>
	/// <remarks>
	/// Setting the store causes all Options' value to be reset to the
	/// values from the store, Options for which no value is defined in
	/// the store are reset to their default value. Setting the store 
	/// will not apply the Options.
	/// </remarks>
	public virtual ValueStore Store {
		get {
			return _store;
		}
		set {
			if (_store == value) return;

			_store = value ?? new ValueStore();

			Load();
		}
	}
	private ValueStore _store;

	/// <summary>
	/// Create a new profile without any defaults.
	/// </summary>
	public RuntimeProfile() : this(null) { }

	/// <summary>
	/// Create a new profile with given defaults.
	/// </summary>
	public RuntimeProfile(ValueStore store)
	{
		// Create Option instances
		foreach (var optionType in AllOptionTypes) {
			if (ShouldCreateOption(optionType)) {
				var option = (Option)Activator.CreateInstance(optionType);
				options.Add(option);
				optionsByName[option.Name] = option;
			}
		}
		options.Sort((a, b) => a.ApplyOrder.CompareTo(b.ApplyOrder));

		// Set store, which sets its values on the Options
		Store = store;
	}

	/// <summary>
	/// Try to find an option by its path, allowing to get nested
	/// child and variant options.
	/// </summary>
	/// <remarks>
	/// Use <see cref="Option.Path"/> to get the path of an existing Option 
	/// that can then be used with this method.
	/// </remarks>
	public Option GetOption(string path)
	{
		// Example paths:
		// "Name"
		// "Name:Parameter/ChildName"
		// "Name/ChildName:Parameter/ChildName"

		// Fast path for looking up a plain name
		if (!path.Contains('/') && !path.Contains(':')) {
			return GetRootOption(path);
		}

		var parts = path.Split('/');

		// Resolve the root option / variant
		var part = parts[0];
		string param;
		ParseParameter(ref part, out param);

		var current = GetRootOption(part);
		if (param != null) {
			current = current.GetVariant(param);
		}

		if (current == null) return null;

		for (int i = 1; i < parts.Length; i++) {
			part = parts[i];
			ParseParameter(ref part, out param);

			if (!current.HasChildren)
				return null;

			current = current.GetChild(part);
			if (current == null) return null;

			if (param != null) {
				if (!current.IsDefaultVariant)
					return null;

				current = current.GetVariant(param);
				if (current == null) return null;
			}
		}

		return current;
	}

	protected void ParseParameter(ref string part, out string parameter)
	{
		parameter = null;
		var colon = part.IndexOf(':');
		if (colon >= 0) {
			parameter = part.Substring(colon + 1);
			part = part.Substring(0, colon);
		}
	}

	/// <summary>
	/// Get a main Option by name (no nested child or variant Options).
	/// </summary>
	public Option GetRootOption(string name)
	{
		if (optionsByName.ContainsKey(name)) {
			return optionsByName[name];
		} else {
			return null;
		}
	}

	/// <summary>
	/// Clear the profile, resetting all Options to their default values.
	/// </summary>
	public void Clear()
	{
		foreach (var option in optionsByName.Values) {
			ClearOption(option);
		}
	}

	/// <summary>
	/// Load the data in the store into the Option instances.
	/// </summary>
	public void Load()
	{
		// Clear first to remove entries not present in store
		Clear();

		// Apply values in store to Options
		foreach (var node in Store.Roots) {
			Option option;
			if (!optionsByName.TryGetValue(node.name, out option))
				continue;

			LoadNode(option, node);
		}
	}

	/// <summary>
	/// Apply the profile, i.e. apply all Options it contains.
	/// </summary>
	public void Apply()
	{
		foreach (var option in this) {
			option.Apply();
		}
	}

	/// <summary>
	/// Save the Options' current values to the store.
	/// </summary>
	/// <remarks>
	/// The store will be cleared of all entries with no maching Option instance.
	/// </remarks>
	public void SaveToStore()
	{
		Store.Clear();

		foreach (var option in optionsByName.Values) {
			var rootNode = Store.GetRoot(option.Name);
			if (rootNode == null) {
				rootNode = Store.AddRoot(option.Name, null);
			}

			SaveNode(rootNode, option);
		}
	}

	/// <summary>
	/// Remove all entries from the store that don't have a matching Option instance.
	/// </summary>
	public void CleanStore()
	{
		foreach (var root in Store.Roots.ToArray()) {
			Option option;
			if (optionsByName.TryGetValue(root.name, out option)) {
				CleanStoreRecursive(root, option);
			} else {
				Store.RemoveRoot(root.name);
			}
		}
	}

	protected void CleanStoreRecursive(ValueStore.Node node, Option option, bool isDefaultNode = false)
	{
		if (!isDefaultNode && option.IsDefaultVariant) {
			// Value and children are stored in the default parameter sub-node
			node.value = null;
			if (node.children != null) {
				node.children.Clear();
			}

			if (node.variants != null && node.variants.Count > 0) {
				foreach (var variant in node.variants.ToArray()) {
					if (variant.name.EqualsIgnoringCase(option.VariantDefaultParameter)) {
						CleanStoreRecursive(variant, option, isDefaultNode:true);
						continue;
					}
					var variantOption = option.GetVariant(variant.name, create:false);
					if (variantOption == null) {
						node.RemoveVariant(variant.name);
					} else {
						CleanStoreRecursive(variant, variantOption);
					}
				}
			}
		} else {
			if (node.variants != null) {
				node.variants.Clear();
			}
		}

		if (node.children != null && node.children.Count > 0) {
			foreach (var child in node.children.ToArray()) {
				var childOption = option.GetChild(child.name);
				if (childOption == null) {
					node.RemoveChild(child.name);
				} else {
					CleanStoreRecursive(child, childOption);
				}
			}
		}
	}

	/// <summary>
	/// Limit the Options the profile creates.
	/// </summary>
	protected virtual bool ShouldCreateOption(Type optionType)
	{
		#if UNITY_EDITOR
		// In the editor, only create options that have the CanPlayInEditor capability
		var caps = optionType.GetOptionCapabilities();
		return (caps & OptionCapabilities.CanPlayInEditor) != 0;
		#else
		// During the build, non-capable Options should have been excluded
		// Therefore, create all available Options in builds
		return true;
		#endif
	}

	/// <summary>
	/// Clear the Option recursively.
	/// </summary>
	private void ClearOption(Option option)
	{
		option.Load(string.Empty);

		foreach (var variant in option.Variants) {
			ClearOption(variant);
		}

		foreach (var child in option.Children) {
			ClearOption(child);
		}
	}

	/// <summary>
	/// Recursive method to apply a node with all its variants and
	/// children to an Option.
	/// </summary>
	private void LoadNode(Option option, ValueStore.Node node, bool isDefaultNode = false)
	{
		if (!isDefaultNode && option.IsDefaultVariant) {
			// Load default variant sub-node into the main variant option
			// (The container node's value is ignored)
			var defaultNode = node.GetVariant(option.VariantDefaultParameter);
			if (defaultNode != null) {
				LoadNode(option, defaultNode, isDefaultNode:true);
			}

			if (node.variants != null) {
				foreach (var variantNode in node.variants) {
					if (variantNode.Name.EqualsIgnoringCase(option.VariantDefaultParameter))
						continue;
					var variantOption = option.GetVariant(variantNode.name);
					LoadNode(variantOption, variantNode);
				}
			}
		} else {
			option.Load(node.value ?? string.Empty);

			if (node.children != null) {
				foreach (var childNode in node.children) {
					var childOption = option.GetChild(childNode.name);
					if (childOption != null) {
						LoadNode(childOption, childNode);
					}
				}
			}
		}
	}

	/// <summary>
	/// Recursive method to save the Option's value and its variants'
	/// and children's values to the node.
	/// </summary>
	private void SaveNode(ValueStore.Node node, Option option, bool isDefaultNode = false)
	{
		if (!isDefaultNode && option.IsDefaultVariant) {
			var defaultVariant = node.GetOrCreateVariant(option.VariantDefaultParameter);
			SaveNode(defaultVariant, option, isDefaultNode:true);

			foreach (var variantOption in option.Variants) {
				var variantNode = node.GetOrCreateVariant(variantOption.VariantParameter);
				SaveNode(variantNode, variantOption);
			}
		} else {
			node.value = option.Save();

			foreach (var childOption in option.Children) {
				var childNode = node.GetOrCreateChild(childOption.Name);
				SaveNode(childNode, childOption);
			}
		}
	}

	// -------- IEnumerable --------

	public IEnumerator<Option> GetEnumerator()
	{
		return options.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

}

#endif
