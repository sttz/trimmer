//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Editor for distributions.
/// </summary>
/// <remarks>
/// The editor only handles the custom UI at the bottom, the distribution's UI
/// is the default Unity property UI and be customized as such with attributes
/// and property drawers.
/// </remarks>
[CustomEditor(typeof(DistroBase), true)]
public class DistroEditor : UnityEditor.Editor
{
    DistroBase distro;
    UnityEditorInternal.ReorderableList list;

    protected void OnEnable()
    {
        distro = (DistroBase)target;
        if (distro.builds == null) distro.builds = new List<BuildProfile>();

        list = new UnityEditorInternal.ReorderableList(
            distro.builds, typeof(BuildProfile),
            true, false, true, true
        );
        list.elementHeight = EditorGUIUtility.singleLineHeight + 2;
        list.headerHeight = 3;
        list.drawElementCallback = (rect, index, selected, focused) => {
            rect.y += 1;
            rect.height -= 2;
            distro.builds[index] = (BuildProfile)EditorGUI.ObjectField(rect, distro.builds[index], typeof(BuildProfile), false);
        };
        list.onAddCallback = (list) => {
            list.list.Add(null);
        };
    }

    override public void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(25);

        GUILayout.Label("Build Profiles", EditorStyles.boldLabel);
        list.DoLayoutList();

        GUILayout.Space(25);

        EditorGUI.BeginDisabledGroup(distro.builds == null || distro.builds.Count == 0);
        {
            GUILayout.Label("Distribution", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginDisabledGroup(BuildRunner.Current != null);
                {
                    if (GUILayout.Button("Build & Distribute")) {
                        distro.Distribute(DistroBuildMode.BuildAll);
                        GUIUtility.ExitGUI();
                    }
                    EditorGUI.BeginDisabledGroup(!distro.HasAllBuilds());
                    {
                        if (GUILayout.Button("Distribute")) {
                            distro.Distribute();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.EndDisabledGroup();
    }
}

}
