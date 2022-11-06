//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if !NO_TRIMMER || UNITY_EDITOR

using System.Collections.Generic;
using sttz.Trimmer.Extensions;
using UnityEngine;
using UnityEngine.Assertions;

namespace sttz.Trimmer
{

/// <summary>
/// Container for the Runtime Profile.
/// </summary>
/// <remarks>
/// For the player, we need to get the <see cref="ValueStore"/> of the Build
/// Profile, containing all the Option values, into the build. We use this 
/// container `MonoBehaviour` to contain the store and inject it into
/// the build's scene using <see cref="T:sttz.Trimmer.Editor.BuildManager" /> and 
/// [IProcessScene](https://docs.unity3d.com/ScriptReference/Build.IProcessScene.html).
/// 
/// The container also provides an API to inject additional Unity objects
/// into the build.
/// </remarks>
public class ProfileContainer : MonoBehaviour
{
    // ------ Profile Store ------

    /// <summary>
    /// Field for the store which Unity will serialize in the build.
    /// </summary>
    public ValueStore store;

    // ------ Object References ------

    [SerializeField] List<string> referenceGUIDs;
    [SerializeField] List<Object> references;

    /// <summary>
    /// Add an Unity object reference to be included in the build.
    /// </summary>
    /// <remarks>
    /// This method can be called during the <see cref="Option.PostprocessScene*"/> callback 
    /// of the first scene (`scene.buildIndex` is 0) to add Unity object references
    /// that can then be recalled in the build using <see cref="GetReference"/>.
    /// </remarks>
    public void AddReference(string guid, Object reference)
    {
        if (referenceGUIDs == null) referenceGUIDs = new List<string>();
        if (references == null) references = new List<Object>();

        Assert.AreEqual(referenceGUIDs.Count, references.Count, "GUID/Reference lists are out of sync");

        var index = referenceGUIDs.IndexOf(guid);
        if (index >= 0) {
            references[index] = reference;
        } else {
            referenceGUIDs.Add(guid);
            references.Add(reference);
        }
    }

    /// <summary>
    /// Get a Unity object reference in the build.
    /// </summary>
    /// <remarks>
    /// The reference needs to have been added using <see cref="AddReference"/> during
    /// the build.
    /// </remarks>
    public T GetReference<T>(string guid) where T : Object
    {
        if (referenceGUIDs == null || references == null || referenceGUIDs.Count != references.Count) {
            Debug.LogError("ProfileContainer.GetReference referenceGUIDs/references properties are null or have different lengths");
            return null;
        }
        
        for (int i = 0; i < referenceGUIDs.Count; i++) {
            if (referenceGUIDs[i].EqualsIgnoringCase(guid)) {
                return references[i] as T;
            }
        }

        Debug.LogError($"ProfileContainer.GetReference no reference registered for GUID '{guid}'");
        return null;
    }

    // ------ Behaviour ------

    /// <summary>
    /// The shared instance of the profile container.
    /// </summary>
    /// <remarks>
    /// This instance is set in the player but not when playing in the editor.
    /// 
    /// During build, the shared instance is also set when building the first
    /// scene (to add references to the build) but not when building 
    /// subsequent scenes.
    /// 
    /// To access the runtime profile, use <see cref="RuntimeProfile.Main"/>.
    /// </remarks>
    public static ProfileContainer Instance { get; set; }

    void OnEnable()
    {
        if (Instance != null) {
            Debug.LogWarning("Multiple ProfileContainers loaded!");
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (RuntimeProfile.Main == null) {
            RuntimeProfile.CreateMain(store);
            RuntimeProfile.Main.Apply();
        }
    }
}

}

#endif
