//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Distro that creates ZIP files for each build.
/// </summary>
/// <remarks>
/// Zip distro uses 7zip, which is bundled with the Unity editor.
/// Supported formats are 7z, Bzip2 (Tar), Gzip (Tar), Uncompressed Tar, 
/// Wim, Xz (Tar) and Zip.
/// 
/// If the build directory contains multiple files, the archive will
/// contain the build folder. However, if the build directory only
/// contains a single file or directory, that file or directory will
/// be put at the root of the archive (e.g. macOS app bundles).
/// 
/// `.DS_Store`, `Thumbs.db`, `desktop.ini` and `build.json` files
/// are ignored.
/// 
/// The <see cref="prettyNames"/> field allows to configure different
/// archive file names per platform. If the archive includes the 
/// build directory, the name will also be applied to the archive's
/// root directory.
/// </remarks>
[CreateAssetMenu(fileName = "Zip Distro.asset", menuName = "Trimmer/Zip", order = 100)]
public class ZipDistro : DistroBase
{
    /// <summary>
    /// Used to customize archive file names.
    /// </summary>
    [System.Serializable]
    public struct PrettyName {
        /// <summary>
        /// The build target to apply the name to.
        /// </summary>
        public BuildTarget target;
        /// <summary>
        /// The archive file name to use for the build.
        /// </summary>
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
        Zip,      // Zip
        RawFile,  // Uncompressed (only supported for single files)
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
    /// <summary>
    /// Notarize macOS build.
    /// </summary>
    public NotarizationDistro macNotarization;

    static readonly string[] ZipIgnore = new string[] {
        ".DS_Store",
        "Thumbs.db",
        "desktop.ini",
        "*_BackUpThisFolder_ButDontShipItWithYourGame",
        "*_BurstDebugInformation_DoNotShip",
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

    static readonly string[] SevenZipNames = new string[] {
        "7z",
        "7z.exe",
        "7za",
        "7za.exe",
        "7zr",
        "7zr.exe"
    };

    protected string Get7ZipPath()
    {
        var toolsPath = Path.Combine(EditorApplication.applicationContentsPath, "Tools");
        foreach (var name in SevenZipNames) {
            var path = Path.Combine(toolsPath, name);

            if (File.Exists(path)) {
                return path;
            }
        }

        throw new FileNotFoundException($"ZipDistro: Could not find Unity's bundled 7zip executable within {toolsPath}");
    }

    protected string GetPrettyName(BuildPath buildPath, string fallback = null)
    {
        string prettyName = fallback;
        foreach (var name in prettyNames) {
            if (name.target == buildPath.target) {
                prettyName = name.name;
                break;
            }
        }

        if (appendVersion) {
            var versionSuffix = "";
            var buildInfo = BuildInfo.FromPath(buildPath.path);
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

            prettyName += versionSuffix;
        }

        return prettyName;
    }

    protected async Task<IEnumerable<BuildPath>> ZipBuilds(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        var queue = new Queue<BuildPath>(buildPaths);
        var results = new List<BuildPath>();

        task.Report(0, queue.Count);

        while (queue.Count > 0) {
            var next = queue.Dequeue();

            if (macNotarization != null) {
                await macNotarization.NotarizeIfMac(next, task);
            }

            results.Add(await Zip(next, task));

            task.baseStep++;
        }

        return results;
    }

    protected async Task<BuildPath> Zip(BuildPath buildPath, TaskToken task)
    {
        var target = buildPath.target;
        var path = buildPath.path;

        if (!File.Exists(path) && !Directory.Exists(path)) {
            throw new Exception("ZipDistro: Path to compress does not exist: " + path);
        }

        // Pass-through build artifact for RawFile format
        if (format == CompressionFormat.RawFile) {
            if (!File.Exists(path)) {
                throw new Exception("ZipDistro: Format RawFile only supports build targets with a single output file: " + path);
            }
            var prettySingleName = GetPrettyName(buildPath);
            if (prettySingleName != null) {
                var originalExtension = Path.GetExtension(path);
                var directory = Path.GetDirectoryName(path);
                var newPath = Path.Combine(directory, prettySingleName + originalExtension);
                File.Move(path, newPath);
                buildPath.path = newPath;
            }
            return buildPath;
        }

        if (ZipIgnorePatterns == null) {
            ZipIgnorePatterns = new Regex[ZipIgnore.Length];
            for (int i = 0; i < ZipIgnore.Length; i++) {
                var regex = Regex.Escape(ZipIgnore[i]).Replace(@"\*", ".*").Replace(@"\?", ".");
                ZipIgnorePatterns[i] = new Regex(regex);
            }
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
            throw new Exception("ZipDistro: Nothing to ZIP in directory: " + basePath);
        }

        var sevenZPath = Get7ZipPath();

        // Determine output path first to make it consistent and use absolute path
        // since the script will be run in a different working directory
        var prettyName = GetPrettyName(buildPath, fallback: Path.GetFileNameWithoutExtension(basePath));
        var extension = FileExtensions[(int)format];
        var zipName = prettyName + extension;
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
            "a '{0}' '{1}' -mx{2} {3}",
            outputPath, inputName, (int)compression, excludes
        );

        var startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.FileName = sevenZPath;
        startInfo.Arguments = args;
        startInfo.WorkingDirectory = Path.GetDirectoryName(basePath);

        task.Report(0, description: $"Archiving {inputName}");

        await Execute(new ExecutionArgs() { startInfo = startInfo }, task);

        if (!singleFile && prettyName != inputName) {
            await RenameRoot(outputPath, inputName, prettyName, task);
        }

        return new BuildPath(buildPath.profile, target, outputPath);
    }

    protected async Task RenameRoot(string archivePath, string oldName, string newName, TaskToken task)
    {
        if (!File.Exists(archivePath)) {
            throw new Exception("ZipDistro: Path to archive does not exist: " + archivePath);
        }

        var args = string.Format(
            "rn '{0}' '{1}' '{2}'",
            archivePath, oldName, newName
        );
        await Execute(new ExecutionArgs(Get7ZipPath(), args), task);
    }

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        await ZipBuilds(buildPaths, task);
    }
}

}
