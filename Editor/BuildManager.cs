//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

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
using JetBrains.Annotations;
using sttz.Trimmer.Extensions;
using UnityEditor.Build.Reporting;

#if UNITY_CLOUD_BUILD
using UnityEngine.CloudBuild;
#endif

namespace sttz.Trimmer.Editor
{

#if !UNITY_CLOUD_BUILD
/// <summary>
/// Dummy implementation of Unity Cloud Build manifest object,
/// used to avoid errors since the Cloud Build dll is not available.
/// </summary>
public abstract class BuildManifestObject : ScriptableObject
{
    // Try to get a manifest value - returns true if key was found and could be cast to type T, otherwise returns false.
    public abstract bool TryGetValue<T>(string key, out T result);
    // Retrieve a manifest value or throw an exception if the given key isn't found.
    public abstract T GetValue<T>(string key);
    // Set the value for a given key.
    public abstract void SetValue(string key, object value);
    // Copy values from a dictionary. ToString() will be called on dictionary values before being stored.
    public abstract void SetValues(Dictionary<string, object> sourceDict);
    // Remove all key/value pairs.
    public abstract void ClearValues();
    // Return a dictionary that represents the current BuildManifestObject.
    public abstract Dictionary<string, object> ToDictionary();
    // Return a JSON formatted string that represents the current BuildManifestObject
    public abstract string ToJson();
    // Return an INI formatted string that represents the current BuildManifestObject
    public abstract override string ToString();
}
#endif

/// <summary>
/// Type of the current Trimmer build.
/// </summary>
/// <seealso cref="BuildManager.BuildType" />
public enum TrimmerBuildType
{
    /// <summary>
    /// Not currently building.
    /// </summary>
    None,
    /// <summary>
    /// A non-Trimmer script triggered the build.
    /// </summary>
    NonTrimmer,
    /// <summary>
    /// The build was started from Unity's build window.
    /// </summary>
    BuildWindow,
    /// <summary>
    /// The build was started from a Trimmer profile or using the Trimmer API.
    /// </summary>
    Profile,
    /// <summary>
    /// The build was started by Unity Cloud Build.
    /// </summary>
    CloudBuild,
}

/// <summary>
/// The Build Manager controls the build process and calls the Option's
/// callbacks.
/// </summary>
public class BuildManager : IProcessSceneWithReport, IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    /// <summary>
    /// Scripting define symbol added to remove Trimmer code in player.
    /// </summary>
    public const string NO_TRIMMER = "NO_TRIMMER";

    // -------- Building --------

    /// <summary>
    /// The type of the current build.
    /// </summary>
    public static TrimmerBuildType BuildType { get; private set; }

    /// <summary>
    /// Populate the `BuildPlayerOptions` with default values.
    /// </summary>
    public static BuildPlayerOptions GetDefaultOptions(BuildTarget target)
    {
        var playerOptions = new BuildPlayerOptions();
        playerOptions.target = target;
        playerOptions.targetGroup = BuildPipeline.GetBuildTargetGroup(target);

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
    /// Based on BuildPlayerWindow.PickBuildLocation in private Unity engine code.
    /// </remarks>
    static string PickBuildLocation(BuildTarget target)
    {
        var buildLocation = EditorUserBuildSettings.GetBuildLocation(target);

        if (target == BuildTarget.Android && EditorUserBuildSettings.exportAsGoogleAndroidProject) {
            var location = EditorUtility.SaveFolderPanel("Export Google Android Project", buildLocation, "");
            return location;
        }

        string directory = "", filename = "";
        if (!string.IsNullOrEmpty(buildLocation)) {
            directory = Path.GetDirectoryName(buildLocation);
            filename = Path.GetFileName(buildLocation);
        }

        // Call internal method:
        // string SaveBuildPanel(BuildTarget target, string title, string directory, string defaultName, string extension, out bool updateExistingBuild)
        var method = typeof(EditorUtility).GetMethod("SaveBuildPanel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null) {
            Debug.LogError("Could not find SaveBuildPanel method on EditorUtility class.");
            return null;
        }

        var args = new object[] { target, "Build " + target, directory, filename, "", null };
        var path = (string)method.Invoke(null, args);

        return path;
    }

    /// <summary>
    /// Entry point for Unity Cloud builds.
    /// </summary>
    /// <remarks>
    /// If you don't configure anything, Unity Cloud Build will build without a 
    /// profile, all Options will use their default value and will be removed.
    /// 
    /// Add the name of the Build Profile you want to build with to the target name
    /// in Unity Cloud Build, enclosed in double underscores, e.g. `__Profile Name__`.
    /// Note that since the target name can contain only alphanumeric characters,
    /// spaces, dashes and underscores, those characters cannot appear in the profile
    /// name either.
    /// 
    /// Also note that the ability for Options to set build options is limited,
    /// currently only setting a custom  scene list is supported.
    /// </remarks>
    public static void UnityCloudBuild(BuildManifestObject manifest)
    {
        BuildType = TrimmerBuildType.CloudBuild;

        Debug.Log("UnityCloudBuild: Parsing profile name...");

        // Get profile name from could build target name
        string targetName;
        if (!manifest.TryGetValue("cloudBuildTargetName", out targetName)) {
            Debug.LogError("Could not get target name from cloud build manifest.");
        } else {
            var match = Regex.Match(targetName, @"__([\w\-. ]+)__");
            if (match.Success) {
                targetName = match.Groups[1].Value;
                Debug.Log("Parsed build profile name from target name: " + targetName);
            }
        }

        Debug.Log("UnityCloudBuild: Looking for profile...");

        BuildProfile buildProfile = null;
        if (!string.IsNullOrEmpty(targetName)) {
            buildProfile = BuildProfile.Find(targetName);
            if (buildProfile == null) {
                Debug.LogError("Build Profile named '" + targetName + "' could not be found.");
                return;
            }
        }

        if (buildProfile == null) {
            Debug.LogWarning("No Build Profile selected. Add the Build Profile enclosed in double underscores (__) to the target name.");
            return;
        }

        Debug.Log("UnityCloudBuild: Running PrepareBuild callbacks...");

        // Prepare build
        var options = GetDefaultOptions(EditorUserBuildSettings.activeBuildTarget);
        currentProfile = buildProfile;
        currentTarget = options.target;

        // Run options' PrepareBuild
        foreach (var option in GetCurrentEditProfile().OrderBy(o => o.PostprocessOrder)) {
            if ((option.Capabilities & OptionCapabilities.ConfiguresBuild) == 0) continue;
            var inclusion = buildProfile == null ? OptionInclusion.Remove : buildProfile.GetInclusionOf(option, options.target);
            options = option.PrepareBuild(options, inclusion);
        }

        // Cloud Build doesn't allow changing BuildPlayerOptions.extraScriptingDefines,
        // so we have to apply scripting define symbols to player settings
        ApplyScriptingDefineSymbolsToPlayerSettings(buildProfile, options.target);

        Debug.Log("UnityCloudBuild: Apply scenes...");

        // Apply scenes
        if (options.scenes != null && options.scenes.Length > 0) {
            var scenes = new EditorBuildSettingsScene[options.scenes.Length];
            for (int i = 0; i < scenes.Length; i++) {
                scenes[i] = new EditorBuildSettingsScene(
                    options.scenes[i],
                    true
                );
            }
            EditorBuildSettings.scenes = scenes;
        }

        OptionHelper.currentBuildOptions = options;
        Debug.Log("UnityCloudBuild: Done!");
    }

    /// <summary>
    /// Set up a Unity build started using the build player window.
    /// </summary>
    static void UnityDefaultBuild(ref BuildPlayerOptions options)
    {
        BuildType = TrimmerBuildType.BuildWindow;

        currentProfile = EditorProfile.Instance.ActiveProfile;
        currentTarget = options.target;

        AddScriptingDefineSymbols(currentProfile, ref options);

        OptionHelper.currentBuildOptions = options;
    }

    /// <summary>
    /// Set up a non-Trimmer build.
    /// </summary>
    static void NonTrimmerBuild(BuildTarget target)
    {
        BuildType = TrimmerBuildType.NonTrimmer;

        Debug.LogWarning($"Trimmer: Build started using an unsupported method, some Trimmer features will not work.");

        currentProfile = EditorProfile.Instance.ActiveProfile;
        currentTarget = target;

        // We can only react when the build has already started and cannot
        // edit BuildPlayerOptions.extraScriptingDefines, so we have to
        // change PlayerSettings.
        ApplyScriptingDefineSymbolsToPlayerSettings(currentProfile, target);

        OptionHelper.currentBuildOptions = default;
    }

    /// <summary>
    /// Builds complete listener implementation for command line builds
    /// that exits on success and throws on failure.
    /// </summary>
    public class CommandLineBuildsCompleteListener : ScriptableObject, IBuildsCompleteListener
    {
        public void OnComplete(bool success, ProfileBuildResult[] results)
        {
            if (success) {
                EditorApplication.Exit(0);
            } else {
                throw new BuildFailedException("Trimmer build failed");
            }
        }
    }

    /// <summary>
    /// Build the profile specified on the command line or the active profile.
    /// </summary>
    /// <remarks>
    /// You can use this method to automate Unity builds using the command line.
    /// 
    /// Use the following command to build a Build Profile:
    /// `unity -batchmode -executeMethod sttz.Trimmer.Editor.BuildManager.Build -profileName "PROFILE_NAME"`
    /// 
    /// You need to replace `unity` with the path to the Unity executable and `PROFILE_NAME`
    /// with the name of the profile you want to build. Run this in the folder of your
    /// Unity project or add `-projectPath "PATH_TO_PROJECT"` to select one.
    /// 
    /// When doing a command line build, the path set by the profile is used. If the
    /// profile doesn't set a path, add the `-output "PATH"` option to the arguments. 
    /// By default, all build targets of the profile are built, add the `-buildTarget NAME` 
    /// option to build only a single target.
    /// 
    /// See the Unity documentation for [more information on command line usage](https://docs.unity3d.com/Manual/CommandLineArguments.html).
    /// </remarks>
    public static void Build()
    {
        Build(null);
    }

    public static void Build(IBuildsCompleteListener onComplete = null)
    {
        string commandLineBuildPath = null;
        string profileName = null;
        var buildActiveTarget = false;

        if (Application.isBatchMode) {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) {
                if (args[i].EqualsIgnoringCase("-profileName")) {
                    if (i + 1 == args.Length || args[i + 1].StartsWith("-")) {
                        throw new Exception("-profileName needs to be followed by a profile name.");
                    }
                    profileName = args[++i];
                } else if (args[i].EqualsIgnoringCase("-output")) {
                    if (i + 1 == args.Length || args[i + 1].StartsWith("-")) {
                        throw new Exception("-output needs to be followed by a path.");
                    }
                    commandLineBuildPath = args[++i];
                } else if (args[i].EqualsIgnoringCase("-buildTarget")) {
                    // Unity will validate the value of the -buildTarget option
                    buildActiveTarget = true;
                }
            }
        }

        // Throw if command line build failed to cause non-zero exit code
        if (Application.isBatchMode && onComplete == null) {
            onComplete = ScriptableObject.CreateInstance<CommandLineBuildsCompleteListener>();
        }

        BuildProfile profile = null;
        if (Application.isBatchMode && profileName != null) {
            profile = BuildProfile.Find(profileName);
            if (profile == null) {
                var err = "Build profile named '" + profileName + "' could not be found.";
                if (onComplete != null) {
                    onComplete.OnComplete(false, new[] { ProfileBuildResult.Error(null, err) });
                } else {
                    Debug.LogError(err);
                }
                return;
            }

            Debug.Log("Building " + profile.name + ", selected from command line.");
        }

        if (profile == null) {
            if (EditorProfile.Instance.ActiveProfile == null) {
                var err = "No profile specified and not active profile set: Nothing to build";
                if (onComplete != null) {
                    onComplete.OnComplete(false, new[] { ProfileBuildResult.Error(null, err) });
                } else {
                    Debug.LogError(err);
                }
                return;
            }
            profile = EditorProfile.Instance.ActiveProfile;
            Debug.Log("Building active profile.");
        }

        BuildRunner.Job[] jobs;
        if (buildActiveTarget) {
            jobs = new[] { 
                new BuildRunner.Job(profile, EditorUserBuildSettings.activeBuildTarget, commandLineBuildPath)
            };
        } else {
            var targets = profile.BuildTargets.ToArray();
            jobs = new BuildRunner.Job[targets.Length];
            for (int i = 0; i < targets.Length; i++) {
                jobs[i] = new BuildRunner.Job() {
                    profile = profile,
                    target = targets[i],
                    outputPath = commandLineBuildPath,
                };
            }
        }

        var runner = ScriptableObject.CreateInstance<BuildRunner>();
        runner.Run(jobs, onComplete, TrimmerPrefs.RestoreActiveBuildTarget && !Application.isBatchMode, context: profile);
    }

    /// <summary>
    /// Build a profile for its default targets and with the default build options.
    /// </summary>
    public static void Build(BuildProfile profile, IBuildsCompleteListener onComplete = null)
    {
        var targets = profile.BuildTargets.ToArray();
        var jobs = new BuildRunner.Job[targets.Length];
        for (int i = 0; i < targets.Length; i++) {
            jobs[i] = new BuildRunner.Job() {
                profile = profile,
                target = targets[i],
            };
        }

        var runner = ScriptableObject.CreateInstance<BuildRunner>();
        runner.Run(jobs, onComplete, TrimmerPrefs.RestoreActiveBuildTarget, context: profile);
    }

    /// <summary>
    /// Build a specific target of a profile with the default build options.
    /// </summary>
    /// <param name="profile">Profile to build</param>
    /// <param name="target">Target to build, needs to be part of profile</param>
    public static void Build(BuildProfile profile, BuildTarget target, IBuildsCompleteListener onComplete = null)
    {
        if (profile.BuildTargets != null && profile.BuildTargets.Any() && !profile.BuildTargets.Contains(target)) {
            var err = $"Build target {target} is not part of the build profile {profile.name}";
            if (onComplete != null) {
                onComplete.OnComplete(false, new[] { ProfileBuildResult.Error(profile, err) });
            } else {
                Debug.LogError(err);
            }
            return;
        }

        var runner = ScriptableObject.CreateInstance<BuildRunner>();
        runner.Run(new[] { new BuildRunner.Job(profile, target) }, onComplete, TrimmerPrefs.RestoreActiveBuildTarget, context: profile);
    }

    /// <summary>
    /// Build a profile with the given build options, synchronously starting the build.
    /// </summary>
    /// <remarks>
    /// The `BuildPlayerOptions` will be passed through the profile's Options'
    /// <see cref="Option.PrepareBuild"/>, which can modify it before the build is started.
    /// 
    /// > [!NOTE]
    /// > If you do not set `options.locationPathName` and no option sets
    /// > it in the `PrepareBuild` callback, then a save dialog will be shown.
    /// 
    /// > [!WARNING]
    /// > With this method it's possible to build for a build target even if this
    /// > target is not the active build target. This can lead to issues where
    /// > build code for the given target is not run.
    /// </remarks>
    public static BuildReport BuildSync(BuildProfile buildProfile, BuildPlayerOptions options)
    {
        // Prepare build
        BuildType = TrimmerBuildType.Profile;
        currentProfile = buildProfile;
        currentTarget = options.target;

        try {
            // Add Trimmer scripting define symbols
            AddScriptingDefineSymbols(buildProfile, ref options);

            // Run options' PrepareBuild
            foreach (var option in GetCurrentEditProfile().OrderBy(o => o.PostprocessOrder)) {
                if ((option.Capabilities & OptionCapabilities.ConfiguresBuild) == 0) continue;
                var inclusion = buildProfile == null ? OptionInclusion.Remove : buildProfile.GetInclusionOf(option, options.target);
                options = option.PrepareBuild(options, inclusion);
            }
        }
        catch (Exception) {
            OnBuildError(null);
            throw;
        }

        // Ask for location if none has been set
        if (string.IsNullOrEmpty(options.locationPathName)) {
            if (Application.isBatchMode) {
                // Cannot pick path in batch mode
                OnBuildError(null);
                throw new Exception($"Trimmer: No build path set by profile or in build player options.");
            } else {
                options.locationPathName = PickBuildLocation(options.target);
                if (string.IsNullOrEmpty(options.locationPathName)) {
                    Debug.Log("Cancelled build location dialog");
                    OnBuildError(null); // Arguably not an error here, but there might still be some cleanup to do
                    return null;
                }
            }
        }

        // Make sure the path has the right extension
        // Call internal method:
        // string PostprocessBuildPlayer.GetExtensionForBuildTarget(BuildTargetGroup targetGroup, BuildTarget target, BuildOptions options)
        var PostprocessBuildPlayer = typeof(BuildPipeline).Assembly.GetType("UnityEditor.PostprocessBuildPlayer");
        if (PostprocessBuildPlayer == null) {
            Debug.LogWarning("Could not find PostprocessBuildPlayer to determine build file extension.");
        } else {
            var GetExtensionForBuildTarget = PostprocessBuildPlayer.GetMethod(
                "GetExtensionForBuildTarget", 
                BindingFlags.Public | BindingFlags.Static, 
                null,
                new Type[] { typeof(BuildTargetGroup), typeof(BuildTarget), typeof(BuildOptions) },
                null
            );
            if (GetExtensionForBuildTarget == null) {
                Debug.LogWarning("Could not find GetExtensionForBuildTarget to determine build file extension.");
            } else {
                var args = new object[] { options.targetGroup, options.target, options.options };
                var ext = (string)GetExtensionForBuildTarget.Invoke(null, args);

                var current = Path.GetExtension(options.locationPathName);
                if (current.Length > 0) {
                    current = current.Substring(1); // Remove leading dot
                }

                if (!string.IsNullOrEmpty(ext) 
                        && Path.GetExtension(options.locationPathName).EqualsIgnoringCase(current)) {
                    options.locationPathName += "." + ext;
                }
            }
        }

        // Run the build
        OptionHelper.currentBuildOptions = options;

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded) {
            OnBuildError(report);
        } else {
            Debug.Log(string.Format("Trimmer: Built {0} to '{1}'", options.target, options.locationPathName));
            buildProfile.SetLastBuildPath(options.target, options.locationPathName);
        }

        currentProfile = null;
        currentTarget = BuildTarget.NoTarget;
        OptionHelper.currentBuildOptions = default;
        return report;
    }

    // -------- BuildInfo --------

    /// <summary>
    /// Generate the BuildInfo for the current build.
    /// </summary>
    static void GenerateBuildInfo(GUID buildGuid)
    {
        var profileGuid = "";
        if (currentProfile != null) {
            var path = AssetDatabase.GetAssetPath(currentProfile);
            profileGuid = AssetDatabase.AssetPathToGUID(path);
        }

        if (buildGuid.Empty()) {
            buildGuid = GUID.Generate();
        }

        BuildInfo.Current = new BuildInfo() {
            version = Version.ProjectVersion,
            profileGuid = profileGuid,
            buildTime = DateTime.UtcNow.ToString("o"),
            buildGuid = buildGuid.ToString()
        };
    }

    // -------- Profiles --------

    /// <summary>
    /// The build profile used for the current build.
    /// </summary>
    static BuildProfile currentProfile;
    /// <summary>
    /// 
    /// </summary>
    static BuildTarget currentTarget = BuildTarget.NoTarget;

    /// <summary>
    /// Create and configure the <see cref="ProfileContainer"/> during the build.
    /// </summary>
    static void InjectProfileContainer(Scene scene)
    {
        ProfileContainer.Instance = null;

        if (!includesAnyOption)
            return;

        if (!OptionHelper.IsFirstScene(scene))
            return;

        var go = new GameObject("Trimmer");
        var container = go.AddComponent<ProfileContainer>();
        ProfileContainer.Instance = container;
        container.store = GetCurrentEditProfile().Store;
    }

    /// <summary>
    /// Get the edit profile of the current build profile or
    /// the an empty edit profile if there's none.
    /// </summary>
    static RuntimeProfile GetCurrentEditProfile()
    {
        if (currentProfile != null) {
            return currentProfile.EditProfile;
        } else {
            return BuildProfile.EmptyEditProfile;
        }
    }

    /// <summary>
    /// Convenience method to get the current scripting define symbols as a
    /// hash set (instead of a colon-delimited string).
    /// </summary>
    static HashSet<string> GetCurrentScriptingDefineSymbols(BuildTargetGroup targetGroup)
    {
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup).Split(';');
        return new HashSet<string>(defines);
    }

    /// <summary>
    /// Determine all the scripting define symbols set by a build profile for the given build target.
    /// </summary>
    static void GetScriptingDefineSymbols(BuildProfile buildProfile, BuildTarget target, HashSet<string> symbols)
    {
        var includesAnyOption = false;

        foreach (var option in GetCurrentEditProfile().OrderBy(o => o.PostprocessOrder)) {
            var inclusion = buildProfile == null ? OptionInclusion.Remove : buildProfile.GetInclusionOf(option, target);
            includesAnyOption |= ((inclusion & OptionInclusion.Option) != 0);

            option.GetScriptingDefineSymbols(inclusion, symbols);
        }

        if (!includesAnyOption) {
            symbols.Add(NO_TRIMMER);
        }
    }

    /// <summary>
    /// Add Trimmer's scripting define symbols to the build player option's extraScriptingDefines.
    /// </summary>
    static void AddScriptingDefineSymbols(BuildProfile buildProfile, ref BuildPlayerOptions options)
    {
    #if !UNITY_2020_1_OR_NEWER
        // Before Unity 2020.1 BuildPlayerOptions.extraScriptingDefines didn't exist,
        // fall back to changing player settings
        ApplyScriptingDefineSymbolsToPlayerSettings(buildProfile, options.target);
    #else
        var symbols = new HashSet<string>();

        if (options.extraScriptingDefines != null) {
            symbols.AddRange(options.extraScriptingDefines);
        }

        GetScriptingDefineSymbols(buildProfile, options.target, symbols);

        options.extraScriptingDefines = symbols.ToArray();
    #endif
    }

    /// <summary>
    /// For builds where BuildPlayerOptions is not available, apply the
    /// required scripting define symbols to the the player settings.
    /// </summary>
    static void ApplyScriptingDefineSymbolsToPlayerSettings(BuildProfile buildProfile, BuildTarget target)
    {
        // Run options' PreprocessBuild and collect scripting define symbols
        var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
        previousScriptingDefineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        var symbols = GetCurrentScriptingDefineSymbols(targetGroup);

        // Remove all symbols previously added by Trimmer
        symbols.RemoveWhere(d => d.StartsWith(Option.DEFINE_PREFIX));
        symbols.Remove(NO_TRIMMER);

        // Add Trimmer's symbols
        GetScriptingDefineSymbols(buildProfile, target, symbols);

        // Apply scripting define symbols
        PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", symbols.ToArray()));
    }

    /// <summary>
    /// Restore previously changed scripting define symbols in player settings.
    /// </summary>
    static void RestoreScriptingDefineSymbolsInPlayerSettings(BuildTarget target)
    {
        if (previousScriptingDefineSymbols == null) return;

        var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, previousScriptingDefineSymbols);

        previousScriptingDefineSymbols = null;
    }

    // ------ Unity Callbacks ------

    static string previousScriptingDefineSymbols;
    static bool includesAnyOption;

    public int callbackOrder { get { return 0; } }

    [InitializeOnLoadMethod]
    static void RegisterBuildPlayerHandler()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerHandler);
    }

    static void BuildPlayerHandler(BuildPlayerOptions options)
    {
        // Show a dialog of no active build profile has been set
        if (currentProfile == null 
            && EditorProfile.Instance.ActiveProfile == null
            && !EditorUtility.DisplayDialog(
                "Trimmer: No Active Profile Set", 
                "There's no active Build Profile set, a null profile will be applied "
                + " and all Options removed.\n\n"
                + "The active profile can be set in Unity's Preferences under 'Trimmer'.", 
                "Continue Anyway", "Cancel"
            )) {
            return;
        }

        UnityDefaultBuild(ref options);
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        var target = report.summary.platform;
        var path = report.summary.outputPath;

        if (BuildType == TrimmerBuildType.None) {
            NonTrimmerBuild(target);
        }

        // Run options' PreprocessBuild
        includesAnyOption = false;
        foreach (var option in GetCurrentEditProfile().OrderBy(o => o.PostprocessOrder)) {
            var inclusion = currentProfile == null ? OptionInclusion.Remove : currentProfile.GetInclusionOf(option, target);
            includesAnyOption |= ((inclusion & OptionInclusion.Option) != 0);

            if ((option.Capabilities & OptionCapabilities.ConfiguresBuild) != 0) {
                option.PreprocessBuild(report, inclusion);
            }
        }

        GenerateBuildInfo(report.summary.guid);

        string defines = null;
    #if UNITY_2020_1_OR_NEWER
        var extraScriptingDefines = OptionHelper.currentBuildOptions.extraScriptingDefines;
        if (extraScriptingDefines != null && extraScriptingDefines.Length > 0) {
            defines = extraScriptingDefines.Join();
        }
    #else
        var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
        defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
    #endif

        Debug.Log(string.Format(
            "Trimmer: Building profile '{0}' for '{1}' to '{2}'\nIncluded: {3}\nSymbols: {4}",
            currentProfile != null ? currentProfile.name : "null", 
            target, path, 
            GetCurrentEditProfile()
                .Where(o => {
                    if (currentProfile == null) return false;
                    var inclusion = currentProfile.GetInclusionOf(o, target);
                    return inclusion.HasFlag(OptionInclusion.Feature) || inclusion.HasFlag(OptionInclusion.Option);
                })
                .Select(o => o.Name)
                .Join(),
            defines
        ));
    }
    
    // Unfortunately not a proper Unity event
    public static void OnBuildError([CanBeNull] BuildReport report)
    {
        foreach (var option in GetCurrentEditProfile().OrderBy(o => o.PostprocessOrder)) {
            if ((option.Capabilities & OptionCapabilities.ConfiguresBuild) == 0) continue;
            try {
                option.OnBuildError(report);
            }
            catch (Exception e) {
                Debug.LogException(e);
            }
        }

        var target = (report != null ? report.summary.platform : currentTarget);
        RestoreScriptingDefineSymbolsInPlayerSettings(target);
        OptionHelper.currentBuildOptions = default;
        BuildType = TrimmerBuildType.None;

        Debug.LogError($"Trimmer: Build failed for platform {target}");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        var target = report.summary.platform;
        var path = report.summary.outputPath;

        // Run options' PostprocessBuild
        foreach (var option in GetCurrentEditProfile().OrderBy(o => o.PostprocessOrder)) {
            if ((option.Capabilities & OptionCapabilities.ConfiguresBuild) == 0) continue;
            var inclusion = currentProfile == null ? OptionInclusion.Remove : currentProfile.GetInclusionOf(option, target);
            option.PostprocessBuild(report, inclusion);
        }

        RestoreScriptingDefineSymbolsInPlayerSettings(target);
        OptionHelper.currentBuildOptions = default;
        BuildType = TrimmerBuildType.None;
    }

    public void OnProcessScene(Scene scene, [CanBeNull] BuildReport report)
    {
        // OnProcessScene is also called when playing in the editor
        if (!BuildPipeline.isBuildingPlayer)
            return;

        // Inject profile container into first scene
        InjectProfileContainer(scene);

        // In an Addressables build, report is null
        var target = (report != null ? report.summary.platform : currentTarget);

        // Inject profile and call PostprocessScene, Apply() isn't called during build
        foreach (var option in GetCurrentEditProfile().OrderBy(o => o.PostprocessOrder)) {
            var inclusion = currentProfile == null ? OptionInclusion.Remove : currentProfile.GetInclusionOf(option, target);

            if ((option.Capabilities & OptionCapabilities.ConfiguresBuild) != 0) {
                option.PostprocessScene(scene, inclusion);
            }
        }
    }
}

}
