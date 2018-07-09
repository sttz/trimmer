using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace sttz.Trimmer.Editor
{

[CustomEditor(typeof(DistroBase), true)]
public class DistroEditor : UnityEditor.Editor
{
    DistroBase distro;
    UnityEditorInternal.ReorderableList list;

    static GUIContent[] spinner;
    const int spinnerFrames = 12;
    const float spinnerSpeed = 10f;

    static GUIContent GetSpinner()
    {
        if (spinner == null) {
            spinner = new GUIContent[spinnerFrames];
            for (int i = 0; i < spinnerFrames; i++) {
                spinner[i] = EditorGUIUtility.IconContent("WaitSpin" + i.ToString("00"));
            }
        }
        var frame = Mathf.RoundToInt(Time.realtimeSinceStartup * spinnerSpeed) % spinnerFrames;
        return spinner[frame];
    }

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

        GUILayout.FlexibleSpace();

        GUILayout.Label("Build Profiles", EditorStyles.boldLabel);
        list.DoLayoutList();

        EditorGUI.BeginDisabledGroup(!distro.CanRunWithoutBuildTargets && (distro.builds == null || distro.builds.Count == 0));
        {
            GUILayout.Label("Distribution", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            {
                if (distro.IsRunning) {
                    if (GUILayout.Button("Cancel")) {
                        distro.Cancel();
                    }
                    GUILayout.Label(GetSpinner(), GUILayout.ExpandWidth(false));
                } else {
                    if (GUILayout.Button("Build & Distribute")) {
                        distro.Distribute(true);
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
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.EndDisabledGroup();
    }

    override public bool RequiresConstantRepaint()
    {
        return distro.IsRunning;
    }
}

}