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
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Distro that uploads builds to GOG (Good Old Games).
/// </summary>
/// <remarks>
/// Uses GOG Galaxy Pipeline Builder to upload the builds.
/// 
/// You need to get GOG's Build Creator app or the Pipeline Builder
/// as a separate download. These are only available to developer
/// accounts that have them activated.
/// 
/// Builds are configured using JSON files. You can use the Build
/// Creator to set up the build and the export it to JSON.
/// 
/// Since Trimmer Distros are path-independent, the project JSON is
/// first processed and variables substituted. Available variables are:
/// - `{{BuiltTarget}}`: Path to the last build of the given target
/// - `{{project}}`: Path to the Unity project root
/// - `{{projects}}`: Path to the original projects folder
/// 
/// The `{{BuildTarget}}` variables come from the given Build Profiles. Adding a 
/// variable from a build target that doesn't exist in any Build Profile will 
/// result in an error. Extra build targets that are never inserted into a project JSON
/// will result in a warning. If multiple Build Profiles share a target, the last
/// one will be used.
/// </remarks>
[CreateAssetMenu(fileName = "GOG Distro.asset", menuName = "Trimmer/GOG", order = 100)]
public class GOGDistro : DistroBase
{
    /// <summary>
    /// Default install location of the Build Creator app.
    /// </summary>
    const string BUILD_CREATOR_PATH = "/Applications/Build Creator.app/Contents";
    /// <summary>
    /// Name of the Pipeline Builder executable.
    /// </summary>
    const string PIPLINE_BUILDER_COMMAND = "GOGGalaxyPipelineBuilder";

    /// <summary>
    /// Path to the GOG Galaxy Pipeline Builder.
    /// </summary>
    public string pipelineBuilderPath;
    /// <summary>
    /// The user used to log into GOG.
    /// </summary>
    [Keychain(keychainService)] public Login gogLogin;
    const string keychainService = "GOGDistro";
    /// <summary>
    /// The folder where the project JSON files are stored.
    /// </summary>
    public string projectsFolder;

    /// <summary>
    /// Path to file containing patterns of files to be ignored.
    /// </summary>
    public string ignoreList;
    /// <summary>
    /// The branch to upload the build to.
    /// </summary>
    [Keychain(branchKeychainService, "Name")] public Login branch;
    const string branchKeychainService = "GOGDistroBranch";
    /// <summary>
    /// Version to use instead of project version.
    /// </summary>
    public string overrideVersion;

    /// <summary>
    /// Notarize macOS build.
    /// </summary>
    public NotarizationDistro macNotarization;

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        // Check Pipeline Builder Executable
        var cmd = FindPipelineBuilder();

        // Check User
        if (string.IsNullOrEmpty(gogLogin.User))
            throw new Exception("GOGDistro: No GOG user set.");

        if (gogLogin.GetPassword(keychainService) == null)
            throw new Exception("GOGDistro: No GOG password found in Keychain.");

        // Check projects
        if (string.IsNullOrEmpty(projectsFolder) || !Directory.Exists(projectsFolder))
            throw new Exception("GOGDistro: Path to projects folder not set.");

        // Check ignore list
        if (!string.IsNullOrEmpty(ignoreList) && !File.Exists(ignoreList))
            throw new Exception("GOGDistro: Ignore list could not be found: " + ignoreList);

        // Process projects
        var tempDir = FileUtil.GetUniqueTempPathInProject();
        try {
            Directory.CreateDirectory(tempDir);

            var targets = new HashSet<BuildTarget>(buildPaths.Select(p => p.target));
            var projects = new List<string>();
            string convertError = null;
            foreach (var file in Directory.GetFiles(projectsFolder)) {
                if (Path.GetExtension(file).ToLower() != ".json") continue;

                var contents = PathVarRegex.Replace(File.ReadAllText(file), (match) => {
                    var platformName = match.Groups[1].Value.ToLower();

                    if (platformName == "project") {
                        return Path.GetDirectoryName(Application.dataPath);
                    } else if (platformName == "projects") {
                        return Path.GetFullPath(projectsFolder);
                    }

                    BuildTarget target;
                    try {
                        target = (BuildTarget)System.Enum.Parse(typeof(BuildTarget), platformName, true);
                    } catch {
                        convertError = $"Invalid build target path variable '{platformName}' in project JSON: {file}";
                        return "";
                    }

                    if (!buildPaths.Any(p => p.target == target)) {
                        convertError = $"Build target '{platformName}' not part of given build profile(s) in project JSON: {file}";
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
                projects.Add(targetPath);
            }

            if (convertError != null) {
                throw new Exception($"GOGDistro: {convertError}");
            }

            if (targets.Count > 0) {
                Debug.LogWarning("GOGDistro: Not all build targets filled into variables. Left over: " 
                    + string.Join(", ", targets.Select(t => t.ToString()).ToArray()));
            }

            task.Report(0, targets.Count + 1);

            // Notarize mac builds
            if (macNotarization != null) {
                task.Report(0, description: "Notarizing macOS builds");
                foreach (var path in buildPaths.Where(p => p.target == BuildTarget.StandaloneOSX)) {
                    await macNotarization.Notarize(path, task);
                }
            }

            // Build
            task.baseStep++;
            foreach (var project in projects) {
                var args = string.Format(
                    "build-game '{0}' --username='{1}' --password='{2}' --version={3}", 
                    Path.GetFullPath(project), gogLogin.User, gogLogin.GetPassword(keychainService), 
                    string.IsNullOrEmpty(overrideVersion) ? Application.version : overrideVersion
                );

                if (!string.IsNullOrEmpty(ignoreList)) {
                    args += $" --ignore_list='{Path.GetFullPath(ignoreList)}'";
                }

                if (!string.IsNullOrEmpty(branch.User)) {
                    args += $" --branch='{branch.User}'";
                    
                    var pwd = branch.GetPassword(branchKeychainService);
                    if (pwd != null) {
                        args += $" --branch_password='{pwd}'";
                    }
                }

                task.Report(0, description: $"Uploading {Path.GetFileName(project)}");

                await Execute(new ExecutionArgs(cmd, args), task);

                task.baseStep++;
            }
        } finally {
            // Always clean up temporary files
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Regex used to match path variables in projects.
    /// </summary>
    static readonly Regex PathVarRegex = new Regex(@"{{([^}]*)}}");

    /// <summary>
    /// Look for the Pipeline Builder command line program.
    /// </summary>
    string FindPipelineBuilder()
    {
        if (string.IsNullOrEmpty(pipelineBuilderPath)) {
            // Auto-detect Pipeline Builder installed as part of Build Creator
            var path = Path.Combine(BUILD_CREATOR_PATH, PIPLINE_BUILDER_COMMAND);
            if (File.Exists(path)) return path;

            throw new Exception("GOGDistro: GOGGalaxyPipelineBuilder path not set.");
        }

        if (Directory.Exists(pipelineBuilderPath)) {
            var path = Path.Combine(pipelineBuilderPath, PIPLINE_BUILDER_COMMAND);
            if (File.Exists(path)) return path;

            throw new Exception("GOGDistro: GOGGalaxyPipelineBuilder could not be found at path: " + path);
        }

        if (!File.Exists(pipelineBuilderPath)) {
            throw new Exception("GOGDistro: Path to Pipeline Builder does not exist: " + pipelineBuilderPath);
        }

        return pipelineBuilderPath;
    }
}

}
