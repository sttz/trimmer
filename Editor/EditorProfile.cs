#if UNITY_5 && !UNITY_5_0 // Introduced in Unity 5.1
#define HAS_HELP_URL_ATTRIBUTE
#endif


using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace sttz.Workbench {

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
		Selection.activeObject = EditorProfile.SharedInstance;
	}

	/// <summary>
	/// The profile used in the editor.
	/// </summary>
	public static EditorProfile SharedInstance {
		get {
			if (_editorProfile == null) {
				var objs = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(EDITOR_PROFILE_PATH);
				if (objs != null && objs.Length > 0 && objs[0] is EditorProfile) {
					_editorProfile = (EditorProfile)objs[0];
				}
				if (_editorProfile == null) {
					_editorProfile = ScriptableObject.CreateInstance<EditorProfile>();
					_editorProfile.name = "Editor Profile";
					_editorProfile.hideFlags = HideFlags.HideAndDontSave;
				}
			}
			return _editorProfile;
		}
	}
	private static EditorProfile _editorProfile;

	// -------- Fields --------

	/// <summary>
	/// The config of the <see cref="MaintenancePanel"/> used in the editor.
	/// </summary>
	//public MaintenancePanel.PanelConfig panelConfig = MaintenancePanel.DefaultConfig;

	/// <summary>
	/// The value store containing the values for the profile's options.
	/// </summary>
	public ValueStore store = new ValueStore();

	public override ValueStore Store {
		get {
			return store;
		}
	}

	// -------- Methods --------

	/// <summary>
	/// Save the editor profile. Since it's stored in ProjectSettings,
	/// it needs to be saved manually.
	/// </summary>
	public override void SaveIfNeeded()
	{
		if (store.IsDirty(true)) {
			UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(
				new UnityEngine.Object[] { this },
				EDITOR_PROFILE_PATH,
				true
			);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public override ValueStore.Node GetStoreRoot(IOption option)
	{
		if (Application.isPlaying) {
			// Force editor to edit options directly at runtime
			return null;
		} else {
			return store.GetOrCreateRoot(option.Name);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	public override IEnumerable<IOption> GetAllOptions()
	{
		if (Application.isPlaying) {
			return Workbench.Instance.Profile;
		} else {
			return AllOptions.Where(o => !o.BuildOnly);
		}
	}

	/// <summary>
	/// Show the edit GUI for the given option.
	/// </summary>
	public override void EditOption(GUIContent label, IOption option, ValueStore.Node node)
	{
		if (Application.isPlaying) {
			var oldValue = option.Save();
			var newValue = option.EditGUI(label, oldValue);
			if (oldValue != newValue) {
				option.Load(newValue);
				option.Apply();
			}
		
		} else if (editModeProfile != null && editModeProfile.GetOption(option.Name) != null) {
			// TODO: What about variant/child options?
			var editModeOption = editModeProfile.GetOption(option.Name);
			var newValue = editModeOption.EditGUI(label, editModeOption.Save());
			editModeOption.Load(newValue);
			node.Value = newValue;
		
		} else {
			node.Value = option.EditGUI(label, node.Value);
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
	// TODO: Check if edit mode options still work...
	private class EditModeProfile : Profile
	{
		public EditModeProfile(ValueStore store) : base(store) { }

		protected override bool ShouldCreateOption(Type optionType)
		{
			return optionType.GetCustomAttributes(typeof(ExecuteInEditMode), true).Length > 0;
		}
	}

	private EditModeProfile editModeProfile;

	/// <summary>
	/// Create the edit mode profile.
	/// </summary>
	private void InitEditModeProfile()
	{
		editModeProfile = new EditModeProfile(store);
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
}

}