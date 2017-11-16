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
/// For the player, we need to get the <see cref="ValueStore"/> of the build
/// profile, containing all the option values, into the build. We use this 
/// container <c>MonoBehaviour</c> to contain the store and inject it into
/// the build's scene using <see cref="Editor.BuildManager" /> and 
/// [IProcessScene](https://docs.unity3d.com/ScriptReference/Build.IProcessScene.html).
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

		referenceGUIDs.Add(guid);
		references.Add(reference);
	}

	/// <summary>
	/// Get a Unity object reference in the build.
	/// </summary>
	/// <remarks>
	/// The reference needs to have been added using <see cref="AddReference"/> during
	/// the the <see cref="Option.PostprocessScene*"/> callback of the first scene.
	/// </remarks>
	public T GetReference<T>(string guid) where T : Object
	{
		if (referenceGUIDs == null || referenceGUIDs == null || referenceGUIDs.Count != references.Count)
			return null;
		
		for (int i = 0; i < referenceGUIDs.Count; i++) {
			if (referenceGUIDs[i].EqualsIgnoringCase(guid)) {
				return references[i] as T;
			}
		}

		return null;
	}

	// ------ Behaviour ------

	public static ProfileContainer Instance { get; set; }

	/// <summary>
	/// Called when a scene is loaded in the player.
	/// </summary>
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