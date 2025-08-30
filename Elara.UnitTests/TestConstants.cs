namespace Elara.UnitTests
{
    /// <summary>
    /// Test-only constants to avoid hardcoded magic strings in tests.
    /// Kept inside the test project to prevent coupling with production code.
    /// </summary>
    public static class TestConstants
    {
        public static class Paths
        {
            public const string SampleRunsFolder = "SampleRuns";
            public const string AudioFileName = "audio.wav";
            public const string ExpectedJsonFileName = "expected.json";

            // Legacy sandbox project name still referenced by tests for backfill copy
            public const string LegacySandboxProjectName = "ErnestAi.Sandbox.Chunking";
            public const string ModelsFolderName = "Models";
            public const string WhisperFolderName = "Whisper";
        }

        public static class Env
        {
            // Legacy environment variable used by tests/documentation
            public const string LegacyRepoRootVar = "ERNESTAI_REPO_ROOT";
        }
    }
}
