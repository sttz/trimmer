//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace sttz.Trimmer.Options
{

/// <summary>
/// Helper class to access secure password storage.
/// </summary>
/// <remarks>
/// On macOS the system Keychain is used via the `security` command 
/// line utility. On Windows, ProtectedData is used to encrypt passwords,
/// which are then stored in EditorPrefs (ProtectedData uses Crypt32.dll's
/// CryptProtectData under the hood).
/// </remarks>
public abstract class Keychain
{
    // ------ Static ------

    /// <summary>
    /// Keychain instance that is backed by the system keychain.
    /// </summary>
    public static Keychain Main {
        get {
            if (_main == null) {
                #if UNITY_EDITOR_OSX
                _main = new MacKeychain();
                #elif UNITY_EDITOR_WIN
                _main = new WindowsKeychain();
                #else
                #warning Keychain has no implementation for your platform
                #endif
            }
            return _main;
        }
    }
    static Keychain _main;

    // ------ API ------

    /// <summary>
    /// Get a password from the Keychain.
    /// </summary>
    /// <param name="service">Name of the service the password belongs to</param>
    /// <param name="name">Name of the password</param>
    /// <returns>The password or null if it doesn't exist in the Keychain</returns>
    public abstract string GetPassword(string service, string name);

    /// <summary>
    /// Add or update a password in the Keychain.
    /// </summary>
    /// <param name="service">Name of the service the password belongs to</param>
    /// <param name="name">Name of the password</param>
    /// <param name="password">The password</param>
    public abstract void SetPassword(string service, string name, string password);

    // ------ Implementations ------

    /// <summary>
    /// Keychain implementation for macOS using `security` command line utility
    /// for the macOS Keychain.
    /// </summary>
    class MacKeychain : Keychain
    {
        override public string GetPassword(string service, string name)
        {
            var command = string.Format(
                "find-generic-password -a '{0}' -s '{1}' -w", 
                name, service
            );
            string output, error;
            var code = OptionHelper.RunScript("security", command, out output, out error);
            if (code != 0) {
                return null;
            } else {
                return output.TrimEnd('\n');
            }
        }

        override public void SetPassword(string service, string name, string password)
        {
            // We use the interactive mode of security here that allows us to
            // pipe the command to stdin and thus avoid having the password
            // exposed in the process table.
            var command = string.Format(
                "add-generic-password -U -a '{0}' -s '{1}' -w '{2}'\n", 
                name, service, password
            );
            string output, error;
            var code = OptionHelper.RunScript("security", "-i", command, out output, out error);
            if (code != 0) {
                Debug.LogError("Failed to store password in Keychain: " + error);
            }
        }
    }

    /// <summary>
    /// Keychain implementation for Windows, using the ProtectedData class
    /// and storing the encrypted data in EditorPrefs.
    /// </summary>
    /// <remarks>
    /// Unity ships with System.Security.dll but it's not referenced by default.
    /// We could reference it using the mcs.rsp file but that would require 
    /// per-project setup steps. Instead, we load the assembly dynamically and
    /// use reflection to call the Protect/Unprotect methods.
    /// </remarks>
    class WindowsKeychain : Keychain
    {
        // byte[] Protect (byte[] userData, byte[] optionalEntropy, DataProtectionScope scope) 
        MethodInfo protectMethod;
        // byte[] Unprotect (byte[] encryptedData, byte[] optionalEntropy, DataProtectionScope scope)
        MethodInfo unprotectMethod;

        byte[] entropy = new byte[] {
            0x54, 0x52, 0x49, 0x4D, 0x4D, 0x45, 0x52, 0x46, 
            0x52, 0x41, 0x4D, 0x45, 0x57, 0x4F, 0x52, 0x4B
        };

        bool FindMethods()
        {
            if (protectMethod != null && unprotectMethod != null)
                return true;

            try {
                var assembly = Assembly.Load(new AssemblyName() { Name = "System.Security" });
                var ProtectedData = assembly.GetType("System.Security.Cryptography.ProtectedData");
                protectMethod = ProtectedData.GetMethod("Protect", BindingFlags.Static | BindingFlags.Public);
                unprotectMethod = ProtectedData.GetMethod("Unprotect", BindingFlags.Static | BindingFlags.Public);
                return protectMethod != null && unprotectMethod != null;
            } catch (Exception e) {
                Debug.LogError("WindowsKeychain: " + e);
                return false;
            }
        }

        override public string GetPassword(string service, string name)
        {
            if (!FindMethods()) return null;

            try {
                var encoded = EditorPrefs.GetString(service + "." + name, null);
                if (string.IsNullOrEmpty(encoded)) return null;

                var encrypted = Convert.FromBase64String(encoded);
                var decrypted = (byte[])unprotectMethod.Invoke(null, new object[] { encrypted, entropy, 0 });

                return System.Text.Encoding.UTF8.GetString(decrypted);
            } catch (Exception e) {
                Debug.LogError("WindowsKeychain: " + e);
                return null;
            }
        }

        override public void SetPassword(string service, string name, string password)
        {
            if (!FindMethods()) return;

            try {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var encryped = (byte[])protectMethod.Invoke(null, new object[] { bytes, entropy, 0 });
                var encoded = Convert.ToBase64String(encryped);
                EditorPrefs.SetString(service + "." + name, encoded);
            } catch (Exception e) {
                Debug.LogError("WindowsKeychain: " + e);
            }
        }
    }
}

}

#endif
