using System;
using UnityEngine;

namespace sttz.Workbench
{

/// <summary>
/// Workbench runtime management script.
/// </summary>
/// <remarks>
/// This script manages the options during runtime, while playing in
/// the editor or in builds. You should not add this script to your
/// scenes, it will be added automatically when needed.
/// </remarks>
public class Workbench : MonoBehaviour
{
	// -------- Singleton --------

	/// <summary>
	/// Access to the Workbench singleton.
	/// </summary>
	public static Workbench Instance { get; protected set; }

	// -------- Script --------

	/// <summary>
	/// The profile used by the Maintenance Panel.
	/// </summary>
	/// <remarks>
	/// Returns null until the maintenance panel has been initialized.
	/// </remarks>
	public Profile Profile { get; protected set; }

	/// <summary>
	/// Values to use during runtime.
	/// </summary>
	public ValueStore store;

	// MonoBehaviour.OnEnable
	protected void OnEnable()
	{
		if (Instance != null) {
			enabled = false;
			Destroy(gameObject);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject);

		if (store != null && Profile == null) {
			Profile = new Profile(store);
			Profile.Apply();
		}
	}
}

}

