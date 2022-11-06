//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace sttz.Trimmer
{

[Serializable]
public class BuildInfo
{
    // -------- Info --------

    /// <summary>
    /// Default name of BuildInfo json files.
    /// </summary>
    public const string DEFAULT_NAME = "build.json";

    /// <summary>
    /// Information about the current build.
    /// </summary>
    public static BuildInfo Current { get; set; }

    /// <summary>
    /// Version of the build.
    /// </summary>
    public Version version;
    /// <summary>
    /// GUID of the profile that was used to create the build.
    /// </summary>
    public string profileGuid;
    /// <summary>
    /// Build time as ISO 8601 date string.
    /// </summary>
    public string buildTime;
    /// <summary>
    /// Generated GUID to identify the build.
    /// </summary>
    public string buildGuid;

    // -------- JSON --------

    /// <summary>
    /// Create a BuildInfo instance from a JSON string.
    /// </summary>
    public static BuildInfo FromJson(string json)
    {
        return JsonUtility.FromJson<BuildInfo>(json);
    }

    /// <summary>
    /// Convert the info to a JSON string.
    /// </summary>
    /// <param name="pretty">Format the output for readability</param>
    public string ToJson(bool pretty = true)
    {
        return JsonUtility.ToJson(this, pretty);
    }

#if UNITY_EDITOR

    /// <summary>
    /// Load a BuildInfo from a given file path. The path
    /// can be a Unity build path and the build.json will
    /// be resolved automatically.
    /// </summary>
    /// <param name="path">Path to the build.json or a Unity build.</param>
    public static BuildInfo FromPath(string path)
    {
        string jsonPath = null;

        if (Path.GetExtension(path) == ".json") {
            jsonPath = path;
        } else {
            var basePath = OptionHelper.GetBuildBasePath(path);
            jsonPath = Path.Combine(basePath, DEFAULT_NAME);
        }

        try {
            var json = File.ReadAllText(jsonPath);
            return FromJson(json);
        } catch (Exception e) {
            Debug.LogError("Failed to read BuildInfo from path '" + jsonPath + "': " + e.Message);
            return null;
        }
    }

#endif
}

}
