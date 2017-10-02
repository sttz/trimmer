#if UNITY_5 && !UNITY_5_0 // Introduced in Unity 5.1
#define HAS_CREATE_ASSET_MENU_ATTRIBUTE
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using System.Reflection;
using sttz.Workbench.Extensions;

namespace sttz.Workbench
{

/// <summary>
/// Build Profile defines available options in builds
/// and their default values using an ini file.
/// </summary>
/// <remarks>
/// <para>A build profile defines which <see cref="IOption"/> types
/// are available in debug or release builds. It defines a set of
/// scripting define symbols based on those options, which allows to
/// conditionally compile code (and the options themselves) depending
/// on the availability of the option in the build.</para>
/// 
/// <para>A build profile also defines a set of default values for
/// thos options using an ini file. Depending on the runtime configuration
/// of the build profile, the values can be changed at runtime using
/// a dynamically loaded ini file or using an in-game prompt
/// (see <see cref="MaintenancePanel"/>). If the ini file or prompt are
/// disabled for a given build, their respective code won't be compiled.</para>
/// 
/// <para>For regular builds, the <see cref="BuildManager.ActiveProfile"/>
/// defines which profile is used. Alternatively, builds can be made with
/// non-active profiles using <see cref="BuildProfile.Build"/>.</para>
/// 
/// <para>Changing if an option is included in the build for the active
/// profile might require the scripting define symbols to be changed, which
/// requires scripts to be recompiled. Be sure to update the symbols
/// before doing a build or the latest changes won't be reflected. When
/// required, the symbols can always be updated when inspecting the active
/// profile in the editor.</para>
/// </remarks>
#if HAS_CREATE_ASSET_MENU_ATTRIBUTE
[CreateAssetMenu(fileName = "Build Profile.asset", menuName = "Build Profile")]
#endif
public class BuildProfile : EditableProfile
{
	// -------- Static --------

	/// <summary>
	/// Enumeration of all build profiles in the current project.
	/// </summary>
	public static IEnumerable<BuildProfile> AllBuildProfiles {
		get {
			if (_buildProfiles == null || _buildProfiles.Any(p => p == null)) {
				_buildProfiles = null;

				var profiles = new List<BuildProfile>();
				var guids = AssetDatabase.FindAssets("t:BuildProfile");
				foreach (var guid in guids) {
					var path = AssetDatabase.GUIDToAssetPath(guid);
					var profile = AssetDatabase.LoadAssetAtPath(path, typeof(BuildProfile));
					profiles.Add((BuildProfile)profile);
				}

				// Assign _buildProfiles only here because LoadAssetAtPath will cause
				// the newly loaded profile's OnEnable to be called, which will check
				// to invalidate _buildProfiles.
				_buildProfiles = profiles;
			}
			return _buildProfiles;
		}
	}
	private static List<BuildProfile> _buildProfiles;

	#if !HAS_CREATE_ASSET_MENU_ATTRIBUTE
	/// <summary>
	/// Create a new <see cref="BuildProfile"/> at the selected location in the project's assets.
	/// </summary>
	/// <remarks>
	/// This tries to mirror Unity's behavior for its built-in asset types.
	/// </remarks>
	[MenuItem("Assets/Create/Build Profile")]
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

		profilePath += Path.DirectorySeparatorChar + "New Build Profile.asset";
		profilePath = AssetDatabase.GenerateUniqueAssetPath(profilePath);

		var profile = ScriptableObject.CreateInstance<BuildProfile>();
		AssetDatabase.CreateAsset(profile, profilePath);
	}
	#endif

	static BuildTarget[] activeBuildTarget;

	// ------ Fields ------

	/// <summary>
	/// The value store containing the values for the profile's options.
	/// </summary>
	public ValueStore store = new ValueStore();

	public override ValueStore Store {
		get {
			return store;
		}
	}

	[SerializeField] List<BuildTarget> _buildTargets;

	public IEnumerable<BuildTarget> BuildTargets {
		get {
			if (_buildTargets != null && _buildTargets.Count > 0) {
				return _buildTargets;
			} else {
				if (activeBuildTarget == null 
						|| activeBuildTarget[0] != EditorUserBuildSettings.activeBuildTarget) {
					activeBuildTarget = new BuildTarget[] {
						EditorUserBuildSettings.activeBuildTarget
					};
				}
				return activeBuildTarget;
			}
		}
	}

	// -------- Methods --------

	public bool UsesActiveBuildTarget()
	{
		return (_buildTargets == null || _buildTargets.Count == 0);
	}

	public void AddBuildTarget(BuildTarget target)
	{
		if (_buildTargets == null) {
			_buildTargets = new List<BuildTarget>();
		} else if (_buildTargets.Contains(target)) {
			return;
		}

		_buildTargets.Add(target);
	}

	public void RemoveBuildTarget(BuildTarget target)
	{
		if (_buildTargets == null) return;
		_buildTargets.Remove(target);
	}

	/// <summary>
	/// Mark the scriptable object dirty when necessary.
	/// </summary>
	public override void SaveIfNeeded()
	{
		if (this == null) return;

		if (store.IsDirty(true)) {
			EditorUtility.SetDirty(this);
		}
	}

	/// <summary>
	/// Check if an option should be included in builds of this profile.
	/// </summary>
	public bool IncludeInBuild(IOption option)
	{
		var node = store.GetRoot(option.Name);
		if (node == null) {
			return false;
		} else {
			return node.IncludeInBuild && option.IsAvailable(BuildTargets);
		}
	}

	/// <summary>
	/// Check if there are any options included in the build.
	/// </summary>
	public bool HasAvailableOptions()
	{
		foreach (var option in AllOptions) {
			if (IncludeInBuild(option))
				return true;
		}
		return false;
	}

	public override Recursion.RecursionType GetRecursionType()
	{
		return Recursion.RecursionType.Nodes;
	}

	public override IEnumerable<IOption> GetAllOptions()
	{
		return AllOptions;
	}

	public override void EditOption(string path, GUIContent label, IOption option, ValueStore.Node node)
	{
		// For build profiles, the store is always directly edited.
		node.Value = option.EditGUI(label, node.Value);
	}

	[ContextMenu("Copy As Ini File")]
	public void CopyAsIniFile()
	{
		var ini = store.SaveIniFile();
		EditorGUIUtility.systemCopyBuffer = ini;
	}

	/// <summary>
	/// Check if the option scripting define symbols match those
	/// that would be defined by this profile.
	/// This method checks the active build target and development build setting.
	/// </summary>
	public bool ScriptingDefineSymbolsUpToDate()
	{
		return ScriptingDefineSymbolsUpToDate(
			EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.development
		);
	}

	/// <summary>
	/// Check if the option scripting define symbols match those
	/// that would be defined by this profile.
	/// </summary>
	public bool ScriptingDefineSymbolsUpToDate(BuildTarget target, bool devBuild)
	{
		return !ScriptingDefineSymbolsDifference().Any();
	}

	/// <summary>
	/// Return the scripting define symbols that need to be removed or added
	/// to match those in this profile. Symbols to remove are prefixed with "-"
	/// and symbols to add with "+".
	/// This method checks the active build target and development build setting.
	/// </summary>
	public IEnumerable<string> ScriptingDefineSymbolsDifference()
	{
		return ScriptingDefineSymbolsDifference(
			EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.development
		);
	}

	/// <summary>
	/// Return the scripting define symbols that need to be removed or added
	/// to match those in this profile. Symbols to remove are prefixed with "-"
	/// and symbols to add with "+".
	/// </summary>
	public IEnumerable<string> ScriptingDefineSymbolsDifference(BuildTarget target, bool devBuild)
	{
		var targetGroup = GetBuildTargetGroup(target);
		if (targetGroup == BuildTargetGroup.Unknown) {
			Debug.LogError("Could not find build target group for target " + target + ".");
			return Enumerable.Empty<string>();
		}

		var current = GetCurrentScriptingDefineSymbols(targetGroup)
			.Where(s => s.StartsWith(Option.DEFINE_PREFIX));
		var needed = GetProfileScriptingDefineSymbols(targetGroup, devBuild);

		var toRemove = current.Except(needed).Select(d => "-" + d);
		var toAdd = needed.Except(current).Select(d => "+" + d);

		return toRemove.Concat(toAdd);
	}

	/// <summary>
	/// Update the scripting define symbols of the active build target, depending
	/// on the current <see cref="EditorUserBuildSettings.development"/> value.
	/// </summary>
	/// <remarks>
	/// This triggers a rebuild if the defines are changed for the active
	/// build target. While not necessary, this cannot be avoided due to
	/// Unity's API.
	/// </remarks>
	public void ApplyScriptingDefineSymbols()
	{
		ApplyScriptingDefineSymbols(
			EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.development
		);
	}

	/// <summary>
	/// Update the scripting define symbols of the given build target.
	/// </summary>
	/// <remarks>
	/// This triggers a rebuild if the defines are changed for the active
	/// build target. While not necessary, this cannot be avoided due to
	/// Unity's API.
	/// </remarks>
	public void ApplyScriptingDefineSymbols(BuildTarget target, bool devBuild)
	{
		var targetGroup = GetBuildTargetGroup(target);
		if (targetGroup == BuildTargetGroup.Unknown) {
			Debug.LogError("Could not find build target group for target " + target + ".");
			return;
		}

		var symbols = GetCurrentScriptingDefineSymbols(targetGroup);
		symbols.RemoveWhere(d => d.StartsWith(Option.DEFINE_PREFIX));
		foreach (var symbol in GetProfileScriptingDefineSymbols(targetGroup, devBuild)) {
			symbols.Add(symbol);
		}

		PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", symbols.ToArray()));
	}

	/// <summary>
	/// Suffixes to remove from BuildTarget to normalize platforms.
	/// </summary>
	/// <remarks>
	/// If Unity doesn't provide a path using <see cref="EditorUserBuildSettings.GetBuildLocation"/>,
	/// the <see cref="Build"/> method will save the last used path per build target, whereas
	/// 32/64/universal count as the same target.
	/// </remarks>
	protected static string[] buildTargetRemoveSuffix = new string[] {
		"Streamed", "Intel", "Intel64", "Universal", "64"
	};

	public string Build(string path = null, BuildOptions? options = null)
	{
		var result = "";
		foreach (var target in BuildTargets) {
			result += Build(target, path, options) + "\n";
		}
		return result;
	}

	/// <summary>
	/// Build the project using this build profile.
	/// </summary>
	/// <param name="target">Build Target.</param>
	/// <param name="path">Output path, show a save panel when null.</param>
	/// <param name="options">Build options, mirrors the build setting's debug options
	/// and always shows the built player when null.</param>
	public string Build(BuildTarget target, string path = null, BuildOptions? options = null)
	{
		// TODO: Fix default path
		/*if (path == null) {
			path = EditorUserBuildSettings.GetBuildLocation(target);
			var directory = "";
			var filename = "";
			var extension = "";

			var platform = target.ToString("G");
			foreach (var suffix in buildTargetRemoveSuffix) {
				if (platform.EndsWith(suffix)) {
					platform = platform.Substring(0, platform.Length - suffix.Length);
					break;
				}
			}

			if (string.IsNullOrEmpty(path)) {
				path = EditorProfile.EditorDefaults["BuildPath" + platform];
			}

			if (!string.IsNullOrEmpty(path)) {
				directory = Path.GetDirectoryName(path);
				filename = Path.GetFileName(path);
				extension = Path.GetExtension(path);
				if (!string.IsNullOrEmpty(extension)) {
					extension = extension.Substring(1);
				}
			}

			path = EditorUtility.SaveFilePanel("Build Location", directory, filename, extension);
			if (string.IsNullOrEmpty(path)) {
				return null;
			}

			EditorProfile.EditorDefaults["BuildPath" + platform] = path;
		}*/

		BuildManager.CurrentProfile = this;
		ApplyScriptingDefineSymbols(target, EditorUserBuildSettings.development);

		var buildOptions = BuildOptions.None;
		if (options != null) {
			buildOptions = (BuildOptions)options;
		
		} else {
			buildOptions |= BuildOptions.ShowBuiltPlayer;
			if (EditorUserBuildSettings.development)
				buildOptions |= BuildOptions.Development;
			if (EditorUserBuildSettings.connectProfiler)
				buildOptions |= BuildOptions.ConnectWithProfiler;
			if (EditorUserBuildSettings.allowDebugging)
				buildOptions |= BuildOptions.AllowDebugging;
		}

		var error = BuildPipeline.BuildPlayer(
			EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
			path, target, buildOptions
		);

		BuildManager.CurrentProfile = null;

		return error;
	}

	// -------- Internals --------

	protected virtual void OnEnable()
	{
		// Invalidate AllBuildProfiles when a new one is created
		if (_buildProfiles != null && !_buildProfiles.Contains(this)) {
			_buildProfiles = null;
		}
	}

	/// <summary>
	/// Gets the build target group for the given build target.
	/// </summary>
	/// <remarks>
	/// Unfortunately Unity doesn't provide a method for this so we have
	/// to do this in a not very nice way.
	/// </remarks>
	protected BuildTargetGroup GetBuildTargetGroup(BuildTarget target)
	{
		var targetGroup = BuildTargetGroup.Unknown;
		var targetName = target.ToString("G");
		foreach (BuildTargetGroup grp in Enum.GetValues(typeof(BuildTargetGroup))) {
			if (targetName.StartsWith(grp.ToString("G"))) {
				targetGroup = grp;
				break;
			}
		}

		return targetGroup;
	}

	/// <summary>
	/// Convenience method to get the current scripting define symbols as a
	/// hash set (instead of a colon-delimited string).
	/// </summary>
	protected HashSet<string> GetCurrentScriptingDefineSymbols(BuildTargetGroup targetGroup)
	{
		var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup).Split(';');
		return new HashSet<string>(defines);
	}

	/// <summary>
	/// The scripting define symbols set by this profile.
	/// </summary>
	protected IEnumerable<string> GetProfileScriptingDefineSymbols(BuildTargetGroup targetGroup, bool devBuild)
	{
		var symbols = new HashSet<string>();
		
		Recursion.Recurse(this, Recursion.RecursionType.Nodes, (context) => {
			if (context.variantType != Recursion.VariantType.VariantContainer) {
				var current = context.option.GetSctiptingDefineSymbols(
					context.includeInBuild, context.Value, context.VariantParameter
				);
				symbols.AddRange(current);
			}
			return true;
		});

		return symbols;
	}
}

}

