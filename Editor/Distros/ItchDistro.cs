using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

[CreateAssetMenu(fileName = "Itch Distro.asset", menuName = "Trimmer/Distro/Itch.io")]
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

    static Dictionary<BuildTarget, string> ChannelNames = new Dictionary<BuildTarget, string>() {
        { BuildTarget.StandaloneOSX, "osx" },
        { BuildTarget.StandaloneWindows, "win32" },
        { BuildTarget.StandaloneWindows64, "win64" },
        { BuildTarget.StandaloneLinux, "linux32" },
        { BuildTarget.StandaloneLinux64, "linux64" },
        { BuildTarget.StandaloneLinuxUniversal, "linux" },
        { BuildTarget.Android, "android" },
    };

    protected override IEnumerator DistributeCoroutine(IEnumerable<KeyValuePair<BuildTarget, string>> buildPaths)
    {
        if (!File.Exists(butlerPath)) {
            Debug.LogError("ItchDistro: Butler path not set or file does not exist.");
            yield return false; yield break;
        }

        if (string.IsNullOrEmpty(project)) {
            Debug.LogError("ItchDistro: project not set.");
            yield return false; yield break;
        }

        foreach (var pair in buildPaths) {
            if (!ChannelNames.ContainsKey(pair.Key)) {
                Debug.LogWarning("ItchDistro: Build target " + pair.Key + " not supported, skipping.");
                continue;
            }

            yield return Distribute(pair);
            if (!GetSubroutineResult<bool>()) {
                yield return false; yield break;
            }
        }

        Debug.Log("ItchDistro: Builds uploaded to itch.io.");
        yield return true;
    }

    IEnumerator Distribute(KeyValuePair<BuildTarget, string> buildPath)
    {
        Debug.Log("ItchDistro: Pushing " + buildPath.Key);

        var path = OptionHelper.GetBuildBasePath(buildPath.Value);

        var channel = ChannelNames[buildPath.Key];
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

        yield return Execute(butlerPath, args);

        var exitcode = GetSubroutineResult<int>();
        yield return exitcode == 0;
    }
}

}