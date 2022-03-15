//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if UNITY_EDITOR

using System;
using System.IO;
using sttz.Trimmer.Extensions;
using sttz.Trimmer.BaseOptions;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Reporting;

namespace sttz.Trimmer.Options
{

/// <summary>
/// Set the signing information for Android (keystore, alias and passwords).
/// </summary>
/// <remarks>
/// This Option allows you to set different signing settings per Build Profile.
/// 
/// Additionally, this Options checks if the settings are valid and gives
/// immediate feedback in the inspector.
/// 
/// There are two options for storing passwords:
/// * *With Keychain*: With this setting enabled, passwords are stored using
///   the <see cref="Keychain"/> class, which in turn uses the Keychain on
///   macOS and DPAPI/EditorPrefs on Windows. Passwords are stored per user
///   and have to be entered only once across projects.
/// * *Without Keychain*: Passwords are stored as plaintext directly in the
///   Build Profile. This allows to check in the keystore together with the
///   project and do remote builds but is only recommended for debug builds,
///   since the key is not protected.
/// </remarks>
[Capabilities(OptionCapabilities.ConfiguresBuild)]
public class OptionKeyStore : OptionContainer
{
    protected override void Configure()
    {
        Category = "Build";
        SupportedTargets = new BuildTarget[] {
            BuildTarget.Android
        };
    }

    const string KeychainService = "Android Keystore";
    static readonly string[] Newlines = new string[] { "\r\n", "\n" };

    enum Error {
        Unknonw = -1,
        None = 0,
        NoKeystore = 1,
        KeystoreNotFound = 2,
        NoAlias = 3,
        NoKechain = 4,
        NoKeystorePassword = 5,
        AliasNotFound = 6,
        NoAliasPassword = 7,
        InvalidKeystorePassword = 8,
        InvalidAliasPassword = 9,
        JdkNotFound = 21,
        KeytoolNotFound = 22,
        KeytoolFailed = 23,
        AndroidSDKNotFound = 31,
        BuildToolsNotFound = 32,
        BuildToolsVersionNotFound = 33,
        ApksignerNotFound = 34,
        UnknownApksignerExit = 35,
    }

    static readonly Dictionary<Error, string> ErrorMessages = new Dictionary<Error, string> {
        { Error.None, null },
        { Error.NoKeystore, "No Keystore set" },
        { Error.KeystoreNotFound, "Keystore not found" },
        { Error.NoAlias, "No alias set" },
        { Error.NoKechain, "No Keychain implementation" },
        { Error.NoKeystorePassword, "Keystore password missing" },
        { Error.AliasNotFound, "Alias not found" },
        { Error.NoAliasPassword, "Alias password missing" },
        { Error.InvalidKeystorePassword, "Invalid keystore password" },
        { Error.InvalidAliasPassword, "Invalid alias password" },
        { Error.JdkNotFound, "Java JDK not found" },
        { Error.KeytoolNotFound, "keytool not found" },
        { Error.KeytoolFailed, "keytool failed" },
        { Error.AndroidSDKNotFound, "Android SDK not found" },
        { Error.BuildToolsNotFound, "Directory 'build-tools' not found in SDK" },
        { Error.BuildToolsVersionNotFound, "Could not determine latest 'build-tools' version" },
        { Error.ApksignerNotFound, "Could not find 'apksigner' in build tools" },
        { Error.UnknownApksignerExit, "Unknown apksigner exit code" }
    };

    static GUIContent warning;
    static GUIContent success;

    Error validation = Error.Unknonw;
    string keytoolPath;
    List<string> aliases = new List<string>();

    Rect buttonRect;

    override public bool EditGUI()
    {
        if (warning == null || success == null) {
            success = new GUIContent(EditorGUIUtility.FindTexture("Collab"));
            warning = new GUIContent(EditorGUIUtility.FindTexture("CollabError"));
        }

        if (validation == Error.Unknonw) {
            validation = Validate();
        }

        if (validation != Error.None) {
            GUILayout.Label(warning);
            GUILayout.Label(ErrorMessages[validation]);
        } else {
            GUILayout.Label(success);
        }

        GUILayout.FlexibleSpace();

        return false;
    }

    override public void PreprocessBuild(BuildReport report, OptionInclusion inclusion)
    {
        base.PreprocessBuild(report, inclusion);

        if (inclusion == OptionInclusion.Remove)
            return;

        if (validation == Error.Unknonw) {
            validation = Validate();
        }

        if (validation == Error.NoKeystore) {
            // Option not configured: only print a notice
            Debug.Log("OptionKeyStore: No keystore configured");
            return;
        } else if (validation != Error.None) {
            var error = "Error in the active Build Profile Key Store Option:\n" + ErrorMessages[validation];
            if (Application.isBatchMode) {
                // In batch mode: Log error
                Debug.LogError(error);
                return;
            } else if (EditorUtility.DisplayDialog("Android Keystore", error, "Cancel", "Ignore")) {
                // In editor: Show a warning dialog
                throw new Exception(error);
            }
        }

        var keystore = GetChild<OptionStorePath>().Value;
        var alias = GetChild<OptionAlias>().Value;

        PlayerSettings.Android.keystoreName = keystore;
        PlayerSettings.Android.keyaliasName = alias;

        if (GetChild<OptionUseKeychain>().Value && Keychain.Main != null) {
            PlayerSettings.Android.keystorePass = Keychain.Main.GetPassword(KeychainService, keystore);
            PlayerSettings.Android.keyaliasPass = Keychain.Main.GetPassword(KeychainService, keystore + "#" + alias);
        } else {
            PlayerSettings.Android.keystorePass = GetChild<OptionStorePassword>().Value;
            PlayerSettings.Android.keyaliasPass = GetChild<OptionAliasPassword>().Value;
        }
    }

    void NeedToRevalidate()
    {
        validation = Error.Unknonw;
    }

    /// <summary>
    /// We can't be sure keytool is on the PATH, so we use the JDK
    /// path configured in Unity to find it.
    /// </summary>
    Error FindKeytool()
    {
        if (!string.IsNullOrEmpty(keytoolPath)) {
            return Error.None;
        }

        var jdkPath = EditorPrefs.GetString("JdkPath");
        if (string.IsNullOrEmpty(jdkPath) || !Directory.Exists(jdkPath)) {
            return Error.JdkNotFound;
        }

        var path = System.IO.Path.Combine(System.IO.Path.Combine(jdkPath, "bin"), "keytool");
        if (!File.Exists(path)) {
            return Error.KeytoolNotFound;
        }

        keytoolPath = path;
        return Error.None;
    }

    /// <summary>
    /// Check the alias password.
    /// </summary>
    /// <remarks>
    /// There isn't really a nice way to check the key password from the command line,
    /// so we have to abuse `keytool` a bit.
    /// 
    /// The main issue is that the tools will try the store password on the key and
    /// therefore if the two match, the key password will never be checked. Unity, on
    /// the other hand, will not try the store password on the key and requires the
    /// proper key password to be set.
    /// 
    /// We use the `-keypaswd` command of keytool, which usually changes a key's password,
    /// but we don't supply it with a new password and only use it to check its error
    /// output. In case the store password is wrong 'password was incorrect' will be
    /// printed. In case the key password is wrong 'Cannot recover key' will be printed.
    /// In case both store and key passwords match, we check if the keytool prompted
    /// for the key password (if not, the passwords must match) and check if they match
    /// ourself.
    /// </remarks>
    Error CheckAliasPassword(string keystore, string alias, string keystorePass, string aliasPass)
    {
        var result = FindKeytool();
        if (result != Error.None) return result;

        var arguments = string.Format(
            "-keypasswd -keystore '{0}' -alias '{1}'",
            keystore, alias
        );
        var input = keystorePass + "\n" + aliasPass;
        string output, error;
        OptionHelper.RunScript(keytoolPath, arguments, input, out output, out error);

        if (output.Contains("password was incorrect")) {
            return Error.InvalidKeystorePassword;
        }

        if (output.Contains("Cannot recover key")) {
            return Error.InvalidAliasPassword;
        }

        // keytool first tries the store password on the key, so if they
        // match, the key password is never actually checked. 
        // In this case we didn't get a key password prompt. Therefore
        // make sure keystore and alias passwords match.
        if (!error.Contains("Enter key password") && keystorePass != aliasPass) {
            return Error.InvalidAliasPassword;
        }

        return Error.None;
    }

    /// <summary>
    /// Get aliases defined in a keystore using `keytool`.
    /// </summary>
    Error GetKeyAliases(string path, string password, List<string> aliases)
    {
        var result = FindKeytool();
        if (result != Error.None) return result;

        var arguments = string.Format(
            "-list -v -keystore '{0}'",
            path
        );
        string output, error;
        var code = OptionHelper.RunScript(keytoolPath, arguments, password, out output, out error);
        if (code != 0) {
            if (output.Contains("password was incorrect")) {
                return Error.InvalidKeystorePassword;
            } else {
                return Error.KeytoolFailed;
            }
        }

        aliases.Clear();
        var lines = output.Split(Newlines, StringSplitOptions.None);
        foreach (var line in lines) {
            if (line.StartsWith("Alias name: ")) {
                aliases.Add(line.Substring(12));
            }
        }
        
        return Error.None;
    }

    /// <summary>
    /// Check if the keystore is set up correctly and if the
    /// passwords are correct.
    /// </summary>
    Error Validate()
    {
        aliases.Clear();
        GetChild<OptionAlias>().SetAliases(aliases);

        // -- Check keystore
        var path = GetChild<OptionStorePath>().Value;
        if (string.IsNullOrEmpty(path))
            return Error.NoKeystore;
        if (!File.Exists(path))
            return Error.KeystoreNotFound;

        var useKeychain = GetChild<OptionUseKeychain>().Value;
        if (useKeychain && Keychain.Main == null)
            return Error.NoKechain;

        string keystorePass = null;
        if (!useKeychain) {
            keystorePass = GetChild<OptionStorePassword>().Value;
        } else {
            keystorePass = Keychain.Main.GetPassword(KeychainService, path);
        }

        if (string.IsNullOrEmpty(keystorePass))
            return Error.NoKeystorePassword;
        
        var code = GetKeyAliases(path, keystorePass, aliases);
        if (code != Error.None) return code;

        GetChild<OptionAlias>().SetAliases(aliases);

        // -- Check alias
        var alias = GetChild<OptionAlias>().Value;
        if (string.IsNullOrEmpty(alias))
            return Error.NoAlias;

        if (aliases.FindIndex(a => a.EqualsIgnoringCase(alias)) < 0) {
            return Error.AliasNotFound;
        }

        string aliasPass = null;
        if (!useKeychain) {
            aliasPass = GetChild<OptionAliasPassword>().Value;
        } else {
            aliasPass = Keychain.Main.GetPassword(KeychainService, path + "#" + alias);
        }

        if (string.IsNullOrEmpty(aliasPass))
            return Error.NoAliasPassword;
        
        code = CheckAliasPassword(path, alias, keystorePass, aliasPass);
        if (code != Error.None) return code;

        return Error.None;
    }

    /// <summary>
    /// Path to the keystore to use.
    /// </summary>
    public class OptionStorePath : OptionString
    {
        override protected void Configure()
        {
            ApplyOrder = 1;
        }

        override public bool EditGUI()
        {
            var changed = base.EditGUI();

            if (GUILayout.Button("...", EditorStyles.miniButton)) {
                EditorApplication.delayCall += () => {
                    var path = EditorUtility.OpenFilePanel("Select Android Keystore", "", "keystore");
                    if (!string.IsNullOrEmpty(path)) {
                        Value = path;
                        (Parent as OptionKeyStore).NeedToRevalidate();
                    }
                };
            }

            if (changed) {
                (Parent as OptionKeyStore).NeedToRevalidate();
            }
            return changed;
        }
    }

    /// <summary>
    /// Name of the key in the store.
    /// </summary>
    public class OptionAlias : OptionString
    {
        override protected void Configure()
        {
            ApplyOrder = 2;
        }

        string[] aliasesMenu;
        int index;

        public void SetAliases(List<string> aliases)
        {
            aliasesMenu = aliases.ToArray();
            index = aliases.FindIndex(a => a.EqualsIgnoringCase(Value));
        }

        override public bool EditGUI()
        {
            if (aliasesMenu == null)
                return false;

            if (aliasesMenu != null && aliasesMenu.Length > 0) {
                EditorGUI.BeginChangeCheck();
                index = EditorGUILayout.Popup(index, aliasesMenu);
                if (EditorGUI.EndChangeCheck()) {
                    if (index >= 0 && index < aliasesMenu.Length) {
                        Value = aliasesMenu[index];
                    } else {
                        Value = string.Empty;
                    }
                    (Parent as OptionKeyStore).NeedToRevalidate();
                    return true;
                }
            } else {
                return base.EditGUI();
            }
            return false;
        }
    }

    /// <summary>
    /// Wether to store the store and key passwords using the <see cref="Keychain"/>
    /// class.
    /// </summary>
    public class OptionUseKeychain : OptionToggle
    {
        override protected void Configure()
        {
            ApplyOrder = 3;
        }

        Rect buttonRect;

        override public bool EditGUI()
        {
            bool changed = false;
            var parent = (Parent as OptionKeyStore);

            if (Keychain.Main == null) {
                Value = false;
                changed = true;
            }

            EditorGUI.BeginDisabledGroup(Keychain.Main == null);
            {
                changed = base.EditGUI();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!Value || (parent.validation > Error.None && parent.validation <= Error.NoKechain));
            {
                if (GUILayout.Button("Set Passwords", EditorStyles.miniButton)) {
                    PopupWindow.Show(buttonRect, new PasswordsPopup() {
                        path = Parent.GetChild<OptionStorePath>().Value,
                        alias = Parent.GetChild<OptionAlias>().Value,
                        parent = (OptionKeyStore)Parent
                    });
                }
                if (Event.current.type == EventType.Repaint)
                    buttonRect = GUILayoutUtility.GetLastRect();
            }
            EditorGUI.EndDisabledGroup();

            if (changed) {
                parent.NeedToRevalidate();
            }

            return changed;
        }

        public class PasswordsPopup : PopupWindowContent
        {
            public string path;
            public string alias;
            public OptionKeyStore parent;

            string storePassword;
            string aliasPassword;

            public override Vector2 GetWindowSize()
            {
                return new Vector2(200, 90);
            }

            public override void OnGUI(Rect rect)
            {
                EditorGUIUtility.labelWidth = 60;

                GUILayout.Label("Update Keychain Passwords", EditorStyles.boldLabel);

                storePassword = EditorGUILayout.PasswordField("Store", storePassword);
                aliasPassword = EditorGUILayout.PasswordField("Alias", aliasPassword);

                if (GUILayout.Button("Update", EditorStyles.miniButton)) {
                    if (!string.IsNullOrEmpty(storePassword)) {
                        Keychain.Main.SetPassword(KeychainService, path, storePassword);
                    }
                    if (!string.IsNullOrEmpty(aliasPassword)) {
                        Keychain.Main.SetPassword(KeychainService, path + "#" + alias, aliasPassword);
                    }
                    parent.NeedToRevalidate();
                    editorWindow.Close();
                }
            }
        }
    }

    /// <summary>
    /// Base class for store and alias passwords.
    /// </summary>
    public abstract class OptionPassword : OptionString
    {
        override public bool EditGUI()
        {
            var changed = false;
            var useKeychain = Parent.GetChild<OptionUseKeychain>().Value;

            EditorGUI.BeginDisabledGroup(useKeychain);
            {
                changed = base.EditGUI();
                
                if (useKeychain && Value.Length > 0) {
                    Value = string.Empty;
                    changed = true;
                }
            }
            EditorGUI.EndDisabledGroup();
            
            if (changed) {
                (Parent as OptionKeyStore).NeedToRevalidate();
            }

            return false;
        }
    }

    /// <summary>
    /// Store password (only if <see cref="OptionUseKeychain"/> is disabled).
    /// </summary>
    public class OptionStorePassword : OptionPassword
    {
        override protected void Configure()
        {
            ApplyOrder = 4;
        }
    }
    
    /// <summary>
    /// Key password (only if <see cref="OptionUseKeychain"/> is disabled).
    /// </summary>
    public class OptionAliasPassword : OptionPassword
    {
        override protected void Configure()
        {
            ApplyOrder = 5;
        }
    }
}

}
#endif
