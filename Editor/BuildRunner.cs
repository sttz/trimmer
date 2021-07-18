//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// The result of a single build.
/// </summary>
[Serializable]
public struct ProfileBuildResult
{
    /// <summary>
    /// The profile that was built.
    /// </summary>
    public BuildProfile profile;
    /// <summary>
    /// The Trimmer error, if any.
    /// </summary>
    public string error;
    /// <summary>
    /// The build report, if any.
    /// </summary>
    public BuildReport report;

    /// <summary>
    /// Construct a result that represents a Trimmer error.
    /// </summary>
    public static ProfileBuildResult Error(BuildProfile profile, string error)
    {
        return new ProfileBuildResult() {
            profile = profile,
            error = error,
            report = null,
        };
    }
}

/// <summary>
/// Interface for ScriptableObjects receiving the build complete callback.
/// </summary>
/// <remarks>
/// This interface needs to be implemented by a ScriptableObject subclass.
/// While waiting for the build result, the scriptable object's hide flags
/// will be changed to `HideAndDontSave` and restored before `OnComplete`
/// is called. This required for the scriptable object to survive domain
/// reloads and asset garbage collection.
/// 
/// > [!NOTE]
/// > The listener instance will go through serialization and deserialization
/// > during the domain reload. The regular Unity serialization rules apply,
/// > meaning some fields will not survive the domain reload. This e.g.
/// > means that the listener instance cannot hold a reference to a delegate.
/// </remarks>
public interface IBuildsCompleteListener
{
    /// <summary>
    /// Method called when the builds have completed or failed.
    /// </summary>
    /// <param name="success">Wether the builds were completed successfully</param>
    /// <param name="results">The results of the individual builds, including build reports</param>
    void OnComplete(bool success, ProfileBuildResult[] results);
}

/// <summary>
/// Build runner that's able to switch the active build platform,
/// survive the resulting domain reload and then do a build.
/// </summary>
/// <remarks>
/// This works around issues with faulty editor code that uses 
/// platform-dependent conditional compilation in build code.
/// This breaks down as soon as you try to do cross-building, 
/// e.g. build for Android while the active build platform is iOS.
/// In this case, `UNITY_ANDROID` is not set and any Android-specific
/// editor code that uses that define will not execute.
/// This is unfortunately a pretty common issue, even happens in
/// Unity's own code.
/// 
/// The best-practice would be to never use platform-specific
/// conditional compilation in build code. Instead, check for the 
/// current build target at runtime.
/// 
/// But we cannot depend on all code following this best-practice.
/// So we need to switch the active build platform and do a domain
/// reload before each build.
/// 
/// To deliver the completion event, delegates cannot be used because
/// they can't be serialized and thus survive domain reloads.
/// Instead, ScriptableObjects implementing a given interface are
/// used.
/// </remarks>
public class BuildRunner : ScriptableObject
{
    /// <summary>
    /// Single build executed by the runner.
    /// </summary>
    [Serializable]
    public struct Job
    {
        /// <summary>
        /// The profile to build.
        /// </summary>
        public BuildProfile profile;
        /// <summary>
        /// The target of the profile to build.
        /// </summary>
        public BuildTarget target;
        /// <summary>
        /// Optional output path (can be overwritten by the profile).
        /// </summary>
        public string outputPath;

        public Job(BuildProfile profile, BuildTarget target, string outputPath = null)
        {
            this.profile = profile;
            this.target = target;
            this.outputPath = outputPath;
        }
    }

    /// <summary>
    /// Run the given builds.
    /// </summary>
    public void Run(Job[] jobs, IBuildsCompleteListener listener, bool restoreActiveBuildTarget = true)
    {
        EnsureNotRunning();
        Listener = (ScriptableObject)listener;
        Run(jobs);
    }

    public void Run(Job[] jobs, bool restoreActiveBuildTarget = true)
    {
        if (jobs == null || jobs.Length == 0) {
            throw new Exception($"Trimmer BuildRunner: No jobs given.");
        }

        EnsureNotRunning();

        var results = new ProfileBuildResult[jobs.Length];
        for (int i = 0; i < jobs.Length; i++) {
            var job = jobs[i];
            if (job.profile == null || job.target == 0 || job.target == BuildTarget.NoTarget) {
                throw new Exception($"Trimmer BuildRunner: Invalid job at index {i}: Profile or target not set ({job.profile} / {job.target})");
            }
            results[i].profile = job.profile;
        }

        this.jobs = jobs;
        this.results = results;
        this.restoreActiveTargetTo = EditorUserBuildSettings.activeBuildTarget;
        jobIndex = 0;
        currentTask = Task.Build;
        completeResult = false;

        //Debug.Log($"Trimmer BuildRunner: Got jobs:\n{string.Join("\n", jobs.Select(j => $"- {j.profile?.name ?? "<none>"} {j.target}"))}");

        ContinueDebounced();
    }

    ScriptableObject Listener {
        get {
            return _listener;
        }
        set {
            if (_listener == value) return;

            if (_listener != null) {
                _listener.hideFlags = _previousListenerHideFlags;
            }

            _listener = value;

            if (_listener != null) {
                _previousListenerHideFlags = _listener.hideFlags;
                _listener.hideFlags = HideFlags.HideAndDontSave;
            }
        }
    }
    ScriptableObject _listener;
    HideFlags _previousListenerHideFlags;

    enum Task
    {
        None,
        SwitchingBuildTarget,
        Build,
        RestoreBuildTarget
    }

    BuildTarget restoreActiveTargetTo;
    Job[] jobs;
    ProfileBuildResult[] results;
    int jobIndex;
    Task currentTask;
    bool completeResult;

    void EnsureNotRunning()
    {
        if (jobs != null) {
            throw new Exception($"Trimmer BuildRunner: Cannot run new jobs, already running.");
        }
    }

    void OnEnable()
    {
        hideFlags = HideFlags.HideAndDontSave;

        if (jobs != null) {
            ContinueDebounced();
        }
    }

    void ContinueDebounced()
    {
        //Debug.Log($"Trimmer BuildRunner: Debounce Continue...");

        // Using update because delayedCall doesn't get called in background
        EditorApplication.update += ContinueDebouncedHandler;
    }

    void ContinueDebouncedHandler()
    {
        EditorApplication.update -= ContinueDebouncedHandler;
        Continue();
    }

    void Continue()
    {
        if (jobs == null) {
            throw new Exception($"Trimmer BuildRunner: Cannot continue, not running (jobs == null).");
        }

        if (currentTask == Task.RestoreBuildTarget) {
            Complete(completeResult);
            return;
        }

        var job = jobs[jobIndex];

        if (EditorUserBuildSettings.activeBuildTarget != job.target) {
            if (currentTask == Task.SwitchingBuildTarget) {
                Debug.LogError($"Trimmer BuildRunner: Failed to switch active build target to {job.target}.");
                Complete(false, skipRestore: true);
                return;
            }

            Debug.Log($"Trimmer: Switching active build target to {job.target}");
            currentTask = Task.SwitchingBuildTarget;

            var group = BuildPipeline.GetBuildTargetGroup(job.target);
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, job.target);
            return;
        }

        //Debug.Log($"Trimmer BuildRunner: Building job #{jobIndex} {job.profile?.name ?? "<none>"} {job.target}...");

        currentTask = Task.Build;

        var options = BuildManager.GetDefaultOptions(job.target);
        if (!string.IsNullOrEmpty(job.outputPath))
            options.locationPathName = job.outputPath;

        var report = BuildManager.BuildSync(job.profile, options);
        results[jobIndex].report = report;

        if (report.summary.result != BuildResult.Succeeded) {
            Complete(false);
            return;
        } else {
            jobIndex++;
            if (jobIndex >= jobs.Length) {
                Complete(true);
                return;
            }
        }

        ContinueDebounced();
    }

    void Complete(bool success, bool skipRestore = false)
    {
        //Debug.Log($"Trimmer BuildRunner: Complete!");

        completeResult = success;

        if (!skipRestore && restoreActiveTargetTo != 0 && EditorUserBuildSettings.activeBuildTarget != restoreActiveTargetTo) {
            if (currentTask == Task.RestoreBuildTarget) {
                Debug.LogError($"Trimmer BuildRunner: Failed to restore active build target to {restoreActiveTargetTo}.");
            } else {
                Debug.Log($"Trimmer: Restoring active build target to {restoreActiveTargetTo}");
                currentTask = Task.RestoreBuildTarget;

                var group = BuildPipeline.GetBuildTargetGroup(restoreActiveTargetTo);
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, restoreActiveTargetTo);
                return;
            }
        }

        var results = this.results;

        this.jobs = null;
        this.results = null;
        this.jobIndex = -1;
        this.restoreActiveTargetTo = 0;
        this.completeResult = false;

        if (Listener != null && Listener is IBuildsCompleteListener l) {
            Listener = null;
            l.OnComplete(success, results);
        }

        DestroyImmediate(this);
    }
}

}
