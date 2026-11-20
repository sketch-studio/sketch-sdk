using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;

namespace Sketch.Sdk.Installer
{
    /// <summary>Lists vX.Y.Z tags with `git ls-remote` off the main thread and reports back on the main thread.</summary>
    static class SketchTagFetcher
    {
        const int TIMEOUT_MS = 20000;
        static readonly Regex TagPattern = new Regex(@"refs/tags/(v\d+\.\d+\.\d+)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>onDone(tags newest first, error or null) runs on the main thread exactly once.</summary>
        public static void FetchAsync(Action<List<SketchSemVer>, string> onDone)
        {
            Task<List<SketchSemVer>> task = Task.Run(() => Fetch());
            void Poll()
            {
                if (!task.IsCompleted) return;
                EditorApplication.update -= Poll;
                if (task.IsFaulted)
                {
                    Exception e = task.Exception?.GetBaseException();
                    onDone(new List<SketchSemVer>(), e != null ? e.Message : "unknown error");
                }
                else onDone(task.Result, null);
            }
            EditorApplication.update += Poll;
        }

        static List<SketchSemVer> Fetch()
        {
            var info = new ProcessStartInfo("git", "ls-remote --tags --refs " + SketchInstallerPaths.REPO_URL)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            using (Process process = Process.Start(info))
            {
                if (process == null) throw new InvalidOperationException("git could not be started (is git on PATH?)");
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(TIMEOUT_MS))
                {
                    try { process.Kill(); } catch { /* already gone */ }
                    throw new TimeoutException("git ls-remote timed out");
                }
                if (process.ExitCode != 0) throw new InvalidOperationException("git ls-remote failed: " + stderr.Result.Trim());
                return Parse(stdout.Result);
            }
        }

        public static List<SketchSemVer> Parse(string lsRemoteOutput)
        {
            var result = new List<SketchSemVer>();
            foreach (Match m in TagPattern.Matches(lsRemoteOutput ?? string.Empty))
                if (SketchSemVer.TryParse(m.Groups[1].Value, out SketchSemVer v)) result.Add(v);
            result.Sort((a, b) => b.CompareTo(a));
            return result;
        }
    }
}
