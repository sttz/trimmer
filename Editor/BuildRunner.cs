//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
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
    /// Single job executed by the runner.
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

        /// <summary>
        /// Distribution to run.
        /// </summary>
        public DistroBase distro;

        public Job(BuildProfile profile, BuildTarget target, string outputPath = null)
        {
            this.profile = profile;
            this.target = target;
            this.outputPath = outputPath;
            this.distro = null;
        }

        public Job(DistroBase distro)
        {
            this.distro = distro;
            this.profile = null;
            this.target = BuildTarget.NoTarget;
            this.outputPath = null;
        }
    }

    /// <summary>
    /// The currently running build runner.
    /// </summary>
    public static BuildRunner Current { get; private set; }

    /// <summary>
    /// The currently running job.
    /// </summary>
    public Job CurrentJob => jobs != null && jobIndex >= 0 && jobIndex < jobs.Length ? jobs[jobIndex] : default;

    /// <summary>
    /// The index of the current job.
    /// </summary>
    public int JobIndex => jobIndex;

    /// <summary>
    /// Total count of jobs in the current run.
    /// </summary>
    public int JobCount => jobs != null ? jobs.Length : 0;

    /// <summary>
    /// Run the given builds.
    /// </summary>
    public void Run(Job[] jobs, IBuildsCompleteListener listener, bool restoreActiveBuildTarget = true, UnityEngine.Object context = null)
    {
        EnsureNotRunning();
        Listener = (ScriptableObject)listener;
        Run(jobs, restoreActiveBuildTarget, context);
    }

    public void Run(Job[] jobs, bool restoreActiveBuildTarget = true, UnityEngine.Object context = null)
    {
        if (jobs == null || jobs.Length == 0) {
            DestroyImmediate(this);
            throw new Exception($"Trimmer BuildRunner: No jobs given.");
        }

        EnsureNotRunning();

        var results = new ProfileBuildResult[jobs.Length];
        for (int i = 0; i < jobs.Length; i++) {
            var job = jobs[i];
            if ((job.profile == null || job.target == 0 || job.target == BuildTarget.NoTarget) && job.distro == null) {
                DestroyImmediate(this);
                throw new Exception($"Trimmer BuildRunner: Invalid job at index {i}: Profile or target or distro not set ({job.profile} / {job.target} / {job.distro})");
            }
            results[i].profile = job.profile;
        }

        Current = this;
        this.jobs = jobs;
        this.results = results;
        restoreActiveTargetTo = EditorUserBuildSettings.activeBuildTarget;
        jobIndex = -1;

        token = TaskToken.Start(context?.name ?? "Trimmer", options: Progress.Options.Synchronous);
        token.context = context;
        Progress.SetPriority(token.taskId, Progress.Priority.High);
        token.Report(0, jobs.Length);

        //Debug.Log($"Trimmer BuildRunner: Got jobs:\n{string.Join("\n", jobs.Select(j => $"- {j.profile?.name ?? "<none>"} {j.target}"))}");

        ContinueWith(ContinueTask.NextJob);
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

    const float progressRefreshInterval = 4f;

    enum ContinueTask
    {
        None,
        NextJob,
        BuildAfterSwitchingTarget,
        CompleteAfterSwichingTarget
    }

    BuildTarget restoreActiveTargetTo;
    Job[] jobs;
    ProfileBuildResult[] results;
    int jobIndex;
    ContinueTask continueTask;
    int debounceCount;
    TaskToken token;
    float lastProgressRefresh;

    void EnsureNotRunning()
    {
        if (jobs != null || Current != null) {
            throw new Exception($"Trimmer BuildRunner: Cannot run new jobs, already running.");
        }
    }

    void OnEnable()
    {
        hideFlags = HideFlags.HideAndDontSave;

        if (jobs != null) {
            if (Current != null && Current != this) {
                Debug.LogError($"Trimmer BuildRunner: Multiple build runners, aborting...");
                Complete(false);
                return;
            } else {
                Current = this;
            }

            // Continue with the current task from before the domain reload
            ContinueWith(continueTask);
        }

        EditorApplication.update += EditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
    }

    void EditorUpdate()
    {
        RefreshProgress();
    }

    void RefreshProgress()
    {
        // After 5 seconds, Unity will show a task as "Not Responding"
        // Periodically refresh our tasks to prevent this from happening,
        // as we can't usually report that often.
        if (token.taskId > 0 && Time.realtimeSinceStartup - lastProgressRefresh > progressRefreshInterval) {
            lastProgressRefresh = Time.realtimeSinceStartup;
            var total = Progress.GetCount();
            for (int i = 0; i < total; i++) {
                var id = Progress.GetId(i);
                if (id == token.taskId || Progress.GetParentId(id) == token.taskId) {
                    var step = Progress.GetCurrentStep(id);
                    if (step < 0) {
                        Progress.Report(id, Progress.GetProgress(id));
                    } else {
                        Progress.Report(id, Progress.GetCurrentStep(id), Progress.GetTotalSteps(id));
                    }
                }
            }
        }
    }

    void ContinueWith(ContinueTask task, bool afterDomainRelaod = false)
    {
        continueTask = task;

        if (!afterDomainRelaod) {
            // Using update because delayedCall doesn't get called in background
            debounceCount = 10;
            EditorApplication.update += ContinueDebouncedHandler;
        }
    }

    void ContinueDebouncedHandler()
    {
        debounceCount--;
        if (debounceCount <= 0) {
            EditorApplication.update -= ContinueDebouncedHandler;
            Continue();
        }
    }

    void Continue()
    {
        try {
            if (jobs == null)
                throw new Exception($"Trimmer BuildRunner: Cannot continue, not running (jobs == null).");

            // -- Tasks that do not require a current job
            switch (continueTask) {
                case ContinueTask.NextJob:
                    if (TaskNextJob()) return;
                    break;
                case ContinueTask.CompleteAfterSwichingTarget:
                    if (TaskCompleteAfterSwichingTarget()) return;
                    return;
            }

            if (jobIndex >= jobs.Length)
                throw new Exception($"Trimmer BuildRunner: Cannot continue, job index out of range ({jobIndex} >= {jobs.Length}).");

            // -- Tasks that do require a current job
            var job = jobs[jobIndex];
            if (job.profile != null) {
                TaskBuild(job);
            } else if (job.distro != null) {
                TaskDistribute(job);
            } else {
                throw new Exception($"Trimmer BuildRunner: Invalid job, no profile or distro set.");
            }

        } catch (Exception e) {
            Debug.LogException(e);
            Complete(false);
        }
    }

    bool TaskNextJob()
    {
        jobIndex++;

        if (jobIndex < jobs.Length) {
            return false;
        }

        if (StartRestoreBuildTarget())
            return true;

        Complete(true);

        return true;
    }

    bool TaskCompleteAfterSwichingTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget != restoreActiveTargetTo)
            throw new Exception($"Trimmer BuildRunner: Failed to restore active build target to {restoreActiveTargetTo}.");

        Complete(true);

        return true;
    }

    void TaskBuild(Job job)
    {
        // Handle switching build target when necessary
        var activeTargetMatches = (EditorUserBuildSettings.activeBuildTarget == job.target);
        if (continueTask == ContinueTask.BuildAfterSwitchingTarget && !activeTargetMatches) {
            throw new Exception($"Trimmer BuildRunner: Failed to switch active build target to {job.target}.");
        } else if (!activeTargetMatches) {
            token.Report(jobIndex, description: $"Switching active build target to {job.target}");

            var group = BuildPipeline.GetBuildTargetGroup(job.target);
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, job.target);
            
            ContinueWith(ContinueTask.BuildAfterSwitchingTarget, afterDomainRelaod: true);
            return;
        }

        // Build
        token.Report(jobIndex, description: $"Building {job.target}");
        BuildReport report;
        try {
            var options = BuildManager.GetDefaultOptions(job.target);
            if (!string.IsNullOrEmpty(job.outputPath))
                options.locationPathName = job.outputPath;

            report = BuildManager.BuildSync(job.profile, options);
            results[jobIndex].report = report;
        } catch (Exception e) {
            results[jobIndex] = ProfileBuildResult.Error(job.profile, e.Message);
            throw;
        }

        if (report.summary.result != BuildResult.Succeeded) {
            throw new Exception($"Trimmer BuildRunner: Build failed");
        }

        ContinueWith(ContinueTask.NextJob);
    }

    void TaskDistribute(Job job)
    {
        token.Report(jobIndex, description: $"Running {job.distro.name}");

        var distroToken = token;
        if (jobs.Length > 1) {
            distroToken = token.StartChild(job.distro.name);
        }

        var source = new CancellationTokenSource();
        distroToken.cancellation = source.Token;
        Progress.RegisterCancelCallback(distroToken.taskId, () => {
            source.Cancel();
            return true;
        });

        job.distro.DistributeWithoutBuilding(distroToken)
            .ContinueWith(t => {
                try {
                    if (jobs.Length > 1)
                        distroToken.Remove();

                    if (t.IsFaulted) {
                        Debug.LogException(t.Exception);
                        Complete(false);
                    } else {
                        ContinueWith(ContinueTask.NextJob);
                    }
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    bool StartRestoreBuildTarget()
    {
        if (restoreActiveTargetTo == 0 || EditorUserBuildSettings.activeBuildTarget == restoreActiveTargetTo)
            return false;

        var group = BuildPipeline.GetBuildTargetGroup(restoreActiveTargetTo);
        EditorUserBuildSettings.SwitchActiveBuildTarget(group, restoreActiveTargetTo);

        token.Report(jobIndex, description: $"Restoring active build target to {restoreActiveTargetTo}");
        ContinueWith(ContinueTask.CompleteAfterSwichingTarget, afterDomainRelaod: true);
        return true;
    }

    void Complete(bool success)
    {
        //Debug.Log($"Trimmer BuildRunner: Complete! success = {success}");

        var results = this.results;

        this.jobs = null;
        this.results = null;
        this.jobIndex = -1;
        this.restoreActiveTargetTo = 0;
        Current = null;

        token.Remove();

        if (Listener != null && Listener is IBuildsCompleteListener l) {
            Listener = null;
            l.OnComplete(success, results);
        }

        DestroyImmediate(this);
    }
}

}
