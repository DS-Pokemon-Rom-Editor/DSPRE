using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>Shells out to the linked hg-engine checkout's Makefile. Only for invoking `make`; the
    /// toolchain (gcc, armips, ndstool) is POSIX-native, so it runs under WSL or MSYS2 depending on
    /// which the checkout was linked with. hg-engine documents both.</summary>
    public static class HgEngineBuild
    {
        /// <summary>Builds one or more make targets (e.g. "build/narc/a055.narc") in one `make` call.</summary>
        public static bool RunMakeTargets(System.Collections.Generic.IEnumerable<string> targets, out string stdout, out string stderr)
            => Run($"make -C '{HgEngineProject.RepoPathPosix}' {string.Join(' ', targets)}", out stdout, out stderr);

        /// <summary>Runs the full `make` build (all data domains + ASM hooks + repack into test.nds),
        /// streaming output line by line for a live log panel.</summary>
        public static bool RunFullBuild(Action<string> onOutputLine, out string stderr)
            => RunStreaming($"make -C '{HgEngineProject.RepoPathPosix}'", onOutputLine, out stderr);

        private static bool Run(string bashCommand, out string stdout, out string stderr)
        {
            stdout = ""; stderr = "";
            if (!HgEngineProject.IsLinked) { stderr = "No hg-engine checkout linked."; return false; }

            using var proc = new Process { StartInfo = BuildStartInfo(bashCommand, redirectOutput: true) };
            AppLogger.Info("hg-engine: " + bashCommand);

            try
            {
                proc.Start();
                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                proc.WaitForExit();
                stdout = outTask.Result;
                stderr = errTask.Result.Trim();
            }
            catch (Win32Exception ex)
            {
                stderr = $"Failed to start {ShellName}: " + ex.Message;
                AppLogger.Error(stderr);
                return false;
            }

            if (proc.ExitCode != 0)
            {
                AppLogger.Error("hg-engine make failed: " + stderr);
                return false;
            }
            if (!string.IsNullOrWhiteSpace(stdout)) AppLogger.Info("hg-engine make stdout: " + stdout);
            return true;
        }

        private static bool RunStreaming(string bashCommand, Action<string> onOutputLine, out string stderr)
        {
            var errBuf = new StringBuilder();
            stderr = "";
            if (!HgEngineProject.IsLinked) { stderr = "No hg-engine checkout linked."; return false; }

            using var proc = new Process { StartInfo = BuildStartInfo(bashCommand, redirectOutput: true), EnableRaisingEvents = true };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) onOutputLine?.Invoke(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) { errBuf.AppendLine(e.Data); onOutputLine?.Invoke(e.Data); } };

            AppLogger.Info("hg-engine: " + bashCommand);
            try
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
            }
            catch (Win32Exception ex)
            {
                stderr = $"Failed to start {ShellName}: " + ex.Message;
                AppLogger.Error(stderr);
                return false;
            }

            stderr = errBuf.ToString().Trim();
            if (proc.ExitCode != 0)
            {
                AppLogger.Error("hg-engine full build failed: " + stderr);
                return false;
            }
            return true;
        }

        private static string ShellName =>
            HgEngineProject.Shell == HgEngineShell.Msys2 ? HgEngineProject.MsysBashPath : "wsl.exe";

        private static ProcessStartInfo BuildStartInfo(string bashCommand, bool redirectOutput)
        {
            var psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            if (HgEngineProject.Shell == HgEngineShell.Msys2)
            {
                // A login shell on purpose: hg-engine's MSYS2 setup has the user append /mingw64/bin
                // to PATH in ~/.bashrc, and the arm-none-eabi toolchain lives there.
                psi.FileName = HgEngineProject.MsysBashPath;
                psi.ArgumentList.Add("-lc");
                psi.ArgumentList.Add(bashCommand);
                return psi;
            }

            psi.FileName = "wsl.exe";
            // Only a checkout inside WSL names its distro. One on a Windows drive built through WSL
            // has no distro in its path, so wsl.exe uses the default rather than being told a wrong one.
            if (!string.IsNullOrWhiteSpace(HgEngineProject.WslDistro))
            {
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(HgEngineProject.WslDistro);
            }
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add("bash");
            psi.ArgumentList.Add("-lc");
            psi.ArgumentList.Add(bashCommand);
            return psi;
        }
    }
}
