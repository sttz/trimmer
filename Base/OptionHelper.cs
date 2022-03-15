//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if !NO_TRIMMER || UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Trimmer
{

/// <summary>
/// Helper methods for Options.
/// </summary>
/// <remarks>
/// This static class contains methods to be used by Option subclasses
/// that contain common design patterns or utilities.
/// </remarks>
public static class OptionHelper
{
    // ------ Injection ------

    /// <summary>
    /// Name used for the container that holds the singletons
    /// created by <see cref="GetSingleton*"> and <see cref="InjectFeature*"/>.
    /// </summary>
    const string CONTAINER_NAME = "_Trimmer";

    /// <summary>
    /// Get a singleton script instance in the current scene.
    /// Intended for use in Options' <see cref="Option.Apply"/> methods.
    /// </summary>
    /// <remarks>
    /// This method can be used to implement the feature injection design pattern
    /// in Options (together with <see cref="InjectFeature*"/>). In this case the
    /// Option has an associated feature that isn't statically configured in
    /// the project but instead injected on-demand. The feature must be implemented
    /// as a `MonoBehaviour` script that is not added to any scene but instead
    /// exclusively injected and configured by the Option.
    /// 
    /// This method should be used in an Option's <see cref="Option.Apply"/> method 
    /// to inject the feature when playing in the editor or when running a build
    /// with the Option included.
    /// 
    /// Example for a typical implementation:
    /// ```cs
    /// protected bool Validate()
    /// {
    ///     // Check if the Option is properly configured and/or enabled
    ///     return Value &amp;&amp; !string.IsNullOrEmpty(GetChild&lt;OptionChild&gt;().Value);
    /// }
    /// 
    /// public override void Apply()
    /// {
    ///     base.Apply();
    ///     
    ///     var enable = Validate();
    ///     var singleton = OptionHelper.GetSingleton&lt;MyScript>(enable);
    ///     if (singleton != null) {
    ///         singleton.enabled = enable;
    ///         singleton.option = false;
    ///         singleton.otherOption = GetChild&lt;OptionChild>().Value;
    ///     }
    /// }
    /// ```
    /// 
    /// Here, the Option first checks if it is enabled and properly configured
    /// in `Validate`. This is then passed to GetSingleton, so that it doesn't
    /// create the singleton if the feature is not enabled.
    /// 
    /// GetSingleton will always return the instance if it exists (even if the
    /// feature is disabled, so that it can be turned off when it was enabled
    /// before). Therefore always check if GetSingleton returns a non-null value
    /// and apply the configuration if it does.
    /// 
    /// Singletons created by this method will be stored on the "_Trimmer"
    /// game object, which is marked `DontDestroyOnLoad`, so that singletons
    /// persist over scene loads.
    /// 
    /// Use <see cref="InjectFeature*"/> in <see cref="Option.PostprocessScene"/> to
    /// inject the script into the build if the Option is not included.
    /// </remarks>
    /// <param name="create">Wether to create the script if it does not exists.</param>
    /// <returns>The script or null if <paramref name="create"/> is <c>false</c> and the script doesn't exist</returns>
    public static T GetSingleton<T>(bool create = true) where T : Component
    {
        var container = GameObject.Find(CONTAINER_NAME);
        if (container == null) {
            if (!create) return null;
            container = new GameObject(CONTAINER_NAME);
            container.AddComponent<InjectionContainer>();
        }

        var script = container.GetComponent<T>();
        if (script == null) {
            if (!create) return null;
            script = container.AddComponent<T>();
        }

        return script;
    }

    #if UNITY_EDITOR

    /// <summary>
    /// The options used to start the current Trimmer build (if any).
    /// </summary>
    public static BuildPlayerOptions currentBuildOptions;

    /// <summary>
    /// Check if the given scene is the first scene in the current build.
    /// </summary>
    /// <remarks>
    /// When doing a build with custom scenes set in `BuildPlayerOptions`,
    /// `scene.buildIndex` does not reflect the correct index.
    /// This method correctly checks the scenes of the current (Trimmer) build.
    /// </remarks>
    public static bool IsFirstScene(Scene scene)
    {
        var scenes = currentBuildOptions.scenes;
        if (scenes != null && scenes.Length > 0) {
            return (scenes[0] == scene.path);
        } else if (EditorBuildSettings.scenes.Length > 0) {
            return (EditorBuildSettings.scenes[0].path == scene.path);
        } else {
            return false;
        }
    }

    /// <summary>
    /// Inject a singleton script in a build.
    /// Intended for use in Options' <see cref="Option.PostprocessScene"/> methods.
    /// </summary>
    /// <remarks>
    /// See <see cref="GetSingleton*"/> for a introductory explanation on how to 
    /// implement the feature injection design pattern.
    /// 
    /// InjectFeature is used in conjunction with GetSingleton. It's needed in case
    /// a build includes an Option's associated feature but not the Option itself.
    /// If the Option is included, it can take care of injecting the feature at
    /// runtime in the build. But if only the feature is included, it needs to be
    /// injected at build-time.
    /// 
    /// This method should be used in an Option's <see cref="Option.PostprocessScene"/> 
    /// to inject the feature into the build at build-time when required.
    /// 
    /// Example for a typical implementation:
    /// ```cs
    /// protected bool Validate()
    /// {
    ///     // Check if the Option is properly configured and/or enabled
    ///     return Value &amp;&amp; !string.IsNullOrEmpty(GetChild&lt;OptionChild&gt;().Value);
    /// }
    /// 
    /// #if UNITY_EDITOR
    /// 
    /// override public bool ShouldIncludeOnlyFeature()
    /// {
    ///     // Removes the feature if it's improperly configured.
    ///     // Otherwise the feature will always be included even if misconfigured/disabled.
    ///     return Validate();
    /// }
    /// 
    /// override public void PostprocessScene(Scene scene, OptionInclusion inclusion)
    /// {
    ///     base.PostprocessScene(scene, inclusion);
    /// 
    ///     var singleton = OptionHelper.InjectFeature&lt;MyScript&gt;(scene, inclusion);
    ///     if (singleton != null) {
    ///         singleton.option = false;
    ///         singleton.otherOption = GetChild&lt;OptionChild&gt;().Value;
    ///     }
    /// }
    /// 
    /// #endif
    /// ```
    /// 
    /// Here overriding ShouldIncludeOnlyFeature takes care of checking if only
    /// the feature is included and removing it when it's not enabled/configured.
    /// InjectFeature then adds the singleton to the first scene so that it will
    /// be loaded in the build.
    /// </remarks>
    /// <param name="scene">Pass in the `scene` parameter from <see cref="Option.PostprocessScene"/></param>
    /// <param name="inclusion">Pass in the `inclusion` parameter from <see cref="Option.PostprocessScene"/></param>
    /// <returns>The script if it's injected or null</returns>
    public static T InjectFeature<T>(Scene scene, OptionInclusion inclusion) where T : Component
    {
        // We only inject when the feature is included but the Option is not
        if (!inclusion.HasFlag(OptionInclusion.Feature) || inclusion.HasFlag(OptionInclusion.Option))
            return null;

        // We only inject to the first scene, because DontDestroyOnLoad is set,
        // the script will persist through scene loads
        if (!IsFirstScene(scene))
            return null;

        return GetSingleton<T>(true);
    }

    /// <summary>
    /// Run an external process/script with given arguments and wait for it to exit.
    /// </summary>
    /// <param name="path">Path to the script (absolute, relative to project directory or on the PATH)</param>
    /// <param name="arguments">Arguments to pass to the script</param>
    /// <returns>`true` if the script runs successfully, `false` on error (details will be logged)</returns>
    public static bool RunScript(string path, string arguments)
    {
        string output, error;

        var exitCode = RunScript(path, arguments, out output, out error);
        if (exitCode != 0) {
            Debug.LogError("RunScript: " + Path.GetFileName(path) + " returned error: " + error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Run an external process/script with given arguments, wait for it to exit
    /// and capture its output.
    /// </summary>
    /// <param name="path">Path to the script (absolute, relative to project directory or on the PATH)</param>
    /// <param name="arguments">Arguments to pass to the script</param>
    /// <param name="output">The standard output of the script</param>
    /// <returns>`true` if the script runs successfully, `false` on error (details will be logged)</returns>
    public static bool RunScript(string path, string arguments, out string output)
    {
        string error;

        var exitCode = RunScript(path, arguments, out output, out error);
        if (exitCode != 0) {
            Debug.LogError("RunScript: " + Path.GetFileName(path) + " returned error: " + error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Run an external process/script with given arguments, wait for it to exit
    /// and capture its output and/or errors.
    /// </summary>
    /// <param name="path">Path to the script (absolute, relative to project directory or on the PATH)</param>
    /// <param name="arguments">Arguments to pass to the script</param>
    /// <param name="output">The standard output of the script</param>
    /// <param name="error">The standard error of the script</param>
    /// <returns>The exit code of the script.</returns>
    public static int RunScript(string path, string arguments, out string output, out string error)
    {
        return RunScript(path, arguments, null, out output, out error);
    }

    /// <summary>
    /// Run an external process/script with given arguments, write the given input
    /// to its standard input, wait for it to exit and capture its output and/or errors.
    /// </summary>
    /// <param name="path">Path to the script (absolute, relative to project directory or on the PATH)</param>
    /// <param name="arguments">Arguments to pass to the script</param>
    /// <param name="input">Input written to the script's standard input (or null)</param>
    /// <param name="output">The standard output of the script</param>
    /// <param name="error">The standard error of the script</param>
    /// <returns>The exit code of the script.</returns>
    public static int RunScript(string path, string arguments, string input, out string output, out string error)
    {
        output = null;

        if (string.IsNullOrEmpty(path)) {
            error = "RunScript: path null or empty";
            return 127;
        }

        var scriptName = Path.GetFileName(path);
        var script = new System.Diagnostics.Process();
        script.StartInfo.UseShellExecute = false;
        script.StartInfo.RedirectStandardOutput = true;
        script.StartInfo.RedirectStandardError = true;
        script.StartInfo.FileName = path;
        script.StartInfo.Arguments = arguments;

        if (!string.IsNullOrEmpty(input)) {
            script.StartInfo.RedirectStandardInput = true;
        }

        var outputBuilder = new StringBuilder();
        script.OutputDataReceived += (s, a) => {
            outputBuilder.AppendLine(a.Data);
        };
        var errorBuilder = new StringBuilder();
        script.ErrorDataReceived += (s, a) => {
            errorBuilder.AppendLine(a.Data);
        };

        try {
            script.Start();

            script.BeginOutputReadLine();
            script.BeginErrorReadLine();

            if (!string.IsNullOrEmpty(input)) {
                // Unity's old Mono runtime writes a BOM to the input stream,
                // tripping up the command. Create a new writer with an encoding
                // that has BOM disabled.
                var writer = new StreamWriter(script.StandardInput.BaseStream, new System.Text.UTF8Encoding(false));
                writer.Write(input);
                writer.Close();
            }

            script.WaitForExit();
        } catch (Exception e) {
            error = "RunScript: Exception running " + scriptName + ": " + e.Message;
            return -1;
        }

        output = outputBuilder.ToString();
        error = errorBuilder.ToString();

        return script.ExitCode;
    }

    /// <summary>
    /// Asynchronous RunScript method. Instead of blocking until the script is finished, this method
    /// takes three methods that are called with the output of the script as it's being read as well
    /// as one with the exit code once the script has finished.
    /// </summary>
    /// <param name="path">Path to the script (absolute, relative to project directory or on the PATH)</param>
    /// <param name="arguments">Arguments to pass to the script</param>
    /// <param name="input">Input written to the script's standard input (or null)</param>
    /// <param name="onOutput">Method called with each output line</param>
    /// <param name="onError">Method called with each error line</param>
    /// <param name="onExit">Method called with the exit code</param>
    /// <returns>A callback that can be used to stop the script (parameter: false = terminate, true = kill)</returns>
    public static Action<bool> RunScriptAsnyc(string path, string arguments, string input, Action<string> onOutput, Action<string> onError, Action<int> onExit)
    {
        if (string.IsNullOrEmpty(path)) {
            if (onError != null) onError("RunScript: path null or empty");
            return null;
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.FileName = path;
        startInfo.Arguments = arguments;

        return RunScriptAsnyc(startInfo, input, onOutput, onError, onExit);
    }

    /// <summary>
    /// Asynchronous RunScript method. Instead of blocking until the script is finished, this method
    /// takes three methods that are called with the output of the script as it's being read as well
    /// as one with the exit code once the script has finished.
    /// </summary>
    /// <param name="startInfo">Process start configuration (note that UseShellExecute, RedirectStandardOutput and RedirectStandardError will be overwritten)</param>
    /// <param name="input">Input written to the script's standard input (or null)</param>
    /// <param name="onOutput">Method called with each output line</param>
    /// <param name="onError">Method called with each error line</param>
    /// <param name="onExit">Method called with the exit code</param>
    /// <returns>A callback that can be used to stop the script (parameter: false = terminate, true = kill)</returns>
    public static Action<bool> RunScriptAsnyc(System.Diagnostics.ProcessStartInfo startInfo, string input, Action<string> onOutput, Action<string> onError, Action<int> onExit)
    {
        var scriptName = Path.GetFileName(startInfo.FileName);

        var script = new System.Diagnostics.Process();
        script.StartInfo = startInfo;
        script.StartInfo.UseShellExecute = false;
        script.StartInfo.RedirectStandardOutput = true;
        script.StartInfo.RedirectStandardError = true;
        script.EnableRaisingEvents = true;

        if (!string.IsNullOrEmpty(input)) {
            script.StartInfo.RedirectStandardInput = true;
        }

        script.OutputDataReceived += (s, a) => {
            if (onOutput != null) {
                onOutput(a.Data);
            }
        };
        script.ErrorDataReceived += (s, a) => {
            if (onError != null) {
                onError(a.Data);
            }
        };
        script.Exited += (s, a) => {
            if (onExit != null) {
                // Wait for stdout and stderr to flush
                script.WaitForExit();
                onExit(script.ExitCode);
            }
        };

        try {
            script.Start();

            script.BeginOutputReadLine();
            script.BeginErrorReadLine();

            if (!string.IsNullOrEmpty(input)) {
                // Unity's old Mono runtime writes a BOM to the input stream,
                // tripping up the command. Create a new writer with an encoding
                // that has BOM disabled.
                var writer = new StreamWriter(script.StandardInput.BaseStream, new System.Text.UTF8Encoding(false));
                writer.Write(input);
                writer.Close();
            }
        } catch (Exception e) {
            if (onError != null) onError("RunScript: Exception running " + scriptName + ": " + e.Message);
            return null;
        }

        return (kill) => {
            if (script.HasExited) return;
            if (kill) {
                script.Kill();
            } else {
                script.CloseMainWindow();
            }
        };
    }

    #endif

    // -------- Plugin Removal --------

    #if UNITY_EDITOR

    static bool DontIncludeInBuild(string path)
    {
        return false;
    }

    /// <summary>
    /// Prevent plugins from being included in the build. This method should be called
    /// from an Option's <see cref="Option.PreprocessBuild"/>.
    /// </summary>
    /// <remarks>
    /// This method uses `PluginImporter.SetIncludeInBuildDelegate` to prevent the plugins
    /// from being included. This may interfere with other scripts using the same method.
    /// </remarks>
    /// <param name="pluginGuids">The GUIDs of the plugins to exclude from the current build.</param>
    static public void RemovePluginsFromBuild(IEnumerable<string> pluginGuids)
    {
        foreach (var guid in pluginGuids) {
            RemovePluginFromBuild(guid);
        }
    }

    static public void RemovePluginFromBuild(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) {
            Debug.LogWarning("RemovePluginsFromBuild: No plugin found for GUID: " + guid);
            return;
        }

        var importer = AssetImporter.GetAtPath(path) as PluginImporter;
        if (importer == null) {
            Debug.LogWarning("RemovePluginsFromBuild: No plugin importer for path: '" + path + "' (guid: " + guid + ")");
            return;
        }

        importer.SetIncludeInBuildDelegate(DontIncludeInBuild);
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

    // -------- Build Paths --------

    /// <summary>
    /// Build paths returned from Unity can point either to the executable
    /// (e.g. exe or app bundle) or the root directory of the export 
    /// (e.g. XCode project). This method turns the first into the second
    /// and leaves the second unchanged, so you can get the base directory
    /// for all build targets.
    /// </summary>
    /// <param name="buildPath"></param>
    /// <returns></returns>
    public static string GetBuildBasePath(string buildPath)
    {
        if (File.Exists(buildPath) || Path.GetExtension(buildPath) == ".app") {
            return Path.GetDirectoryName(buildPath);
        }

        return buildPath;
    }

    #endif
}

}

#endif
