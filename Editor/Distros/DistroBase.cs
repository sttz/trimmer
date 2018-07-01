using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

public abstract class DistroBase : ScriptableObject
{
    [HideInInspector] public List<BuildProfile> builds;
    [HideInInspector] public bool forceBuild;

    public virtual bool CanRunWithoutBuilds { get { return false; } }

    public bool IsRunning {
        get {
            return _isRunning;
        }
        protected set {
            if (_isRunning == value)
                return;
            
            _isRunning = value;

            if (_isRunning) {
                EditorApplication.LockReloadAssemblies();
            } else {
                EditorApplication.UnlockReloadAssemblies();
            }
        }
    }
    bool _isRunning;

    public bool HasBuilds()
    {
        if (builds == null || builds.Count == 0) {
            return false;
        }

        foreach (var profile in builds) {
            if (profile == null) continue;
            if (profile.BuildTargets.Any()) return true;
        }

        return false;
    }

    public bool HasAllBuilds()
    {
        foreach (var profile in builds) {
            if (profile == null) continue;
            foreach (var target in profile.BuildTargets) {
                var path = profile.GetLastBuildPath(target);
                if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path))) {
                    return false;
                }
            }
        }

        return true;
    }

    [ContextMenu("Build")]
    public bool Build()
    {
        foreach (var profile in builds) {
            if (profile == null) continue;
            var error = BuildManager.Build(profile);
            if (!string.IsNullOrEmpty(error)) {
                return false;
            }
        }
        return true;
    }

    [ContextMenu("Distribute")]
    public void Distribute()
    {
        RunCoroutine(DistributeCoroutine());
    }

    public virtual void Cancel()
    {
        if (runningScripts != null) {
            foreach (var terminator in runningScripts.ToList()) {
                terminator(true);
            }
        }
    }

    public virtual IEnumerator DistributeCoroutine()
    {
        if (IsRunning) {
            yield return false; yield break;
        }

        IsRunning = true;

        var paths = new Dictionary<BuildTarget, string>();

        foreach (var profile in builds) {
            if (profile == null) continue;
            foreach (var target in profile.BuildTargets) {
                var path = profile.GetLastBuildPath(target);
                if (forceBuild || string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path))) {
                    var options = BuildManager.GetDefaultOptions(target);
                    var error = BuildManager.Build(profile, options);
                    if (!string.IsNullOrEmpty(error)) {
                        IsRunning = false;
                        yield return false; yield break;
                    }
                    path = profile.GetLastBuildPath(target);
                }
                paths[target] = path;
            }
        }

        if (paths.Count == 0 && !CanRunWithoutBuilds) {
            Debug.LogError(name + ": No build paths for distribution");
            IsRunning = false;
            yield return false; yield break;
        }

        yield return DistributeCoroutine(paths);

        IsRunning = false;

        yield return GetSubroutineResult<bool>();
    }

    protected abstract IEnumerator DistributeCoroutine(IEnumerable<KeyValuePair<BuildTarget, string>> buildPaths);

    // -------- Execute Script --------

    protected List<System.Action<bool>> runningScripts;

    /// <summary>
    /// Editor coroutine wrapper for OptionHelper.RunScriptAsnyc.
    /// </summary>
    protected IEnumerator Execute(string path, string arguments, string input = null, System.Action<string> onOutput = null, System.Action<string> onError = null)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.FileName = path;
        startInfo.Arguments = arguments;
        return Execute(startInfo, input, onOutput, onError);
    }

    /// <summary>
    /// Editor coroutine wrapper for OptionHelper.RunScriptAsnyc.
    /// </summary>
    protected IEnumerator Execute(System.Diagnostics.ProcessStartInfo startInfo, string input = null, System.Action<string> onOutput = null, System.Action<string> onError = null)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        int? exitcode = null;
        var terminator = OptionHelper.RunScriptAsnyc(
            startInfo, input,
            (output) => {
                outputBuilder.AppendLine(output);
                if (onOutput != null) onOutput(output);
            },
            (error) => {
                errorBuilder.AppendLine(error);
                if (onError != null) onError(error);
            },
            (code) => {
                exitcode = code;
            }
        );

        if (runningScripts == null) runningScripts = new List<System.Action<bool>>();
        runningScripts.Add(terminator);

        while (exitcode == null) { yield return null; }

        runningScripts.Remove(terminator);

        // 137 happens for Kill() and 143 for CloseMainWindow(),
        // which means the script has ben canceled
        if (exitcode != 0 && exitcode != 137 && exitcode != 143) {
            Debug.LogError(string.Format(
                "{0}: Failed to execute {1}: {2}\nOutput: {3}",
                name, Path.GetFileName(startInfo.FileName),
                errorBuilder.ToString(), outputBuilder.ToString()
            ));
        }
        yield return exitcode;
    }

    // -------- Editor Coroutine --------

    /// <summary>
    /// Editor coroutine runner. It's quite different from Unity's coroutines:
    /// - You can only return null to pause a frame, no WaitForXXX
    /// - You can however return another coroutine and it'll finish that first
    /// - And you can use SubroutineResult to get that coroutine's last yielded value
    /// </summary>
    static public void RunCoroutine(IEnumerator routine)
    {
        Run(routine, true);
    }

    /// <summary>
    /// Get the last yielded value of a subroutine.
    /// This can only be called in the coroutine that yielded the subroutine and
    /// only between after the subroutine finished and the before parent coroutine
    /// yields again.
    /// </summary>
    static public T GetSubroutineResult<T>()
    {
        if (!hasLastRoutineValue) {
            throw new System.Exception("SubroutineResult can only be called in the parent routine right after the subroutine finished.");
        }

        if (lastRoutineValue != null && lastRoutineValue is T) {
            return (T)lastRoutineValue;
        }

        return default(T);
    }

    static List<IEnumerator> routines;
    static Dictionary<IEnumerator, IEnumerator> parentRoutines;
    static Dictionary<IEnumerator, object> lastRoutineValues;
    static bool hasLastRoutineValue;
    static object lastRoutineValue;

    static bool Run(IEnumerator routine, bool advance)
    {
        if (!advance || routine.MoveNext()) {
            if (routines == null) {
                routines = new List<IEnumerator>();
                lastRoutineValues = new Dictionary<IEnumerator, object>();
            }
            if (routines.Count == 0) {
                EditorApplication.update += Runner;
            }
            routines.Add(routine);
            ProcessRoutine(routines.Count - 1);
            return true;
        }
        return false;
    }

    static void Runner()
    {
        // Stop the runner when there are no more active routines
        if (routines == null || routines.Count == 0) {
            EditorApplication.update -= Runner;
            return;
        }

        // Process routines from the back so we can add during the loop
        for (int i = routines.Count - 1; i >= 0; i--) {
            var routine = routines[i];

            if (routine.MoveNext()) {
                // Routine is running
                ProcessRoutine(i);

            } else {
                // Routine has finished
                routines.RemoveAt(i);
                StopRoutine(routine);
            }
        }
    }

    static void ProcessRoutine(int i)
    {
        var routine = routines[i];
        var value = lastRoutineValues[routine] = routine.Current;

        var subroutine = value as IEnumerator;
        if (subroutine != null && subroutine.MoveNext()) {
            // We got a subroutine, pause the routine and run the subroutine
            if (parentRoutines == null) {
                parentRoutines = new Dictionary<IEnumerator, IEnumerator>();
            }
            parentRoutines[subroutine] = routine;
            routines.RemoveAt(i);
            if (!Run(subroutine, false)) StopRoutine(subroutine);
        }
    }

    static void StopRoutine(IEnumerator routine)
    {
        if (parentRoutines != null && parentRoutines.ContainsKey(routine)) {
            // Continue parent routine of subroutine
            var parent = parentRoutines[routine];
            parentRoutines.Remove(routine);

            // Setting the subroutine's last value so it can be
            // accessed by the parent routine using SubroutineResult()
            hasLastRoutineValue = true;
            lastRoutineValue = lastRoutineValues[routine];
            if (!Run(parent, true)) StopRoutine(parent);
            lastRoutineValue = null;
            hasLastRoutineValue = false;
        }
        lastRoutineValues.Remove(routine);
    }
}

}