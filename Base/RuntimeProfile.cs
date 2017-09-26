using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using sttz.Workbench.Extensions;
using UnityEngine;

namespace sttz.Workbench
{

// TODO: Better handling of when values are applied

/// <summary>
/// A profile manages options and their default values at runtime.
/// </summary>
/// <remarks>
/// The profile can be enumerated to access the individual options.
/// </remarks>
public class RuntimeProfile : IEnumerable<IOption>
{
	// -------- Static --------

	/// <summary>
	/// All available options types.
	/// </summary>
	/// <remarks>
	/// All options in the editor and only included options in builds.
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
							&& typeof(IOption).IsAssignableFrom(t)
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
	/// during building when postprocessing scenes and in the player when
	/// any options have been built in.
	/// </remarks>
	public static RuntimeProfile Main { get; protected set; }

#if UNITY_EDITOR
	[RuntimeInitializeOnLoadMethod]
	static void LoadMainRuntimeProfile()
	{
		//
	}
#endif

	/// <summary>
	/// Create the main runtime profile with the given value store.
	/// </summary>
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
	/// Options by name.
	/// </summary>
	protected Dictionary<string, IOption> options
		= new Dictionary<string, IOption>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Values used for this profile.
	/// </summary>
	/// <remarks>
	/// Setting the store causes all options' value to be reset to the
	/// value from the store, options for which no value is defined in
	/// the store are reset to their default value. Setting the store 
	/// will not apply the option.
	/// </remarks>
	public virtual ValueStore Store {
		get {
			return _store;
		}
		set {
			if (_store == value) return;

			_store = value ?? new ValueStore();

			// Apply values in store to options
			foreach (var node in _store.Roots) {
				IOption option;
				if (!options.TryGetValue(node.name, out option))
					continue;

				LoadNode(option, node);
			}
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
		// Create option instances
		foreach (var optionType in AllOptionTypes) {
			if (ShouldCreateOption(optionType)) {
				var option = (IOption)Activator.CreateInstance(optionType);
				options[option.Name] = option;
			}
		}

		// Set store, which sets its values on the options
		Store = store;
	}

	/// <summary>
	/// Try to find an option by its path, allowing to get nested
	/// child and variant options.
	/// </summary>
	public IOption GetOption(string path)
	{
		if (!path.Contains('/')) {
			return GetRootOption(path);
		}

		var parts = path.Split('/');
		var current = GetRootOption(parts[0]);
		for (int i = 1; i < parts.Length && current != null; i++) {
			if (current.HasChildren) {
				current = current.GetChild(parts[i]);
			} else if (current.IsDefaultVariant) {
				current = current.GetVariant(parts[i]);
			} else {
				current = null;
			}
		}

		return current;
	}

	/// <summary>
	/// Get a root option by name (no nested child or variant options).
	/// </summary>
	public IOption GetRootOption(string name)
	{
		if (options.ContainsKey(name)) {
			return options[name];
		} else {
			return null;
		}
	}

	/// <summary>
	/// Apply the profile, i.e. apply all options it contains.
	/// </summary>
	public void Apply()
	{
		foreach (var option in this) {
			option.Apply();
		}
	}

	/// <summary>
	/// Save the options' current values to the store.
	/// </summary>
	/// <remarks>
	/// The store will not be cleared, i.e. nodes and values for options that
	/// do not exist will not be removed.
	/// </remarks>
	public void SaveToStore()
	{
		foreach (var option in options.Values) {
			var rootNode = Store.GetRoot(option.Name);
			if (rootNode == null) {
				rootNode = Store.AddRoot(option.Name, null);
			}

			SaveNode(rootNode, option);
		}
	}

	/// <summary>
	/// Limit the options the profile creates (for subclasses).
	/// </summary>
	protected virtual bool ShouldCreateOption(Type optionType)
	{
		return optionType.GetCustomAttributes(typeof(BuildOnlyAttribute), true).Length == 0;
	}

	/// <summary>
	/// Recursive method to apply a node with all its variants and
	/// children to an option.
	/// </summary>
	private void LoadNode(IOption option, ValueStore.Node node)
	{
		option.Load(node.value ?? string.Empty);

		if (option.IsDefaultVariant) {
			// Reset variants since the node might not contain a value
			foreach (var variantOption in option.Variants) {
				variantOption.Load(string.Empty);
			}

			if (node.variants != null) {
				foreach (var variantNode in node.variants) {
					var variantOption = option.GetVariant(variantNode.name);
					LoadNode(variantOption, variantNode);
				}
			}
		}

		// Reset children since the node might not contain a value
		foreach (var childOption in option.Children) {
			childOption.Load(string.Empty);
		}

		if (node.children != null) {
			foreach (var childNode in node.children) {
				var childOption = option.GetChild(childNode.name);
				if (childOption != null) {
					LoadNode(childOption, childNode);
				}
			}
		}
	}

	/// <summary>
	/// Recursive method to save the option's value and its variants'
	/// and children's values to the node.
	/// </summary>
	private void SaveNode(ValueStore.Node node, IOption option)
	{
		node.value = option.Save();

		if (option.IsVariant) {
			// Remove the variant sub-node matching the default variant parameter if it exists
			// as there would be ambiguity between the main node and the variant sub-node
			node.RemoveVariant(option.VariantDefaultParameter);

			foreach (var variantOption in option.Variants) {
				var variantNode = node.GetVariant(variantOption.VariantParameter);
				if (variantNode == null) {
					node.AddVariant(variantOption.VariantParameter, variantOption.Save());
				} else {
					variantNode.value = variantOption.Save();
				}
			}
		}

		foreach (var childOption in option.Children) {
			var childNode = node.GetChild(childOption.Name);
			if (childNode == null) {
				node.AddChild(childOption.Name, childOption.Save());
			} else {
				childNode.value = childOption.Save();
			}
		}
	}

	// -------- IEnumerable --------

	public IEnumerator<IOption> GetEnumerator()
	{
		return options.Values.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

}

