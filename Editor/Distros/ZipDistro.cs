using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Zip up the builds.
/// </summary>
/// <remarks>
/// Compresses the builds using 7zip, which is bundled with Unity.
/// 
/// Special processing is done to make the archives more friendly:
/// - If the folder to the archived only contains a single file, 
///   the file is archived instead.
/// - If a pretty name speicifed for a platform and the archive
///   contains a root folder, the name is applied to this root
///   folder as well.
/// - Desktop files like .DS_Store, Thumbs.db and dekstop.ini 
///   are excluded.
/// </remarks>
[CreateAssetMenu(fileName = "Zip Distro.asset", menuName = "Trimmer/Distro/Zip")]
public class ZipDistro : DistroBase
{
    [System.Serializable]
    public struct PrettyName {
        public BuildTarget target;
        public string name;
    }

    /// <summary>
    /// The compression level to use (higher = slower but smaller size).
    /// </summary>
    public enum CompressionLevel {
        Copy = 0,
        Fastest = 1,
        Fast = 3,
        Normal = 5,
        Maximum = 7,
        Ultra = 9
    }

    /// <summary>
    /// The compression format to use.
    /// </summary>
    public enum CompressionFormat {
        Undefined,
        SevenZip, // 7-zip
        TBZ,      // Bzip2 in Tar
        TGZ,      // Gzip in Tar
        Tar,      // Uncompressed Tar
        Wim,      // Windows Imaging Format
        TXZ,      // XZ in Tar
        Zip       // Zip
    }

    /// <summary>
    /// Define nicer names to use per-platform. The name will be used
    /// for the archive file as well as the root folder in the zip
    /// (if any).
    /// </summary>
    public PrettyName[] prettyNames;
    /// <summary>
    /// The compression format of the resulting archives.
    /// </summary>
    public CompressionFormat format = CompressionFormat.Zip;
    /// <summary>
    /// The compression level used when creating the archives.
    /// </summary>
    public CompressionLevel compression = CompressionLevel.Normal;
    /// <summary>
    /// Append the version number to the ZIP file name.
    /// </summary>
    public bool appendVersion;

    static readonly string[] ZipIgnore = new string[] {
        ".DS_Store",
        "Thumbs.db",
        "desktop.ini",
        BuildInfo.DEFAULT_NAME
    };
    static Regex[] ZipIgnorePatterns;

    static readonly string[] FileExtensions = new string[] {
        ".zip",
        ".7z",
        ".tbz",
        ".tgz",
        ".tar",
        ".wim",
        ".txz",
        ".zip"
    };

    protected string Get7ZipPath()
    {
        var path = Path.Combine(EditorApplication.applicationContentsPath, "Tools/7za");
        if (!File.Exists(path)) {
            Debug.LogError("ZipDistro: Could not find 7za bundled with Unity at path: " + path);
            return null;
        }
        return path;
    }

    protected string GetPrettyName(BuildTarget target)
    {
        foreach (var name in prettyNames) {
            if (name.target == target) {
                return name.name;
            }
        }
        return null;
    }

    protected IEnumerator ZipBuilds(IEnumerable<BuildPath> buildPaths)
    {
        var queue = new Queue<BuildPath>(buildPaths);
        var results = new List<BuildPath>();
        while (queue.Count > 0) {
            var next = queue.Dequeue();
            yield return Zip(next);
            var result = GetSubroutineResult<BuildPath>();
            if (result.path == null) {
                yield return null; yield break;
            } else {
                results.Add(result);
            }
        }
        yield return results;
    }

    protected IEnumerator Zip(BuildPath buildPath)
    {
        var target = buildPath.target;
        var path = buildPath.path;

        if (!File.Exists(path) && !Directory.Exists(path)) {
            Debug.LogError("ZipDistro: Path to compress does not exist: " + path);
            yield return null; yield break;
        }

        if (ZipIgnorePatterns == null) {
            ZipIgnorePatterns = new Regex[ZipIgnore.Length];
            for (int i = 0; i < ZipIgnore.Length; i++) {
                var regex = Regex.Escape(ZipIgnore[i]).Replace(@"\*", ".*").Replace(@"\?", ".");
                ZipIgnorePatterns[i] = new Regex(regex);
            }
        }

        var sevenZPath = Get7ZipPath();
        if (sevenZPath == null) {
            yield return null; yield break;
        }

        // Path can point to executable file but there might be files
        // in the containing directory we need as well
        var basePath = OptionHelper.GetBuildBasePath(path);

        // Check the files in containing directory
        var files = new List<string>(Directory.GetFileSystemEntries(basePath));
        for (int i = files.Count - 1; i >= 0; i--) {
            var filename = Path.GetFileName(files[i]);
            foreach (var pattern in ZipIgnorePatterns) {
                if (pattern.IsMatch(filename)) {
                    files.RemoveAt(i);
                    goto ContinueOuter;
                }
            }
            ContinueOuter:;
        }

        if (files.Count == 0) {
            Debug.LogError("ZipDistro: Nothing to ZIP in directory: " + basePath);
            yield return null; yield break;
        }

        // Determine output path first to make it consistent and use absolute path
        // since the script will be run in a different working directory
        var prettyName = GetPrettyName(target);
        if (prettyName == null) {
            prettyName = Path.GetFileNameWithoutExtension(basePath);
        }

        var versionSuffix = "";
        if (appendVersion) {
            var buildInfo = BuildInfo.FromPath(path);
            if (buildInfo != null) {
                if (!buildInfo.version.IsDefined) {
                    Debug.LogWarning("ZipDistro: build.json exists but contains no version");
                } else {
                    versionSuffix = " " + buildInfo.version.MajorMinorPatch;
                }
            }

            if (versionSuffix.Length == 0) {
                versionSuffix = " " + Application.version;
            }
        }

        var extension = FileExtensions[(int)format];
        var zipName = prettyName + versionSuffix + extension;
        zipName = zipName.Replace(" ", "_");

        var outputPath = Path.Combine(Path.GetDirectoryName(basePath), zipName);
        outputPath = Path.GetFullPath(outputPath);

        // Delete existing archive, otherwise 7za will update it
        if (File.Exists(outputPath)) {
            File.Delete(outputPath);
        }

        // In case it only contains a single file, just zip that file
        var singleFile = false;
        if (files.Count == 1) {
            singleFile = true;
            basePath = files[0];
        }

        // Run 7za command to create ZIP file
        var excludes = "";
        foreach (var pattern in ZipIgnore) {
            excludes += @" -xr\!'" + pattern + "'";
        }

        var inputName = Path.GetFileName(basePath);
        var args = string.Format(
            "a '{0}' '{1}' -r -mx{2} {3}",
            outputPath, inputName, (int)compression, excludes
        );

        var startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.FileName = sevenZPath;
        startInfo.Arguments = args;
        startInfo.WorkingDirectory = Path.GetDirectoryName(basePath);

        Debug.Log("ZipDistro: Archiving " + inputName);
        yield return Execute(startInfo);

        var exitcode = GetSubroutineResult<int>();
        if (exitcode != 0) {
            yield return null; yield break;
        }
        
        if (!singleFile && prettyName != inputName) {
            yield return RenameRoot(outputPath, inputName, prettyName);
            var success = GetSubroutineResult<bool>();
            if (!success) {
                yield return null; yield break;
            }
        }

        Debug.Log("ZipDistro: Archived to: " + outputPath);
        yield return new BuildPath(target, outputPath);
    }

    protected IEnumerator RenameRoot(string archivePath, string oldName, string newName)
    {
        if (!File.Exists(archivePath)) {
            Debug.LogError("ZipDistro: Path to archive does not exist: " + archivePath);
            yield return false; yield break;
        }

        var sevenZPath = Get7ZipPath();
        if (sevenZPath == null) {
            yield return false; yield break;
        }

        var args = string.Format(
            "rn '{0}' '{1}' '{2}'",
            archivePath, oldName, newName
        );

        yield return Execute(sevenZPath, args);

        var exitcode = GetSubroutineResult<int>();
        yield return exitcode == 0;
    }

    protected override IEnumerator DistributeCoroutine(IEnumerable<BuildPath> buildPaths, bool forceBuild)
    {
        yield return ZipBuilds(buildPaths);
        if (GetSubroutineResult<IEnumerable<BuildPath>>() != null) {
            Debug.Log("ZipDistro: Archives created successfully");
            yield return true;
        } else {
            yield return false;
        }
    }
}

}