using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Build;
using UnityEngine.SceneManagement;
using System.Reflection;

namespace sttz.Workbench.Editor
{

/// <summary>
/// The build manager defines which profile is used for builds and 
/// manages the build process.
/// </summary>
public class BuildManager : IProcessScene, IPreprocessBuild, IPostprocessBuild
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
	/// Key used to save the editor source profile GUID.
	/// </summary>
	public const string SourceProfileGUIDKey = "SourceProfileGUID";


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
		}
	}
	private static BuildProfile _activeProfile;

	/// <summary>
	/// Profile providing the current configuration for the editor.
	/// </summary>
	/// <remarks>
	/// Instead of using the editor's unique configuration values, it's
	/// possible to use a build profile's configuration instead, allowing to 
	/// quickly switch between sets of configuration values.
	/// </remarks>
	/// <value>
	/// <c>null</c> when using the editor's own configuration, otherwise the 
	/// build profile whose configuration is used.
	/// </value>
	public static BuildProfile EditorSourceProfile {
		get {
			if (_editorSourceProfile == null) {
				var guid = EditorUserBuildSettings.GetPlatformSettings(SettingsPlatformName, SourceProfileGUIDKey);
				if (!string.IsNullOrEmpty(guid)) {
					_editorSourceProfile = BuildManager.LoadAssetByGUID<BuildProfile>(guid);
				}
			}
			return _editorSourceProfile;
		}
		set {
			if (_editorSourceProfile == value)
				return;
			
			_editorSourceProfile = value;

			var guid = string.Empty;
			if (value != null)
				guid = BuildManager.GetAssetGUID(value);

			EditorUserBuildSettings.SetPlatformSettings(SettingsPlatformName, SourceProfileGUIDKey, guid);

			if (Application.isPlaying) {
				CreateOrUpdateMainRuntimeProfile();
				RuntimeProfile.Main.Apply();
			}
		}
	}
	private static BuildProfile _editorSourceProfile;

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

	/// <summary>
	/// Show the active build profile in the inspector.
	/// </summary>
	[MenuItem("Window/Active Build Profile %&b")]
	public static void OpenEditorProfile()
	{
		Selection.activeObject = ActiveProfile;
	}

	[MenuItem("Window/Active Build Profile %&b", true)]
	static bool ValidateOpenEditorProfile()
	{
		return ActiveProfile != null;
	}

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

	private static BuildTarget lastBuildTarget;
	private static bool lastDevelopmentBuild;

	// -------- Building --------

	/// <summary>
	/// Populate the <c>BuildPlayerOptions</c> with default values.
	/// </summary>
	public static BuildPlayerOptions GetDefaultOptions(BuildTarget target)
	{
		// TODO: Use BuildPlayerWindow.DefaultBuildMethods.GetBuildPlayerOptions in 2017.2?
		var playerOptions = new BuildPlayerOptions();
		playerOptions.target = target;
		playerOptions.targetGroup = BuildPipeline.GetBuildTargetGroup(target);
		
		playerOptions.locationPathName = EditorUserBuildSettings.GetBuildLocation(target);

		playerOptions.scenes = EditorBuildSettings.scenes
			.Where(s => s.enabled)
			.Select(s => s.path)
			.ToArray();

		playerOptions.options = BuildOptions.None;

		return playerOptions;
	}

	/// <summary>
	/// Show a dialog to let the user pick a build location.
	/// </summary>
	/// <remarks>
	/// Base on BuildPlayerWindow.PickBuildLocation in private Unity engine code.
	/// </remarks>
	public static string PickBuildLocation(BuildTarget target)
	{
		var buildLocation = EditorUserBuildSettings.GetBuildLocation(target);
		
		if (target == BuildTarget.Android && EditorUserBuildSettings.exportAsGoogleAndroidProject) {
			var location = EditorUtility.SaveFolderPanel("Export Google Android Project", buildLocation, "");
			EditorUserBuildSettings.SetBuildLocation(target, location);
			return location;
		}

		string directory = "", filename = "";
		if (!string.IsNullOrEmpty(buildLocation)) {
			directory = Path.GetDirectoryName(buildLocation);
			filename = Path.GetFileName(buildLocation);
		}

		// Call internal method:
		// string SaveBuildPanel(BuildTarget target, string title, string directory, string defaultName, string extension, out bool updateExistingBuild)
		var method = typeof(EditorUtility).GetMethod("SaveBuildPanel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
		if (method == null) {
			Debug.LogError("Could no find SaveBuildPanel method on EditorUtility class.");
			return null;
		}

		var args = new object[] { target, "Build " + target, directory, filename, "", null };
		var path = (string)method.Invoke(null, args);

		return path;
	}

	/// <summary>
	/// Build a profile for its default target and with the default build options.
	/// </summary>
	public static string Build(BuildProfile profile)
	{
		foreach (var target in profile.BuildTargets) {
			var options = GetDefaultOptions(target);
			var error = Build(profile, options);
			if (!string.IsNullOrEmpty(error)) {
				return error;
			}
		}
		return null;
	}

	/// <summary>
	/// Build a profile with the given build options.
	/// </summary>
	/// <remarks>
	/// Note that the <c>BuildPlayerOptions</c> will be passed through the profile's
	/// options' <see cref="IOption.PrepareBuild"/>, which can modify it before
	/// the build is started.<br />
	/// Note that if you do not set <c>options.locationPathName</c> and no option sets
	/// it in the <c>PrepareBuild</c> callback, then a save dialog will be shown.
	/// </remarks>
	public static string Build(BuildProfile buildProfile, BuildPlayerOptions options)
	{
		// Prepare build
		BuildManager.CurrentProfile = buildProfile;

		// Run options' PrepareBuild
		var removeAll = (buildProfile == null || !buildProfile.HasAvailableOptions());
		
		CreateOrUpdateMainRuntimeProfile();
		var profile = RuntimeProfile.Main;

		foreach (var option in profile.OrderBy(o => o.PostprocessOrder)) {
			var included = !removeAll && buildProfile.IncludeInBuild(option);
			options = option.PrepareBuild(options, included, profile);
		}

		// Ask for location if none has been set
		if (string.IsNullOrEmpty(options.locationPathName)) {
			options.locationPathName = PickBuildLocation(options.target);
			if (string.IsNullOrEmpty(options.locationPathName)) {
				return "Cancelled build location dialog";
			}
			EditorUserBuildSettings.SetBuildLocation(options.target, options.locationPathName);
		}

		// Run the build
		var error = BuildPipeline.BuildPlayer(options);

		BuildManager.CurrentProfile = null;
		return error;
	}

	// -------- Processing --------

	/// <summary>
	/// Create and configure the <see cref="ProfileContainer"/> during the build.
	/// </summary>
	private static void InjectProfileContainer(ValueStore store)
	{
		var go = new GameObject("Workbench");
		var container = go.AddComponent<ProfileContainer>();
		container.store = store;
	}

	/// <summary>
	/// Create or udpate the main runtime profile with the apropriate value store.
	/// </summary>
	private static void CreateOrUpdateMainRuntimeProfile()
	{
		ValueStore store = null;

		// During playback in the editor
		if (Application.isPlaying) {
			if (EditorSourceProfile != null) {
				store = EditorSourceProfile.Store;
			} else {
				store = EditorProfile.SharedInstance.Store;
			}

		// During the build process
		} else {
			if (CurrentProfile != null) {
				store = CurrentProfile.Store;
			}
		}
		
		RuntimeProfile.CreateMain(store);
		RuntimeProfile.Main.CleanStore();
	}

	public int callbackOrder { get { return 0; } }

	public void OnPreprocessBuild(BuildTarget target, string path)
	{
		// Warn if no profile is set
		var buildProfile = CurrentProfile;
		if (buildProfile == null) {
			Debug.LogError("Build Configuration: No current or default profile set, all options removed.");
			return;
		}

		CurrentProfile.ApplyScriptingDefineSymbols(target);

		// Run options' PostprocessBuild
		var removeAll = (buildProfile == null || !buildProfile.HasAvailableOptions());
		
		CreateOrUpdateMainRuntimeProfile();
		var profile = RuntimeProfile.Main;

		foreach (var option in profile.OrderBy(o => o.PostprocessOrder)) {
			var included = !removeAll && buildProfile.IncludeInBuild(option);
			option.PostprocessBuild(target, path, included, profile);
		}
	}
	
	public void OnPostprocessBuild(BuildTarget target, string path)
	{
		var buildProfile = CurrentProfile;

		// Run options' PostprocessBuild
		var removeAll = (buildProfile == null || !buildProfile.HasAvailableOptions());
		
		CreateOrUpdateMainRuntimeProfile();
		var profile = RuntimeProfile.Main;

		foreach (var option in profile.OrderBy(o => o.PostprocessOrder)) {
			var included = !removeAll && buildProfile.IncludeInBuild(option);
			option.PostprocessBuild(target, path, included, profile);
		}
	}

	public void OnProcessScene(Scene scene)
	{
		RuntimeProfile profile;

		// Playing in editor
		if (!BuildPipeline.isBuildingPlayer) {
			if (RuntimeProfile.Main == null) {
				CreateOrUpdateMainRuntimeProfile();
				RuntimeProfile.Main.Apply();
			}

			profile = RuntimeProfile.Main;
			foreach (var option in profile) {
				option.PostprocessScene(scene, false, true, profile);
			}

		// Building
		} else {
			var buildProfile = CurrentProfile;
			var removeAll = (buildProfile == null || !buildProfile.HasAvailableOptions());

			CreateOrUpdateMainRuntimeProfile();
			profile = RuntimeProfile.Main;

			if (!removeAll) {
				InjectProfileContainer(profile.Store);
			}

			foreach (var option in profile) {
				var include = !removeAll && buildProfile.IncludeInBuild(option);
				option.PostprocessScene(scene, true, include, profile);
			}
		}
	}
}

}