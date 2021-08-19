//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Distribution to upload builds to itch.io.
/// </summary>
/// <remarks>
/// The distribution relies on itch's butler command line tool: https://itch.io/docs/butler/
/// Download the tool and set <see cref="butlerPath"/> to its location (relative to the project root).
/// 
/// Authentication is handled via butler, which stores an API token on the local machine.
/// Call `butler login` once to authorize it.
/// </remarks>
[CreateAssetMenu(fileName = "Itch Distro.asset", menuName = "Trimmer/Itch.io", order = 100)]
public class ItchDistro : DistroBase
{
    /// <summary>
    /// Path to the itch.io butler binary.
    /// </summary>
    public string butlerPath;
    /// <summary>
    /// Name of the project to push to (in the form "user/name").
    /// </summary>
    public string project;
    /// <summary>
    /// Suffix added to the channel name (preceding dash will be added).
    /// </summary>
    public string channelSuffix;
    /// <summary>
    /// Notarize macOS build.
    /// </summary>
    public NotarizationDistro macNotarization;

    static Dictionary<BuildTarget, string> ChannelNames = new Dictionary<BuildTarget, string>() {
        { BuildTarget.StandaloneOSX, "osx" },
        { BuildTarget.StandaloneWindows, "win32" },
        { BuildTarget.StandaloneWindows64, "win64" },
        { BuildTarget.StandaloneLinux64, "linux64" },
        { BuildTarget.Android, "android" },
        #if !UNITY_2019_2_OR_NEWER
        { BuildTarget.StandaloneLinux, "linux32" },
        { BuildTarget.StandaloneLinuxUniversal, "linux" },
        #endif
    };

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        if (!File.Exists(butlerPath))
            throw new Exception("ItchDistro: Butler path not set or file does not exist.");

        if (string.IsNullOrEmpty(project))
            throw new Exception("ItchDistro: project not set.");

        task.Report(0, buildPaths.Count());

        foreach (var pair in buildPaths) {
            if (!ChannelNames.ContainsKey(pair.target)) {
                Debug.LogWarning("ItchDistro: Build target " + pair.target + " not supported, skipping.");
                continue;
            }

            if (macNotarization != null) {
                await macNotarization.NotarizeIfMac(pair, task);
            }

            await Distribute(pair, task);

            task.baseStep++;
        }
    }

    async Task Distribute(BuildPath buildPath, TaskToken task)
    {
        task.Report(0, description: $"Pushing {buildPath.target}");

        var path = OptionHelper.GetBuildBasePath(buildPath.path);

        var channel = ChannelNames[buildPath.target];
        if (!string.IsNullOrEmpty(channelSuffix)) {
            channel += "-" + channelSuffix;
        }

        var version = Application.version;

        var buildInfo = BuildInfo.FromPath(path);
        if (buildInfo != null) {
            if (!buildInfo.version.IsDefined) {
                Debug.LogWarning("ItchDistro: build.json exists but contains no version.");
            } else {
                version = buildInfo.version.MajorMinorPatchBuild;
            }
        }

        var args = string.Format(
            "push '{0}' '{1}:{2}' --userversion '{3}' --ignore='*.DS_Store' --ignore='build.json'",
            path, project, channel, Application.version
        );

        await Execute(new ExecutionArgs(butlerPath, args), task);
    }
}

}
