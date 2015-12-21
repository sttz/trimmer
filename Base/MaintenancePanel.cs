using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Security.Policy;

namespace sttz.Workbench
{

// TODO: Grouping of Options
// TODO: No Debug/Release, only include/exclude
// TODO: Full build support: Options & Scenes
// TODO: Panel & ini loading as option group
// TODO: Integrate settings in profile?
// TODO: Input pausing callback
// TODO: Runtime options separate
// TODO: Scene sets

// name = Value
// name.child = Value
// name(variant) = Value
// name(variant).child = Value
// name(variant).child(variant) = Value
// etc..

// Variants: 1..n instances of same option with variant name as parameter
// Children: Fixed number of different options associated to a parent

/// <summary>
/// Ini file loader as well as runtime prompt for displaying and 
/// changing option values.
/// </summary>
/// <remarks>
/// <para>The Maintenance Panel is managed by the Workbench and should not be
/// added to builds manually. Only the enabled loading/prompt part will be 
/// compiled and if neither is enabled or no options are available, 
/// the script won't be injected at all.</para>
/// 
/// <para>The prompt is activated using an activation sequence with optional
/// modifiers. The sequence is triggered if all defined keys are pressed in
/// order and no other key is pressed inbetween.</para>
/// 
/// <para>Pressing enter on an empty prompt or using escape closes the prompt.
/// Pressing enter on an option name shows the options value, pressing enter
/// on an option name and value separated with an equal sign sets the option's
/// value.</para>
/// </remarks>
/*public class MaintenancePanel : MonoBehaviour
{
	// -------- Static --------

	/// <summary>
	/// All available options types.
	/// </summary>
	/// <remarks>
	/// All options in the editor and only included options in builds.
	/// </remarks>
	public static IEnumerable<Type> AllOptions {
		get {
			if (_options == null) {
				_options = new List<Type>();
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
					if (assembly is System.Reflection.Emit.AssemblyBuilder)
						continue;
					_options.AddRange(assembly.GetExportedTypes()
						.Where(t => 
							t.IsClass 
							&& !t.IsAbstract 
							&& typeof(IOption).IsAssignableFrom(t)
							&& !typeof(IChildOption).IsAssignableFrom(t)
						)
					);
				}
			}
			return _options;
		}
	}
	private static List<Type> _options;

	// -------- Singleton --------

	/// <summary>
	/// Access to the Configuration Manager singleton.
	/// </summary>
	/// <remarks>
	/// In case both ini file loading and the prompt are disabled or when no options
	/// are available this property will be null.
	/// </remarks>
	public static MaintenancePanel Instance { get; protected set; }
	private static MaintenancePanel _instance;

	// -------- Configuration --------

	/// <summary>
	/// Position of the prompt on the screen.
	/// </summary>
	public enum PrompPosition
	{
		TopLeft,
		BottomLeft,
		TopRight,
		BottomRight
	}

	/// <summary>
	/// Configuration values for the maintenance panel.
	/// </summary>
	[Serializable]
	public struct PanelConfig
	{
		/// <summary>
		/// The activation sequence to open the prompt.
		/// </summary>
		public KeyCode[] activationSequence;
		/// <summary>
		/// Activation sequence requires shift modifier.
		/// </summary>
		public bool activationShift;
		/// <summary>
		/// Activation sequence requires alt modifier.
		/// </summary>
		public bool activationAlt;
		/// <summary>
		/// Activation sequence requires ctrl (Windows) or cmd (Mac) modifier.
		/// </summary>
		public bool activationCtrlCmd;

		/// <summary>
		/// The prompt prefix added to the beginning.
		/// </summary>
		public string prompt;
		/// <summary>
		/// Font size of the prompt.
		/// </summary>
		public int promptFontSize;
		/// <summary>
		/// Position of the prompt on the screen.
		/// </summary>
		public PrompPosition promptPosition;
		/// <summary>
		/// The padding between the screen borders and the prompt.
		/// </summary>
		public float promptPadding;
		
		/// <summary>
		/// Name of the ini file to load at runtime.
		/// </summary>
		public string iniFileName;
		/// <summary>
		/// Paths the ini file is searched in.
		/// </summary>
		/// <remarks>
		/// Certain <c>%variables%</c> are filled in at runtime.
		/// Available variables are:
		/// <list type="bullet">
		/// <item>
		/// 	<term>DataPath</term>
		/// 	<description>Data path, as per Unity's <c>Application.dataPath</c></description>
		/// </item>
		/// <item>
		/// 	<term>PersistentDataPath</term>
		/// 	<description>Data path, as per Unity's <c>Application.persistentDataPath</c></description>
		/// </item>
		/// <item>
		/// 	<term>Personal</term>
		/// 	<description>Documents folder in Windows and home directory in Mac and Linux</description>
		/// </item>
		/// <item>
		/// 	<term>Desktop</term>
		/// 	<description>Dekstop folder</description>
		/// </item>
		/// </list>
		/// <remarks>
		public string[] iniFilePaths;

		/// <summary>
		/// Replace special variables in a ini file path.
		/// </summary>
		public string ResolveIniPath(string path)
		{
			path = path.Replace("%DataPath%", Application.dataPath);
			path = path.Replace("%PersistentDataPath%", Application.persistentDataPath);
			path = path.Replace("%Personal%", Environment.GetFolderPath(Environment.SpecialFolder.Personal));
			path = path.Replace("%Desktop%", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
			return path;
		}
	}

	/// <summary>
	/// Default values for the panel configuration.
	/// </summary>
	public static PanelConfig DefaultConfig {
		get {
			return new PanelConfig() {
				activationSequence = new KeyCode[] {
					KeyCode.O, KeyCode.O, KeyCode.O
				},
				activationShift = false,
				activationAlt = false,
				activationCtrlCmd = false,
				prompt = "> ",
				promptFontSize = 0,
				promptPosition = PrompPosition.BottomLeft,
				promptPadding = 30,
				iniFileName = "cfg.ini",
				iniFilePaths = new string[] {
					"%DataPath%/..",
					"%DataPath%/../.."
				}
			};
		}
	}

	/// <summary>
	/// Ini file with default values of the options.
	/// </summary>
	public TextAsset defaults;
	/// <summary>
	/// Configuration values for this maintenance panel.
	/// </summary>
	public PanelConfig config;

	// -------- Panel --------

	/// <summary>
	/// The profile used by the Maintenance Panel.
	/// </summary>
	/// <remarks>
	/// Returns null until the maintenance panel has been initialized.
	/// </remarks>
	public Profile Profile {
		get {
			return profile;
		}
	}

	/// <summary>
	/// Set the option defaults used by the manager.
	/// </summary>
	/// <remarks>
	/// This is used when instantiating the configuration
	/// manager and it's <c>Awake</c> method is called
	/// before the <c>defaults</c> can be set.
	/// </remarks>
	public void SetDefaults(TextAsset defaults)
	{
		this.defaults = defaults;
		LoadDefaults(defaults.text);
	}

	public void SetDefaults(string iniContents)
	{
		this.defaults = null;
		LoadDefaults(iniContents);
	}

	void LoadDefaults(string iniContents)
	{
		if (this != null) {
			profile = new Profile(new Defaults(iniContents));

			#if OPTION_INTERNAL_EnableIniFileLoading || UNITY_EDITOR
			LoadIniFile();
			#endif
		}
	}

	/// <summary>
	/// Re-Apply all options.
	/// </summary>
	public void Apply()
	{
		if (profile != null) {
			profile.Apply();
		}
	}

	// -------- Internals --------

	protected Profile profile;

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

		if (defaults != null && profile == null) {
			LoadDefaults(defaults.text);
		}
	}

	// MonoBehaviour.OnLevelWasLoaded
	protected void OnLevelWasLoaded()
	{
		if (!enabled)
			return;
		
		if (profile == null) {
			Debug.LogWarning("OnLevelWasLoaded: Null Profile: " + GetInstanceID());
		}

		Apply();
	}

	// MonoBehaviour.Update
	protected void Update()
	{
		#if OPTION_INTERNAL_EnablePrompt || UNITY_EDITOR
		PromptUpdate();
		#endif
	}

	// MonoBehaviour.OnGUI
	protected void OnGUI()
	{
		#if OPTION_INTERNAL_EnablePrompt || UNITY_EDITOR
		PromptOnGUI();
		#endif
	}

	// -------- Ini File Loading --------

	#if OPTION_INTERNAL_EnableIniFileLoading || UNITY_EDITOR

	/// <summary>
	/// Look for the ini file at the configured locations and load the
	/// first that is found.
	/// </summary>
	public bool LoadIniFile()
	{
		if (string.IsNullOrEmpty(config.iniFileName))
			return false;
		if (config.iniFilePaths == null || config.iniFilePaths.Length == 0)
			return false;

		foreach (var path in config.iniFilePaths) {
			var laoded = LoadIniFile(Path.Combine(path, config.iniFileName));
			if (laoded)
				return laoded;
		}

		return false;
	}

	/// <summary>
	/// Load the ini file at the specified path.
	/// </summary>
	public bool LoadIniFile(string path)
	{
		// Replace variables
		path = config.ResolveIniPath(path);

		if (!File.Exists(path))
			return false;

		string contents = null;
		try {
			contents = File.ReadAllText(path);
		} catch (Exception e) {
			Debug.LogError("Could not read config file: " + e.Message);
			return false;
		}

		var defaults = new Defaults(contents);
		if (profile == null) {
			profile = new Profile(defaults);
		} else {
			// Keep parent or use existing defaults as parent if they have no parent
			defaults.Parent = profile.Defaults.Parent ?? profile.Defaults;
			profile.Defaults = defaults;
		}

		Debug.Log("Loaded config file at '" + path + "'.");
		return true;
	}

	#endif

	// -------- Prompt --------

	#if OPTION_INTERNAL_EnablePrompt || UNITY_EDITOR

	/// <summary>
	/// Current position in the activation sequence.
	/// </summary>
	protected int sequencePos;
	/// <summary>
	/// Prompt is enabled (visible).
	/// </summary>
	protected bool enablePrompt;

	/// <summary>
	/// Current input string.
	/// </summary>
	protected string input = "";
	/// <summary>
	/// GUI rect of the prompt.
	/// </summary>
	protected Rect promptRect;

	/// <summary>
	/// Cache of complections for the current input.
	/// </summary>
	protected List<string> completions = new List<string>();
	/// <summary>
	/// Currently selected completion.
	/// </summary>
	protected int completionIndex;

	/// <summary>
	/// <see cref="GUIStyle"/> used for the prompt.
	/// </summary>
	protected GUIStyle promptStyle;
	/// <summary>
	/// Calculated height of the prompt based on <see cref="promptStyle"/>.
	/// </summary>
	protected float promptHeight;

	/// <summary>
	/// Process user input for the prompt.
	/// </summary>
	protected void PromptUpdate()
	{
		if (!enablePrompt) {
			// Track activation sequence
			if (UnityEngine.Input.anyKeyDown) {
				if (config.activationShift
				    && !UnityEngine.Input.GetKey(KeyCode.LeftShift)
				    && !UnityEngine.Input.GetKey(KeyCode.RightShift)) {
					sequencePos = 0;
					return;
				} else if (config.activationAlt
					&& !UnityEngine.Input.GetKey(KeyCode.LeftAlt)
					&& !UnityEngine.Input.GetKey(KeyCode.RightAlt)) {
					sequencePos = 0;
					return;
				} else if (config.activationCtrlCmd
					&& !UnityEngine.Input.GetKey(KeyCode.LeftControl)
					&& !UnityEngine.Input.GetKey(KeyCode.RightControl)
					&& !UnityEngine.Input.GetKey(KeyCode.LeftCommand)
					&& !UnityEngine.Input.GetKey(KeyCode.RightCommand)) {
					sequencePos = 0;
					return;
				}

				if (UnityEngine.Input.GetKeyDown(config.activationSequence[sequencePos])) {
					sequencePos++;
					if (sequencePos == config.activationSequence.Length) {
						StartPrompt();
						sequencePos = 0;
					}
				} else {
					sequencePos = 0;
				}
			}

		} else {
			// Don't process input if a modifier key is held down
			// .e.g. when using cmd-P in editor to stop playback
			if (UnityEngine.Input.GetKey(KeyCode.LeftCommand)
				|| UnityEngine.Input.GetKey(KeyCode.RightCommand)
				|| UnityEngine.Input.GetKey(KeyCode.LeftControl)
				|| UnityEngine.Input.GetKey(KeyCode.RightControl)) {
				return;
			}

			// Handle function keys
			if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) {
				StopPrompt();
				return;
			} else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow)) {
				CompletePrompt(1);
				return;
			} else if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow)) {
				CompletePrompt(-1);
				return;
			} else if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) {
				CompletePrompt();
				return;
			}

			if (UnityEngine.Input.inputString.Length == 0)
				return;

			// If anything is added or deleted, completions are cleared
			completions.Clear();

			foreach (var c in UnityEngine.Input.inputString) {
				if (c == '\b') {
					if (input.Length > 0)
						input = input.Remove(input.Length - 1, 1);
				} else if (c == '\n' || c == '\r') {
					ExecutePrompt();
				} else {
					input += c;
				}
			}
		}
	}

	/// <summary>
	/// Display the prompt.
	/// </summary>
	protected void PromptOnGUI()
	{
		if (promptStyle == null) {
			promptStyle = new GUIStyle(GUI.skin.label);
			promptStyle.fontSize = config.promptFontSize;
			promptHeight = promptStyle.CalcSize(new GUIContent(config.prompt)).y;
		}

		if (!enablePrompt)
			return;

		GUI.Label(promptRect, config.prompt + input, promptStyle);
	}

	/// <summary>
	/// Starts the prompt.
	/// </summary>
	protected void StartPrompt()
	{
		enablePrompt = true;

		var pos = config.promptPosition;
		promptRect = new Rect();
		if (pos == PrompPosition.TopLeft || pos == PrompPosition.BottomLeft) {
			promptRect.x = config.promptPadding;
			promptRect.width = Screen.width - 2 * config.promptPadding;
		} else {
			promptRect.x = Screen.width / 2;
			promptRect.width = Screen.width / 2 - config.promptPadding;
		}
		if (pos == PrompPosition.TopLeft || pos == PrompPosition.TopRight) {
			promptRect.y = config.promptPadding;
			promptRect.height = Screen.height - 2 * config.promptPadding;
		} else {
			promptRect.y = Screen.height - config.promptPadding - promptHeight;
			promptRect.height = promptHeight;
		}
	}

	/// <summary>
	/// Stops the prompt.
	/// </summary>
	protected void StopPrompt()
	{
		enablePrompt = false;
	}

	/// <summary>
	/// Show completions and cycle through them.
	/// </summary>
	protected void CompletePrompt(int moveIndex = 0)
	{
		// Generate completions list
		if (completions.Count == 0 || moveIndex == 0) {
			completions.Clear();

			foreach (var option in profile) {
				#if UNITY_EDITOR
				if (option.BuildOnly)
					continue;
				#endif

				if (option.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase)) {
					completions.Add(option.Name + " = " + option.Save());
				}
			}
			if (completions.Count > 0) {
				completions.Sort();
				completions.Insert(0, input);
				completions.Add("");
				completionIndex = 1;
			}
		
		// Move complections index
		} else {
			completionIndex = Mathf.Max(Mathf.Min(completionIndex + moveIndex, completions.Count - 1), 0);
		}

		// Show new completion
		if (completionIndex < completions.Count) {
			input = completions[completionIndex];
		}
	}

	/// <summary>
	/// Trigger prompt action.
	/// </summary>
	protected void ExecutePrompt()
	{
		// Enter on empty prompt closes it
		if (input.Length == 0) {
			StopPrompt();

		// Set a option value
		} else if (input.Contains("=")) {
			var parsed = Defaults.ScanLine(input);
			var option = profile.GetOption(parsed.name, parsed.parameter);
			if (option != null) {
				option.Load(parsed.value);
				input = "";
			}
		
		// Enter on an option shows it's value
		} else {
			var option = profile.GetOption(input);
			if (option != null) {
				input += " = " + option.Save();
			}
		}
	}

	#endif
}*/

}

