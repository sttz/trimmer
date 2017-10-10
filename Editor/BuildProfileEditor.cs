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

	/// <summary>
	/// Same as <see cref="Foldout"/> but stores the expanded state in the
	/// <see cref="EditorProfile"/> based on the given path.
	/// </summary>
	public static bool Foldout(string path, bool def, string content, GUIStyle style = null)
	{
		EditorGUI.BeginChangeCheck();
		
		var wasExpanded = EditorProfile.SharedInstance.IsExpanded(path);
		if (def) wasExpanded = !wasExpanded;
		var isExpanded = wasExpanded;

		isExpanded = Foldout(isExpanded, content, style);
		
		if (isExpanded != wasExpanded) {
			var newValue = isExpanded;
			if (def) newValue = !newValue;
			EditorProfile.SharedInstance.SetExpanded(path, newValue);
		}

		return isExpanded;
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

		options = Recursion.SortOptionsByCategoryAndName(profile.GetAllOptions());

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

	GUIStyle plusStyle;
	GUIStyle minusStyle;
	GUIStyle boldFoldout;
	GUIStyle greyLabel;
	GUIStyle greyFoldout;
	GUIStyle boldLabel;

	string lastCategory;
	bool categoryExpanded;
	bool hasUnavailable;
	bool recurseUnavailable;
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
			boldFoldout.fontSize = 13;
			boldFoldout.alignment = TextAnchor.MiddleLeft;
		}

		if (greyLabel == null) {
			greyLabel = new GUIStyle(EditorStyles.label);
			greyLabel.normal.textColor = EditorStyles.centeredGreyMiniLabel.normal.textColor;
		}

		if (boldLabel == null) {
			boldLabel = new GUIStyle(EditorStyles.label);
			boldLabel.font = EditorStyles.boldFont;
			boldLabel.alignment = TextAnchor.MiddleLeft;
		}

		if (greyFoldout == null) {
			greyFoldout = new GUIStyle(EditorStyles.foldout);
			greyFoldout.font = EditorStyles.boldFont;
			greyFoldout.normal.textColor = EditorStyles.centeredGreyMiniLabel.normal.textColor;
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

		categoryExpanded = true;
		hasUnavailable = false;
		recurseUnavailable = false;
		Recursion.Recurse(profile, profile.GetRecursionType(), options, OptionGUI);

		if (hasUnavailable) {
			EditorGUILayout.Space();
			var isExpanded = Foldout(pathBase + "/_Unavailable", false, "Unavailable", greyFoldout);

			if (isExpanded) {
				categoryExpanded = true;
				recurseUnavailable = true;
				Recursion.Recurse(profile, profile.GetRecursionType(), options, OptionGUI);
			}
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

		if (Event.current.type != EventType.Layout) {
			foreach (var action in delayedRemovals) {
				action();
			}
			delayedRemovals.Clear();
		}

		GUI.enabled = true;
	}

	protected void BuildGUI()
	{
		EditorGUILayout.BeginHorizontal();
		{
			GUILayout.Label("Build Targets", boldLabel);
			if (GUILayout.Button(GUIContent.none, plusStyle)) {
				var menu = new GenericMenu();
				var type = typeof(BuildTarget);
				var obsoleteType = typeof(ObsoleteAttribute);
				foreach (BuildTarget target in Enum.GetValues(type)) {
					var isObsolete = type.GetMember(target.ToString()).First().GetCustomAttributes(obsoleteType, true).Length > 0;
					if (isObsolete || (int)target < 0 || buildProfile.BuildTargets.Contains(target))
						continue;
					menu.AddItem(new GUIContent(target.ToString()), false, AddBuildTarget, target);
				}
				menu.ShowAsContext();
			}
			GUILayout.FlexibleSpace();
		}
		EditorGUILayout.EndHorizontal();

		var usesActive = buildProfile.UsesActiveBuildTarget();
		foreach (var target in buildProfile.BuildTargets) {
			EditorGUILayout.BeginHorizontal();
			EditorGUI.BeginDisabledGroup(usesActive);
			{
				if (GUILayout.Button(GUIContent.none, minusStyle)) {
					delayedRemovals.Add(() => {
						buildProfile.RemoveBuildTarget(target);
					});
				}
				EditorGUILayout.LabelField(target.ToString());
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.Space();

		var count = buildProfile.BuildTargets.Count();
		if (GUILayout.Button("Build " + count + " Target" + (count > 1 ? "s" : ""), EditorStyles.miniButton)) {
			BuildManager.Build(buildProfile);
			GUIUtility.ExitGUI();
		}
	}

	protected void AddBuildTarget(object userData)
	{
		var target = (BuildTarget)userData;
		buildProfile.AddBuildTarget(target);
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

		var optionEnabled = true;
		if (buildProfile != null) {
			optionEnabled = context.option.IsAvailable(buildProfile.BuildTargets);
		}
		if (optionEnabled == recurseUnavailable) {
			hasUnavailable = true;
			return false;
		}

		var expansionPath = pathBase + context.path;
		var isExpanded = true;
		var wasExpanded = true;
		if (context.IsRecursable) {
			isExpanded = wasExpanded = EditorProfile.SharedInstance.IsExpanded(expansionPath);
		}

		// Category headers
		if (context.IsRoot && !recurseUnavailable) {
			if (option.Category != lastCategory) {
				if (!string.IsNullOrEmpty(option.Category)) {
					EditorGUILayout.Space();
					var path = context.path + "/_" + option.Category;
					categoryExpanded = Foldout(path, true, option.Category, boldFoldout);

					lastCategory = option.Category;
				}
			}
			if (!categoryExpanded) return false;
		}

		// Option GUI
		var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
		{
			// Variant container
			if (context.variantType == Recursion.VariantType.VariantContainer) {
				EditorGUILayout.LabelField(displayName, width);
				if (isExpanded && GUILayout.Button(GUIContent.none, plusStyle)) {
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
							EditorGUILayout.DelayedTextField(option.VariantDefaultParameter, width);
						} else {
							var newParam = EditorGUILayout.DelayedTextField(context.node.Name, width);
							if (newParam != context.node.Name) {
								// Prevent naming the node the same as the default parameter
								if (context.node.Name == option.VariantDefaultParameter
										|| context.parentNode.GetVariant(newParam) != null) {
									context.node.Name = FindUniqueVariantName(option, context.parentNode, newParam);
								} else {
									context.node.Name = newParam;
								}
							}
						}
					} else {
						// TODO: Rename in play mode?
						EditorGUI.BeginDisabledGroup(true);
						{
							EditorGUILayout.DelayedTextField(option.VariantParameter, width);
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
					if (!option.BuildOnly && !option.EditorOnly) {
						var value = root.IncludeInBuild;
						if (!optionEnabled) {
							value = false;
							GUI.enabled = false;
						}
						root.IncludeInBuild = EditorGUILayout.Toggle(value, GUILayout.Width(toggleWidth));
						GUI.enabled = true;
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
		if (context.IsRecursable) {
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

	static Regex RemoveTrailingNumbersRegex = new Regex(@"^(.*?)\d*$");

	string FindUniqueVariantName(IOption option, ValueStore.Node node, string baseParam = null)
	{
		if (baseParam == null) {
			baseParam = option.VariantDefaultParameter;
		} else {
			var match = RemoveTrailingNumbersRegex.Match(baseParam);
			baseParam = match.Groups[1].Value;
		}

		int i = 1;
		string parameter;
		do {
			parameter = baseParam + (i++).ToString();
		} while (
			(node == null && option.GetVariant(parameter) != null)
			|| (node != null && node.GetVariant(parameter) != null)
		);

		return parameter;
	}
}

}

