using System;
using System.Collections.Generic;
using System.Linq;
using sttz.Workbench.Extensions;
using UnityEditor;
using UnityEngine;

namespace sttz.Workbench.Editor
{

/// <summary>
/// Helper class to recurse over the options hierarchy.
/// </summary>
public static class Recursion
{
	/// <summary>
	/// The type of recursion, indicating if the recursion
	/// happens directly over the options or over the store.
	/// </summary>
    public enum RecursionType
	{
		/// <summary>
		/// Options are recursed directly, variant options exist.
		/// </summary>
		Options,
		/// <summary>
		/// The store is recursed, every option type has only a single shared instance.
		/// </summary>
		Nodes
	}

	/// <summary>
	/// The type of variant node being recursed.
	/// </summary>
	public enum VariantType
	{
		/// <summary>
		/// The node is not a variant option.
		/// </summary>
		None,
		/// <summary>
		/// The node is the variant option container.
		/// </summary>
		VariantContainer,
		/// <summary>
		/// The node is the default variant option.
		/// </summary>
		/// <remarks>
		/// Note that the option and node instance recursed here is
		/// the same as with <see cref="VariantContainer" />. This extra
		/// step exists for rendering the options where the container node
		/// can be expanded to reveal the variants, including the default.
		/// </remarks>
		DefaultVariant,
		/// <summary>
		/// The node is a regular variant.
		/// </summary>
		VariantChild
	}

	/// <summary>
	/// Context storing information about the recursion.
	/// </summary>
	public struct RecurseOptionsContext
	{
		/// <summary>
		/// The type of recursion currently ongoing.
		/// </summary>
		public RecursionType type;
		/// <summary>
		/// The path to the current node.
		/// </summary>
		public string path;
		/// <summary>
		/// The nesting depth of the current node.
		/// </summary>
		public int depth;

		/// <summary>
		/// The current option instance.
		/// </summary>
		/// <remarks>
		/// Note that with <see cref="RecursionType.Nodes"/> there's only a 
		/// single shared options instance per type, so you can only query
		/// it for non-specific information.
		/// </remarks>
		public IOption option;
		/// <summary>
		/// The current store node.
		/// </summary>
		/// <remarks>
		/// Note that with <see cref="RecursionType.Options"/> <c>node</c> is always null.
		/// </remarks>
		public ValueStore.Node node;
		
		/// <summary>
		/// The variant type of the current node.
		/// </summary>
		public VariantType variantType;

		/// <summary>
		/// The parent option of the current node.
		/// </summary>
		public IOption parentOption;
		/// <summary>
		/// The parent store node of the current node.
		/// </summary>
		public ValueStore.Node parentNode;
		/// <summary>
		/// Wether the current sub-tree is included in the build.
		/// </summary>
		public OptionInclusion inclusion;

		/// <summary>
		/// Wether the current node is a root.
		/// </summary>
		/// <returns></returns>
		public bool IsRoot {
			get {
				if (type == RecursionType.Nodes) {
					return node is ValueStore.RootNode && node != parentNode;
				} else {
					return option.Parent == null;
				}
			}
		}

		/// <summary>
		/// Wether the current node has variants/childrens that can be recursed into.
		/// </summary>
		public bool IsRecursable {
			get {
				return variantType == VariantType.VariantContainer || option.HasChildren;
			}
		}

		/// <summary>
		/// The variant parameter of the current node.
		/// </summary>
		public string VariantParameter {
			get {
				if (variantType != VariantType.DefaultVariant && variantType != VariantType.VariantChild)
					return null;
				
				if (type == RecursionType.Nodes) {
					if (variantType == VariantType.DefaultVariant) {
						return option.VariantDefaultParameter;
					} else {
						return node.Name;
					}
				} else {
					return option.VariantParameter;
				}
			}
		}

		/// <summary>
		/// The value of the current node.
		/// </summary>
		public string Value {
			get {
				if (type == RecursionType.Nodes) {
					return node.Value;
				} else {
					return option.Save();
				}
			}
		}

		/// <summary>
		/// Internal method used to set up the state when recursing into a node.
		/// </summary>
		internal RecurseOptionsContext Recurse(IOption childOption, ValueStore.Node childNode, bool defaultVariant = false)
		{
			var child = this;
			child.type = type;

			child.parentOption = option;
			child.parentNode = node;
			child.option = childOption;
			child.node = childNode;

			if (child.type == RecursionType.Nodes && child.IsRoot) {
				child.inclusion = ((ValueStore.RootNode)child.node).Inclusion;
			} else {
				child.inclusion = inclusion;
			}

			// Determine the variant type
			if (variantType == VariantType.None && child.option.Variance != OptionVariance.Single) {
				child.variantType = VariantType.VariantContainer;
			} else if (variantType == VariantType.VariantContainer) {
				if (defaultVariant) {
					child.variantType = VariantType.DefaultVariant;
				} else {
					child.variantType = VariantType.VariantChild;
				}
			} else {
				child.variantType = VariantType.None;
			}

			// Determine the child path
			string pathName;
			if (type == RecursionType.Nodes) {
				if (child.variantType == VariantType.VariantContainer) {
					pathName = child.option.Name;
				} else if (child.variantType == VariantType.DefaultVariant) {
					pathName = child.option.VariantDefaultParameter;
				} else if (child.variantType == VariantType.VariantChild) {
					pathName = child.node.Name;
				} else {
					pathName = child.option.Name;
				}
			} else {
				if (child.variantType == VariantType.VariantContainer) {
					pathName = child.option.Name;
				} else if (child.variantType == VariantType.DefaultVariant) {
					pathName = child.option.VariantParameter;
				} else if (child.variantType == VariantType.VariantChild) {
					pathName = child.option.VariantParameter;
				} else {
					pathName = child.option.Name;
				}
			}
			child.path += pathName + "/";

			child.depth = depth + 1;

			return child;
		}
	}

	/// <summary>
	/// Sort the root options in the given profile first by category and then by name.
	/// </summary>
	public static List<IOption> SortOptionsByCategoryAndName(IEnumerable<IOption> options)
	{
		var list = new List<IOption>(options);
		list.Sort((o1, o2) => {
			var cat = string.CompareOrdinal(o1.Category, o2.Category);
			if (cat != 0) {
				return cat;
			} else {
				return string.CompareOrdinal(o1.Name, o2.Name);
			}
		});
		return list;
	}

	/// <summary>
	/// Recursive method for <see cref="RecursionType.Nodes"/>.
	/// </summary>
	static void RecurseNodesRecursive(RecurseOptionsContext context, Func<RecurseOptionsContext, bool> callback)
	{
		if (!callback(context)) return;

		if (context.variantType == VariantType.VariantContainer) {
			// Find or create default variant node
			var defaultNode = context.node.GetOrCreateVariant(context.option.VariantDefaultParameter);
			RecurseNodesRecursive(context.Recurse(context.option, defaultNode, defaultVariant:true), callback);

			// Recurse into variants, note that the same option instance is used
			foreach (var variantNode in context.node.Variants) {
				if (variantNode.Name == context.option.VariantDefaultParameter)
					continue;
				RecurseNodesRecursive(context.Recurse(context.option, variantNode), callback);
			}
		}

		// Recurse into children (the variant container cannot have children)
		if (context.option.HasChildren && context.variantType != VariantType.VariantContainer) {
			foreach (var childOption in context.option.Children) {
				var childNode = context.node.GetOrCreateChild(childOption.Name);
				RecurseNodesRecursive(context.Recurse(childOption, childNode), callback);
			}
		}
	}

	/// <summary>
	/// Recursive method for <see cref="RecursionType.Options"/>.
	/// </summary>
	static void RecurseOptionsRecursive(RecurseOptionsContext context, Func<RecurseOptionsContext, bool> callback)
	{
		if (!callback(context)) return;

		if (context.variantType == VariantType.VariantContainer) {
			// Recurse same option but with VarianType.DefaultVariant
			RecurseOptionsRecursive(context.Recurse(context.option, null, defaultVariant:true), callback);

			// Recurse into variants
			foreach (var variantOption in context.option.Variants) {
				RecurseOptionsRecursive(context.Recurse(variantOption, null), callback);
			}
		}

		// Recurse into children (the variant container cannot have children)
		if (context.option.HasChildren && context.variantType != VariantType.VariantContainer) {
			foreach (var childOption in context.option.Children) {
				RecurseOptionsRecursive(context.Recurse(childOption, null), callback);
			}
		}
	}

	/// <summary>
	/// Recurse over the options of the given profile.
	/// </summary>
	/// <param name="profile">The profile the options belong to.</param>
	/// <param name="type">The type of recursion to perform.</param>
	/// <param name="callback">The callback to call for each node.</param>
	public static void Recurse(EditableProfile profile, RecursionType type, Func<RecurseOptionsContext, bool> callback)
	{
		Recurse(profile, type, profile.GetAllOptions(), callback);
	}

	/// <summary>
	/// Recurse over the options of the given profile.
	/// </summary>
	/// <param name="profile">The profile the options belong to.</param>
	/// <param name="type">The type of recursion to perform.</param>
	/// <param name="options">Custom list of root options, mostly for custom sorting.</param>
	/// <param name="callback">The callback to call for each node.</param>
	public static void Recurse(EditableProfile profile, RecursionType type, IEnumerable<IOption> options, Func<RecurseOptionsContext, bool> callback)
	{
		var context = new RecurseOptionsContext();
		context.type = type;
		context.path = "";
		context.depth = -1;

		foreach (var option in options) {
			if (type == RecursionType.Nodes) {
				var rootNode = profile.Store.GetOrCreateRoot(option.Name);
				RecurseNodesRecursive(context.Recurse(option, rootNode), callback);
			} else {
				RecurseOptionsRecursive(context.Recurse(option, null), callback);
			}
		}
	}
}

}