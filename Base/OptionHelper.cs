using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

/// <summary>
/// Helper methods for Options.
/// </summary>
public static class OptionHelper
{
    // ------ Injection ------

    /// <summary>
    /// Get a singleton script instance in the current scene.
    /// Intended for use in Options' <see cref="Apply"/> methods.
    /// </summary>
    /// <remarks>
    /// Helper methods for Options to inject a feature into the project.
    /// 
    /// This method can be used in Options' <see cref="Apply"/> method, to 
    /// create the feature on-demand or return the existing instance. Scripts 
    /// created by `GetSingleton` are automatically marked `DontDestroyOnLoad`.
    /// 
    /// First determine if the feature is activated based on the Option's value.
    /// Then set the <paramref name="create"/> parameter accordingly to not create
    /// instances when not needed. If the method returns a non-null value, apply
    /// the Option's configuration to the script.
    /// 
    /// Use <see cref="InjectFeature*"/> in <see cref="PostprocessScene"/> to
    /// inject the script into the build if the Option is not included.
    /// </remarks>
    /// <param name="create">Wether to create the script if it not exists.</param>
    /// <param name="containerName">The name of the container the script is created on (defaults to the script's name)</param>
    /// <returns>The script or null if <paramref name="create"/> is <c>false</c> and the script doesn't exist</returns>
    public static T GetSingleton<T>(bool create = true, string containerName = null) where T : Component
    {
        containerName = containerName ?? typeof(T).Name;

        var container = GameObject.Find(containerName);
        if (container == null) {
            if (!create) return null;
            container = new GameObject(containerName);
            UnityEngine.Object.DontDestroyOnLoad(container);
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
    /// Inject a feature script when necessary in an Option's <see cref="PostprocessScene"/> method.
    /// </summary>
    /// <remarks>
    /// This method is analogous to <see cref="GetSingleton*"/>.
    /// 
    /// This method can be used in Options' <see cref="PostprocessScene"/> method to 
    /// inject a feature script when it's needed (only if the feature is included but
    /// the option is not and only in the first scene).
    /// 
    /// Make sure to check if the feature is properly configured before injecting it.
    /// </remarks>
    /// <param name="scene">Pass in the `scene` parameter from <see cref="PostprocessScene"/></param>
    /// <param name="inclusion">Pass in the `inclusion` parameter from <see cref="PostprocessScene"/></param>
    /// <param name="container">The name of the container the script is created on (defaults to the script's name)</param>
    /// <returns>The script if it's injected or null</returns>
    public static T InjectFeature<T>(Scene scene, OptionInclusion inclusion, string containerName = null) where T : Component
    {
        // We only inject when the feature is included but the Option is not
        if (inclusion != OptionInclusion.Feature)
            return null;

        // We only inject to the first scene, because DontDestroyOnLoad is set,
        // the script will persist through scene loads
        if (scene.buildIndex != 0)
            return null;

        return GetSingleton<T>(true, containerName);
    }

    /// <summary>
    /// Simple helepr to run a script with arguments.
    /// </summary>
    /// <param name="path">Path to the script (absolute or relative to project directory)</param>
    /// <param name="arguments">Arguments to pass to the script</param>
    /// <returns>`true` if the script runs successfully, `false` on error (details will be logged)</returns>
    public static bool RunScript(string path, string arguments)
    {
        if (string.IsNullOrEmpty(path)) {
            Debug.LogError("RunScript: path null or empty");
            return false;
        }

        var scriptName = Path.GetFileName(path);

        if (!File.Exists(path)) {
            Debug.LogError("RunScript: " + scriptName + " script not found at '" + path + "'");
            return false;
        }

        var script = new System.Diagnostics.Process();
        script.StartInfo.UseShellExecute = false;
        script.StartInfo.RedirectStandardError = true;
        script.StartInfo.FileName = path;
        script.StartInfo.Arguments = arguments;

        try {
            script.Start();
            script.WaitForExit();
        } catch (Exception e) {
            Debug.LogError("RunScript: Exception running " + scriptName + ": " + e.Message);
            return false;
        }

        if (script.ExitCode != 0) {
            Debug.LogError("RunScript: " + scriptName + " returned error: " + script.StandardError.ReadToEnd());
            return false;
        }

        return true;
    }

    #endif

    // -------- Plugin Removal --------

    #if UNITY_EDITOR

    class PluginDescription
    {
        public string[] deployPaths;
        public string[] extensions;
    }

    static PluginDescription pluginsOSX = new PluginDescription() {
        deployPaths = new string[] { "Contents/Plugins" },
        extensions = new string[] { ".bundle" }
    };
    static PluginDescription pluginsWindows = new PluginDescription() {
        deployPaths = new string[] {
            "",
            "{Product}_Data/Plugins", 
        },
        extensions = new string[] { ".dll" }
    };
    static PluginDescription pluginsLinux = new PluginDescription() {
        deployPaths = new string[] { 
            "{Product}_Data/Plugins", 
            "{Product}_Data/Plugins/x86", 
            "{Product}_Data/Plugins/x86_64", 
        },
        extensions = new string[] { ".so" }
    };

    static Dictionary<BuildTarget, PluginDescription> pluginDescs
        = new Dictionary<BuildTarget, PluginDescription>() {

        { BuildTarget.StandaloneOSXIntel, pluginsOSX },
        { BuildTarget.StandaloneOSXIntel64, pluginsOSX },
        { BuildTarget.StandaloneOSXUniversal, pluginsOSX },

        { BuildTarget.StandaloneWindows, pluginsWindows },
        { BuildTarget.StandaloneWindows64, pluginsWindows },

        { BuildTarget.StandaloneLinux, pluginsLinux },
        { BuildTarget.StandaloneLinux64, pluginsLinux },
        { BuildTarget.StandaloneLinuxUniversal, pluginsLinux },
    };

    /// <summary>
    /// Remove a plugin from a build.
    /// </summary>
    /// <remarks>
    /// A feature that an Option is configuring might use native plugins that Unity
    /// always includes in builds that the plugin supports. Depending on the Option's
    /// configuration, the feature using the native plugins might be removed completely
    /// but the native plugins will still remain in the build.
    /// 
    /// This helper method can be used by Options to remove plugins from builds after
    /// the fact, i.e. in the Option's <see cref="sttz.Workbench.Option.PostprocessBuild*"/>
    /// callback.
    /// 
    /// Note that this method currently only supports removing plugins from standalone
    /// build targets.
    /// </remarks>
    public static void RemovePluginFromBuild(BuildTarget target, string pathToBuiltProject, Regex pluginNameMatch)
    {
        // TODO: Check out Unity 2017.2's PluginImporter.SetIncludeInBuildDelegate,
        // which could potentially replace this functionality

        PluginDescription desc;
        if (!pluginDescs.TryGetValue(target, out desc)) {
            Debug.LogError(string.Format("Build target {0} not supported for plugin removal.", target));
            return;
        }

        if (File.Exists(pathToBuiltProject)) {
            pathToBuiltProject = System.IO.Path.GetDirectoryName(pathToBuiltProject);
        }

        foreach (var pathTemplate in desc.deployPaths) {
            var path = pathTemplate.Replace("{Product}", PlayerSettings.productName);
            path = System.IO.Path.Combine(pathToBuiltProject, path);

            if (!Directory.Exists(path)) {
                Debug.Log("Plugin path does not exist: " + path);
                continue;
            }

            foreach (var entry in Directory.GetFileSystemEntries(path)) {
                var extension = System.IO.Path.GetExtension(entry);
                if (!desc.extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) {
                    Debug.Log("Extension does not match: " + entry);
                    continue;
                }

                var fileName = System.IO.Path.GetFileNameWithoutExtension(entry);
                if (!pluginNameMatch.IsMatch(fileName)) {
                    Debug.Log("Name does not match: " + entry);
                    continue;
                }

                Debug.Log("Removing plugin: " + entry);
                if (File.Exists(entry))
                    File.Delete(entry);
                else
                    Directory.Delete(entry, true);
            }
        }
    }

    #endif
}

}