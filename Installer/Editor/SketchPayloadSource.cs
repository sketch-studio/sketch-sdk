using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using UnityEditor;
using UnityEngine.Networking;

namespace Sketch.Sdk.Installer
{
    /// <summary>Gets the package root of a tag into a temp folder: archive zip first, `git clone --depth 1` as fallback (private repo, proxy).</summary>
    static class SketchPayloadSource
    {
        const int CLONE_TIMEOUT_MS = 120000;

        /// <summary>Returns the extracted package root folder. Throws on failure; caller deletes workDir.</summary>
        public static string Fetch(string tag, string workDir)
        {
            Directory.CreateDirectory(workDir);
            string zipError;
            try { return DownloadZip(tag, workDir); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { zipError = e.Message; }

            UnityEngine.Debug.LogWarning("[SketchSdkInstaller] Archive download failed (" + zipError + "); trying git clone.");
            string cloneDir = Path.Combine(workDir, "clone");
            Clone(tag, cloneDir);
            return cloneDir;
        }

        static string DownloadZip(string tag, string workDir)
        {
            string zipPath = Path.Combine(workDir, "payload.zip");
            string url = string.Format(SketchInstallerPaths.ARCHIVE_URL_FORMAT, tag);
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(zipPath) { removeFileOnAbort = true };
                request.timeout = 120;
                UnityWebRequestAsyncOperation op = request.SendWebRequest();
                DateTime giveUpAt = DateTime.UtcNow.AddSeconds(150);
                while (!op.isDone)
                {
                    if (DateTime.UtcNow > giveUpAt)
                    {
                        request.Abort();
                        throw new TimeoutException("download timed out");
                    }
                    if (EditorUtility.DisplayCancelableProgressBar("Sketch SDK", "Downloading " + tag + "...", request.downloadProgress))
                    {
                        request.Abort();
                        throw new OperationCanceledException("download cancelled");
                    }
                    Thread.Sleep(30);
                }
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException(request.error + " (" + url + ")");
            }

            string extractDir = Path.Combine(workDir, "unzipped");
            Extract(zipPath, extractDir);
            // GitHub archives hold a single top folder: <repo>-<version>/
            string[] dirs = Directory.GetDirectories(extractDir);
            if (dirs.Length != 1) throw new IOException("unexpected archive layout");
            return dirs[0];
        }

        static void Extract(string zipPath, string targetDir)
        {
            string root = Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar;
            using (FileStream zipStream = File.OpenRead(zipPath))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                    if (!destination.StartsWith(root, StringComparison.Ordinal)) throw new IOException("unsafe path in archive: " + entry.FullName);
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) { Directory.CreateDirectory(destination); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (Stream input = entry.Open())
                    using (FileStream output = File.Create(destination)) input.CopyTo(output);
                }
            }
        }

        static void Clone(string tag, string cloneDir)
        {
            EditorUtility.DisplayProgressBar("Sketch SDK", "git clone " + tag + "...", 0.5f);
            var info = new ProcessStartInfo("git", "clone --depth 1 --branch " + tag + " " + SketchInstallerPaths.REPO_URL + " \"" + cloneDir + "\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            using (Process process = Process.Start(info))
            {
                if (process == null) throw new InvalidOperationException("git could not be started");
                var stderr = process.StandardError.ReadToEndAsync();
                process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(CLONE_TIMEOUT_MS))
                {
                    try { process.Kill(); } catch { /* already gone */ }
                    throw new TimeoutException("git clone timed out");
                }
                if (process.ExitCode != 0) throw new IOException("git clone failed: " + stderr.Result.Trim());
            }
        }
    }
}
