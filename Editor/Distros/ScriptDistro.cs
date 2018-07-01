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
///   - "{targets}": Space-separated quoted list of build targets
///   - "{paths}": Space-separated quoted list of build paths
///   - "{targetspaths}": Space-separated quoted list of target and path pairs
/// - Individual: Call the script for each build. Variables:
///   - "{target}": Quoted build target name
///   - "{path}": Quoted path to build
/// 
/// Both modes support the "{project}" variable, which is replaced with a quoted string
/// to the proect's Assets folder.
/// </remarks>
[CreateAssetMenu(fileName = "Script Distro.asset", menuName = "Trimmer/Distro/Script")]
public class ScriptDistro : DistroBase
{
    public string scriptPath;
    public string arguments;
    public bool individual;

    protected override IEnumerator DistributeCoroutine(IEnumerable<KeyValuePair<BuildTarget, string>> buildPaths)
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

    string ReplaceVariablesIndividual(string input, KeyValuePair<BuildTarget, string> buildPath)
    {
        input = input.Replace("{target}",  "'" + buildPath.Key.ToString() + "'");
        input = input.Replace("{path}",    "'" + buildPath.Value + "'");
        input = input.Replace("{project}", "'" + Application.dataPath + "'");
        return input;
    }

    string ReplaceVariables(string input, IEnumerable<KeyValuePair<BuildTarget, string>> buildPaths)
    {
        input = input.Replace("{targets}", string.Join(" ", buildPaths.Select(p => "'" + p.Key.ToString() + "'").ToArray()));
        input = input.Replace("{paths}", string.Join(" ", buildPaths.Select(p => "'" + p.Value + "'").ToArray()));
        input = input.Replace("{targetspaths}", string.Join(" ", buildPaths.Select(p => "'" + p.Key.ToString() + "' '" + p.Value + "'").ToArray()));
        input = input.Replace("{project}", Application.dataPath);
        return input;
    }
}

}