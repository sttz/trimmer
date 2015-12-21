using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

class VersionIncrementer
{
	/// <summary>
	/// Create a new version info at the selected location
	/// in the project's assets.
	/// </summary>
	[MenuItem("Assets/Create/Version Info")]
	public static void CreateBuildProfile()
	{
		// Get the first selected folder or the first asset if no folder is selected
		string profilePath = "Assets";
		foreach (var guid in Selection.assetGUIDs) {
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if (Directory.Exists(path)) {
				profilePath = path;
				break;
			} else if (profilePath == "Assets") {
				profilePath = path;
			}
		}

		if (!Directory.Exists(profilePath)) {
			profilePath = Path.GetDirectoryName(profilePath);
		}

		profilePath += Path.DirectorySeparatorChar + "New Version Info.asset";
		profilePath = AssetDatabase.GenerateUniqueAssetPath(profilePath);

		var versionInfo = ScriptableObject.CreateInstance<VersionInfo>();
		AssetDatabase.CreateAsset(versionInfo, profilePath);
	}

	[PostProcessBuild]
	static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
	{
		var guids = AssetDatabase.FindAssets("t:VersionInfo");
		foreach (var guid in guids) {
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(path))
				continue;

			var info = (VersionInfo)AssetDatabase.LoadAssetAtPath(path, typeof(VersionInfo));
			if (info == null)
				continue;

			if (!info.autoIncrementBuild)
				continue;

			info.build++;
			EditorUtility.SetDirty(info);
		}
	}
}
