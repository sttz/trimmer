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
    const string PLAYMODE_EXIT_SAVE = "PlaymodeExitSave";

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

    /// <summary>
    /// Save the Editor Profile before exiting play mode.
    /// </summary>
    /// <remarks>
    /// By default, changes in the Editor Profile during play mode will not be
    /// saved, just like changes on game objects. Set this to `true` to save
    /// the Editor Profile when exiting play mode to preserve changes.
    /// </remarks>
    public static bool PlaymodeExitSave {
        get {
            return EditorPrefs.GetBool(WORKBENCH_PREFIX + PLAYMODE_EXIT_SAVE);
        }
        set {
            EditorPrefs.SetBool(WORKBENCH_PREFIX + PLAYMODE_EXIT_SAVE, value);
        }
    }

    [PreferenceItem("Workbench")]
    static void OnPreferencesGUI()
    {
        EditorGUILayout.LabelField("Editor Profile", EditorStyles.boldLabel);

        PlaymodeExitSave = EditorGUILayout.Toggle("Save Play Mode Changes", PlaymodeExitSave);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Build Profiles", EditorStyles.boldLabel);

        ShowUnavailableOptions = EditorGUILayout.Toggle("Show Unavailable Options", ShowUnavailableOptions);
        BuildManager.ActiveProfile = (BuildProfile)EditorGUILayout.ObjectField(
            "Active Build Profile", BuildManager.ActiveProfile, typeof(BuildProfile), false
        );
    }
}

}