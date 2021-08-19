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
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Run a script with the result of a Build Profile build.
/// </summary>
/// <remarks>
/// The distribution supports two main modes, combined with the <see cref="individual"/>
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
[CreateAssetMenu(fileName = "Script Distro.asset", menuName = "Trimmer/Script", order = 100)]
public class ScriptDistro : DistroBase
{
    public string scriptPath;
    public string arguments;
    public bool individual;

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        if (string.IsNullOrEmpty(scriptPath))
            throw new Exception("ScriptDistro: Script path not set.");

        if (individual) {
            task.Report(0, buildPaths.Count());
            foreach (var buildPath in buildPaths) {
                task.Report(0, description: $"Running script {Path.GetFileName(scriptPath)} for {buildPath.target}");
                var args = ReplaceVariablesIndividual(arguments, buildPath);
                await Execute(new ExecutionArgs(scriptPath, args), task);
                task.baseStep++;
            }
        } else {
            task.Report(0, 1, $"Running script {Path.GetFileName(scriptPath)}");
            var args = ReplaceVariables(arguments, buildPaths);
            await Execute(new ExecutionArgs(scriptPath, args), task);
        }
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
