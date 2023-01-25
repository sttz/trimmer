//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections.Generic;
using System.IO;
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
    /// Regex used to add spaces to option names. In contrast
    /// to Unity's approach, this regex tries to keep series
    /// of capital letters together.
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
    /// Label that only takes the space it needs.
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
        
        var wasExpanded = EditorProfile.Instance.IsExpanded(path);
        if (def) wasExpanded = !wasExpanded;
        var isExpanded = wasExpanded;

        isExpanded = Foldout(isExpanded, content, style);
        
        if (isExpanded != wasExpanded) {
            var newValue = isExpanded;
            if (def) newValue = !newValue;
            EditorProfile.Instance.SetExpanded(path, newValue);
        }

        return isExpanded;
    }

    /// <summary>
    /// Sort the root options in the given profile first by category and then by name.
    /// </summary>
    public static List<Option> SortOptionsByCategoryAndName(IEnumerable<Option> options)
    {
        var list = new List<Option>(options);
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

    // ------ Menu ------

    [MenuItem("CONTEXT/BuildProfile/Toggle Show Unavailable")]
    static void ToggleShowUnavailable(MenuCommand cmd)
    {
        TrimmerPrefs.ShowUnavailableOptions = !TrimmerPrefs.ShowUnavailableOptions;
    }

    // -------- Badge --------

    const string IconsGuid = "1a23befd4719d4a76b88b096f78a22ad";
    const string BadgeName = "Active Badge";
    const string BadgeXSName = "Active Badge Small";
    static Texture2D badgeTexture;
    static Texture2D badgeXSTexture;

    static void LoadAssets()
    {
        if (badgeTexture != null && badgeXSTexture != null)
            return;

        var path = AssetDatabase.GUIDToAssetPath(IconsGuid);
        if (string.IsNullOrEmpty(path))
            return;
        
        var icons = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var icon in icons) {
            if (!(icon is Texture2D)) continue;

            if (icon.name == BadgeName) {
                badgeTexture = (Texture2D)icon;
            } else if (icon.name == BadgeXSName) {
                badgeXSTexture = (Texture2D)icon;
            }
        }
    }

    [InitializeOnLoadMethod]
    static void RegisterOnPostIconGUI()
    {
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemOnGUI;

        var editorType = typeof(UnityEditor.Editor);
        var OnEditorGUIDelegate = editorType.GetNestedType("OnEditorGUIDelegate", BindingFlags.NonPublic);
        if (OnEditorGUIDelegate == null) {
            // Not available in Unity < 2017.1
            return;
        }

        var del = Delegate.CreateDelegate(OnEditorGUIDelegate, typeof(ProfileEditor), "OnPostIconGUI", false, false);
        if (del == null) {
            Debug.LogWarning("Could not bind OnPostIconGUI as OnEditorGUIDelegate.");
            return;
        }

        var OnPostIconGUI = editorType.GetField("OnPostIconGUI", BindingFlags.Static | BindingFlags.NonPublic);
        if (OnPostIconGUI == null) {
            Debug.LogWarning("Could not find OnPostIconGUI field on Editor class.");
            return;
        }

        var value = (Delegate)OnPostIconGUI.GetValue(null);
        OnPostIconGUI.SetValue(null, Delegate.Combine(value, del));
    }

    static void OnPostIconGUI(UnityEditor.Editor editor, Rect drawRect)
    {
        if (editor.target != EditorProfile.Instance.ActiveProfile)
            return;

        LoadAssets();
        if (badgeTexture == null)
            return;

        drawRect.x -= 22;
        GUI.DrawTexture(drawRect, badgeTexture);
    }

    static void OnProjectWindowItemOnGUI(string guid, Rect selectionRect)
    {
        if (string.IsNullOrEmpty(guid) || guid != EditorProfile.Instance.ActiveProfileGUID)
            return;
        
        LoadAssets();
        if (badgeXSTexture == null)
            return;

        var rect = selectionRect;
        rect.width = badgeXSTexture.width;
        rect.height = badgeXSTexture.height;
        if (selectionRect.width / selectionRect.height > 5) {
            // List mode (slider to the far left)
            rect.y += selectionRect.height - rect.height;
        } else {
            // Grid mode
            rect.y = selectionRect.width;
        }

        GUI.DrawTexture(rect, badgeXSTexture);
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

        if (buildProfile != null) {
            // Unity 2019.2+ uses UIElements in the background, which
            // don't take up the full vertical space anymore. It's therefore
            // no longer possible to place something at the bottom of the
            // inspector.
            #if UNITY_2019_2_OR_NEWER
            GUILayout.Space(50);
            #else
            GUILayout.FlexibleSpace();
            #endif

            BuildGUI();
        }

        if (Event.current.type != EventType.Layout) {
            foreach (var action in delayedRemovals) {
                action();
            }
            delayedRemovals.Clear();
        }
    }

    protected override bool ShouldHideOpenButton()
    {
        return true;
    }

    protected void OnEnable()
    {
        profile = (EditableProfile)target;
        editorProfile = target as EditorProfile;
        buildProfile = target as BuildProfile;

        options = SortOptionsByCategoryAndName(profile.EditProfile);

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
    GUIStyle boldLabel;

    GUIStyle plusStyle;
    GUIStyle minusStyle;

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

                var sourceProfile = editorProfile.EditorSourceProfile;

                int selected = Array.IndexOf(sourceProfiles, sourceProfile);
                var newSelected = EditorGUILayout.Popup("Source Profile", selected, sourceProfilesNames);

                if (selected != newSelected) {
                    BuildProfile newProfile = null;
                    if (newSelected > 0) {
                        newProfile = sourceProfiles[newSelected];
                    }
                    editorProfile.EditorSourceProfile = newProfile;
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
        GUI.enabled = (editorProfile == null || editorProfile.EditorSourceProfile == null);

        ResurseOptionsGUI();

        GUI.enabled = true;
    }

    protected void BuildGUI()
    {
        ResurseOptionsGUI(showBuild:true);

        if (EditorProfile.Instance.IsExpanded(pathBase + "_Build")) {
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

            var path = buildProfile.GetLastBuildPath(target);
            if (!string.IsNullOrEmpty(path) 
                    && (File.Exists(path) || Directory.Exists(path))
                    && GUILayout.Button("Show", EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
                EditorUtility.RevealInFinder(path);
            }
            EditorGUI.BeginDisabledGroup(BuildRunner.Current != null);
            {
                if (GUILayout.Button("Build", EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
                    BuildManager.Build(buildProfile, target);
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(BuildRunner.Current != null);
        {
            var count = buildProfile.BuildTargets.Count();
            if (GUILayout.Button("Build " + count + " Target" + (count > 1 ? "s" : ""), EditorStyles.miniButton)) {
                BuildManager.Build(buildProfile);
            }
        }
        EditorGUI.EndDisabledGroup();
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
            pathBase = OptionHelper.GetAssetGUID(buildProfile) + "/";
        }

        lastCategory = null;
        categoryExpanded = true;
        optionAvailable = true;
        buildCategory = showBuild;

        foreach (var option in options) {
            OptionGUIRecursive(option);
        }

        if (Option.changed) {
            FlushProfile();
            Option.changed = false;
        }
    }

    protected void OptionGUIRecursive(Option option, int depth = 0)
    {
        if (!OptionGUI(option, depth)) return;

        if (option.IsDefaultVariant) {
            if (!OptionGUI(option, depth + 1, showDefaultVariant: true)) return;
        }

        foreach (var variant in option.Variants) {
            OptionGUIRecursive(variant, depth + 1);
        }

        foreach (var child in option.Children) {
            OptionGUIRecursive(child, depth + 1);
        }
    }

    protected bool OptionGUI(Option option, int depth, bool showDefaultVariant = false)
    {
        var displayName = OptionDisplayName(option.Name);
        var width = GUILayout.Width(EditorGUIUtility.labelWidth - 4);

        var isRoot = (option.Parent == null && !showDefaultVariant);
        var lastDepth = EditorGUI.indentLevel;
        EditorGUI.indentLevel = depth;

        if (buildProfile != null && isRoot) {
            optionAvailable = option.IsAvailable(buildProfile.BuildTargets);
        }
        
        if (!TrimmerPrefs.ShowUnavailableOptions && !optionAvailable) {
            return false;
        }

        var color = GUI.color;
        color.a = optionAvailable ? 1 : unavailableAlpha;
        GUI.color = color;

        var expansionPath = option.Path;
        var isExpanded = true;
        var wasExpanded = true;
        var expandable = option.HasChildren || (option.IsDefaultVariant && !showDefaultVariant);
        if (expandable) {
            isExpanded = wasExpanded = EditorProfile.Instance.IsExpanded(expansionPath);
        }

        // Category headers
        if (isRoot) {
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
            if (option.IsDefaultVariant && !showDefaultVariant) {
                EditorGUILayout.LabelField(displayName, width);
                if (isExpanded && GUILayout.Button(GUIContent.none, plusStyle)) {
                    AddNewVariant(option);
                }
                GUILayout.FlexibleSpace();

            // Variant child
            } else if (option.Variance != OptionVariance.Single) {
                var isDefault = option.IsDefaultVariant;

                if (option.Variance == OptionVariance.Dictionary) {
                    // Disable when editing the default variant
                    EditorGUI.BeginDisabledGroup(isDefault);
                    {
                        EditorGUI.BeginDisabledGroup(isDefault);
                        {
                            var newParam = EditorGUILayout.DelayedTextField(option.VariantParameter, width);
                            if (newParam != option.VariantParameter) {
                                if (option.Parent.GetVariant(newParam, false) != null) {
                                    option.VariantParameter = FindUniqueVariantName(option.Parent, newParam);
                                } else {
                                    option.VariantParameter = newParam;
                                }
                                Option.changed = true;
                            }
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    EditorGUI.EndDisabledGroup();
                } else {
                    EditorGUILayout.PrefixLabel(" ");
                }

                var level = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                profile.EditOption(option);
                EditorGUI.indentLevel = level;

                if (!isDefault) {
                    if (GUILayout.Button(GUIContent.none, minusStyle)) {
                        delayedRemovals.Add(() => {
                            option.Parent.RemoveVariant(option);
                            FlushProfile();
                        });
                    }
                }

            // Regular option
            } else {
                tempContent.text = displayName;
                EditorGUILayout.PrefixLabel(tempContent);

                var level = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                profile.EditOption(option);
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
                        buildProfile != null
                        && isRoot
                        && (
                            (option.Capabilities & OptionCapabilities.CanIncludeOption) != 0
                            || (option.Capabilities & OptionCapabilities.HasAssociatedFeature) != 0
                        )
                    ) {
                        EditorGUI.BeginDisabledGroup(!optionAvailable);
                        var root = buildProfile.Store.GetOrCreateRoot(option.Name);
                        GUILayout.Label(LabelForInclusion(root, option.Capabilities), inclusionLabel);
                        if (optionAvailable) {
                            DoInclusionMenu(root, option.Capabilities);
                        }
                        EditorGUI.EndDisabledGroup();
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
        if (expandable) {
            rect.y += EditorStyles.foldout.padding.top + linePadding / 2;
            isExpanded = EditorGUI.Foldout(rect, isExpanded, GUIContent.none, true);

            if (wasExpanded != isExpanded) {
                EditorProfile.Instance.SetExpanded(expansionPath, isExpanded);
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
                return inclusionII;
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
                    if (capOption && (root.Inclusion & OptionInclusion.Feature) == 0) {
                        // Unsetting feature also unsets option
                        root.Inclusion &= ~OptionInclusion.Option;
                    }
                });
            } else {
                menu.AddDisabledItem(new GUIContent("Include Feature"));
            }

            if (capOption) {
                menu.AddItem(new GUIContent("Include Option"), (value & OptionInclusion.Option) != 0, () => {
                    root.Inclusion ^= OptionInclusion.Option;
                    if (capFeature && (root.Inclusion & OptionInclusion.Option) != 0) {
                        // Setting option also sets feature
                        root.Inclusion |= OptionInclusion.Feature;
                    }
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

    void AddNewVariant(Option option)
    {
        if (option.Variance == OptionVariance.Single)
            throw new Exception("Option is not variant.");

        var parameter = FindUniqueVariantName(option);
        option.AddVariant(parameter);
        Option.changed = true;
    }

    static Regex RemoveTrailingNumbersRegex = new Regex(@"^(.*?)\d*$");

    string FindUniqueVariantName(Option option, string baseParam = null)
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
        } while (option.GetVariant(parameter, false) != null);

        return parameter;
    }

    void FlushProfile()
    {
        profile.SaveToStore();
        profile.SaveIfNeeded();
    }
}

}

