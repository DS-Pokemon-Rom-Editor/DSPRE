using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DSPRE.HgEngine
{
    /// <summary>Shells out to the linked hg-engine checkout's Makefile. The toolchain is POSIX-native, so on
    /// Windows it runs under WSL or MSYS2, whichever the checkout was linked with; elsewhere in the host's bash.</summary>
    public static class HgEngineBuild
    {
        /// <summary>Builds one or more make targets (e.g. "build/narc/a055.narc") in one `make` call.</summary>
        public static bool RunMakeTargets(System.Collections.Generic.IEnumerable<string> targets, out string stdout, out string stderr)
            => Run(MakeCall() + " " + string.Join(' ', targets), out stdout, out stderr);

        /// <summary>Runs the full `make` build (all data domains + ASM hooks + repack into test.nds),
        /// streaming output line by line for a live log panel.</summary>
        public static bool RunFullBuild(Action<string> onOutputLine, out string stderr, string buildRom = null)
            => RunStreaming(FullBuildCommand(buildRom), onOutputLine, out stderr);

        /// <summary>The command Compile ROM runs.</summary>
        public static string FullBuildCommand(string buildRom = null)
            => MakeCall() + (HgEngineProject.IsSafeBuildRomName(buildRom) ? $" BUILDROM='{buildRom}'" : "");

        private static string MakeCall()
        {
            string call = $"make -C '{HgEngineProject.RepoPathPosix}'";
            return HgEngineProject.MakeArguments.Length == 0 ? call : call + " " + HgEngineProject.MakeArguments;
        }

        /// <summary>
        /// Runs hg-engine's text-archive validator over one source file. It reports problems on stdout
        /// and still exits 0, so output is the result, not the exit code. False means it could not run.
        /// </summary>
        public static bool TryValidateTextArchive(string repoRelPath, out string problems)
        {
            problems = null;

            string root = HgEngineProject.RepoRootWindows;
            string validator = HgEngineOwnedFiles.TextValidatorRelPath(root);
            string charmap = HgEngineOwnedFiles.CharMapRelPath(root);
            if (validator == null || charmap == null) return false;

            string command = $"cd '{HgEngineProject.RepoPathPosix}' && " +
                $"python3 '{validator}' '{charmap}' '{repoRelPath}'";
            if (!Run(command, out string stdout, out string stderr)) return false;

            string reported = (stdout + Environment.NewLine + stderr).Trim();
            if (reported.Length > 0) problems = reported;
            return true;
        }

        /// <summary>False if no generator script or it failed; script is its repo path.</summary>
        public static bool TryRegenerateItemIconRules(out string script)
        {
            script = HgEngineMachineIcons.RulesGeneratorRelPath();
            if (script == null) return false;
            bool ran = Run($"cd '{HgEngineProject.RepoPathPosix}' && python3 '{script}'", out _, out _);
            HgEngineFileCache.ClearCache();
            if (!ran) AppLogger.Error($"hg-engine: the icon rules generator {script} failed.");
            return ran;
        }

        /// <summary>Whether the next make wipes base/ and extracts it again from rom.nds. False when make couldn't tell.</summary>
        public static bool TryCheckBaseReextract(out bool reextracts)
        {
            reextracts = false;
            // Not make -q: hg-engine's Makefile exits 0 even when the target is out of date. A dry run prints "is up to
            // date" or "Nothing to be done" when nothing would run.
            string target = HgEngineBase.BaseDirName + "/arm9.bin";
            // Uses the build's arguments, since -f or a variable can change what the target needs. LC_ALL=C keeps make's
            // messages in English.
            if (!Run($"LC_ALL=C {MakeCall()} -n '{target}' 2>&1; true", out string stdout, out _)) return false;
            // make marks its errors with "***", and then it can't say what would run.
            if (string.IsNullOrWhiteSpace(stdout) || stdout.Contains("***", StringComparison.Ordinal)) return false;
            reextracts = !stdout.Contains("is up to date", StringComparison.Ordinal) && !stdout.Contains("Nothing to be done", StringComparison.Ordinal);
            return true;
        }

        /// <summary>Runs one command in the checkout's build shell, for checks such as finding the toolchain.</summary>
        public static bool TryRunShell(string bashCommand, out string stdout, out string stderr) => Run(bashCommand, out stdout, out stderr);

        private static bool Run(string bashCommand, out string stdout, out string stderr)
        {
            stdout = ""; stderr = "";
            if (!HgEngineProject.IsLinked) { stderr = "No hg-engine checkout is linked."; return false; }

            using Process proc = new Process { StartInfo = BuildStartInfo(bashCommand, redirectOutput: true) };
            AppLogger.Info("hg-engine: " + bashCommand);

            try
            {
                proc.Start();
                Task<string> outTask = proc.StandardOutput.ReadToEndAsync();
                Task<string> errTask = proc.StandardError.ReadToEndAsync();
                proc.WaitForExit();
                stdout = outTask.Result;
                stderr = errTask.Result.Trim();
            }
            catch (Win32Exception ex)
            {
                stderr = $"Couldn't start {ShellName}: " + ex.Message;
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
            StringBuilder errBuf = new StringBuilder();
            stderr = "";
            if (!HgEngineProject.IsLinked) { stderr = "No hg-engine checkout is linked."; return false; }

            using Process proc = new Process { StartInfo = BuildStartInfo(bashCommand, redirectOutput: true), EnableRaisingEvents = true };
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
                stderr = $"Couldn't start {ShellName}: " + ex.Message;
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

        private static string ShellName => HgEngineProject.Shell switch
        {
            HgEngineShell.Msys2 => HgEngineProject.MsysBashPath,
            HgEngineShell.Native => "bash",
            _ => "wsl.exe",
        };

        private static ProcessStartInfo BuildStartInfo(string bashCommand, bool redirectOutput)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            if (HgEngineProject.Shell == HgEngineShell.Native)
            {
                // Login shell for the same reason as MSYS2: toolchain PATH setup usually lives in the profile.
                psi.FileName = "bash";
                psi.ArgumentList.Add("-lc");
                psi.ArgumentList.Add(bashCommand);
                return psi;
            }

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
            // Only a checkout inside WSL names its distro; one on a Windows drive uses the default.
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
