using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using sttz.Workbench.Extensions;

namespace sttz.Workbench.Editor
{

/// <summary>
/// Editor GUI for <see cref="BuildProfile"/> and <see cref="EditorProfile"/>.
/// </summary>
[CustomEditor(typeof(EditableProfile), true)]
public class ProfileEditor : UnityEditor.Editor
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
	const float buildColumnWidth = 21;

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

	// ------ Menu ------

	[MenuItem("CONTEXT/BuildProfile/Toggle Show Unavailable")]
	static void ToggleShowUnavailable(MenuCommand cmd)
	{
		WorkbenchPrefs.ShowUnavailableOptions = !WorkbenchPrefs.ShowUnavailableOptions;
	}

	// -------- Editor --------

	private EditableProfile profile;
	private EditorProfile editorProfile;
	private BuildProfile buildProfile;

	public override void OnInspectorGUI()
	{
		// Editing a not-saved scriptable object starts the editor
		// with GUI.enabled == false (seen in Unity 4.5.2)
		GUI.enabled = true;

		InitializeGUI();

		SourceProfileGUI();

		EditorGUILayout.Space();

		OptionsGUI();

		GUILayout.FlexibleSpace();

		if (buildProfile != null) {
			BuildGUI();
		}
	}

	public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
	{
		return null;
	}

	protected void OnEnable()
	{
		profile = (EditableProfile)target;
		editorProfile = target as EditorProfile;
		buildProfile = target as BuildProfile;

		options = Recursion.SortOptionsByCategoryAndName(profile.GetAllOptions());

		// Invalidate source profile dropdown
		sourceProfiles = null;
		sourceProfilesNames = null;
	}

	protected void OnDisable()
	{
		profile.SaveIfNeeded();
	}

	// -------- Fields --------

	List<IOption> options;

	GUIStyle categoryBackground;
	GUIStyle categoryFoldout;
	GUIStyle includeBackground;
	GUIStyle separator;

	GUIStyle plusStyle;
	GUIStyle minusStyle;
	GUIStyle greyFoldout;
	GUIStyle boldLabel;

	string lastCategory;
	bool categoryExpanded;
	bool buildCategory;
	string pathBase;
	List<Action> delayedRemovals = new List<Action>();
	
	[NonSerialized] BuildProfile[] sourceProfiles;
	[NonSerialized] string[] sourceProfilesNames;

	// -------- GUI --------

	Texture2D CreateColorTexture(Color color)
	{
		var tex = new Texture2D(1, 1);
		tex.SetPixel(0, 0, color);
		tex.Apply(false, true);
		return tex;
	}

	void InitializeGUI()
	{
		if (categoryBackground == null) {
			categoryBackground = new GUIStyle();
			categoryBackground.normal.background = CreateColorTexture(Color.white * 0.6f);
			categoryBackground.overflow = new RectOffset(20, 20, -3, -3);
			categoryBackground.margin = categoryBackground.padding = new RectOffset();
		}

		if (categoryFoldout == null) {
			categoryFoldout = new GUIStyle(EditorStyles.foldout);
			categoryFoldout.font = EditorStyles.boldFont;
			categoryFoldout.alignment = TextAnchor.MiddleLeft;
			categoryFoldout.margin = new RectOffset(0, 0, 5, 5);
		}

		if (includeBackground == null) {
			includeBackground = new GUIStyle();
			includeBackground.normal.background = CreateColorTexture(Color.white * 0.7f);
			//includeBackground.overflow = new RectOffset(20, 20, -3, -3);
			//includeBackground.margin = includeBackground.padding = new RectOffset();
		}

		if (separator == null) {
			separator = new GUIStyle();
			separator.fixedHeight = 1;
			separator.normal.background = CreateColorTexture(Color.white * 0.8f);
		}


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

	void SourceProfileGUI()
	{
		if (editorProfile != null) {
			EditorGUILayout.BeginHorizontal();
			{
				if (sourceProfiles == null) {
					sourceProfiles = BuildProfile.AllBuildProfiles.Prepend(null).ToArray();
					sourceProfilesNames = sourceProfiles.Select(p => p == null ? "Editor" : p.name).ToArray();
				}

				var sourceProfile = BuildManager.EditorSourceProfile;

				int selected = Array.IndexOf(sourceProfiles, sourceProfile);
				var newSelected = EditorGUILayout.Popup("Source Profile", selected, sourceProfilesNames);

				if (selected != newSelected) {
					BuildProfile newProfile = null;
					if (newSelected > 0) {
						newProfile = sourceProfiles[newSelected];
					}
					BuildManager.EditorSourceProfile = newProfile;
				}

				GUI.enabled = (sourceProfile != null);
				if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(40))) {
					Selection.activeObject = sourceProfile;
					EditorApplication.ExecuteMenuItem("Window/Inspector");
				}
				GUI.enabled = true;
			}
			EditorGUILayout.EndHorizontal();
		}
	}

	void OptionsGUI()
	{
		GUI.enabled = (buildProfile != null || BuildManager.EditorSourceProfile == null);

		// Include column header
		if (buildProfile != null) {
			EditorGUILayout.BeginHorizontal();
			{
				if (BuildManager.ActiveProfile == buildProfile) {
					EditorGUILayout.LabelField("Active Profile", EditorStyles.boldLabel);
				}
				GUILayout.FlexibleSpace();
				EditorGUILayout.LabelField("Include in Builds", EditorStyles.boldLabel, GUILayout.Width(100));
			}
			EditorGUILayout.EndHorizontal();
		}

		ResurseOptionsGUI();

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
		ResurseOptionsGUI(showBuild:true);

		if (EditorProfile.SharedInstance.IsExpanded(pathBase + "_Build")) {
			return;
		}

		EditorGUILayout.Space();

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

	protected void ResurseOptionsGUI(bool showBuild = false)
	{
		if (editorProfile != null) {
			pathBase = "EditorProfile/";
		} else {
			pathBase = BuildManager.GetAssetGUID(buildProfile) + "/";
		}

		lastCategory = null;
		categoryExpanded = true;
		buildCategory = showBuild;
		Recursion.Recurse(profile, profile.GetRecursionType(), options, OptionGUI);
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
		if (!WorkbenchPrefs.ShowUnavailableOptions && !optionEnabled) {
			return false;
		}

		var expansionPath = pathBase + context.path;
		var isExpanded = true;
		var wasExpanded = true;
		if (context.IsRecursable) {
			isExpanded = wasExpanded = EditorProfile.SharedInstance.IsExpanded(expansionPath);
		}

		// Category headers
		if (context.IsRoot) {
			var category = option.Category;
			
			var isBuildCategory = (!string.IsNullOrEmpty(category) && category.EqualsIgnoringCase("build"));
			if (isBuildCategory != buildCategory) {
				return false;
			}

			if (category != lastCategory) {
				EditorGUILayout.BeginHorizontal(categoryBackground);
				{
					var path = pathBase + "_" + category;
					categoryExpanded = Foldout(path, true, category, categoryFoldout);
				}
				EditorGUILayout.EndHorizontal();
				lastCategory = category;
			} else if (categoryExpanded) {
				GUILayout.Label(GUIContent.none, separator);
			}

			if (!categoryExpanded) {
				return false;
			}
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

				if (!option.IsArrayVariant) {
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
				} else {
					EditorGUILayout.PrefixLabel(" ");
				}

				var level = EditorGUI.indentLevel;
				EditorGUI.indentLevel = 0;
				profile.EditOption(context.path, GUIContent.none, option, context.node);
				EditorGUI.indentLevel = level;

				if (!isDefault) {
					if (GUILayout.Button(GUIContent.none, minusStyle)) {
						if (context.type == Recursion.RecursionType.Nodes) {
							delayedRemovals.Add(() => {
								context.parentNode.RemoveVariant(context.node.Name);
								if (option.IsArrayVariant) {
									context.parentNode.NumberVariantsSequentially();
								}
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
			if (buildProfile != null) {
				EditorGUILayout.BeginHorizontal(includeBackground, GUILayout.Width(buildColumnWidth), GUILayout.ExpandHeight(true));
				{
					if (
						context.type == Recursion.RecursionType.Nodes
						&& context.IsRoot
						&& !option.BuildOnly && !option.EditorOnly
					) {
						var root = (ValueStore.RootNode)context.node;
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
				// Not including a layout group here somehow makes the parent group taller
				EditorGUILayout.BeginHorizontal(GUILayout.Width(0));
				EditorGUILayout.EndHorizontal();
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
			if (option.IsArrayVariant) {
				node.NumberVariantsSequentially();
			}
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

