using System;
using System.Linq;

namespace DSPRE.Tracking
{
    /// <summary>Plain messages for git's errors, each with what to do next.</summary>
    public static class GitMessages
    {
        public const string PushedFirst = "Someone pushed first. Pull, then push.";
        public const string NeedsIdentity = "git needs a name and email for commits.";

        public static string Explain(CliRunner.Result result)
        {
            string text = result?.Message ?? "";
            if (Has(text, "[rejected]") && (Has(text, "fetch first") || Has(text, "non-fast-forward"))) return PushedFirst;
            if (Has(text, "Could not resolve host") || Has(text, "unable to access") && Has(text, "Failed to connect")) return "GitHub can't be reached. Check your connection and try again.";
            if (Has(text, "Permission denied (publickey)") || Has(text, "Authentication failed") || Has(text, "could not read Username"))
                return "git couldn't sign in to the remote. Use Help me set it up to sign in again.";
            if (Has(text, "Repository not found") || Has(text, "does not appear to be a git repository")) return "The remote repo wasn't found, or you don't have access to it.";
            if (Has(text, "would be overwritten")) return "Commit your changes first, then pull.";
            if (Has(text, "not possible because you have unmerged files") || Has(text, "You have not concluded your merge")) return "A merge is still open. Finish it or abort it first.";
            if (Has(text, "tell me who you are")) return NeedsIdentity;
            string line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("fatal:", StringComparison.Ordinal) || l.StartsWith("error:", StringComparison.Ordinal));
            if (line != null) return "git: " + line.Substring(line.IndexOf(':') + 1).Trim();
            return string.IsNullOrWhiteSpace(text) ? "git failed without an error message." : text.Trim();
        }

        private static bool Has(string text, string part) => text.Contains(part, StringComparison.OrdinalIgnoreCase);
    }
}
