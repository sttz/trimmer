//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if TR_OptionIniFile || UNITY_EDITOR

using System;
using System.IO;
using sttz.Trimmer.Extensions;
using sttz.Trimmer.BaseOptions;
using UnityEngine;

namespace sttz.Trimmer.Options
{

/// <summary>
/// Load an ini file in the player to change option values.
/// </summary>
/// <remarks>
/// Using a ini file is the easiest way to tweak options in a build player
/// since it doesn't require any in-game interface. Ini files are easy to
/// edit and allow to easily enable debug options or tweak the behaviour
/// without having to rebuild.<br/>
/// The main option configures the file name of the ini file. The extension
/// doesn't need to be «ini», it could also be «txt» to make editing easier.<br/>
/// The child option <see cref="OptionSearchPath"/> controls where the ini
/// is loaded from. It's a list of paths separated by «;». The paths are 
/// searched in order and the first file matching the file name is loaded.<br/>
/// Search paths can contain relative paths, in which case they are resolved
/// relative to the player.
/// <br/>
/// Search paths support a few expansions for varying directory paths:<br/>
/// <c>%DataPath%</c> is replaced by <c>Application.dataPath</c><br />
/// <c>%PersistentDataPath%</c> is replaced by <c>Application.persistentDataPath</c><br />
/// <c>%Personal%</c> is replaced by <c>Environment.SpecialFolder.Personal</c><br />
/// <c>%Desktop%</c> is replaced by <c>Environment.SpecialFolder.DesktopDirectory</c><br />
/// </remarks>
[Capabilities(OptionCapabilities.CanIncludeOption)]
public class OptionIniFile : OptionString
{
    protected override void Configure()
    {
        Category = "Configuration";
        DefaultValue = "trimmer.ini";
        ApplyOrder = -1000;
    }

    public override void Apply()
    {
        base.Apply();

        if (!Application.isEditor) {
            LoadIniFile();
        }
    }

    /// <summary>
    /// Look for an ini file in the configured search path(s).
    /// </summary>
    public void LoadIniFile()
    {
        var filename = Value;
        if (string.IsNullOrEmpty(filename)) return;

        var searchPath = GetChild<OptionSearchPath>().Value;
        if (string.IsNullOrEmpty(searchPath)) return;

        var paths = searchPath.Split(';');
        foreach (var path in paths) {
            var iniPath = System.IO.Path.Combine(ExpandPath(path), filename);
            if (File.Exists(iniPath)) {
                LoadIniFile(iniPath);
                break;
            }
        }
    }

    /// <summary>
    /// Load an ini file from the given path.
    /// </summary>
    public void LoadIniFile(string path)
    {
        if (!File.Exists(path)) {
            Debug.LogError("Invalid path: Ini file '" + path + "' does not exist");
            return;
        }

        Debug.Log("Loading Ini file: " + path);

        var data = File.ReadAllText(path);
        IniAdapter.Load(RuntimeProfile.Main.Store, data);
        RuntimeProfile.Main.Load();
    }

    /// <summary>
    /// Replace special variables in a ini file path.
    /// </summary>
    public string ExpandPath(string path)
    {
        path = path.ReplaceCaseInsensitive("%DataPath%", Application.dataPath);
        path = path.ReplaceCaseInsensitive("%PersistentDataPath%", Application.persistentDataPath);
        path = path.ReplaceCaseInsensitive("%Personal%", Environment.GetFolderPath(Environment.SpecialFolder.Personal));
        path = path.ReplaceCaseInsensitive("%Desktop%", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        return path;
    }

    /// <summary>
    /// <see cref="OptionIniFile"/>
    /// </summary>
    public class OptionSearchPath : OptionString
    {
        protected override void Configure()
        {
            DefaultValue = ".";
        }
    }
}

}
#endif
