using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using sttz.Workbench.Extensions;

namespace sttz.Workbench
{

// TODO: Way to clean up store from old nodes

/// <summary>
/// Store of option values.
/// </summary>
[Serializable]
public class ValueStore : ISerializationCallbackReceiver
{
	/// <summary>
	/// Named value node with optional variants and children.
	/// </summary>
	[Serializable]
	public class Node
	{
		/// <summary>
		/// Name of the node, variant parameter for variant nodes
		/// and child name for child nodes.
		/// </summary>
		public string name;
		/// <summary>
		/// The value (if any) of the node.
		/// </summary>
		public string value = string.Empty;
		/// <summary>
		/// Used in the editor to track expanded state.
		/// </summary>
		public bool isExpanded;

		/// <summary>
		/// Variant sub-nodes.
		/// </summary>
		[NonSerialized] public List<Node> variants;
		/// <summary>
		/// Child nodes.
		/// </summary>
		[NonSerialized] public List<Node> children;

		/// <summary>
		/// Used for compatibility with Unity's serialization.
		/// </summary>
		[SerializeField] internal int numVariants;
		/// <summary>
		/// Used for compatibility with Unity's serialization.
		/// </summary>
		[SerializeField] internal int numChildren;

		/// <summary>
		/// Look up a variant node by name.
		/// </summary>
		public Node GetVariant(string name)
		{
			if (variants == null || string.IsNullOrEmpty(name))
				return null;

			foreach (var variant in variants) {
				if (variant.name.EqualsIgnoringCase(name)) {
					return variant;
				}
			}

			return null;
		}

		/// <summary>
		/// Look up a variant node by name, create it if it doesn't exist.
		/// </summary>
		public Node GetOrCreateVariant(string name)
		{
			var variant = GetVariant(name);
			if (variant == null)
				variant = AddVariant(name, string.Empty);
			return variant;
		}

		/// <summary>
		/// Add a new variant node.
		/// </summary>
		public Node AddVariant(string name, string value)
		{
			var node = new Node();
			node.name = name;
			node.value = value;

			if (variants == null)
				variants = new List<Node>();

			variants.Add(node);
			return node;
		}

		/// <summary>
		/// Remove a variant node.
		/// </summary>
		public void RemoveVariant(string name)
		{
			if (variants == null)
				return;

			for (int i = 0; i < variants.Count; i++) {
				if (variants[i].name.EqualsIgnoringCase(name)) {
					variants.RemoveAt(i);
					return;
				}
			}
		}

		/// <summary>
		/// Look up a child node by name.
		/// </summary>
		public Node GetChild(string name)
		{
			if (children == null || string.IsNullOrEmpty(name))
				return null;

			foreach (var child in children) {
				if (child.name.EqualsIgnoringCase(name)) {
					return child;
				}
			}

			return null;
		}

		/// <summary>
		/// Look up a child node by name, create it if it doesn't exist.
		/// </summary>
		public Node GetOrCreateChild(string name)
		{
			var child = GetChild(name);
			if (child == null)
				child = AddChild(name, string.Empty);
			return child;
		}

		/// <summary>
		/// Add a new child node.
		/// </summary>
		public Node AddChild(string name, string value)
		{
			var node = new Node();
			node.name = name;
			node.value = value;

			if (children == null)
				children = new List<Node>();

			children.Add(node);
			return node;
		}

		/// <summary>
		/// Remove a child node.
		/// </summary>
		public void RemoveChild(string name)
		{
			if (children == null)
				return;

			for (int i = 0; i < children.Count; i++) {
				if (children[i].name.EqualsIgnoringCase(name)) {
					children.RemoveAt(i);
					return;
				}
			}
		}

		/// <summary>
		/// Create a clone of this node, with all its variants and children.
		/// </summary>
		public virtual Node Clone()
		{
			return Clone<Node>();
		}

		/// <summary>
		/// Clone method to be used by subclasses for overriding.
		/// </summary>
		protected T Clone<T>() where T : Node, new()
		{
			var clone = new T() {
				name = name,
				value = value,
				isExpanded = isExpanded,
				numChildren = numChildren,
				numVariants = numVariants
			};

			if (variants != null) {
				clone.variants = new List<Node>(variants.Count);
				foreach (var variant in variants) {
					clone.variants.Add(variant.Clone());
				}
			}

			if (children != null) {
				clone.children = new List<Node>(children.Count);
				foreach (var child in children) {
					clone.children.Add(child.Clone());
				}
			}

			return clone;
		}
	}

	/// <summary>
	/// Root named value node that has additional fields that only
	/// apply to the root nodes.
	/// </summary>
	[Serializable]
	public class RootNode : Node
	{
		/// <summary>
		/// The category of the node, only valid for root nodes.
		/// </summary>
		public string category;
		/// <summary>
		/// Wether the option should be included in builds.
		/// </summary>
		public bool includeInBuild;

		public override Node Clone()
		{
			var clone = Clone<RootNode>();
			clone.category = category;
			clone.includeInBuild = includeInBuild;
			return clone;
		}
	}

	/// <summary>
	/// Root nodes by name.
	/// </summary>
	[NonSerialized] public List<RootNode> nodes;

	/// <summary>
	/// Get a root node by name.
	/// </summary>
	public RootNode GetRoot(string name)
	{
		if (nodes == null || string.IsNullOrEmpty(name))
			return null;

		foreach (var node in nodes) {
			if (node.name.EqualsIgnoringCase(name)) {
				return node;
			}
		}

		return null;
	}

	/// <summary>
	/// Get a root node by name, create it if it doesn't exist.
	/// </summary>
	public RootNode GetOrCreateRoot(string name)
	{
		var root = GetRoot(name);
		if (root == null) {
			Debug.Log("Create new root node: " + name);
			root = AddRoot(name, string.Empty);
		}
		return root;
	}

	/// <summary>
	/// Add a new root node.
	/// </summary>
	public RootNode AddRoot(string name, string value)
	{
		var node = new RootNode();
		node.name = name;
		node.value = value;

		if (nodes == null)
			nodes = new List<RootNode>();

		nodes.Add(node);
		return node;
	}

	/// <summary>
	/// Remove a root node.
	/// </summary>
	public void RemoveRoot(string name)
	{
		if (nodes == null)
			return;

		for (int i = 0; i < nodes.Count; i++) {
			if (nodes[i].name.EqualsIgnoringCase(name)) {
				nodes.RemoveAt(i);
				return;
			}
		}
	}

	/// <summary>
	/// Load the values in an INI file into this value store instance,
	/// replacing the value store's contents.
	/// </summary>
	public void LoadIniFile(string content)
	{
		// TODO: Parse ini file
	}

	/// <summary>
	/// Save the content of this value store as a INI file formatted string.
	/// </summary>
	public string SaveIniFile()
	{
		return null;
	}

	// -------- Unity Serialization --------

	/// <summary>
	/// Unity doesn't support serialization of types that contain themselves,
	/// like in this case <c>Node</c> that contains lists of <c>Node</c>.
	/// To enable this we don't serialize the nested lists but instead just
	/// the size of the lists (i.e. Node.numVariants and Node.numChildren) 
	/// and then use <c>ISerializationCallbackReceiver</c> to flatten and
	/// unpack the tree before and after Unity serializes it.
	/// 
	/// We process the nodes in the same order flattening and unpacking them
	/// and that allows us to get away with just serializing the list lengths
	/// instead of some more complex graph structure.
	/// </summary>
	[SerializeField] List<RootNode> serializedRootNodes;
	[SerializeField] List<Node> serializedNodes;
	[SerializeField] int numNodes;

	void ISerializationCallbackReceiver.OnBeforeSerialize()
	{
		serializedRootNodes = new List<RootNode>();
		serializedNodes = new List<Node>();

		if (nodes == null)
			return;
		
		numNodes = nodes.Count;
		serializedRootNodes.AddRange(nodes.Select(n => (RootNode)n.Clone()));
		for (int i = 0; i < numNodes; i++) {
			FlattenNode(serializedRootNodes[i], serializedNodes);
		}
	}

	void FlattenNode(Node node, List<Node> appendTo)
	{
		if (node.variants == null) {
			node.numVariants = 0;
		} else {
			node.numVariants = node.variants.Count;
			appendTo.AddRange(node.variants);

			foreach (var variant in node.variants) {
				FlattenNode(variant, appendTo);
			}

			node.variants = null;
		}

		if (node.children == null) {
			node.numChildren = 0;
		} else {
			node.numChildren = node.children.Count;
			appendTo.AddRange(node.children);

			foreach (var child in node.children) {
				FlattenNode(child, appendTo);
			}

			node.children = null;
		}
	}

	void ISerializationCallbackReceiver.OnAfterDeserialize()
	{
		nodes = new List<RootNode>();

		if (serializedRootNodes == null)
			return;

		int offset = 0;
		for (int i = 0; i < serializedRootNodes.Count; i++) {
			nodes.Add((RootNode)serializedRootNodes[i].Clone());
			UnpackNode(nodes[i], serializedNodes, ref offset);
		}
	}

	void UnpackNode(Node node, List<Node> takeFrom, ref int offset)
	{
		if (node.numVariants > 0) {
			node.variants = takeFrom.Skip(offset).Take(node.numVariants).Select(n => n.Clone()).ToList();
			offset += node.numVariants;

			foreach (var variant in node.variants) {
				UnpackNode(variant, takeFrom, ref offset);
			}
		}

		if (node.numChildren > 0) {
			node.children = takeFrom.Skip(offset).Take(node.numChildren).Select( n => n.Clone()).ToList();
			offset += node.numChildren;

			foreach (var children in node.children) {
				UnpackNode(children, takeFrom, ref offset);
			}
		}
	}
}

}
