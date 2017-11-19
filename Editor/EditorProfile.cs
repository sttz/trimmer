using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using sttz.Trimmer.Extensions;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// The editor profile is a special unique profile that sets the Options'
/// values in the editor.
/// </summary>
[HelpURL("http://sttz.ch/")] // TODO: Update
public class EditorProfile : EditableProfile
{
	// -------- Static --------

	/// <summary>
	/// Path to the asset used to save the editor profile in.
	/// </summary>
	/// <remarks>
	/// The path is relative to the project's folder.
	/// </remarks>
	public const string EDITOR_PROFILE_PATH = "Library/TrimmerEditorProfile.asset";

	/// <summary>
	/// Menu item to show the Editor Profile in the inspector.
	/// </summary>
	[MenuItem("Window/Trimmer/Editor Profile %e")]
	public static void OpenEditorProfile()
	{
		Selection.activeObject = Instance;
	}

	/// <summary>
	/// Menu to show the active Build Profile in the inspector.
	/// </summary>
	[MenuItem("Window/Trimmer/Active Build Profile %&b")]
	public static void OpenActiveProfile()
	{
		Selection.activeObject = Instance.ActiveProfile;
	}

	[MenuItem("Window/Trimmer/Active Build Profile %&b", true)]
	static bool ValidateOpenActiveProfile()
	{
		return Instance.ActiveProfile != null;
	}

	/// <summary>
	/// The profile used in the editor.
	/// </summary>
	public static EditorProfile Instance {
		get {
			CreateSharedInstance();
			return _editorProfile;
		}
	}
	static EditorProfile _editorProfile;

	static void CreateSharedInstance()
	{
		if (_editorProfile == null) {
			UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(EDITOR_PROFILE_PATH);
			if (_editorProfile == null) {
				var instance = ScriptableObject.CreateInstance<EditorProfile>();
				instance.name = "Editor Profile";
				instance.hideFlags = HideFlags.HideAndDontSave;
			}
		}
	}

	[InitializeOnLoadMethod]
	static void ListenForPlayModeStateChanges()
	{
		CreateSharedInstance();
	}

	/// <summary>
	/// Option needs to have one of these capabilities to be 
	/// displayed in the Editor Profile.
	/// </summary>
	const OptionCapabilities requiredCapabilities = (
		OptionCapabilities.CanPlayInEditor
		| OptionCapabilities.ExecuteInEditMode
	);

	// -------- Properties --------

	/// <summary>
	/// The active profile, which is used for regular Unity builds.
	/// </summary>
	/// <remarks>
	/// The active profile is saved per-project in the editor profile's ini
	/// file (usually <c>Editor.ini</c> in the project folder).
	/// </remarks>
	public BuildProfile ActiveProfile {
		get {
			return _activeProfile;
		}
		set {
			if (value == _activeProfile)
				return;
			
			_activeProfile = value;
			profileDirty = true;
		}
	}
	[SerializeField] BuildProfile _activeProfile;

	/// <summary>
	/// Profile providing the current configuration for the editor.
	/// </summary>
	/// <remarks>
	/// Instead of using the editor's unique configuration values, it's
	/// possible to use a build profile's configuration instead, allowing to 
	/// quickly switch between sets of configuration values.
	/// </remarks>
	/// <value>
	/// <c>null</c> when using the editor's own configuration, otherwise the 
	/// build profile whose configuration is used.
	/// </value>
	public BuildProfile EditorSourceProfile {
		get {
			return _editorSourceProfile;
		}
		set {
			if (_editorSourceProfile == value)
				return;
			
			var previousValue = _editorSourceProfile;
			_editorSourceProfile = value;
			profileDirty = true;

			if (Application.isPlaying) {
				if (previousValue == null) {
					// When switching away from editor profile in play mode,
					// we need to save the changes made to the options
					RuntimeProfile.Main.SaveToStore();
					store = RuntimeProfile.Main.Store;
				}
				BuildManager.CreateOrUpdateMainRuntimeProfile();
				RuntimeProfile.Main.Apply();
			}
		}
	}
	[SerializeField] BuildProfile _editorSourceProfile;

	/// <summary>
	/// The value store containing the values for the profile's options.
	/// </summary>
	public ValueStore store = new ValueStore();

	public override ValueStore Store {
		get {
			if (EditorSourceProfile != null) {
				return EditorSourceProfile.Store;
			} else {
				return store;
			}
		}
	}

	/// <summary>
	/// Tracks wether the profile needs to be saved to disk.
	/// </summary>
	bool profileDirty = false;

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
			return;
		}

		#if UNITY_2017_2_OR_NEWER
		EditorApplication.playModeStateChanged += OnPlayModeStateChange;
		#else
		EditorApplication.playmodeStateChanged += OnPlayModeStateChange;
		#endif
	}

	void OnDisable()
	{
		SaveIfNeeded();

		#if UNITY_2017_2_OR_NEWER
		EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
		#else
		EditorApplication.playmodeStateChanged -= OnPlayModeStateChange;
		#endif
	}

	#if UNITY_2017_2_OR_NEWER
	void OnPlayModeStateChange(PlayModeStateChange change)
	{
		if (change == PlayModeStateChange.ExitingPlayMode) {
			OnExitingPlayMode();
		}
	}
	#else
	void OnPlayModeStateChange()
	{
		if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) {
			OnExitingPlayMode();
		}
	}
	#endif

	void OnExitingPlayMode()
	{
		if (TrimmerPrefs.PlaymodeExitSave && EditorSourceProfile == null) {
			// Changes were made directly to the options, save them to the store
			RuntimeProfile.Main.SaveToStore();
			// Apply the store to the editor profile and save it out
			store = RuntimeProfile.Main.Store;
			Save();
		}
	}

	/// <summary>
	/// Save the editor profile. Since it's stored in ProjectSettings,
	/// it needs to be saved manually.
	/// </summary>
	public override void SaveIfNeeded()
	{
		if (store.IsDirty(true) || profileDirty) {
			Save();
		}
	}

	public void Save()
	{
		profileDirty = false;
		UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(
			new UnityEngine.Object[] { this },
			EDITOR_PROFILE_PATH,
			true
		);
	}

	public override Recursion.RecursionType GetRecursionType()
	{
		if (Application.isPlaying && EditorSourceProfile == null) {
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
			Instance.InitEditModeProfile();
		};
	}

	// -------- Expanded --------

	/// <summary>
	/// Used to track expanded state in editors.
	/// </summary>
	[SerializeField] List<int> expanded = new List<int>();

	public void SetExpanded(string identifier, bool isExpanded)
	{
		var hash = identifier.GetHashCode();
		var index = expanded.BinarySearch(hash);
		if (isExpanded && index < 0) {
			expanded.Insert(~index, hash);
			profileDirty = true;
		} else if (!isExpanded && index >= 0) {
			expanded.RemoveAt(index);
			profileDirty = true;
		}
	}

	public bool IsExpanded(string identifier)
	{
		var hash = identifier.GetHashCode();
		return expanded.BinarySearch(hash) >= 0;
	}
}

}