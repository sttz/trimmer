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

		/*if (recordingActivationSequence) {
			RecordActivationSequence();
		}*/

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

	List<Action> delayedRemovals = new List<Action>();

	/*bool recordingActivationSequence;
	KeyCode[] newSequence;
	KeyCode lastRecordedCode;
	Rect recordingButtonRect;*/
	
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
				// TODO: Invalidate
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
		string lastCategory = null;
		foreach (var option in options) {
			if (option.Category != lastCategory) {
				if (!string.IsNullOrEmpty(option.Category)) {
					EditorGUILayout.Space();
					EditorGUILayout.LabelField(option.Category, EditorStyles.boldLabel);
				}
				lastCategory = option.Category;
			}

			ShowOption(option, profile.GetStoreRoot(option));
		}

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

	// TODO: Convert to regular options
	/*protected void RuntimeOptionsGUI()
	{
		var config = profile.panelConfig;
		GUI.changed = false;

		var prop = serializedObject.FindProperty("panelConfig");
		prop.isExpanded = Foldout(prop.isExpanded, "Runtime Options", boldFoldout);

		if (!prop.isExpanded)
			return;

		EditorGUI.indentLevel++;

		EditorGUILayout.BeginHorizontal();
		{
			EditorGUILayout.LabelField("Activation Sequence", GUILayout.Width(EditorGUIUtility.labelWidth));

			var sequence = (recordingActivationSequence ? newSequence : config.activationSequence);

			int i = 0;
			foreach (var code in sequence) {
				if (i++ > 0) {
					TightLabel("-");
				}
				TightLabel(code.ToString("G"));
			}

			if (recordingActivationSequence) {
				if (i++ > 0) {
					TightLabel("-");
				}
				TightLabel("...");
			}

			GUILayout.FlexibleSpace();

			var label = recordingActivationSequence ? "Finish" : "Record";
			if (GUILayout.Button(label, EditorStyles.miniButton)) {
				recordingActivationSequence = !recordingActivationSequence;
				if (recordingActivationSequence) {
					lastRecordedCode = KeyCode.None;
					newSequence = new KeyCode[0];
				} else {
					if (newSequence != null && newSequence.Length > 0) {
						config.activationSequence = newSequence;
					}
					newSequence = null;
				}
			}

			var rect = GUILayoutUtility.GetLastRect();
			if (rect.width > 1 && rect.height > 1) {
				recordingButtonRect = rect;
			}
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		{
			EditorGUILayout.LabelField("Activation Modifiers", GUILayout.Width(EditorGUIUtility.labelWidth));

			config.activationShift = GUILayout.Toggle(config.activationShift, " Shift", GUILayout.ExpandWidth(false));
			config.activationAlt = GUILayout.Toggle(config.activationAlt, " Alt", GUILayout.ExpandWidth(false));
			config.activationCtrlCmd = GUILayout.Toggle(config.activationCtrlCmd, " Ctrl / Cmd", GUILayout.ExpandWidth(false));
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		config.prompt = EditorGUILayout.TextField("Prompt", config.prompt);
		config.promptFontSize = EditorGUILayout.IntField("Prompt Font Size", config.promptFontSize);
		config.promptPadding = EditorGUILayout.FloatField("Prompt Padding", config.promptPadding);
		config.promptPosition = (MaintenancePanel.PrompPosition)EditorGUILayout.EnumPopup("Prompt Position", config.promptPosition);

		EditorGUILayout.Space();

		config.iniFileName = EditorGUILayout.TextField("Ini File Name", config.iniFileName);

		int removePath = -1;
		EditorGUILayout.BeginHorizontal();
		{
			EditorGUILayout.PrefixLabel("Ini File Paths");

			EditorGUILayout.BeginVertical();
			{
				for (int i = 0; i < config.iniFilePaths.Length; i++) {
					EditorGUILayout.BeginHorizontal();
					{
						config.iniFilePaths[i] = GUILayout.TextField(config.iniFilePaths[i]);

						if (GUILayout.Button(GUIContent.none, minusStyle)) {
							removePath = i;
						}
					}
					EditorGUILayout.EndHorizontal();
				}

				var newPath = GUILayout.TextField("...");
				if (newPath != "...") {
					config.iniFilePaths = config.iniFilePaths.Append("").ToArray();
				}
			}
			EditorGUILayout.EndVertical();
		}
		EditorGUILayout.EndHorizontal();

		if (GUI.changed) {
			if (removePath >= 0) {
				config.iniFilePaths = config.iniFilePaths.Where((path, i) => i != removePath).ToArray();
				EditorGUIUtility.keyboardControl = -1;
			}

			if (buildProfile != null) {
				Undo.RecordObject(profile, "Edit Runtime Options");
			}

			profile.panelConfig = config;

			if (!profile.StoreConfigInDefaults) {
				EditorUtility.SetDirty(profile);
			} else {
				profile.SaveConfigToDefaults();
				profile.SaveDefaults();
			}
		}

		EditorGUI.indentLevel--;
	}

	void RecordActivationSequence()
	{
		if (recordingActivationSequence) {
			if (!recordingButtonRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown) {
				recordingActivationSequence = false;
				newSequence = null;
				Event.current.Use();

			} else if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp) {
				if (Event.current.keyCode != KeyCode.None) {
					if (Event.current.keyCode != lastRecordedCode) {
						newSequence = newSequence.Append(Event.current.keyCode).ToArray();
					}
					lastRecordedCode = Event.current.keyCode;
				}
				if (Event.current.type == EventType.KeyUp) {
					lastRecordedCode = KeyCode.None;
				}
				Event.current.Use();
			}
		}
	}
	*/

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

	protected void ShowOption(IOption option, ValueStore.Node node, ValueStore.Node parentNode = null, bool variantChild = false, IOption parentVariant = null)
	{
		var displayName = OptionDisplayName(option.Name);
		var width = GUILayout.Width(EditorGUIUtility.labelWidth - 4);

		var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
		{
			if (option.IsVariant && !variantChild) {
				EditorGUILayout.LabelField(displayName, width);
				if (GUILayout.Button(GUIContent.none, plusStyle)) {
					AddNewVariant(option, node);
				}
				GUILayout.FlexibleSpace();

			} else if (option.IsVariant) {
				// Disable when editing the default variant
				// TODO: Clean this shit up
				var defaultVariant = (
					(node == null && parentVariant == null)
					|| (node != null && node == parentNode)
				);
				EditorGUI.BeginDisabledGroup(defaultVariant);
				{
					if (node != null) {
						if (defaultVariant) {
							EditorGUILayout.TextField(option.VariantDefaultParameter, width);
						} else {
							GUI.SetNextControlName(option.Name);
							node.Name = EditorGUILayout.TextField(node.Name, width);
							// Prevent naming the node the same as the default parameter
							if (node.Name == option.VariantDefaultParameter
									&& GUI.GetNameOfFocusedControl() != option.Name) {
								node.Name = FindUniqueVariantName(option, parentNode);
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
				profile.EditOption(GUIContent.none, option, node);
				EditorGUI.indentLevel = level;

				if (!defaultVariant) {
					if (GUILayout.Button(GUIContent.none, minusStyle)) {
						if (node != null) {
							delayedRemovals.Add(() => {
								parentNode.RemoveVariant(node.Name);
							});
						} else {
							delayedRemovals.Add(() => {
								parentVariant.RemoveVariant(option);
							});
						}
					}
				}

			} else {
				tempContent.text = displayName;
				profile.EditOption(tempContent, option, node);
			}

			if (buildProfile != null && node != null && node != parentNode && node is ValueStore.RootNode) {
				var root = (ValueStore.RootNode)node;
				EditorGUILayout.BeginHorizontal(GUILayout.Width(buildColumnWidth));
				{
					if (option == null || !option.BuildOnly) {
						root.IncludeInBuild = EditorGUILayout.Toggle(root.IncludeInBuild, GUILayout.Width(toggleWidth));
					} else
						EditorGUILayout.Space();
				}
				EditorGUILayout.EndHorizontal();
			} else {
				GUILayout.Space(buildColumnWidth + 4);
			}
		}
		EditorGUILayout.EndHorizontal();

		// TODO: Better place to save expanded state?
		var isExpanded = (node != null ? node.IsExpanded : option.IsExpanded);

		if ((option.IsVariant && !variantChild) || option.HasChildren) {
			rect.y += EditorStyles.foldout.padding.top;
			isExpanded = EditorGUI.Foldout(rect, isExpanded, GUIContent.none, true);
		}

		if (isExpanded) {
			if (option.IsVariant && !variantChild) {
				EditorGUI.indentLevel++;
				if (node == null) {
					ShowOption(option, null, null, true, null);
					foreach (var variant in option.Variants) {
						ShowOption(variant, null, null, true, option);
					}
				} else if (node.Variants != null) {
					ShowOption(option, node, node, true);
					foreach (var variantNode in node.Variants) {
						// We pass in option twice here to signal the node
						// is a variant child but that's not really nice...
						ShowOption(option, variantNode, node, true);
					}
				}
				EditorGUI.indentLevel--;
			}

			if (option.HasChildren && (!option.IsVariant || variantChild)) {
				EditorGUI.indentLevel++;
				foreach (var childOption in option.Children) {
					ValueStore.Node childNode = null;
					if (node != null) {
						childNode = node.GetOrCreateChild(childOption.Name);
					}
					ShowOption(childOption, childNode, node);
				}
				EditorGUI.indentLevel--;
			}
		}

		if (node != null) {
			node.IsExpanded = isExpanded;
		} else {
			option.IsExpanded = isExpanded;
		}
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

