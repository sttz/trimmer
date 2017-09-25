using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using sttz.Workbench.Extensions;

namespace sttz.Workbench
{

/// <summary>
/// Editor GUI for <see cref="BuildProfile"/> and <see cref="EditorProfile"/>.
/// </summary>
[CustomEditor(typeof(EditableProfile), true)]
public class BuildProfileEditor : Editor
{
	// -------- Constants --------

	/// <summary>
	/// Width of Unity's toggle control.
	/// </summary>
	const float toggleWidth = 13;
	/// <summary>
	/// Width of Unity's foldout control.
	/// </summary>
	const float foldoutWidth = 13;
	/// <summary>
	/// Width of columns with toggles on the right-hand side of the profile editor.
	/// </summary>
	const float buildColumnWidth = 17;

	// -------- Static --------

	/// <summary>
	/// Shared and re-used GUIContent instance to reduce allocations.
	/// (Unity is doing this all the time in its own GUI code).
	/// </summary>
	static GUIContent tempContent = new GUIContent();

	/// <summary>
	/// Regex used to add spaces to option names. In constrast
	/// to Unity's approach, this regex tries to keep series
	/// of captial letters together.
	/// </summary>
	static Regex SPACIFY_REGEX = new Regex(@"(?<! )(?:([A-Z]+)$|([A-Z]*)([A-Z])(?=[a-z]))");

	/// <summary>
	/// Prettifies camel case names by adding spaces.
	/// </summary>
	public static string OptionDisplayName(string name)
	{
		// Add spaces to camelCase names
		name = SPACIFY_REGEX.Replace(name, delegate(Match m) {
			var str = "";
			for (int i = 1; i < m.Groups.Count; i++) {
				if (m.Groups[i].Success && m.Groups[i].Length > 0) {
					str += " " + m.Groups[i].Value;
				}
			}
			return str;
		});

		name = name.Trim();
		name = name[0].ToString().ToUpper() + name.Substring(1);
		return name;
	}

	/// <summary>
	/// Gets the control id of the last control or 0.
	/// </summary>
	static int GetLastControlID()
	{
		if (lastControlIdField == null) {
			lastControlIdField = typeof(EditorGUIUtility).GetField("s_LastControlID", BindingFlags.Static | BindingFlags.NonPublic);
		}
		if (lastControlIdField != null) {
			return (int)lastControlIdField.GetValue(null);
		}
		return 0;
	}
	static FieldInfo lastControlIdField;

	/// <summary>
	/// Label that only takes the sapce it needs.
	/// </summary>
	static void TightLabel(string label, GUIStyle style = null)
	{
		if (style == null)
			style = EditorStyles.label;

		tempContent.text = label;
		var width = style.CalcSize(tempContent).x;
		GUILayout.Label(tempContent, style, GUILayout.Width(width));
	}

	/// <summary>
	/// Work around bug in EditorGUI's foldouts that don't toggle when
	/// clicking on the foldout's label.
	/// </summary>
	public static bool Foldout(bool foldout, string content, GUIStyle style = null)
	{
		// Set default style (native Foldout method doesn't like null style)
		if (style == null) style = EditorStyles.foldout;

		// Wrap foldout into a layout region to get the foldout's rect
		var rect = EditorGUILayout.BeginHorizontal(GUIStyle.none);
		var clicked = false;

		// Determine if it is being clicked on the foldout before
		// calling it to avoid it eating the event
		if (Event.current.type == EventType.MouseUp
			&& rect.Contains(Event.current.mousePosition)) {
			clicked = true;
		}

		var newFoldout = EditorGUILayout.Foldout(foldout, content, style);

		EditorGUILayout.EndHorizontal();

		// Act in case we saw a click but the foldout didn't toggle
		if (newFoldout == foldout && clicked) {
			newFoldout = !foldout;
			Event.current.Use();
		}

		return newFoldout;
	}

	// -------- API --------

	private EditableProfile profile;
	private EditorProfile editorProfile;
	private BuildProfile buildProfile;

	public override void OnInspectorGUI()
	{
		// Editing a not-saved scriptable object starts the editor
		// with GUI.enabled == false (seen in Unity 4.5.2)
		GUI.enabled = true;

		InitializeGUI();

		DefaultsGUI();

		EditorGUILayout.Space();

		OptionsGUI();

		GUILayout.FlexibleSpace();

		if (buildProfile != null) {
			BuildGUI();
			EditorGUILayout.Space();
		}

		ActiveProfileGUI();
	}

	protected void OnEnable()
	{
		profile = (EditableProfile)target;
		editorProfile = target as EditorProfile;
		buildProfile = target as BuildProfile;

		buildTarget = EditorUserBuildSettings.activeBuildTarget;

		options = new List<IOption>(profile.GetAllOptions());
		options.Sort((o1, o2) => {
			var cat = string.CompareOrdinal(o1.Category, o2.Category);
			if (cat != 0) {
				return cat;
			} else {
				return string.CompareOrdinal(o1.Name, o2.Name);
			}
		});

		// Invalidate defaults profile dropdown
		defaultsProfiles = null;
		defaultsProfilesNames = null;
	}

	protected void OnDisable()
	{
		profile.SaveIfNeeded();
	}

	// -------- Fields --------

	List<IOption> options;

	BuildTarget buildTarget;

	GUIStyle plusStyle;
	GUIStyle minusStyle;
	GUIStyle boldFoldout;

	string lastCategory;
	string pathBase;
	List<Action> delayedRemovals = new List<Action>();
	
	[NonSerialized] BuildProfile[] defaultsProfiles;
	[NonSerialized] string[] defaultsProfilesNames;
	
	// -------- GUI --------

	void InitializeGUI()
	{
		if (plusStyle == null) {
			plusStyle = new GUIStyle("OL Plus");
			plusStyle.fixedWidth = 18;
			plusStyle.stretchWidth = false;
			plusStyle.margin.top = 3;
		}

		if (minusStyle == null) {
			minusStyle = new GUIStyle("OL Minus");
			minusStyle.fixedWidth = 18;
			minusStyle.stretchWidth = false;
			minusStyle.margin.top = 3;
		}

		if (boldFoldout == null) {
			boldFoldout = new GUIStyle(EditorStyles.foldout);
			boldFoldout.font = EditorStyles.boldFont;
		}
	}

	void DefaultsGUI()
	{
		if (editorProfile != null) {
			EditorGUILayout.BeginHorizontal();
			{
				if (defaultsProfiles == null) {
					defaultsProfiles = BuildProfile.AllBuildProfiles.Prepend(null).ToArray();
					defaultsProfilesNames = defaultsProfiles.Select(p => p == null ? "Editor" : p.name).ToArray();
				}

				var defaultsProfile = BuildManager.EditorDefaultsProfile;

				int selected = Array.IndexOf(defaultsProfiles, defaultsProfile);
				var newSelected = EditorGUILayout.Popup("Defaults", selected, defaultsProfilesNames);

				if (selected != newSelected) {
					BuildProfile newProfile = null;
					if (newSelected > 0) {
						newProfile = defaultsProfiles[newSelected];
					}
					BuildManager.EditorDefaultsProfile = newProfile;
				}

				GUI.enabled = (defaultsProfile != null);
				if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(40))) {
					Selection.activeObject = defaultsProfile;
					EditorApplication.ExecuteMenuItem("Window/Inspector");
				}
				GUI.enabled = true;
			}
			EditorGUILayout.EndHorizontal();
		}
	}

	void OptionsGUI()
	{
		GUI.enabled = (buildProfile != null || BuildManager.EditorDefaultsProfile == null);

		// Options header
		EditorGUILayout.BeginHorizontal();
		{
			EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
			if (buildProfile != null) {
				EditorGUILayout.LabelField("Include in Builds", EditorStyles.boldLabel, GUILayout.Width(100));
			}
		}
		EditorGUILayout.EndHorizontal();

		// Option list
		lastCategory = null;
		if (editorProfile != null) {
			pathBase = "EditorProfile/";
		} else {
			pathBase = BuildManager.GetAssetGUID(buildProfile) + "/";
		}

		Recursion.Recurse(profile, profile.GetRecursionType(), options, OptionGUI);

		if (Event.current.type != EventType.Layout) {
			foreach (var action in delayedRemovals) {
				action();
			}
			delayedRemovals.Clear();
		}

		if (buildProfile != null) {
			EditorGUILayout.Space();

			// Re-set compilation defines for active profile
			if (profile == BuildManager.ActiveProfile
					&& !buildProfile.ScriptingDefineSymbolsUpToDate()) {
				EditorGUILayout.Space();

				EditorGUILayout.BeginHorizontal();
				{
					if (GUILayout.Button("Update", GUILayout.Height(39))) {
						buildProfile.ApplyScriptingDefineSymbols();
					}
					EditorGUILayout.HelpBox("Scripting define symbols need to be updated.\n"
					+ "Updating them will trigger a recompile.", MessageType.Warning);
				}
				EditorGUILayout.EndHorizontal();
			}
		}

		GUI.enabled = true;
	}

	protected void BuildGUI()
	{
		EditorGUILayout.LabelField("Build This Profile", EditorStyles.boldLabel);

		EditorGUILayout.BeginHorizontal();
		{
			buildTarget = (BuildTarget)EditorGUILayout.EnumPopup(buildTarget);
			if (GUILayout.Button("Build", EditorStyles.miniButton)) {
				buildProfile.Build(buildTarget);
				GUIUtility.ExitGUI();
			}
		}
		EditorGUILayout.EndHorizontal();
	}

	protected void ActiveProfileGUI()
	{
		EditorGUILayout.LabelField("Profile Used In Regular Builds", EditorStyles.boldLabel);

		BuildManager.ActiveProfile = (BuildProfile)EditorGUILayout.ObjectField(
			"Active Profile",
			BuildManager.ActiveProfile,
			typeof(BuildProfile),
			false
		);

		if (buildProfile != null 
				&& BuildManager.ActiveProfile != buildProfile 
				&& GUILayout.Button("Activate This Profile", EditorStyles.miniButton)) {
			BuildManager.ActiveProfile = buildProfile;
		}
	}

	protected bool OptionGUI(Recursion.RecurseOptionsContext context)
	{
		var option = context.option;
		var displayName = OptionDisplayName(option.Name);
		var width = GUILayout.Width(EditorGUIUtility.labelWidth - 4);

		var lastDepth = EditorGUI.indentLevel;
		EditorGUI.indentLevel = context.depth;

		// Category headers
		if (context.IsRoot) {
			if (option.Category != lastCategory) {
				if (!string.IsNullOrEmpty(option.Category)) {
					EditorGUILayout.Space();
					EditorGUILayout.LabelField(option.Category, EditorStyles.boldLabel);
				}
				lastCategory = option.Category;
			}
		}

		// Option GUI
		var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
		{
			// Variant container
			if (context.variantType == Recursion.VariantType.VariantContainer) {
				EditorGUILayout.LabelField(displayName, width);
				if (GUILayout.Button(GUIContent.none, plusStyle)) {
					AddNewVariant(option, context.node);
				}
				GUILayout.FlexibleSpace();

			// Variant child
			} else if (
				context.variantType == Recursion.VariantType.DefaultVariant
				|| context.variantType == Recursion.VariantType.VariantChild
			) {
				var isDefault = (context.variantType == Recursion.VariantType.DefaultVariant);

				// Disable when editing the default variant
				EditorGUI.BeginDisabledGroup(isDefault);
				{
					if (context.type == Recursion.RecursionType.Nodes) {
						if (isDefault) {
							EditorGUILayout.TextField(option.VariantDefaultParameter, width);
						} else {
							GUI.SetNextControlName(option.Name);
							context.node.Name = EditorGUILayout.TextField(context.node.Name, width);
							// Prevent naming the node the same as the default parameter
							if (context.node.Name == option.VariantDefaultParameter
									&& GUI.GetNameOfFocusedControl() != option.Name) {
								context.node.Name = FindUniqueVariantName(option, context.parentNode);
							}
						}
					} else {
						// TODO: Rename in play mode?
						EditorGUI.BeginDisabledGroup(true);
						{
							EditorGUILayout.TextField(option.VariantParameter, width);
						}
						EditorGUI.EndDisabledGroup();
					}
				}
				EditorGUI.EndDisabledGroup();

				var level = EditorGUI.indentLevel;
				EditorGUI.indentLevel = 0;
				profile.EditOption(context.path, GUIContent.none, option, context.node);
				EditorGUI.indentLevel = level;

				if (!isDefault) {
					if (GUILayout.Button(GUIContent.none, minusStyle)) {
						if (context.type == Recursion.RecursionType.Nodes) {
							delayedRemovals.Add(() => {
								context.parentNode.RemoveVariant(context.node.Name);
							});
						} else {
							delayedRemovals.Add(() => {
								context.parentOption.RemoveVariant(option);
							});
						}
					}
				}

			// Regular option
			} else {
				tempContent.text = displayName;
				profile.EditOption(context.path, tempContent, option, context.node);
			}

			// Include in build toggle
			if (
				buildProfile != null
				&& context.type == Recursion.RecursionType.Nodes
				&& context.IsRoot
			) {
				var root = (ValueStore.RootNode)context.node;
				EditorGUILayout.BeginHorizontal(GUILayout.Width(buildColumnWidth));
				{
					if (!option.BuildOnly) {
						root.IncludeInBuild = EditorGUILayout.Toggle(root.IncludeInBuild, GUILayout.Width(toggleWidth));
					} else {
						EditorGUILayout.Space();
					}
				}
				EditorGUILayout.EndHorizontal();
			} else {
				GUILayout.Space(buildColumnWidth + 4);
			}
		}
		EditorGUILayout.EndHorizontal();

		// Expansion toggle
		var isExpanded = true;
		if (context.IsRecursable) {
			var expansionPath = pathBase + context.path;
			var wasExpanded = EditorProfile.SharedInstance.IsExpanded(expansionPath);
			isExpanded = wasExpanded;

			rect.y += EditorStyles.foldout.padding.top;
			isExpanded = EditorGUI.Foldout(rect, isExpanded, GUIContent.none, true);

			if (wasExpanded != isExpanded) {
				EditorProfile.SharedInstance.SetExpanded(expansionPath, isExpanded);
			}
		}

		EditorGUI.indentLevel = lastDepth;

		return isExpanded;
	}

	void AddNewVariant(IOption option, ValueStore.Node node)
	{
		if (!option.IsVariant)
			throw new Exception("Option is not variant.");

		var parameter = FindUniqueVariantName(option, node);

		if (node != null) {
			node.AddVariant(parameter, option.DefaultValue ?? string.Empty);
		} else {
			option.AddVariant(parameter);
		}
	}

	string FindUniqueVariantName(IOption option, ValueStore.Node node)
	{
		var parameter = option.VariantDefaultParameter;

		int i = 1;
		do {
			parameter = option.VariantDefaultParameter + (i++).ToString();
		} while (
			(node == null && option.GetVariant(parameter) != null)
			|| (node != null && node.GetVariant(parameter) != null)
		);

		return parameter;
	}
}

}

