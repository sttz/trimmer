//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Base class that only serves to make a combined list for both build profiles and distros.
/// </summary>
public abstract class BatchItem : ScriptableObject {}

/// <summary>
/// Execute a series of builds or distros.
/// </summary>
[CreateAssetMenu(fileName = "Batch Build.asset", menuName = "Trimmer/Batch Build")]
public class BatchBuild : ScriptableObject
{
    /// <summary>
    /// <see cref="BuildProfile"/> and <see cref="DistroBase"/> subclasses
    /// that will be executed in the order of the list.
    /// </summary>
    /// <remarks>
    /// Distros will build their build profiles, so adding their build
    /// profiles is not necessary and can cause duplicate builds, depending 
    /// on <see cref="distroBuildMode"/>.
    /// </remarks>
    public List<BatchItem> jobs;

    /// <summary>
    /// How the build profile of distros will be handled.
    /// </summary>
    public DistroBuildMode distroBuildMode = DistroBuildMode.BuildAll;

    /// <summary>
    /// Run this batch job.
    /// </summary>
    public void Run(IBuildsCompleteListener onComplete = null)
    {
        var runnerJobs = new List<BuildRunner.Job>();
        foreach (var job in jobs) {
            if (job == null) continue;

            if (job is BuildProfile profile) {
                foreach (var target in profile.BuildTargets) {
                    runnerJobs.Add(new BuildRunner.Job(profile, target));
                }

            } else if (job is DistroBase distro) {
                distro.AddBuildJobs(distroBuildMode, runnerJobs);
                runnerJobs.Add(new BuildRunner.Job(distro));
            }
        }

        var runner = ScriptableObject.CreateInstance<BuildRunner>();
        runner.Run(runnerJobs.ToArray(), onComplete, TrimmerPrefs.RestoreActiveBuildTarget, context: this);
    }
}

}
