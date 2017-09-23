using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace sttz.Workbench {

/// <summary>
/// The build manager defines which profile is used for builds and 
/// manages the build process.
/// </summary>
[InitializeOnLoad]
public class BuildManager
{
	// -------- Active Profile --------

	/// <summary>
	/// Platform name used to save project-specific settings.
	/// </summary>
	public const string SettingsPlatformName = "Workbench";
	/// <summary>
	/// Key used to save the active profile GUID.
	/// </summary>
	public const string ActiveProfileGUIDKey = "ActiveProfileGUID";
	/// <summary>
	/// Key used to save the editor defaults profile GUID.
	/// </summary>
	public const string DefaultsProfileGUIDKey = "DefaultsProfileGUID";


	/// <summary>
	/// The active profile, which is used for regular Unity builds.
	/// </summary>
	/// <remarks>
	/// The active profile is saved per-project in the editor profile's ini
	/// file (usually <c>Editor.ini</c> in the project folder).
	/// </remarks>
	public static BuildProfile ActiveProfile {
		get {
			if (_activeProfile == null) {
				var guid = EditorUserBuildSettings.GetPlatformSettings(SettingsPlatformName, ActiveProfileGUIDKey);
				if (string.IsNullOrEmpty(guid))
					return null;

				_activeProfile = LoadAssetByGUID<BuildProfile>(guid);
			}
			return _activeProfile;
		}
		set {
			if (value == _activeProfile)
				return;

			if (value == null) {
				EditorUserBuildSettings.SetPlatformSettings(SettingsPlatformName, ActiveProfileGUIDKey, null);
				return;
			}

			var guid = GetAssetGUID(value);
			if (string.IsNullOrEmpty(guid))
				return;

			EditorUserBuildSettings.SetPlatformSettings(SettingsPlatformName, ActiveProfileGUIDKey, guid);

			_activeProfile = value;
			_activeProfile.ApplyScriptingDefineSymbols();
		}
	}
	private static BuildProfile _activeProfile;

	/// <summary>
	/// Profile providing the current defaults for the editor.
	/// </summary>
	/// <remarks>
	/// Instead of using the editor's unique default option values, it's
	/// possible to use a build profile's defaults instead, allowing to 
	/// quickly switch between sets of option values.
	/// </remarks>
	/// <value>
	/// <c>null</c> when using the editor's own defaults, otherwise the 
	/// build profile whose defaults are used.
	/// </value>
	public static BuildProfile EditorDefaultsProfile {
		get {
			if (_editorDefaultsProfile == null) {
				var guid = EditorUserBuildSettings.GetPlatformSettings(SettingsPlatformName, DefaultsProfileGUIDKey);
				if (!string.IsNullOrEmpty(guid)) {
					_editorDefaultsProfile = BuildManager.LoadAssetByGUID<BuildProfile>(guid);
				}
			}
			return _editorDefaultsProfile;
		}
		set {
			// TODO: Check applying of values when changing profile or the profile's defaults
			Debug.Log("EditorDefaultsProfile = " + value);
			_editorDefaultsProfile = value;

			var guid = string.Empty;
			if (value != null)
				guid = BuildManager.GetAssetGUID(value);

			EditorUserBuildSettings.SetPlatformSettings(SettingsPlatformName, DefaultsProfileGUIDKey, guid);
		}
	}
	private static BuildProfile _editorDefaultsProfile;

	/// <summary>
	/// The profile used for the current build.
	/// </summary>
	/// <remarks>
	/// This allows to temporarily overwrite the active profile.
	/// Set the current profile to null to revert to the active profile.
	/// The current profile is not saved, it will be reset after
	/// script compilation or opening/closing the project.
	/// </remarks>
	public static BuildProfile CurrentProfile {
		get {
			return _currentProfile ?? ActiveProfile;
		}
		set {
			_currentProfile = value;
		}
	}
	private static BuildProfile _currentProfile;

	// -------- GUID Helper Methods --------

	/// <summary>
	/// Helper method to get the GUID of an asset object.
	/// </summary>
	/// <returns>
	/// The GUID or null if the object has no GUID (is not an asset).
	/// </returns>
	public static string GetAssetGUID(UnityEngine.Object target)
	{
		var path = AssetDatabase.GetAssetPath(target);
		if (string.IsNullOrEmpty(path))
			return null;

		var guid = AssetDatabase.AssetPathToGUID(path);
		if (string.IsNullOrEmpty(guid))
			return null;

		return guid;
	}

	/// <summary>
	/// Load an asset by its GUID.
	/// </summary>
	/// <returns>
	/// The object of given type in the asset with the given GUID or null
	/// if either no asset with this GUID exists or the asset does not contain
	/// an object of given type.
	/// </returns>
	public static T LoadAssetByGUID<T>(string guid) where T : UnityEngine.Object
	{
		if (string.IsNullOrEmpty(guid))
			return null;

		var path = AssetDatabase.GUIDToAssetPath(guid);
		if (string.IsNullOrEmpty(path))
			return null;

		return AssetDatabase.LoadAssetAtPath(path, typeof(T)) as T;
	}

	// -------- Build Settings Tracking --------

	static BuildManager()
	{
		EditorApplication.update += Update;
	}

	private static BuildTarget lastBuildTarget;
	private static bool lastDevelopmentBuild;

	/// <summary>
	/// Wait for changes of the build settings and prompt the
	/// user to update the scripting define symbols when needed.
	/// This will also prompt on startup or after recompiling scripts.
	/// </summary>
	private static void Update()
	{
		if (EditorApplication.isPlaying)
			return;

		// TODO: Better check when to ask
		if (ActiveProfile != null
				&& (EditorUserBuildSettings.activeBuildTarget != lastBuildTarget
				|| EditorUserBuildSettings.development != lastDevelopmentBuild)) {
			lastBuildTarget = EditorUserBuildSettings.activeBuildTarget;
			lastDevelopmentBuild = EditorUserBuildSettings.development;

			var symbolsDifference = ActiveProfile.ScriptingDefineSymbolsDifference();
			if (symbolsDifference.Any()) {
				if (EditorUtility.DisplayDialog(
					"Build Manager",
					"The active build profile needs to update the scripting define symbols.",
					"Update Now", "Later"
				)) {
					ActiveProfile.ApplyScriptingDefineSymbols();
				}
			}
		}
	}

	// -------- Postprocessing --------

	/// <summary>
	/// Unity callback for each scene, allowing to edit the scene before it's built.
	/// </summary>
	/// <remarks>
	/// Workbench uses the <see cref="PostProcessScene"/> callback to apply the options'
	/// default values or to remove them during a build.
	/// </remarks>
	[PostProcessScene]
	private static void OnPostprocessScene()
	{
		var buildProfile = CurrentProfile;

		// Playing in editor
		if (!BuildPipeline.isBuildingPlayer) {
			if (!EditorApplication.isPlaying || Workbench.Instance == null) {
				InjectWorkbench(EditorDefaultsProfile ?? EditorProfile.SharedInstance);
			}
			return;
		}

		// Actual building
		var devBuild = EditorUserBuildSettings.development;
		var removeAll = (buildProfile == null || !buildProfile.HasAvailableOptions(devBuild));

		// Create runtime profile so it applies its options
		Profile profile = new Profile(buildProfile != null ? buildProfile.store : null);

		if (!removeAll) {
			InjectWorkbench(buildProfile);
		}

		foreach (var option in profile) {
			if (removeAll || !buildProfile.IncludeInBuild(option.Name)) {
				option.Remove();
			}
		}
	}

	/// <summary>
	/// Unity callback after the build has completed.
	/// </summary>
	/// <remarks>
	/// Workbench uses the <see cref="PostProcessBuild"/> callback to warn the user 
	/// if no profile is set (there is unfortunately no pre-build callback) and to
	/// copy the ini file to the build, if enabled.
	/// </remarks>
	[PostProcessBuild(100)]
	private static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
	{
		if (!BuildPipeline.isBuildingPlayer)
			return;

		// Warn if no profile is set
		var buildProfile = CurrentProfile;
		if (buildProfile == null) {
			Debug.LogError("Build Configuration: No current or default profile set, all options removed.");
			return;
		}

		// Copy ini file to built project directory
		/*var addIniFile = buildProfile.GetSetting(BuildProfile.OPTION_ADD_INI_FILE);
		if (!addIniFile.ShouldBeRemovedInBuild()) {
			if (buildProfile.panelConfig.iniFilePaths.Length < 1) {
				Debug.LogError("Cannot add ini file to build: No ini file paths defined.");
			} else {
				var path = buildProfile.panelConfig.iniFilePaths[0];
				path = buildProfile.panelConfig.ResolveIniPath(path);
				path = Path.Combine(path, buildProfile.panelConfig.iniFileName);

				var iniFile = AssetDatabase.GetAssetPath(buildProfile.defaults);

				try {
					File.Copy(iniFile, path);
				} catch (Exception e) {
					Debug.LogError("Could not copy ini file to built project: " + e.Message);
				}
			}
		}*/

		// Run options' PostprocessBuild
		var devBuild = EditorUserBuildSettings.development;
		var removeAll = (buildProfile == null || !buildProfile.HasAvailableOptions(devBuild));
		Profile profile = new Profile(buildProfile != null ? buildProfile.store : null);

		foreach (var option in profile.OrderBy(o => o.PostprocessOrder)) {
			var removeOption = removeAll || !buildProfile.IncludeInBuild(option.Name);
			option.PostprocessBuild(target, pathToBuiltProject, removeOption, profile);
		}
	}

	/// <summary>
	/// Create and configure the <see cref="Workbench"/> instance in the current scene.
	/// </summary>
	private static void InjectWorkbench(EditorProfile profile)
	{
		var go = new GameObject("Workbench");
		var bench = go.AddComponent<Workbench>();

		bench.store = profile.store;
		if (bench.Profile != null) {
			bench.Profile.Store = profile.store;
		}
	}
}

}