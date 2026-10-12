namespace DotnetSlnxItems.Core;

/// <summary>
/// Machine-readable error kinds raised by
/// <see cref="SolutionItemsService"/>. Carried on
/// <see cref="SolutionItemsException.Kind"/> so callers can react
/// programmatically instead of matching the message string.
/// </summary>
public enum SolutionItemsErrorKind
{
    /// <summary>The solution file path does not exist on disk.</summary>
    SolutionFileNotFound,

    /// <summary>The file extension is not <c>.sln</c> or <c>.slnx</c>.</summary>
    UnsupportedFormat,

    /// <summary>The file is a <c>.slnf</c> (Solution Filter); writes are not supported.</summary>
    SlnfNotSupported,

    /// <summary>A directory was given and contains multiple solution files; the caller must disambiguate.</summary>
    AmbiguousSolutionInDirectory,

    /// <summary>The folder name is empty or consists only of slashes.</summary>
    InvalidFolderPath,

    /// <summary>The file path is not relative to the solution directory.</summary>
    InvalidFilePath,

    /// <summary>The requested folder does not exist in the solution.</summary>
    FolderNotFound,

    /// <summary>The underlying persistence library reported an error.</summary>
    PersistenceError,

    /// <summary>The <c>.slnx-items.yaml</c> manifest file does not exist on disk.</summary>
    ManifestNotFound = 8,

    /// <summary>The manifest file is not parseable as the <c>manifest-schema.md</c> subset (bad YAML, unsafe constructs, wrong shape).</summary>
    InvalidManifest = 9,

    /// <summary>A file path declared in the manifest does not exist on disk.</summary>
    FileReferencedInManifestNotFound = 10,
}