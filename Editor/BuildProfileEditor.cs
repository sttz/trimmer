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
[CustomEditor(typeof(EditorProfile), true)]
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

	private EditorProfile profile;
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
		profile = (EditorProfile)target;
		buildProfile = target as BuildProfile;

		buildTarget = EditorUserBuildSettings.activeBuildTarget;

		var allOptions = EditorProfile.AllOptions;
		if (buildProfile == null) {
			allOptions = allOptions.Where(o => !o.BuildOnly);
		}

		options = new List<IOption>(allOptions);
		// TODO: Option sorting
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
		if (buildProfile == null) {
			EditorGUILayout.BeginHorizontal();
			{
				// TODO: Invalidate
				if (defaultsProfiles == null) {
					defaultsProfiles = BuildProfile.AllBuildProfiles.Prepend(null).ToArray();
					defaultsProfilesNames = defaultsProfiles.Select(p => p == null ? "Editor" : p.ProfileName).ToArray();
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
			var root = profile.store.GetOrCreateRoot(option.Name);
			var category = root != null ? root.Category : option.Category;
			if (category != lastCategory) {
				if (!string.IsNullOrEmpty(category)) {
					EditorGUILayout.Space();
					EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
				}
				lastCategory = category;
			}
			// TODO: Categories GUI
			ShowOption(option, root);
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

	protected void ShowOption(IOption option, ValueStore.Node node, ValueStore.Node parentNode = null, bool isSubVariant = false)
	{
		var displayName = OptionDisplayName(option.Name);

		var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
		{
			if (option.IsVariant && !isSubVariant) {
				EditorGUILayout.LabelField(displayName, GUILayout.Width(EditorGUIUtility.labelWidth - 4));
				if (GUILayout.Button(GUIContent.none, plusStyle)) {
					AddNewVariant(option, node);
				}
				GUILayout.FlexibleSpace();

			} else if (option.IsVariant) {
				// TODO: Rename in play mode?
				node.Name = EditorGUILayout.TextField(node.Name, GUILayout.Width(EditorGUIUtility.labelWidth - 4));

				var level = EditorGUI.indentLevel;
				EditorGUI.indentLevel = 0;
				profile.EditOption(GUIContent.none, option, node);
				EditorGUI.indentLevel = level;

				if (GUILayout.Button(GUIContent.none, minusStyle)) {
					// TODO: Delayed remove
					parentNode.RemoveVariant(node.Name);
				}

			} else {
				tempContent.text = displayName;
				profile.EditOption(tempContent, option, node);
			}

			if (buildProfile != null && node is ValueStore.RootNode) {
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

		if ((option.IsVariant && !isSubVariant) || option.HasChildren) {
			rect.y += EditorStyles.foldout.padding.top;
			node.IsExpanded = EditorGUI.Foldout(rect, node.IsExpanded, GUIContent.none, true);
		}

		if (node.IsExpanded) {
			if (option.IsVariant && node.Variants != null) {
				EditorGUI.indentLevel++;
				foreach (var variantNode in node.Variants) {
					ShowOption(option, variantNode, node, true);
				}
				EditorGUI.indentLevel--;
			}

			if (option.HasChildren && (!option.IsVariant || isSubVariant)) {
				EditorGUI.indentLevel++;
				foreach (var childOption in option.Children) {
					var childNode = node.GetOrCreateChild(childOption.Name);
					ShowOption(childOption, childNode, node);
				}
				EditorGUI.indentLevel--;
			}
		}
	}

	void AddNewVariant(IOption option, ValueStore.Node node)
	{
		if (!option.IsVariant)
			throw new Exception("Option is not variant.");

		var name = option.VariantDefaultParameter;
		int i = 0;
		do {
			name = option.VariantDefaultParameter + (i++ == 0 ? "" : i.ToString());
		} while (node.GetVariant(name) != null);

		node.AddVariant(name, option.DefaultValue ?? string.Empty);
	}
}

}

