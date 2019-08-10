//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using UnityEngine;
using UnityEditor;
using System;
using sttz.Trimmer.Options;
using System.Linq;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Attribute to add to fields of the <see cref="Password"/> and <see cref="Login"/>
/// type to set the service and user (for Password) used for the <see cref="Keychain"/>.
/// </summary>
public class KeychainAttribute : Attribute
{
    public string Service { get; protected set; }
    public string User { get; protected set; }
    public string UserLabel { get; protected set; }

    /// <summary>
    /// Set the service and user (for fields of type Password) for storing the
    /// password in the Keychain.
    /// </summary>
    /// <param name="keychainInfo">Service name and user name separated by a colon.</param>
    /// <param name="userLabel">Override label before user field (does not apply to Password fields).</param>
    public KeychainAttribute(string keychainInfo, string userLabel = null)
    {
        string service, user;
        ParseKeychainInfo(keychainInfo, out service, out user);
        Service = service;
        User = user;
        UserLabel = userLabel;
    }

    public static void ParseKeychainInfo(string keychainInfo, out string service, out string user)
    {
        var colon = keychainInfo.IndexOf(':');
        if (colon < 0) {
            service = keychainInfo;
            user = null;
        } else {
            service = keychainInfo.Substring(0, colon);
            user = keychainInfo.Substring(colon + 1);
        }
    }
}

/// <summary>
/// Password field for the Unity editor.
/// </summary>
/// <remarks>
/// The password field gives the user two options to store the password:
/// - Plaintext in the project itself. This leaves the password unprotected
///   but allows it to be stored in the project and e.g. checked into version control.
/// - Securely and on a per-user basis in the <see cref="Keychain"/>. The password
///   will be saved on the user's computer and can be used across Unity projects.
/// 
/// The plaintext password can optionally be revealed in the editor. The Keychain
/// password can only be set but not viewed.
/// 
/// Storing the password in the Keychain also requires a service and user name. You 
/// need to specify them using the <see cref="KeychainAttribute"/> and pass them
/// to <see cref="GetPassword"/>. It's recommended to store the Keychain info
/// in a const string so it can be used for the attribute and method:
/// <code>
/// const string keychainInfo = "UploadDistro:blah";
/// [Keychain(keychainInfo)] public Password password;
/// password.GetPassword(keychainInfo);
/// </code>
/// </remarks>
[Serializable]
public struct Password
{
    /// <summary>
    /// The plaintext password if <see cref="useKeychain"/> is <c>false</c>.
    /// </summary>
    public string PlaintextPassword { get { return plaintextPassword; } set { plaintextPassword = value; } }
    [SerializeField] string plaintextPassword;
    /// <summary>
    /// Wether to use <see cref="Keychain"/> or store the password in plaintext.
    /// </summary>
    public bool UseKeychain { get { return useKeychain; } set { useKeychain = value; } }
    [SerializeField] bool useKeychain;

    /// <summary>
    /// Use this method to retrieve the password based on where it's stored.
    /// </summary>
    public string GetPassword(string keychainInfo)
    {
        if (useKeychain) {
            string service, user;
            KeychainAttribute.ParseKeychainInfo(keychainInfo, out service, out user);
            if (user == null) {
                throw new ArgumentException("Password is missing user part in keychainInfo");
            }
            return Keychain.Main.GetPassword(service, user);
        } else {
            return plaintextPassword;
        }
    }
}

/// <summary>
/// User and password field for the Unity editor.
/// </summary>
/// <remarks>
/// See <see cref="Password"/> for more details.
/// 
/// The login field allows the user to specify a user name together with 
/// the password. In this case, the user name must not be set using the
/// <see cref="KeychainAttribute"/> (only the service name).
/// </remarks>
[Serializable]
public struct Login
{
    /// <summary>
    /// The username.
    /// </summary>
    public string User { get { return user; } set { user = value; } }
    [SerializeField] string user;
    /// <summary>
    /// The plaintext password if <see cref="useKeychain"/> is <c>false</c>.
    /// </summary>
    public string PlaintextPassword { get { return plaintextPassword; } set { plaintextPassword = value; } }
    [SerializeField] string plaintextPassword;
    /// <summary>
    /// Wether to use <see cref="Keychain"/> or store the password in plaintext.
    /// </summary>
    public bool UseKeychain { get { return useKeychain; } set { useKeychain = value; } }
    [SerializeField] bool useKeychain;

    /// <summary>
    /// Use this method to retrieve the password based on where it's stored.
    /// </summary>
    public string GetPassword(string service)
    {
        if (useKeychain) {
            return Keychain.Main.GetPassword(service, user);
        } else {
            return plaintextPassword;
        }
    }
}

[CustomPropertyDrawer(typeof(Password))]
[CustomPropertyDrawer(typeof(Login))]
public class LoginDrawer : PropertyDrawer
{
    public enum PasswordType {
        Plaintext,
        Keychain
    }

    const float typeWidth = 60;
    const float revealWidth = 16;
    const float revealHeight = 14;
    const float buttonWidth = 40;
    const float padding = 2;

    bool? hasPassword;
    bool revealPassword;
    string updatePassword;
    GUIContent hideContent;
    GUIContent revealContent;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.FindPropertyRelative("user") == null) {
            return base.GetPropertyHeight(property, label);
        } else {
            return 2 * base.GetPropertyHeight(property, label);
        }
    }

    // Draw the property inside the given rect
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (KeychainAttribute)fieldInfo.GetCustomAttributes(typeof(KeychainAttribute), true).FirstOrDefault();

        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw label
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Don't make child fields be indented
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        var fullWidth = position.width;
        var leftOffset = position.x;
        position.height = 15;

        var userField = property.FindPropertyRelative("user");
        if (userField != null) {
            position.width = typeWidth;
            GUI.Label(position, attr.UserLabel ?? "User");

            EditorGUI.BeginChangeCheck();
            {
                position.x += typeWidth + padding;
                position.width = fullWidth - typeWidth - padding;
                EditorGUI.PropertyField(position, userField, GUIContent.none);
            }
            if (EditorGUI.EndChangeCheck()) {
                hasPassword = null;
            }

            position.x = leftOffset;
            position.width = fullWidth;
            position.y += EditorGUIUtility.singleLineHeight + 1;
        }

        var useKeychain = property.FindPropertyRelative("useKeychain");
        position.width = typeWidth;
        EditorGUI.showMixedValue = useKeychain.hasMultipleDifferentValues;
        useKeychain.boolValue = (PasswordType)EditorGUI.EnumPopup(position, (PasswordType)(useKeychain.boolValue ? 1 : 0)) == PasswordType.Keychain ? true : false;

        if (!useKeychain.boolValue) {
            // Plaintext password
            var plaintextPassword = property.FindPropertyRelative("plaintextPassword");

            position.x += typeWidth + padding;
            position.width = fullWidth - typeWidth - revealWidth - 2 * padding;

            if (revealPassword) {
                EditorGUI.PropertyField(position, plaintextPassword, GUIContent.none);
            } else {
                EditorGUI.showMixedValue = plaintextPassword.hasMultipleDifferentValues;
                plaintextPassword.stringValue = EditorGUI.PasswordField(position, plaintextPassword.stringValue);
            }

            // Reveal password button
            if (hideContent == null) {
                hideContent = EditorGUIUtility.IconContent("animationvisibilitytoggleoff");
                revealContent = EditorGUIUtility.IconContent("animationvisibilitytoggleon");
            }

            position.x += position.width + padding;
            position.y += Mathf.RoundToInt((position.height - revealHeight) / 2);
            position.width = revealWidth;
            position.height = revealHeight;
            if (GUI.Button(position, revealPassword ? revealContent : hideContent, EditorStyles.label)) {
                revealPassword = !revealPassword;
            }
        } else {
            // Keychain password

            // Check attribute and if user is present
            var user = userField != null ? userField.stringValue : attr.User;
            string error = null;
            if (attr == null) {
                error = "KeychainAttribute missing on field";
            } else {
                if (hasPassword == null) {
                    if (userField != null && string.IsNullOrEmpty(userField.stringValue)) {
                        hasPassword = false;
                    } else if (userField == null && string.IsNullOrEmpty(attr.User)) {
                        error = "KeychainAttribute is missing user";
                    } else {
                        hasPassword = Keychain.Main.GetPassword(attr.Service, user) != null;
                    }
                }
            }

            if (error != null) {
                // Show error
                position.x += typeWidth + padding;
                position.width = fullWidth - typeWidth - padding;
                GUI.Label(position, error);
            
            } else if (updatePassword == null) {
                // Show button to set/change password
                position.x += typeWidth + padding;
                position.width = fullWidth - typeWidth - padding;
                if (GUI.Button(position, hasPassword == true ? "Change Password" : "Set Password", EditorStyles.miniButton)) {
                    updatePassword = "";
                }
            
            } else {
                // Set password UI
                position.x += typeWidth + padding;
                position.width = fullWidth - typeWidth - 2 * buttonWidth - 3 * padding;
                updatePassword = EditorGUI.PasswordField(position, updatePassword);

                position.x += position.width + padding;
                position.width = buttonWidth;
                if (GUI.Button(position, "Save", EditorStyles.miniButton)) {
                    Keychain.Main.SetPassword(attr.Service, user, updatePassword);
                    hasPassword = true;
                    updatePassword = null;
                }
                position.x += position.width + padding;
                position.width = buttonWidth;
                if (GUI.Button(position, "Cancel", EditorStyles.miniButton)) {
                    updatePassword = null;
                }
            }
        }

        // Set indent back to what it was
        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }
}

}
