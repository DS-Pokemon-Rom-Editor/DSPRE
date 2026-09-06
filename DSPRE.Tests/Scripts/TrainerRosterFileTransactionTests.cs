using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class TrainerRosterFileTransactionTests
    {
        [Fact]
        public void CommitWritesExistingAndNewOutputsTogether()
        {
            string root = MakeRoot();
            try
            {
                string existing = Path.Combine(root, "existing.bin");
                string added = Path.Combine(root, "nested", "added.bin");
                File.WriteAllBytes(existing, new byte[] { 1 });

                Assert.True(TrainerRosterFileTransaction.TryCommit(new[]
                {
                    new TrainerRosterFileMutation(existing, new byte[] { 2 }),
                    new TrainerRosterFileMutation(added, new byte[] { 3 })
                }, out string error), error);
                Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(existing));
                Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(added));
                Assert.Empty(Directory.GetFiles(root, "*.dspre-stage-*", SearchOption.AllDirectories));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void FailedCommitRestoresAlreadyReplacedOutputs()
        {
            string root = MakeRoot();
            try
            {
                string existing = Path.Combine(root, "existing.bin");
                string directoryTarget = Path.Combine(root, "cannot-replace-directory");
                File.WriteAllBytes(existing, new byte[] { 1 });
                Directory.CreateDirectory(directoryTarget);

                Assert.False(TrainerRosterFileTransaction.TryCommit(new[]
                {
                    new TrainerRosterFileMutation(existing, new byte[] { 2 }),
                    new TrainerRosterFileMutation(directoryTarget, new byte[] { 3 })
                }, out string error));
                Assert.Contains("rolled back", error);
                Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(existing));
                Assert.True(Directory.Exists(directoryTarget));
                Assert.Empty(Directory.GetFiles(root, "*.dspre-stage-*", SearchOption.AllDirectories));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CommitCanDeleteAndReplaceInOneTransaction()
        {
            string root = MakeRoot();
            try
            {
                string removed = Path.Combine(root, "removed.bin");
                string replaced = Path.Combine(root, "replaced.bin");
                File.WriteAllBytes(removed, new byte[] { 1 });
                File.WriteAllBytes(replaced, new byte[] { 2 });

                Assert.True(TrainerRosterFileTransaction.TryCommit(new[]
                {
                    TrainerRosterFileMutation.Delete(removed),
                    new TrainerRosterFileMutation(replaced, new byte[] { 3 })
                }, out string error), error);
                Assert.False(File.Exists(removed));
                Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(replaced));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void FailedCommitRestoresAnEarlierDeletion()
        {
            string root = MakeRoot();
            try
            {
                string removed = Path.Combine(root, "removed.bin");
                string directoryTarget = Path.Combine(root, "cannot-replace-directory");
                File.WriteAllBytes(removed, new byte[] { 1 });
                Directory.CreateDirectory(directoryTarget);

                Assert.False(TrainerRosterFileTransaction.TryCommit(new[]
                {
                    TrainerRosterFileMutation.Delete(removed),
                    new TrainerRosterFileMutation(directoryTarget, new byte[] { 3 })
                }, out string error));
                Assert.Contains("rolled back", error);
                Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(removed));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static string MakeRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "DSPRE", "trainer-roster-transaction",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
