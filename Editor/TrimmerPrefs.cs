//
// Trimmer Framework for Unity - https://sttz.ch/trimmer
// Copyright © 2017 Adrian Stutz
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using UnityEngine;
using UnityEditor;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Editor Preferences for Trimmer.
/// </summary>
public static class TrimmerPrefs
{
    const string TRIMMER_PREFIX = "Trimmer.";
    const string SHOW_UNAVAILABLE = "ShowUnavailable";
    const string PLAYMODE_EXIT_SAVE = "PlaymodeExitSave";

    /// <summary>
    /// Show unavailable options in Build Profiles.
    /// </summary>
    public static bool ShowUnavailableOptions {
        get {
            return EditorPrefs.GetBool(TRIMMER_PREFIX + SHOW_UNAVAILABLE);
        }
        set {
            EditorPrefs.SetBool(TRIMMER_PREFIX + SHOW_UNAVAILABLE, value);
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
            return EditorPrefs.GetBool(TRIMMER_PREFIX + PLAYMODE_EXIT_SAVE);
        }
        set {
            EditorPrefs.SetBool(TRIMMER_PREFIX + PLAYMODE_EXIT_SAVE, value);
        }
    }

    /// <summary>
    /// GUI shown in Unity's Preferences window.
    /// </summary>
    [PreferenceItem("Trimmer")]
    static void OnPreferencesGUI()
    {
        EditorGUILayout.LabelField("Editor Profile", EditorStyles.boldLabel);

        PlaymodeExitSave = EditorGUILayout.Toggle("Save Play Mode Changes", PlaymodeExitSave);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Build Profiles", EditorStyles.boldLabel);

        ShowUnavailableOptions = EditorGUILayout.Toggle("Show Unavailable Options", ShowUnavailableOptions);
        EditorProfile.Instance.ActiveProfile = (BuildProfile)EditorGUILayout.ObjectField(
            "Active Build Profile", EditorProfile.Instance.ActiveProfile, typeof(BuildProfile), false
        );
    }
}

}