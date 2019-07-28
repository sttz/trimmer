//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using UnityEngine;

namespace sttz.Trimmer
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
        Debug.Log(Application.productName + " " + Version.ProjectVersion);
    }
}

}
