using System;
using UnityEngine;

#if !NO_WORKBENCH || UNITY_EDITOR

namespace sttz.Workbench
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
	/// <summary>
	/// Field for the store which Unity will serialize in the build.
	/// </summary>
	public ValueStore store;

	/// <summary>
	/// Called when a scene is loaded in the player.
	/// </summary>
	void OnEnable()
	{
		if (RuntimeProfile.Main == null) {
			RuntimeProfile.CreateMain(store);
			RuntimeProfile.Main.Apply();
		}
	}
}

}

#endif