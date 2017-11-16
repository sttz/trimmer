using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using sttz.Trimmer.Extensions;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Editor GUI for <see cref="BuildProfile"/> and <see cref="EditorProfile"/>.
/// </summary>
[CustomEditor(typeof(EditableProfile), true)]
public class ProfileEditor : UnityEditor.Editor
{
	// -------- Constants --------

	/// <summary>
	/// Extra space between lines.
	/// </summary>
	const float linePadding = 4;
	/// <summary>
	/// Width of columns with toggles on the right-hand side of the profile editor.
	/// </summary>
	const float buildColumnWidth = 21;
	/// <summary>
	/// Alpha value applied to unavailable options.
	/// </summary>
	const float unavailableAlpha = 0.5f;

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
		TrimmerPrefs.ShowUnavailableOptions = !TrimmerPrefs.ShowUnavailableOptions;
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

		if (Event.current.type != EventType.Layout) {
			foreach (var action in delayedRemovals) {
				action();
			}
			delayedRemovals.Clear();
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
		if (profile != null) {
			profile.SaveIfNeeded();
		}
	}

	// ------ Styling ------

	static Color Grey(float amount)
	{
		return new Color(amount, amount, amount, 1f);
	}

	struct Styling
	{
		public Color categoryBackground;
		public Color includeBackground;
		public Color separator;
	}

	Styling personalStyling = new Styling() {
		categoryBackground = Grey(0.65f),
		includeBackground =  Grey(0.725f),
		separator = Grey(0.8f)
	};

	Styling professionalStyling = new Styling() {
		categoryBackground = Grey(0.32f),
		includeBackground =  Grey(0.2f),
		separator = Grey(0.175f)
	};

	GUIStyle categoryBackground;
	GUIStyle categoryFoldout;
	GUIStyle includeBackground;
	GUIStyle separator;
	GUIStyle inclusionLabel;

	GUIStyle plusStyle;
	GUIStyle minusStyle;
	GUIStyle greyFoldout;
	GUIStyle boldLabel;

	// -------- Fields --------

	List<Option> options;

	GUIContent inclusionO;
	GUIContent inclusionI;
	GUIContent inclusionII;

	string lastCategory;
	bool categoryExpanded;
	bool optionAvailable;
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
		var style = EditorGUIUtility.isProSkin ? professionalStyling : personalStyling;

		if (inclusionO == null) {
			inclusionO = new GUIContent("O");
			inclusionI = new GUIContent("I");
			inclusionII = new GUIContent("II");
		}

		if (categoryBackground == null) {
			categoryBackground = new GUIStyle();
			categoryBackground.normal.background = CreateColorTexture(style.categoryBackground);
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
			includeBackground.normal.background = CreateColorTexture(style.includeBackground);
			//includeBackground.overflow = new RectOffset(20, 20, -3, -3);
			//includeBackground.margin = includeBackground.padding = new RectOffset();
		}

		if (separator == null) {
			separator = new GUIStyle();
			separator.fixedHeight = 1;
			separator.normal.background = CreateColorTexture(style.separator);
		}

		if (inclusionLabel == null) {
			inclusionLabel = new GUIStyle(EditorStyles.label);
			inclusionLabel.fixedWidth = 13;
			inclusionLabel.alignment = TextAnchor.MiddleCenter;
		}


		if (plusStyle == null) {
			plusStyle = new GUIStyle("OL Plus");
			plusStyle.fixedWidth = 17;
			plusStyle.stretchWidth = false;
			plusStyle.margin.top = 3;
		}

		if (minusStyle == null) {
			minusStyle = new GUIStyle("OL Minus");
			minusStyle.fixedWidth = 17;
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

		ResurseOptionsGUI();

		GUI.enabled = true;
	}

	protected void BuildGUI()
	{
		ResurseOptionsGUI(showBuild:true);

		if (EditorProfile.SharedInstance.IsExpanded(pathBase + "_Build")) {
			return;
		}

		EditorGUILayout.Space();

		var usesActive = buildProfile.UsesActiveBuildTarget();
		EditorGUILayout.BeginHorizontal();
		{
			GUILayout.Label("Build Targets", boldLabel);
			if (GUILayout.Button(GUIContent.none, plusStyle)) {
				var menu = new GenericMenu();
				var type = typeof(BuildTarget);
				var obsoleteType = typeof(ObsoleteAttribute);
				foreach (var target in Enum.GetValues(type).Cast<BuildTarget>().OrderBy(b => b.ToString())) {
					var isObsolete = type.GetMember(target.ToString()).First().GetCustomAttributes(obsoleteType, true).Length > 0;
					if (isObsolete 
						|| (int)target < 0 
						|| (!usesActive && buildProfile.BuildTargets.Contains(target)))
						continue;
					menu.AddItem(new GUIContent(target.ToString()), false, AddBuildTarget, target);
				}
				menu.ShowAsContext();
			}
			GUILayout.FlexibleSpace();
		}
		EditorGUILayout.EndHorizontal();

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
		optionAvailable = true;
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

		if (buildProfile != null && context.IsRoot) {
			optionAvailable = context.option.IsAvailable(buildProfile.BuildTargets);
		}
		
		if (!TrimmerPrefs.ShowUnavailableOptions && !optionAvailable) {
			return false;
		}

		var color = GUI.color;
		color.a = optionAvailable ? 1 : unavailableAlpha;
		GUI.color = color;

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
		var lineHeight = EditorGUIUtility.singleLineHeight + linePadding;
		var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(lineHeight));
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

				if (option.Variance == OptionVariance.Dictionary) {
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
				profile.EditOption(context.path, option, context.node);
				EditorGUI.indentLevel = level;

				if (!isDefault) {
					if (GUILayout.Button(GUIContent.none, minusStyle)) {
						if (context.type == Recursion.RecursionType.Nodes) {
							delayedRemovals.Add(() => {
								context.parentNode.RemoveVariant(context.node.Name);
								if (option.Variance == OptionVariance.Array) {
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
				EditorGUILayout.PrefixLabel(tempContent);

				var level = EditorGUI.indentLevel;
				EditorGUI.indentLevel = 0;
				profile.EditOption(context.path, option, context.node);
				EditorGUI.indentLevel = level;
			}

			// Include in build toggle
			if (buildProfile != null) {
				color = GUI.color;
				color.a = 1;
				GUI.color = color;
				
				EditorGUILayout.BeginHorizontal(includeBackground, GUILayout.Width(buildColumnWidth), GUILayout.Height(lineHeight));
				{
					if (
						context.type == Recursion.RecursionType.Nodes
						&& context.IsRoot
						&& (
							(option.Capabilities & OptionCapabilities.CanIncludeOption) != 0
							|| (option.Capabilities & OptionCapabilities.HasAssociatedFeature) != 0
						)
					) {
						var root = (ValueStore.RootNode)context.node;
						var value = root.Inclusion;
						if (!optionAvailable) {
							value = OptionInclusion.Remove;
							GUI.enabled = false;
						}
						GUILayout.Label(LabelForInclusion(root, option.Capabilities), inclusionLabel);
						if (optionAvailable) {
							DoInclusionMenu(root, option.Capabilities);
						}
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
			rect.y += EditorStyles.foldout.padding.top + linePadding / 2;
			isExpanded = EditorGUI.Foldout(rect, isExpanded, GUIContent.none, true);

			if (wasExpanded != isExpanded) {
				EditorProfile.SharedInstance.SetExpanded(expansionPath, isExpanded);
			}
		}

		EditorGUI.indentLevel = lastDepth;

		return isExpanded;
	}

	GUIContent LabelForInclusion(ValueStore.RootNode root, OptionCapabilities capabilities)
	{
		var inclusion = root.Inclusion;
		var capFeature = (capabilities & OptionCapabilities.HasAssociatedFeature) != 0;
		var capOption = (capabilities & OptionCapabilities.CanIncludeOption) != 0;

		if (capFeature && capOption) {
			if (inclusion == OptionInclusion.Remove) {
				return inclusionO;
			} else if (inclusion == OptionInclusion.Feature) {
				return inclusionI;
			} else if (inclusion == OptionInclusion.FeatureAndOption) {
				return inclusionII;
			} else {
				// Fix invalid value
				root.Inclusion = OptionInclusion.Remove;
				return inclusionO;
			}
		} else if (capFeature) {
			if (inclusion == OptionInclusion.Remove) {
				return inclusionO;
			} else if (inclusion == OptionInclusion.Feature) {
				return inclusionI;
			} else {
				// Fix invalid value
				root.Inclusion = OptionInclusion.Remove;
				return inclusionO;
			}
		} else if (capOption) {
			if (inclusion == OptionInclusion.Remove) {
				return inclusionO;
			} else if (inclusion == OptionInclusion.Option) {
				return inclusionI;
			} else {
				// Fix invalid value
				root.Inclusion = OptionInclusion.Remove;
				return inclusionO;
			}
		}

		return GUIContent.none;
	}

	void DoInclusionMenu(ValueStore.RootNode root, OptionCapabilities capabilities)
	{
		if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)
				&& Event.current.type == EventType.MouseDown) {
			Event.current.Use();

			var value = root.Inclusion;
			var menu = new GenericMenu();
			
			var capFeature = (capabilities & OptionCapabilities.HasAssociatedFeature) != 0;
			var capOption = (capabilities & OptionCapabilities.CanIncludeOption) != 0;

			if (capFeature) {
				menu.AddItem(new GUIContent("Include Feature"), (value & OptionInclusion.Feature) != 0, () => {
					root.Inclusion ^= OptionInclusion.Feature;
					if ((root.Inclusion & OptionInclusion.Feature) == 0) {
						root.Inclusion &= ~OptionInclusion.Option;
					}
				});
			} else {
				menu.AddDisabledItem(new GUIContent("Include Feature"));
			}

			if (capOption) {
				menu.AddItem(new GUIContent("Include Option"), (value & OptionInclusion.Option) != 0, () => {
					root.Inclusion ^= OptionInclusion.Option;
				});
			} else {
				menu.AddDisabledItem(new GUIContent("Include Option"));
			}

			menu.AddSeparator("");

			if (capOption && capFeature) {
				menu.AddItem(new GUIContent("Include Both"), (value & OptionInclusion.FeatureAndOption) == OptionInclusion.FeatureAndOption, () => {
					root.Inclusion |= OptionInclusion.FeatureAndOption;
				});

				menu.AddItem(new GUIContent("Remove Both"), (value & OptionInclusion.FeatureAndOption) == 0, () => {
					root.Inclusion &= ~OptionInclusion.FeatureAndOption;
				});
			} else {
				menu.AddDisabledItem(new GUIContent("Include Both"));
				menu.AddDisabledItem(new GUIContent("Remove Both"));
			}
			
			menu.ShowAsContext();
		}
	}

	void AddNewVariant(Option option, ValueStore.Node node)
	{
		if (option.Variance == OptionVariance.Single)
			throw new Exception("Option is not variant.");

		var parameter = FindUniqueVariantName(option, node);

		if (node != null) {
			node.AddVariant(parameter, string.Empty);
			if (option.Variance == OptionVariance.Array) {
				node.NumberVariantsSequentially();
			}
		} else {
			option.AddVariant(parameter);
		}
	}

	static Regex RemoveTrailingNumbersRegex = new Regex(@"^(.*?)\d*$");

	string FindUniqueVariantName(Option option, ValueStore.Node node, string baseParam = null)
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

