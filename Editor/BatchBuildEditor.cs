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
/// Editor for batch builds.
/// </summary>
[CustomEditor(typeof(BatchBuild), true)]
public class BatchBuildEditor : UnityEditor.Editor
{
    BatchBuild batch;

    protected void OnEnable()
    {
        batch = (BatchBuild)target;
    }

    override public void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(25);

        EditorGUI.BeginDisabledGroup(batch.jobs == null || batch.jobs.Count == 0);
        {
            if (GUILayout.Button("Run Batch")) {
                batch.Run();
            }
        }
        EditorGUI.EndDisabledGroup();
    }
}

}
