using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

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
///
/// Construction is async (it loads the solution from disk) and
/// goes through <see cref="CreateAsync"/> because constructors
/// cannot be <c>async</c> in C#. The service is not thread-safe:
/// callers should not invoke its methods concurrently.
/// </summary>
public sealed class SolutionItemsService
{
    private const string DefaultFolderPath = "/Solution Items/";

    private readonly SolutionModel _solution;
    private readonly string _solutionFilePath;
    private readonly ISolutionSerializer _serializer;

    private SolutionItemsService(SolutionModel solution, string solutionFilePath, ISolutionSerializer serializer)
    {
        _solution = solution;
        _solutionFilePath = solutionFilePath;
        _serializer = serializer;
    }

    /// <summary>
    /// Creates a new service bound to the given solution file path
    /// and loads the solution into memory. The constructor is
    /// private because loading the solution requires an async call
    /// to the persistence library, and C# constructors cannot be
    /// <c>async</c>; the factory method lets us <c>await</c> it
    /// properly without blocking.
    /// </summary>
    /// <param name="solutionFilePath">
    /// Absolute or working-directory-relative path to a <c>.sln</c>
    /// or <c>.slnx</c> file, or to a directory that contains exactly
    /// one such file.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="SolutionItemsException">
    /// Thrown with <see cref="SolutionItemsErrorKind.SolutionFileNotFound"/>
    /// if the path does not exist (or the directory has no
    /// solution file); with
    /// <see cref="SolutionItemsErrorKind.SlnfNotSupported"/> if
    /// the file has a <c>.slnf</c> extension; with
    /// <see cref="SolutionItemsErrorKind.UnsupportedFormat"/>
    /// for any other unsupported extension; with
    /// <see cref="SolutionItemsErrorKind.AmbiguousSolutionInDirectory"/>
    /// if a directory is given and contains multiple solution
    /// files; with
    /// <see cref="SolutionItemsErrorKind.PersistenceError"/> if
    /// the underlying persistence library fails to load.
    /// </exception>
    public static async Task<SolutionItemsService> CreateAsync(
        string solutionFilePath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(solutionFilePath))
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.InvalidFilePath,
                "Solution file path cannot be null or empty.");
        }

        string resolvedPath = solutionFilePath;

        if (Directory.Exists(resolvedPath))
        {
            resolvedPath = ResolveDirectoryToSolutionFile(resolvedPath);
        }
        else if (!File.Exists(resolvedPath))
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.SolutionFileNotFound,
                $"Solution file not found: {resolvedPath}");
        }

        ValidateExtension(resolvedPath);

        ISolutionSerializer? serializer = SolutionSerializers.GetSerializerByMoniker(resolvedPath);
        if (serializer is null)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.PersistenceError,
                $"No serializer found for file: {resolvedPath}");
        }

        SolutionModel solution;
        try
        {
            solution = await serializer.OpenAsync(resolvedPath, ct).ConfigureAwait(false);
        }
        catch (SolutionException ex)
        {
            throw TranslatePersistenceException(ex);
        }
        catch (Exception ex) when (ex is not SolutionItemsException)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.PersistenceError,
                $"Failed to load solution '{resolvedPath}': {ex.Message}",
                ex);
        }

        return new SolutionItemsService(solution, resolvedPath, serializer);
    }

    /// <summary>
    /// Adds one or more files as Solution Items. Files already
    /// present in the target folder are silently skipped (dedupe).
    /// </summary>
    /// <param name="filePaths">
    /// Paths to the files. Each path must be absolute or relative
    /// to the current working directory, and the resolved file
    /// must live inside the solution directory.
    /// </param>
    /// <param name="targetFolder">
    /// The destination folder, normalized to <c>/Foo/Bar/</c>. If
    /// <c>null</c>, the default <c>/Solution Items/</c> is used.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="SolutionItemsException">
    /// Thrown with <see cref="SolutionItemsErrorKind.InvalidFolderPath"/>
    /// if the folder name is empty or only slashes; with
    /// <see cref="SolutionItemsErrorKind.InvalidFilePath"/> if
    /// any file path resolves outside the solution directory.
    /// </exception>
    public Task AddFilesAsync(
        IReadOnlyList<string> filePaths,
        string? targetFolder = null,
        CancellationToken ct = default)
    {
        if (filePaths is null || filePaths.Count == 0)
        {
            return Task.CompletedTask;
        }

        string folderPath = targetFolder is null
            ? DefaultFolderPath
            : NormalizeFolderPath(targetFolder);
        SolutionFolderModel folder = _solution.FindFolder(folderPath) ?? _solution.AddFolder(folderPath);
        string solutionDir = GetSolutionDirectoryOrThrow();

        foreach (string filePath in filePaths)
        {
            ct.ThrowIfCancellationRequested();
            string relativePath = ResolveRelativeFilePath(solutionDir, filePath);
            if (folder.Files is null || !ContainsFile(folder.Files, relativePath))
            {
                folder.AddFile(relativePath);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a folder. Nested paths like <c>a/b/c</c> create
    /// the intermediate virtual folders automatically (delegated
    /// to the persistence library).
    /// </summary>
    /// <param name="folderPath">
    /// The folder path, normalized to <c>/Foo/Bar/</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="SolutionItemsException">
    /// Thrown with <see cref="SolutionItemsErrorKind.InvalidFolderPath"/>
    /// if the folder name is empty or only slashes.
    /// </exception>
    public Task AddFolderAsync(string folderPath, CancellationToken ct = default)
    {
        string normalized = NormalizeFolderPath(folderPath);
        _solution.AddFolder(normalized);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes one or more files from a folder. Removing a file
    /// that is not present is a silent no-op.
    /// </summary>
    /// <param name="filePaths">
    /// Paths to the files, resolved relative to the solution
    /// directory.
    /// </param>
    /// <param name="targetFolder">
    /// The folder to remove the files from, normalized to
    /// <c>/Foo/Bar/</c>. If <c>null</c>, the default
    /// <c>/Solution Items/</c> is used.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="SolutionItemsException">
    /// Thrown with <see cref="SolutionItemsErrorKind.InvalidFilePath"/>
    /// if any file path resolves outside the solution directory.
    /// </exception>
    public Task RemoveFilesAsync(
        IReadOnlyList<string> filePaths,
        string? targetFolder = null,
        CancellationToken ct = default)
    {
        if (filePaths is null || filePaths.Count == 0)
        {
            return Task.CompletedTask;
        }

        string folderPath = targetFolder is null
            ? DefaultFolderPath
            : NormalizeFolderPath(targetFolder);
        SolutionFolderModel? folder = _solution.FindFolder(folderPath);
        if (folder is null)
        {
            return Task.CompletedTask;
        }

        string solutionDir = GetSolutionDirectoryOrThrow();
        foreach (string filePath in filePaths)
        {
            ct.ThrowIfCancellationRequested();
            string relativePath = ResolveRelativeFilePath(solutionDir, filePath);
            if (folder.Files is not null && ContainsFile(folder.Files, relativePath))
            {
                folder.RemoveFile(relativePath);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a folder and all files inside it. Removing a folder
    /// that does not exist is a silent no-op.
    /// </summary>
    /// <param name="folderPath">
    /// The folder path, normalized to <c>/Foo/Bar/</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public Task RemoveFolderAsync(string folderPath, CancellationToken ct = default)
    {
        string normalized = NormalizeFolderPath(folderPath);
        SolutionFolderModel? folder = _solution.FindFolder(normalized);
        if (folder is not null)
        {
            _solution.RemoveFolder(folder);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns all Solution Items (folders and files) in tree
    /// order: for each folder, the folder record is emitted first,
    /// followed by its files. Empty folders are included.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task<IReadOnlyList<SolutionItemInfo>> ListAsync(CancellationToken ct = default)
    {
        List<SolutionItemInfo> items = new();
        foreach (SolutionFolderModel folder in _solution.SolutionFolders)
        {
            ct.ThrowIfCancellationRequested();
            items.Add(new SolutionItemInfo(folder.Path, SolutionItemKind.Folder));
            if (folder.Files is not null)
            {
                foreach (string file in folder.Files)
                {
                    ct.ThrowIfCancellationRequested();
                    items.Add(new SolutionItemInfo(file, SolutionItemKind.File));
                }
            }
        }
        return Task.FromResult<IReadOnlyList<SolutionItemInfo>>(items);
    }

    /// <summary>
    /// Persists all pending mutations to disk. The serializer that
    /// originally opened the file is reused, so the original
    /// format is preserved. Encoding and whitespace settings are
    /// left at the persistence library's defaults; the concrete
    /// serializer types that would expose those settings are
    /// internal, so per-format overrides are deferred to a
    /// follow-up. The SDK's <c>dotnet sln</c> commands use the
    /// same defaults, so behaviour matches the upstream tool.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="SolutionItemsException">
    /// Thrown with <see cref="SolutionItemsErrorKind.PersistenceError"/>
    /// if the underlying persistence library fails to save.
    /// </exception>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await _serializer.SaveAsync(_solutionFilePath, _solution, ct).ConfigureAwait(false);
        }
        catch (SolutionException ex)
        {
            throw TranslatePersistenceException(ex);
        }
        catch (Exception ex) when (ex is not SolutionItemsException)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.PersistenceError,
                $"Failed to save solution '{_solutionFilePath}': {ex.Message}",
                ex);
        }
    }

    // ---- Internal helpers ----

    private string GetSolutionDirectoryOrThrow()
    {
        string? dir = Path.GetDirectoryName(_solutionFilePath);
        if (string.IsNullOrEmpty(dir))
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.SolutionFileNotFound,
                $"Could not determine directory for solution file: {_solutionFilePath}");
        }
        return dir;
    }

    private static void ValidateExtension(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (string.Equals(extension, ".slnf", StringComparison.Ordinal))
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.SlnfNotSupported,
                "Solution filters (.slnf) are read-only projections. " +
                "Run the command on the parent .sln or .slnx instead.");
        }
        if (!string.Equals(extension, ".sln", StringComparison.Ordinal)
            && !string.Equals(extension, ".slnx", StringComparison.Ordinal))
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.UnsupportedFormat,
                $"Unsupported solution file format '{extension}'. " +
                "Only .sln and .slnx are supported.");
        }
    }

    private static string ResolveDirectoryToSolutionFile(string directory)
    {
        List<string> solutionFiles = Directory
            .EnumerateFiles(directory)
            .Where(f =>
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                return ext == ".sln" || ext == ".slnx";
            })
            .ToList();

        if (solutionFiles.Count == 0)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.SolutionFileNotFound,
                $"No solution file found in directory: {directory}");
        }

        if (solutionFiles.Count > 1)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.AmbiguousSolutionInDirectory,
                $"Multiple solution files found in directory '{directory}'. " +
                "Pass an explicit --solution <path> to disambiguate. " +
                $"Found: {string.Join(", ", solutionFiles.Select(Path.GetFileName))}");
        }

        return solutionFiles[0];
    }

    private static string NormalizeFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || folderPath.Trim('/').Length == 0)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.InvalidFolderPath,
                $"Invalid folder name: '{folderPath}'. " +
                "Folder names cannot be empty or contain only slashes.");
        }
        return "/" + folderPath.Trim('/') + "/";
    }

    private static string ResolveRelativeFilePath(string solutionDir, string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        string relative = Path.GetRelativePath(solutionDir, fullPath);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative == ".."
            || Path.IsPathRooted(relative))
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.InvalidFilePath,
                $"File path '{filePath}' is outside the solution directory " +
                $"'{solutionDir}'. Paths must be relative to the solution directory.");
        }
        return relative.Replace('\\', '/');
    }

    private static bool ContainsFile(IEnumerable<string> files, string file)
    {
        foreach (string existing in files)
        {
            if (string.Equals(existing, file, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static SolutionItemsException TranslatePersistenceException(SolutionException ex)
    {
        return ex.ErrorType switch
        {
            SolutionErrorType.InvalidFolderPath => new SolutionItemsException(
                SolutionItemsErrorKind.InvalidFolderPath,
                ex.Message,
                ex),
            SolutionErrorType.InvalidName => new SolutionItemsException(
                SolutionItemsErrorKind.InvalidFilePath,
                ex.Message,
                ex),
            _ => new SolutionItemsException(
                SolutionItemsErrorKind.PersistenceError,
                ex.Message,
                ex),
        };
    }
}