using System;
using UnityEngine;

namespace sttz.Workbench.Options
{

/// <summary>
/// Container to hold the project's <see cref="Version"/> to
/// let Unity serialize it during the build.
/// </summary>
public class VersionContainer : MonoBehaviour
{
    public Version version;

    void OnEnable()
    {
        Version.ProjectVersion = version;
    }
}

}