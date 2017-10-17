using UnityEngine;
using UnityEditor;

namespace sttz.Workbench.Editor
{

/// <summary>
/// Editor Preferences for Workbench.
/// </summary>
public static class WorkbenchPrefs
{
    const string WORKBENCH_PREFIX = "BuildProfile.";
    const string SHOW_UNAVAILABLE = "ShowUnavailable";

    /// <summary>
    /// Show unavailable options in Build Profiles.
    /// </summary>
    public static bool ShowUnavailableOptions {
        get {
            return EditorPrefs.GetBool(WORKBENCH_PREFIX + SHOW_UNAVAILABLE);
        }
        set {
            EditorPrefs.SetBool(WORKBENCH_PREFIX + SHOW_UNAVAILABLE, value);
        }
    }

    [PreferenceItem("Workbench")]
    static void OnPreferencesGUI()
    {
        EditorGUILayout.LabelField("Build Profiles", EditorStyles.boldLabel);

        ShowUnavailableOptions = EditorGUILayout.Toggle("Show Unavailable Options", ShowUnavailableOptions);
        BuildManager.ActiveProfile = (BuildProfile)EditorGUILayout.ObjectField(
            "Active Build Profile", BuildManager.ActiveProfile, typeof(BuildProfile), false
        );
    }
}

}