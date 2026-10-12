namespace DotnetSlnxItems.Cli;

/// <summary>
/// Exit codes for the <c>dotnet-slnx-items</c> CLI. Documented as
/// the contract scripts and CI pipelines key off of. Pre-1.0; the
/// codes may move between minor versions before 1.0.
/// </summary>
public static class CliExitCodes
{
    /// <summary>
    /// Catch-all for unhandled errors and data-error cases that
    /// don't warrant a specific code: <c>FolderNotFound</c>,
    /// <c>PersistenceError</c>, <c>InvalidManifest</c>,
    /// <c>FileReferencedInManifestNotFound</c>, and unhandled
    /// exceptions.
    /// </summary>
    public const int Unhandled = 1;

    /// <summary>
    /// CLI pre-flight: no <c>.sln</c> or <c>.slnx</c> file in the
    /// current working directory (and no <c>--solution</c> given).
    /// </summary>
    public const int NoSolutionInCwd = 2;

    /// <summary>
    /// The path given via <c>--solution</c> does not exist, OR the
    /// <c>.slnx-items.yaml</c> manifest file does not exist
    /// (sync). Both are "file not found" semantics.
    /// </summary>
    public const int SolutionFileNotFound = 3;

    /// <summary>
    /// Core: file extension is neither <c>.sln</c> nor
    /// <c>.slnx</c>.
    /// </summary>
    public const int UnsupportedFormat = 4;

    /// <summary>
    /// Core: file is a <c>.slnf</c> (solution filter) which is a
    /// read-only projection.
    /// </summary>
    public const int SlnfNotSupported = 5;

    /// <summary>
    /// Core: directory given via <c>--solution</c> contains
    /// multiple solution files.
    /// </summary>
    public const int AmbiguousSolutionInDirectory = 6;

    /// <summary>
    /// Core: the <c>--solution-folder</c> argument is empty or
    /// only slashes; OR the CLI pre-flight for <c>..</c>/absolute
    /// paths in folder names.
    /// </summary>
    public const int InvalidFolderPath = 7;

    /// <summary>
    /// Core: file path is outside the solution directory.
    /// </summary>
    public const int InvalidFilePath = 8;
}