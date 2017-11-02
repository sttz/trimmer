#if UNITY_5 && !UNITY_5_0 // Introduced in Unity 5.1
#define HAS_HELP_URL_ATTRIBUTE
#endif


using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using sttz.Workbench.Extensions;

namespace sttz.Workbench.Editor
{

/// <summary>
/// The editor profile is a special unique profile that sets the options'
/// values in the editor.
/// </summary>
/// <remarks>
/// <para>Regarding play mode the editor profile behaves like scene objects:
/// Changes made outside of play mode are permanent (and saved in <c>Editor.ini</c>
/// in the project folder), while changes made during play mode only persist
/// until playback is stopped.</para>
/// 
/// <para>There should only ever be once instance of the editor profile.
/// Don't create additional instances but instead use <see cref="SharedInstance"/>
/// to get the instance.</para>
/// </remarks>
[InitializeOnLoad]
#if HAS_HELP_URL_ATTRIBUTE
[HelpURL("http://sttz.ch/")]
#endif
public class EditorProfile : EditableProfile
{
	// -------- Static --------

	/// <summary>
	/// Path to the asset used to save the editor profile in.
	/// </summary>
	/// <remarks>
	/// The path is relative to the project's folder.
	/// </remarks>
	public const string EDITOR_PROFILE_PATH = "Library/EditorProfile.asset";

	/// <summary>
	/// Show the editor profile in the inspector.
	/// </summary>
	[MenuItem("Edit/Project Settings/Editor Profile")]
	[MenuItem("Window/Editor Profile %e")]
	public static void OpenEditorProfile()
	{
		Selection.activeObject = SharedInstance;
	}

	/// <summary>
	/// The profile used in the editor.
	/// </summary>
	public static EditorProfile SharedInstance {
		get {
			if (_editorProfile == null) {
				UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(EDITOR_PROFILE_PATH);
				if (_editorProfile == null) {
					var instance = ScriptableObject.CreateInstance<EditorProfile>();
					instance.name = "Editor Profile";
					instance.hideFlags = HideFlags.HideAndDontSave;
				}
			}
			return _editorProfile;
		}
	}
	private static EditorProfile _editorProfile;

	/// <summary>
	/// Option needs to have one of these capabilities to be 
	/// displayed in the Editor Profile.
	/// </summary>
	const OptionCapabilities requiredCapabilities = (
		OptionCapabilities.CanPlayInEditor
		| OptionCapabilities.ExecuteInEditMode
	);

	[InitializeOnLoadMethod]
	static void ListenForPlayModeStateChanges()
	{
		#if UNITY_2017_2_OR_NEWER
		EditorApplication.playModeStateChanged += PlayModeStateChange;
		#else
		EditorApplication.playmodeStateChanged += PlayModeStateChange;
		#endif
	}

	#if UNITY_2017_2_OR_NEWER
	static void PlayModeStateChange(PlayModeStateChange change)
	{
		if (change == PlayModeStateChange.ExitingPlayMode) {
			OnExitingPlayMode();
		}
	}
	#else
	static void PlayModeStateChange()
	{
		if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) {
			OnExitingPlayMode();
		}
	}
	#endif

	static void OnExitingPlayMode()
	{
		if (WorkbenchPrefs.PlaymodeExitSave) {
			// Changes were made directly to the options, save them to the store
			RuntimeProfile.Main.SaveToStore();
			// Apply the store to the editor profile and save it out
			SharedInstance.store = RuntimeProfile.Main.Store;
			SharedInstance.Save();
		}
	}

	// -------- Fields --------

	// <summary>
	/// The value store containing the values for the profile's options.
	/// </summary>
	public ValueStore store = new ValueStore();

	public override ValueStore Store {
		get {
			if (BuildManager.EditorSourceProfile != null) {
				return BuildManager.EditorSourceProfile.Store;
			} else {
				return store;
			}
		}
	}

	// -------- Methods --------

	public EditorProfile()
	{
		if (_editorProfile != null) {
			Debug.LogWarning("Multiple editor profile instances loaded.");
		} else {
			_editorProfile = this;
		}
	}

	void OnEnable()
	{
		if (_editorProfile != this) {
			Debug.LogWarning("Cleaning up additional editor profile instance.");
			DestroyImmediate(this);
		}
	}

	void OnDisable()
	{
		SaveIfNeeded();
	}

	/// <summary>
	/// Save the editor profile. Since it's stored in ProjectSettings,
	/// it needs to be saved manually.
	/// </summary>
	public override void SaveIfNeeded()
	{
		if (store.IsDirty(true) || expandedDirty) {
			Save();
		}
	}

	public void Save()
	{
		expandedDirty = false;
		UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(
			new UnityEngine.Object[] { this },
			EDITOR_PROFILE_PATH,
			true
		);
	}

	public override Recursion.RecursionType GetRecursionType()
	{
		if (Application.isPlaying) {
			return Recursion.RecursionType.Options;
		} else {
			return Recursion.RecursionType.Nodes;
		}
		
	}

	/// <summary>
	/// 
	/// </summary>
	public override IEnumerable<Option> GetAllOptions()
	{
		if (Application.isPlaying) {
			return RuntimeProfile.Main;
		} else {
			return AllOptions.Where(o => (o.Capabilities & requiredCapabilities) != 0);
		}
	}

	/// <summary>
	/// Show the edit GUI for the given option.
	/// </summary>
	public override void EditOption(string path, Option option, ValueStore.Node node)
	{
		if (Application.isPlaying) {
			var oldValue = option.Save();
			var newValue = option.EditGUI(oldValue);
			if (oldValue != newValue) {
				option.Load(newValue);
				option.ApplyFromRoot();
			}
			return;
		}
		
		Option editModeOption = null;
		if (editModeProfile != null) {
			editModeOption = editModeProfile.GetOption(path);
		}

		if (editModeOption != null) {
			var oldValue = editModeOption.Save();
			var newValue = editModeOption.EditGUI(oldValue);
			if (oldValue != newValue) {
				editModeOption.Load(newValue);
				editModeOption.ApplyFromRoot();
			}
			node.Value = newValue;
		
		} else {
			node.Value = option.EditGUI(node.Value);
		}
	}

	// -------- Edit Mode --------

	/// <summary>
	/// Profile used for options with <see cref="ExecuteInEditMode"/>.
	/// </summary>
	/// <remarks>
	/// This profile only creates the options that have the <see cref="ExecuteInEditMode"/>
	/// attribute, avoiding other options to interfere outside of playmode.
	/// </remarks>
	private class EditModeProfile : RuntimeProfile
	{
		public EditModeProfile(ValueStore store) : base(store) { }

		protected override bool ShouldCreateOption(Type optionType)
		{
			var caps = optionType.GetOptionCapabilities();
			return (caps & OptionCapabilities.ExecuteInEditMode) != 0;
		}
	}

	private EditModeProfile editModeProfile;

	/// <summary>
	/// Create the edit mode profile.
	/// </summary>
	private void InitEditModeProfile()
	{
		editModeProfile = new EditModeProfile(store);
		editModeProfile.Apply();
	}

	/// <summary>
	/// Static constructor.
	/// </summary>
	static EditorProfile()
	{
		// Delay creation of edit mode profile since some Unity API
		// is not yet ready when the static constructor is called
		EditorApplication.delayCall += () => {
			SharedInstance.InitEditModeProfile();
		};
	}

	// -------- Expanded --------

	/// <summary>
	/// Used to track expanded state in editors.
	/// </summary>
	[SerializeField] List<int> expanded = new List<int>();
	bool expandedDirty = false;

	public void SetExpanded(string identifier, bool isExpanded)
	{
		var hash = identifier.GetHashCode();
		var index = expanded.BinarySearch(hash);
		if (isExpanded && index < 0) {
			expanded.Insert(~index, hash);
			expandedDirty = true;
		} else if (!isExpanded && index >= 0) {
			expanded.RemoveAt(index);
			expandedDirty = true;
		}
	}

	public bool IsExpanded(string identifier)
	{
		var hash = identifier.GetHashCode();
		return expanded.BinarySearch(hash) >= 0;
	}
}

}