namespace DotnetSlnxItems.Core;

/// <summary>
/// A thin domain layer over
/// <c>Microsoft.VisualStudio.SolutionPersistence</c> that loads,
/// mutates, and saves <c>.sln</c> and <c>.slnx</c> solutions to
/// manage Solution Items.
///
/// The service owns solution path resolution, folder path
/// normalization, dedupe, and exception translation. Callers
/// (typically the CLI commands) depend on this service and stay
/// thin. All mutation methods modify the in-memory model only;
/// <see cref="SaveAsync"/> is the single method that persists
/// changes to disk.
/// </summary>
public sealed class SolutionItemsService
{
    /// <summary>
    /// Creates a new service bound to the given solution file path.
    /// The file is not opened until the first operation.
    /// </summary>
    /// <param name="solutionFilePath">
    /// Absolute or working-directory-relative path to a <c>.sln</c> or
    /// <c>.slnx</c> file. The file must exist.
    /// </param>
    /// <exception cref="SolutionItemsException">
    /// Thrown with <see cref="SolutionItemsErrorKind.SolutionFileNotFound"/>
    /// if the path does not exist; with
    /// <see cref="SolutionItemsErrorKind.SlnfNotSupported"/> if the
    /// file has a <c>.slnf</c> extension; with
    /// <see cref="SolutionItemsErrorKind.UnsupportedFormat"/> for any
    /// other unsupported extension; with
    /// <see cref="SolutionItemsErrorKind.AmbiguousSolutionInDirectory"/>
    /// if a directory is given and contains multiple solution files.
    /// </exception>
    public SolutionItemsService(string solutionFilePath)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds one or more files as Solution Items. Files already
    /// present in the target folder are silently skipped (dedupe).
    /// </summary>
    /// <param name="filePaths">
    /// Paths to the files, relative to the solution directory.
    /// </param>
    /// <param name="targetFolder">
    /// The destination folder, normalized to <c>/Foo/Bar/</c>. If
    /// <c>null</c>, the default <c>/Solution Items/</c> is used.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public Task AddFilesAsync(
        IReadOnlyList<string> filePaths,
        string? targetFolder = null,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a folder. Nested paths like <c>a/b/c</c> create the
    /// intermediate virtual folders automatically.
    /// </summary>
    /// <param name="folderPath">
    /// The folder path, normalized to <c>/Foo/Bar/</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public Task AddFolderAsync(string folderPath, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Removes one or more files from a folder. Removing a file
    /// that is not present is a silent no-op.
    /// </summary>
    /// <param name="filePaths">
    /// Paths to the files, relative to the folder.
    /// </param>
    /// <param name="targetFolder">
    /// The folder to remove the files from, normalized to
    /// <c>/Foo/Bar/</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public Task RemoveFilesAsync(
        IReadOnlyList<string> filePaths,
        string? targetFolder = null,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Removes a folder and all files inside it.
    /// </summary>
    /// <param name="folderPath">
    /// The folder path, normalized to <c>/Foo/Bar/</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public Task RemoveFolderAsync(string folderPath, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns all Solution Items (folders and files) in tree order:
    /// folders first, then files within each folder. Empty folders
    /// are included.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task<IReadOnlyList<SolutionItemInfo>> ListAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Persists all pending mutations to disk. The serializer that
    /// originally opened the file is reused so the original format
    /// is preserved (<c>UTF-8</c> with BOM for <c>.sln</c>,
    /// <c>PreserveWhitespace = true</c> for <c>.slnx</c>).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task SaveAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
