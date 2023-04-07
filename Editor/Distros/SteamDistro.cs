//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using sttz.Trimmer.Options;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Distro that uploads builds to Steam.
/// </summary>
/// <remarks>
/// Uses Steam's ContentBuilder to upload the builds to Steam.
/// 
/// You need the Steam SDK (or only the ContentBuilder folder inside its tools
/// directory as well as a Steam developer account.
/// 
/// The upload is configured using VDF scripts, refer to Valve's documentation
/// on how they are set up.
/// 
/// Trimmer distros are path independent, i.e. the build path can be anything.
/// The Build Profile decides on the path and then passes it on to the 
/// distribution for processing. However, Steam's scripts only allow for 
/// configuring fixed paths and don't support variable substitution.
/// 
/// Therefore, SteamDistro performs its own variable substitution to fill in 
/// the dynamic build paths into the VDF scripts. It takes a directory of
/// scripts as input, processes those scripts and then writes them to a 
/// temporary directory. Your scripts will therefore not run from the scripts
/// folder but from an essentially random path. Use variables to refer to
/// the build, assets inside the script folder or files in the Unity project.
/// 
/// Supported variables:
/// - `{{BuiltTarget}}`: Path to the last build of the given target
/// - `{{project}}`: Path to the Unity project root
/// - `{{scripts}}`: Path to the original scripts folder
/// 
/// The `{{BuildTarget}}` variables come from the given Build Profiles. Adding a 
/// variable from a build target that doesn't exist in any Build Profile will 
/// result in an error. Extra build targets that are never inserted into a script
/// will result in a warning. If multiple Build Profiles share a target, the last
/// one will be used.
/// </remarks>
[CreateAssetMenu(fileName = "Steam Distro.asset", menuName = "Trimmer/Steam", order = 100)]
public class SteamDistro : DistroBase
{
    /// <summary>
    /// Path to the Steam SDK.
    /// </summary>
    public string steamdSDKPath;
    /// <summary>
    /// The user used to log into steam.
    /// </summary>
     [Keychain(keychainService)] public Login steamLogin;
     const string keychainService = "SteamDistro";
    /// <summary>
    /// The folder where the VDF content builder scripts are stored.
    /// </summary>
    public string scriptsFolder;
    /// <summary>
    /// Main app VDF script file.
    /// </summary>
    public string appScript;

    /// <summary>
    /// Notarize macOS build.
    /// </summary>
    public NotarizationDistro macNotarization;

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        // Check SDK
        var cmd = FindSteamCmd();

        // Check User
        if (string.IsNullOrEmpty(steamLogin.User))
           throw new Exception("SteamDistro: No Steam user set.");

        // Check scripts
        if (string.IsNullOrEmpty(scriptsFolder) || !Directory.Exists(scriptsFolder))
            throw new Exception("SteamDistro: Path to scripts folder not set.");

        if (string.IsNullOrEmpty(appScript))
            throw new Exception("SteamDistro: Name of app script not set.");

        var appScriptPath = Path.Combine(scriptsFolder, appScript);
        if (!File.Exists(appScriptPath))
            throw new Exception("SteamDistro: App script not found in scripts folder.");

        // Process scripts
        var tempDir = FileUtil.GetUniqueTempPathInProject();
        try {
            Directory.CreateDirectory(tempDir);

            var targets = new HashSet<BuildTarget>(buildPaths.Select(p => p.target));
            string convertError = null;
            foreach (var file in Directory.GetFiles(scriptsFolder)) {
                if (Path.GetExtension(file).ToLower() != ".vdf") continue;

                var contents = PathVarRegex.Replace(File.ReadAllText(file), (match) => {
                    var platformName = match.Groups[1].Value.ToLower();

                    if (platformName == "project") {
                        return Path.GetDirectoryName(Application.dataPath);
                    } else if (platformName == "scripts") {
                        return Path.GetFullPath(scriptsFolder);
                    }

                    BuildTarget target;
                    try {
                        target = (BuildTarget)System.Enum.Parse(typeof(BuildTarget), platformName, true);
                    } catch {
                        convertError = $"SteamDistro: Invalid build target path variable '{platformName}' in VDF script: {file}";
                        return "";
                    }

                    if (!buildPaths.Any(p => p.target == target)) {
                        convertError = $"SteamDistro: Build target '{platformName}' not part of given build profile(s) in VDF script: {file}";
                        return "";
                    }
                    targets.Remove(target);

                    var path = buildPaths.Where(p => p.target == target).Select(p => p.path).First();
                    path = OptionHelper.GetBuildBasePath(path);

                    return Path.GetFullPath(path);
                });
                if (convertError != null) break;

                var targetPath = Path.Combine(tempDir, Path.GetFileName(file));
                File.WriteAllText(targetPath, contents);
            }

            if (convertError != null) {
                throw new Exception($"SteamDistro: {convertError}");
            }

            if (targets.Count > 0) {
                Debug.LogWarning("SteamDistro: Not all build targets filled into variables. Left over: " 
                    + string.Join(", ", targets.Select(t => t.ToString()).ToArray()));
            }

            // Notarize mac builds
            if (macNotarization != null) {
                task.Report(0, description: "Notarizing macOS builds");
                foreach (var path in buildPaths.Where(p => p.target == BuildTarget.StandaloneOSX)) {
                    await macNotarization.Notarize(path, task);
                }
            }

            // Build
            string loginArgs;
            var password = steamLogin.GetPassword(keychainService);
            if (string.IsNullOrEmpty(password)) {
                loginArgs = $"'{steamLogin.User}'";
            } else {
                loginArgs = $"'{steamLogin.User}' '{password}'";
            }

            var scriptPath = Path.GetFullPath(Path.Combine(tempDir, appScript));
            var args = $"+login {loginArgs} +run_app_build '{scriptPath}' +quit";
            await Execute(new ExecutionArgs(cmd, args) { 
                onOutput = (output) => {
                    if (output.Contains("Logged in OK")) {
                        task.Report(0, description: "Logged in");
                    } else if (output.Contains("Building depot")) {
                        var match = BuildingDepotRegex.Match(output);
                        if (match.Success) {
                            task.Report(0, description: $"Building depo {match.Groups[1].Value}");
                        }
                    } else if (output.Contains("")) {
                        var match = SuccessBuildIdRegex.Match(output);
                        if (match.Success) {
                            Debug.Log("SteamDistro: Build uploaded, ID = " + match.Groups[1].Value);
                        }
                    }
                }
            }, task);
        } finally {
            // Always clean up temp files
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Path to the SteamCMD inside the SDK.
    /// </summary>
    static readonly string[] SteamCMDPath = {
        "tools",
        "ContentBuilder",
        #if UNITY_EDITOR_OSX
        "builder_osx",
        "steamcmd.sh",
        #elif UNITY_EDITOR_LINUX
        "builder_linux",
        "steamcmd.sh",
        #elif UNITY_EDITOR_WIN
        "builder",
        "steamcmd.exe",
        #else
        #error "Unknown Unity Editor platform"
        #endif
    };

    /// <summary>
    /// Regex used to match path variables in scripts.
    /// </summary>
    static readonly Regex PathVarRegex = new Regex(@"{{([^}]*)}}");
    /// <summary>
    /// Regex used to match SteamCmd output.
    /// </summary>
    static readonly Regex BuildingDepotRegex = new Regex(@"Building depot (\d+)");
    /// <summary>
    /// Regex used to match SteamCmd output.
    /// </summary>
    static readonly Regex SuccessBuildIdRegex = new Regex(@"Successfully finished appID.*\(BuildID (\d+)\)");

    /// <summary>
    /// Find the SteamCmd tool in the Steam SDK.
    /// </summary>
    public string FindSteamCmd()
    {
        if (string.IsNullOrEmpty(steamdSDKPath))
            throw new Exception("SteamDistro: Steam SDK path not set.");

        if (!File.Exists(steamdSDKPath) && !Directory.Exists(steamdSDKPath))
            throw new Exception("SteamDistro: Steam SDK path does not exist.");

        if (File.Exists(steamdSDKPath)) {
            // Path points to a file, assume it's SteamCmd
            return steamdSDKPath;
        }

        // Look for SteamCmd in given SDK path,
        // so that we can be pointed to a sub-directory of the SDK
        // but still can find the executable at its sub-path.
        var current = steamdSDKPath;
        for (int i = 0; i < SteamCMDPath.Length; i++) {
            var next = Path.Combine(current, SteamCMDPath[i]);

            var isExecutable = (i == SteamCMDPath.Length - 1);
            if (isExecutable) {
                if (File.Exists(next)) {
                    return next; // Found SteamCmd executable
                }
            } else {
                if (!Directory.Exists(next)) {
                    continue; // Try with next path segment
                } else {
                    current = next; // Path matched, continue search inside
                }
            }
        }

        throw new Exception($"SteamDistro: Could not find {SteamCMDPath.Last()} at the SDK path: {steamdSDKPath}");
    }
}

}
