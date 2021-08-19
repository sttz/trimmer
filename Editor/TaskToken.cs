//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Token used to report progress from and cancel tasks.
/// </summary>
/// <remarks>
/// A wrapper around <see cref="CancellationToken"/> and <see cref="Progress"/>.
/// </remarks>
[Serializable]
public struct TaskToken
{
    /// <summary>
    /// Default progress options for all progress tasks.
    /// </summary>
    public static Progress.Options defaultOptions = Progress.Options.Unmanaged;

    /// <summary>
    /// Context object that is passed to <see cref="Debug.Log"/>.
    /// </summary>
    public UnityEngine.Object context;
    /// <summary>
    /// <see cref="Progress"/> task id.
    /// </summary>
    public int taskId;
    /// <summary>
    /// The id of the root parent task (Unity only supports nesting one level deep).
    /// </summary>
    public int parentId;
    /// <summary>
    /// Base step for tasks, so sub-tasks can report their sub-step without
    /// knowing the current or total steps.
    /// </summary>
    public int baseStep;
    /// <summary>
    /// The cancellation token for the task.
    /// </summary>
    public CancellationToken cancellation;

    /// <summary>
    /// Create a new task token without a parent.
    /// </summary>
    /// <param name="name">Name of the task</param>
    /// <param name="cancellation">Token to cancel the task</param>
    /// <param name="description">Description of the task</param>
    /// <param name="options">Options for the progress, will be combined with <see cref="defaultOptions"/></param>
    public static TaskToken Start(string name, CancellationToken cancellation = default, string description = null, Progress.Options options = default)
    {
        return new TaskToken() {
            taskId = Progress.Start(name, description, options | defaultOptions),
            cancellation = cancellation
        };
    }

    /// <summary>
    /// Report a child progress task and return a token with its id and the same cancellation token.
    /// </summary>
    /// <param name="name">Name of the task</param>
    /// <param name="description">Description of the task</param>
    /// <param name="options">Options for the progress, will be combined with <see cref="defaultOptions"/></param>
    public TaskToken StartChild(string name, string description = null, Progress.Options options = default)
    {
        var parent = parentId > 0 ? parentId : taskId;
        return new TaskToken() {
            taskId = Progress.Start(name, description, options | defaultOptions, parent),
            parentId = parent,
            cancellation = cancellation,
            context = context,
        };
    }

    /// <summary>
    /// Report continuous progress.
    /// </summary>
    /// <param name="progress">Current progress</param>
    /// <param name="description">New optional description</param>
    public void Report(float progress, string description = null)
    {
        Progress.Report(taskId, progress, description);

        if (!string.IsNullOrEmpty(description)) {
            Debug.Log($"{Progress.GetName(taskId)}: {description}", context);
        }
    }

    /// <summary>
    /// Report discrete progress.
    /// </summary>
    /// <param name="currentStep">Current step</param>
    /// <param name="totalSteps">Total steps (only needs to be set the first time)</param>
    /// <param name="description">New optional description</param>
    public void Report(int currentStep, int totalSteps = -1, string description = null)
    {
        if (totalSteps == -1) totalSteps = Progress.GetTotalSteps(taskId);
        Progress.Report(taskId, baseStep + currentStep, totalSteps, description);

        if (!string.IsNullOrEmpty(description)) {
            Debug.Log($"{Progress.GetName(taskId)}: {description} ({currentStep}/{totalSteps})", context);
        }
    }

    /// <summary>
    /// Remove the current progress task.
    /// </summary>
    public void Remove()
    {
        taskId = Progress.Remove(taskId);
    }

    /// <summary>
    /// Throw if the cancellation has been requested.
    /// </summary>
    public void ThrowIfCancellationRequested()
    {
        cancellation.ThrowIfCancellationRequested();
    }
}

}
