//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using sttz.Trimmer.Options;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Extension of ZipDistro that also uploads the zip files to a server.
/// </summary>
/// <remarks>
/// The distro uses curl to upload files with different protocols. curl typically 
/// supports at least file, ftp(s), http(s) and smb(s). When built with libssh2,
/// it also support scp and sftp.
/// 
/// Note that the binary shipped with macOS does not support scp/sftp and might
/// have issues with ftpes. Install and use a current curl binary using Homebrew:
/// 
///     brew install curl
/// 
/// If you want to use public key authentication, specify the username in the
/// url and leave the user field blank.
/// 
/// Note that curl will not create remote directories. Make sure the upload 
/// path exists on the server.
/// </remarks>
[CreateAssetMenu(fileName = "Upload Distro.asset", menuName = "Trimmer/Upload", order = 100)]
public class UploadDistro : ZipDistro
{
    public string curlPath;
    public string uploadUrl;

    const string keychainService = "UploadDistro";
    [Keychain(keychainService)] public Login login;

    protected override async Task RunDistribute(IEnumerable<BuildPath> buildPaths, TaskToken task)
    {
        if (string.IsNullOrEmpty(curlPath))
            throw new Exception("UploadDistro: Path to curl not set.");

        if (!File.Exists(curlPath))
            throw new Exception("UploadDistro: curl not found at path: " + curlPath);

        if (string.IsNullOrEmpty(uploadUrl))
            throw new Exception("UploadDistro: No upload URL set.");

        if (!string.IsNullOrEmpty(login.User) && login.GetPassword(keychainService) == null)
            throw new Exception("UploadDistro: No password set for user: " + login.User);

        task.Report(0, 2, description: "Archiving builds");

        IEnumerable<BuildPath> zipPaths;
        var child = task.StartChild("Zip Builds");
        try {
            zipPaths = await ZipBuilds(buildPaths, child);
        } finally {
            child.Remove();
        }

        task.Report(1, description: "Uploading builds");

        child = task.StartChild("Upload Builds");
        try {
            foreach (var path in zipPaths) {
                await Upload(path, child);
            }
        } finally {
            child.Remove();
        }
    }

    protected async Task Upload(BuildPath zipPath, TaskToken task)
    {
        var archive = zipPath.path;
        if (!File.Exists(archive))
            throw new System.Exception("UploadDistro: Archive file does not exist: " + archive);

        // Append a / to the url if necessary, otherwise curl treats the last part as a file name
        var url = uploadUrl;
        if (url[url.Length - 1] != '/') {
            url += "/";
        }

        string input = null;
        if (!string.IsNullOrEmpty(login.User)) {
            input = string.Format("-u \"{0}:{1}\"", login.User, login.GetPassword(keychainService));
        }

        var arguments = string.Format(
            "-T '{0}' {1} --ssl -v '{2}'",
            archive, input != null ? "-K -" : "", url
        );
        task.Report(0, $"Uploading {Path.GetFileName(archive)} to {uploadUrl}");
        await Execute(new ExecutionArgs(curlPath, arguments) { input = input }, task);
    }
}

}
