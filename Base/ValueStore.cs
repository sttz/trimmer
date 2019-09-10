//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if !NO_TRIMMER || UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using sttz.Trimmer.Extensions;
using System.Text;
using System.Text.RegularExpressions;

namespace sttz.Trimmer
{

// TODO: Way to clean up store from old nodes

/// <summary>
/// Unity compatible store of nested string key/value pairs.
/// </summary>
/// <remarks>
/// The ValueStore is used to serialize the nested Option values in
/// Unity using its `ISerializationCallbackReceiver` callbacks.
/// </remarks>
[Serializable]
public class ValueStore : ISerializationCallbackReceiver
{
    /// <summary>
    /// Named value node with optional variants and children.
    /// </summary>
    [Serializable]
    public class Node
    {
        [SerializeField] internal string name;
        [SerializeField] internal string value = string.Empty;

        [NonSerialized] internal List<Node> variants;
        [NonSerialized] internal List<Node> children;

        /// <summary>
        /// Used for compatibility with Unity's serialization.
        /// </summary>
        [SerializeField] internal int numVariants;
        /// <summary>
        /// Used for compatibility with Unity's serialization.
        /// </summary>
        [SerializeField] internal int numChildren;

        /// <summary>
        /// Flag to track if store needs to be saved.
        /// </summary>
        [NonSerialized] internal bool isDirty;

        /// <summary>
        /// Name of the node, variant parameter for variant nodes
        /// and child name for child nodes.
        /// </summary>
        public string Name {
            get {
                return name;
            }
            set {
                if (this.name == value) return;
                this.name = value;
                isDirty = true;
            }
        }

        /// <summary>
        /// The value (if any) of the node.
        /// </summary>
        public string Value {
            get {
                return value;
            }
            set {
                if (this.value == value) return;
                this.value = value;
                isDirty = true;
            }
        }

        /// <summary>
        /// Enumerate the node's variants.
        /// </summary>
        public IEnumerable<Node> Variants {
            get {
                if (variants != null) {
                    return variants;
                } else {
                    return Enumerable.Empty<Node>();
                }
            }
        }

        /// <summary>
        /// Number of variants in this node.
        /// </summary>
        public int VariantCount {
            get {
                if (variants != null) {
                    return variants.Count;
                } else {
                    return 0;
                }
            }
        }

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
            isDirty = true;
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
                    isDirty = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Remove a variant node.
        /// </summary>
        public void RemoveVariant(int index)
        {
            if (variants == null)
                return;

            variants.RemoveAt(index);
            isDirty = true;
        }

        /// <summary>
        /// Remove all variant nodes.
        /// </summary>
        public void ClearVariants()
        {
            if (variants != null) {
                variants.Clear();
            }
        }

        /// <summary>
        /// Set variants parameters to a sequential index,
        /// assigned based on natural sort order of existing
        /// parameters.
        /// </summary>
        public void NumberVariantsSequentially()
        {
            var comparer = NumericStringComparer.Instance;
            variants.Sort((a, b) => comparer.Compare(a.name, b.name));
            for (int i = 0; i < variants.Count; i++) {
                variants[i].name = i.ToString();
            }
        }

        /// <summary>
        /// Enumerate the node's children.
        /// </summary>
        public IEnumerable<Node> Children {
            get {
                if (children != null) {
                    return children;
                } else {
                    return Enumerable.Empty<Node>();
                }
            }
        }

        /// <summary>
        /// Number of children in this node.
        /// </summary>
        public int ChildCount {
            get {
                if (children != null) {
                    return children.Count;
                } else {
                    return 0;
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
            isDirty = true;
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
                    isDirty = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Remove all child nodes.
        /// </summary>
        public void ClearChildren()
        {
            if (children != null) {
                children.Clear();
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

        override public string ToString()
        {
            return string.Format(
                "[Node {0} = {1}, children = {2}, variants = {3}, isDirty = {4}]",
                name, value, ChildCount, VariantCount, isDirty
            );
        }
    }

    /// <summary>
    /// Root named value node that has additional fields that only
    /// apply to the root nodes.
    /// </summary>
    [Serializable]
    public class RootNode : Node
    {
        #if UNITY_EDITOR

        [SerializeField] internal OptionInclusion inclusion;

        /// <summary>
        /// Wether the option should be included in builds.
        /// </summary>
        public OptionInclusion Inclusion {
            get {
                return inclusion;
            }
            set {
                if (inclusion == value) return;
                inclusion = value;
                isDirty = true;
            }
        }

        #endif

        public override Node Clone()
        {
            var clone = Clone<RootNode>();

            #if UNITY_EDITOR
            clone.inclusion = inclusion;
            #endif
            
            return clone;
        }

        override public string ToString()
        {
            #if UNITY_EDITOR
            return string.Format(
                "[RootNode {0} = {1}, children = {2}, variants = {3}, isDirty = {4}, inclusion = {5}]",
                name, value, ChildCount, VariantCount, isDirty, inclusion
            );
            #else
            return string.Format(
                "[RootNode {0} = {1}, children = {2}, variants = {3}, isDirty = {4}]",
                name, value, ChildCount, VariantCount, isDirty
            );
            #endif
        }
    }

    /// <summary>
    /// Root nodes by name.
    /// </summary>
    [NonSerialized] List<RootNode> nodes;

    /// <summary>
    /// Track if the store roots have been changed.
    /// </summary>
    [NonSerialized] bool isDirty;

    /// <summary>
    /// Check if the store has been modified and optionally
    /// reset the modification state.
    /// </summary>
    public bool IsDirty(bool clear = false)
    {
        var anyDirty = isDirty;

        if (!clear && anyDirty)
            return true;
        else if (clear)
            isDirty = false;

        foreach (var root in Roots) {
            anyDirty |= IsDirtyRecursive(root, clear);

            if (!clear && anyDirty)
                return true;
        }

        return anyDirty;
    }

    bool IsDirtyRecursive(Node node, bool clear = false)
    {
        var anyDirty = node.isDirty;

        if (!clear && anyDirty)
            return true;
        else if (clear)
            node.isDirty = false;

        foreach (var variant in node.Variants) {
            anyDirty |= IsDirtyRecursive(variant, clear);

            if (!clear && anyDirty)
                return true;
        }

        foreach (var child in node.Children) {
            anyDirty |= IsDirtyRecursive(child, clear);

            if (!clear && anyDirty)
                return true;
        }

        return anyDirty;
    }

    /// <summary>
    /// Enumerate the root nodes in this store.
    /// </summary>
    public IEnumerable<RootNode> Roots {
        get {
            if (nodes != null) {
                return nodes;
            } else {
                return Enumerable.Empty<RootNode>();
            }
        }
    }

    /// <summary>
    /// Number of root nodes in this store.
    /// </summary>
    public int RootCount {
        get {
            if (nodes != null) {
                return nodes.Count;
            } else {
                return 0;
            }
        }
    }

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
        isDirty = true;
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
                isDirty = true;
                return;
            }
        }
    }

    /// <summary>
    /// Removes all content form the store.
    /// </summary>
    public void Clear()
    {
        if (nodes == null)
            return;
        
        nodes.Clear();
    }

    /// <summary>
    /// Create a clone of the current store.
    /// </summary>
    public ValueStore Clone()
    {
        var clone = new ValueStore();
        
        clone.nodes = new List<RootNode>(nodes.Count);
        foreach (var node in nodes) {
            clone.nodes.Add((RootNode)node.Clone());
        }

        return clone;
    }

    // -------- Unity Serialization --------

    [SerializeField] List<RootNode> serializedRootNodes;
    [SerializeField] List<Node> serializedNodes;
    [SerializeField] int numNodes;

    /// <summary>
    /// Unity doesn't support serialization of types that contain themselves,
    /// like in this case <c>Node</c> that contains lists of <c>Node</c>.
    /// To enable this we don't serialize the nested lists but instead just
    /// the size of the lists (i.e. Node.numVariants and Node.numChildren) 
    /// and then use <c>ISerializationCallbackReceiver</c> to flatten and
    /// unpack the tree before and after Unity serializes it.
    /// 
    /// We process the nodes in the same order flattening and unpacking them
    /// and that allows us to get away with just serializing the list lengths.
    /// </summary>
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

/// <summary>
/// Helper methods to save a <see cref="ValueStore"/> to an Ini file and loading it back again.
/// </summary>
/// <remarks>
/// The ini file supports comments on lines starting with "#" or "//" but not
/// trailing comments. Categories are not supported.
/// 
/// Each line assigns a value to an Option. The name part in front of the equal
/// sign can contain child names separated by a dot and variant parameters
/// enclosed with square brackets. Variant parameters must be quoted with double
/// quotes if they contain square brackets (and double quotes then escaped).
/// 
/// The value after the equal sign will be trimmed. It can be quoted with double
/// quotes to retain the white space.
/// </remarks>
public static class IniAdapter
{
    // -------- Ini Serialization --------

    /// <summary>
    /// Save the content of this value store as a Ini file formatted string.
    /// </summary>
    public static string Save(ValueStore store)
    {
        var output = new StringBuilder();
        foreach (var node in store.Roots) {
            SaveIniRecursive(output, "", node, false);
        }
        return output.ToString();
    }

    static void SaveIniRecursive(StringBuilder output, string path, ValueStore.Node node, bool isVariantChild)
    {
        if (!isVariantChild) {
            path += node.name;
        } else {
            path += "[" + QuoteParameterIfNeeded(node.name) + "]";
        }

        if (!string.IsNullOrEmpty(node.value)) {
            output.Append(path);
            output.Append(" = ");
            output.Append(QuoteValueIfNeeded(node.value));
            output.Append("\n");
        }

        if (node.VariantCount > 0) {
            foreach (var variant in node.variants) {
                SaveIniRecursive(output, path, variant, true);
            }
        }

        if (node.ChildCount > 0) {
            var childPath = path + ".";
            foreach (var child in node.children) {
                SaveIniRecursive(output, childPath, child, false);
            }
        }
    }

    /// <summary>
    /// Quote a parameter if it contains square brackets or
    /// return it as-is otherwise.
    /// </summary>
    public static string QuoteParameterIfNeeded(string parameter)
    {
        if (parameter.IndexOf('[') < 0 && parameter.IndexOf(']') < 0) {
            return parameter;
        }

        return '"' + parameter.Replace("\\", "\\\\").Replace("\"", "\\\"") + '"';
    }

    /// <summary>
    /// Quote a value if it contains leading or trailing whitespace or
    /// return it as-is otherwise.
    /// </summary>
    public static string QuoteValueIfNeeded(string value)
    {
        if (value.Trim() == value) {
            return value;
        }

        return '"' + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + '"';
    }

    // ------ Ini Deserialization ------

    /// <summary>
    /// Load the values in an Ini file into this value store instance,
    /// replacing the value store's contents.
    /// </summary>
    public static void Load(ValueStore store, string content)
    {
        // TODO: More input validation
        var lines = content.Split('\n');
        foreach (var line in lines) {
            if (commentedLineRegex.IsMatch(line) || line.Trim().Length == 0)
                continue;
            
            var match = LineRegex.Match(line);
            if (!match.Success) {
                Debug.LogWarning("Failed to read line in Ini file: " + line);
                continue;
            }

            // Groups:
            // 1 = Regular Name
            // 2 = Child name
            // 3 = Quoted Parameter
            // 4 = Unquoted Parameter
            // 5 = Quoted Value
            // 6 = Unquoted Value

            var rootCapture = match.Groups[1];
            var rootNode = store.GetOrCreateRoot(rootCapture.Value);

            var node = GetNodeRecursive(rootNode, match, rootCapture.Index + rootCapture.Length - 1);
            
            var isQuoted = true;
            var valueCapture = match.Groups[5];
            if (!valueCapture.Success) {
                isQuoted = false;
                valueCapture = match.Groups[6];
            }

            node.value = ProcessQuotedString(valueCapture.Value, isQuoted);
        }
    }

    /// <summary>
    /// Regex fragment matching identifiers based on valid C# identifiers.
    /// L = All letter characters (upper, lower, title, modifier and other)
    /// Nl = Number, Letter, Mn = Mark, Nonspacing, Mc = Mark, Combining
    /// Nd = Number, Decimal Digit, Pc = Punctuation, Connector, Cf = Other, Format
    /// Note that C# disallows leading numbers but this regex matches them.
    /// </summary>
    const string IdentifierRegex = @"([\p{L}|\p{Nl}|\p{Mn}|\p{Mc}|\p{Nd}|\p{Pc}|\p{Cf}]+)";

    /// <summary>
    /// Regex fragment matching an optionally quoted string containing escape sequences.
    /// E.g. Unquoted String, "Quoted String", "Quoted with \"Escaped\" Quotes", "Quoted Double Escape \\"
    /// </summary>
    const string QuotedStringRegex = @"
        (?: 				# Group to make quotes optional
            ""(				# Opening quote
                (?:
                    [^""\\]	# Anything but "" or \
                  | \\.		# Or any escaped character
                )*
            )""				# Ending quote
          | (.*?)			# Anything unqutoed
        )
    ";

    /// <summary>
    /// Regex fragment matching the name part of a ini line.
    /// </summary>
    const string IniNameRegex = @"
        \s*										# Any leading whitespce
        " + IdentifierRegex + @"				# The name of the root option
        (?:										# Start of name part
          \." + IdentifierRegex + @"			# A child name starting with .
          | \[ " + QuotedStringRegex + @" \]	# Or a paramter in []
        )*
        \s*										# Any trailing whitespace
    ";

    /// <summary>
    /// Regex matching only the name part of an ini line.
    /// </summary>
    public static readonly Regex NameRegex = new Regex(
        "^" + IniNameRegex, 
        RegexOptions.IgnorePatternWhitespace
    );

    /// <summary>
    /// Regex matching a line in a Ini file.
    /// </summary>
    public static readonly Regex LineRegex = new Regex(@"
        ^" + IniNameRegex + @"					# Name part
        =\s*									# Assignment =
        " + QuotedStringRegex + @"				# Value part
        \s*$									# Any trailing whitespace
    ", RegexOptions.IgnorePatternWhitespace);

    /// <summary>
    /// Regex matching a comment line starting with # or //
    /// </summary>
    static readonly Regex commentedLineRegex = new Regex(@"^\s*(?:#|//)");

    /// <summary>
    /// Find the next capture in the match and return the group index
    /// or <c>-1</c> if no capture was found.
    /// </summary>
    static int FindNextCapture(Match match, int startIndex, out Capture capture)
    {
        capture = null;
        var index = -1;
        for (int i = 0; i < match.Groups.Count; i++) {
            foreach (Capture candidate in match.Groups[i].Captures) {
                if (candidate.Index >= startIndex) {
                    if (capture == null || candidate.Index < capture.Index) {
                        capture = candidate;
                        index = i;
                    } else {
                        break;
                    }
                }
            }
        }
        return index;
    }

    static ValueStore.Node GetNodeRecursive(ValueStore.Node current, Match match, int index)
    {
        Capture next;
        var groupIndex = FindNextCapture(match, index, out next);

        if (groupIndex < 0) {
            Debug.LogWarning("Unexpected end of captures in match: " + match);
            return current;
        } else if (groupIndex == 2) {
            var child = current.GetOrCreateChild(next.Value);
            return GetNodeRecursive(child, match, next.Index + next.Length);
        } else if (groupIndex == 3 || groupIndex == 4) {
            var isQuoted = (groupIndex == 3);
            var variant = current.GetOrCreateVariant(ProcessQuotedString(next.Value, isQuoted));
            return GetNodeRecursive(variant, match, next.Index + next.Length);
        } else {
            return current;
        }
    }

    static string ProcessQuotedString(string input, bool isQuoted)
    {
        if (!isQuoted) {
            return input;
        } else {
            return input.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }

    // ------ Helpers ------

    /// <summary>
    /// Take the name part of an ini line and convert it to a option path
    /// that then can be used with <see cref="RuntimeProfile.GetOption(string)"/>.
    /// </summary>
    public static string NameToPath(string name)
    {
        var match = NameRegex.Match(name);
        if (!match.Success) return null;

        var rootCapture = match.Groups[1];
        return GetPathRecursive(rootCapture.Value, match, rootCapture.Index + rootCapture.Length - 1);
    }

    static string GetPathRecursive(string path, Match match, int index)
    {
        Capture next;
        var groupIndex = FindNextCapture(match, index, out next);

        if (groupIndex == 2) {
            return GetPathRecursive(path + "/" + next.Value, match, next.Index + next.Length);
        } else if (groupIndex == 3 || groupIndex == 4) {
            var name = ProcessQuotedString(next.Value, groupIndex == 3);
            return GetPathRecursive(path + ":" + name, match, next.Index + next.Length);
        } else {
            return path;
        }
    }

    /// <summary>
    /// Get the value part of an ini line, unquoting where necessary.
    /// </summary>
    public static string GetValue(string iniLine)
    {
        var match = LineRegex.Match(iniLine);
        if (!match.Success) return null;

        var isQuoted = true;
        var valueCapture = match.Groups[5];
        if (!valueCapture.Success) {
            isQuoted = false;
            valueCapture = match.Groups[6];
        }

        return ProcessQuotedString(valueCapture.Value, isQuoted);
    }
}

}

#endif
