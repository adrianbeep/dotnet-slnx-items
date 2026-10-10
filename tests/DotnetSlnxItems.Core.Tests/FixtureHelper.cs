namespace DotnetSlnxItems.Core.Tests;

/// <summary>
/// Helpers for copying fixture files (committed under
/// <c>Fixtures/</c>) into a unique per-test temp directory.
/// </summary>
internal static class FixtureHelper
{
    private const string FixturesSubfolder = "Fixtures";

    /// <summary>
    /// Copies the named fixture file (e.g. <c>RealWorldFixture.slnx</c>)
    /// to a fresh per-call temp directory and returns the path to the
    /// copied file. Any additional fixture names listed in
    /// <paramref name="additionalFixtures"/> are copied into the same
    /// directory next to the main fixture.
    /// </summary>
    public static string CopyFixtureTo(string fixtureName, params string[] additionalFixtures)
    {
        string sourceDir = Path.Combine(AppContext.BaseDirectory, FixturesSubfolder);
        string tempDir = Path.Combine(
            Path.GetTempPath(),
            "DotnetSlnItems-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string targetMain = CopyOne(sourceDir, tempDir, fixtureName);

        foreach (string additional in additionalFixtures ?? Array.Empty<string>())
        {
            CopyOne(sourceDir, tempDir, additional);
        }

        return targetMain;
    }

    /// <summary>
    /// Best-effort delete of a temp directory containing the file at
    /// <paramref name="filePath"/>. Silently swallows exceptions
    /// (test cleanup must never fail the test).
    /// </summary>
    public static void Cleanup(string filePath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static string CopyOne(string sourceDir, string targetDir, string fileName)
    {
        string source = Path.Combine(sourceDir, fileName);
        string target = Path.Combine(targetDir, fileName);
        File.Copy(source, target);
        return target;
    }
}