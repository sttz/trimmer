using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Run a script with the result of a Build Profile build.
/// </summary>
/// <remarks>
/// The distribution supports two main modes, controlled with the <see cref="individual"/>
/// option:
/// - Combined: Call the script only once, independent of how many builds are being
///   processed. Variables:
///   - "{targets}": Space-separated list of quoted build targets
///   - "{paths}": Space-separated list of quoted build paths
///   - "{targetspaths}": Space-separated list of quoted target and path pairs
/// - Individual: Call the script for each build. Variables:
///   - "{target}": Quoted build target name
///   - "{path}": Quoted path to build
/// 
/// Both modes support the "{project}" variable, which is replaced with a quoted string
/// to the project's Assets folder.
/// </remarks>
[CreateAssetMenu(fileName = "Script Distro.asset", menuName = "Trimmer/Distro/Script")]
public class ScriptDistro : DistroBase
{
    public string scriptPath;
    public string arguments;
    public bool individual;

    protected override IEnumerator DistributeCoroutine(IEnumerable<BuildPath> buildPaths, bool forceBuild)
    {
        if (string.IsNullOrEmpty(scriptPath)) {
            Debug.LogError("ScriptDistro: Script path not set.");
            yield return false; yield break;
        }

        if (individual) {
            foreach (var buildPath in buildPaths) {
                var args = ReplaceVariablesIndividual(arguments, buildPath);
                yield return Execute(scriptPath, args);
                var exitcode = GetSubroutineResult<int>();
                if (exitcode != 0) {
                    yield return false; yield break;
                }
            }
        } else {
            var args = ReplaceVariables(arguments, buildPaths);
            yield return Execute(scriptPath, args);
            var exitcode = GetSubroutineResult<int>();
            if (exitcode != 0) {
                yield return false; yield break;
            }
        }

        Debug.Log("ScriptDistro: Script finished.");
        yield return true;
    }

    string ReplaceVariablesIndividual(string input, BuildPath buildPath)
    {
        input = input.Replace("{target}",  "'" + buildPath.target.ToString() + "'");
        input = input.Replace("{path}",    "'" + buildPath.path + "'");
        input = input.Replace("{project}", "'" + Application.dataPath + "'");
        return input;
    }

    string ReplaceVariables(string input, IEnumerable<BuildPath> buildPaths)
    {
        input = input.Replace("{targets}", string.Join(" ", buildPaths.Select(p => "'" + p.target.ToString() + "'").ToArray()));
        input = input.Replace("{paths}", string.Join(" ", buildPaths.Select(p => "'" + p.path + "'").ToArray()));
        input = input.Replace("{targetspaths}", string.Join(" ", buildPaths.Select(p => "'" + p.target.ToString() + "' '" + p.path + "'").ToArray()));
        input = input.Replace("{project}", Application.dataPath);
        return input;
    }
}

}