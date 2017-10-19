using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Helper methods for Options.
/// </summary>
public static class OptionHelper
{
    // -------- Plugin Removal --------

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
}