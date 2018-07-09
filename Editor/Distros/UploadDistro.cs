using System.Collections;
using System.Collections.Generic;
using System.IO;
using sttz.Trimmer.Options;
using UnityEditor;
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
///     brew install curl --with-libssh2
/// 
/// If you want to use public key authentication, specify the username in the
/// url and leave the user field blank.
/// 
/// Note that curl will not create remote directories. Make sure the upload 
/// path exists on the server.
/// </remarks>
[CreateAssetMenu(fileName = "Upload Distro.asset", menuName = "Trimmer/Distro/Upload")]
public class UploadDistro : ZipDistro
{
    public string curlPath;
    public string uploadUrl;

    const string keychainService = "UploadDistro";
    [Keychain(keychainService)] public Login login;

    protected override IEnumerator DistributeCoroutine(IEnumerable<BuildPath> buildPaths, bool forceBuild)
    {
        if (string.IsNullOrEmpty(curlPath)) {
            Debug.LogError("UploadDistro: Path to curl not set.");
            yield return false; yield break;
        }

        if (!File.Exists(curlPath)) {
            Debug.LogError("UploadDistro: curl not found at path: " + curlPath);
            yield return false; yield break;
        }

        if (string.IsNullOrEmpty(uploadUrl)) {
            Debug.LogError("UploadDistro: No upload URL set.");
            yield return false; yield break;
        }

        if (!string.IsNullOrEmpty(login.User) && login.GetPassword(keychainService) == null) {
            Debug.LogError("UploadDistro: No password set for user: " + login.User);
            yield return false; yield break;
        }

        yield return ZipBuilds(buildPaths);
        var zipPaths = GetSubroutineResult<IEnumerable<BuildPath>>();
        if (zipPaths == null) {
            yield return false; yield break;
        }

        foreach (var path in zipPaths) {
            yield return Upload(path);
            if (!GetSubroutineResult<bool>()) {
                yield return false; yield break;
            }
        }

        Debug.Log("UploadDistro: Files uploaded successfully");
        yield return true;
    }

    protected IEnumerator Upload(BuildPath zipPath)
    {
        var archive = zipPath.path;
        if (!File.Exists(archive)) {
            Debug.LogError("UploadDistro: Archive file does not exist: " + archive);
            yield return false; yield break;
        }

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

        Debug.Log("UploadDistro: Uploading " + Path.GetFileName(archive) + " to " + uploadUrl);
        yield return Execute(curlPath, arguments, input);
        var exitcode = GetSubroutineResult<int>();

        yield return exitcode == 0;
    }
}

}