using System.CommandLine;

using DotnetSlnxItems.Cli.Errors;
using DotnetSlnxItems.Cli.Paths;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Commands;

/// <summary>
/// Builds the <c>slnx-items sync</c> subcommand and wires it to
/// <see cref="RunAsync"/>. The body of <see cref="RunAsync"/> is
/// delegated to a private static so the <c>SetAction</c> lambda
/// stays single-expression and <c>dotnet format</c> does not
/// reformat a multi-statement lambda.
/// <para>
/// <c>sync</c> is the declarative, idempotent counterpart to
/// <see cref="AddFileCommand"/> / <see cref="RemoveFileCommand"/> /
/// <see cref="AddFolderCommand"/> / <see cref="RemoveFolderCommand"/>.
/// It reads a <c>.slnx-items.yaml</c> manifest (per
/// <c>docs/design/manifest-schema.md</c>) and applies its declared
/// folder + file set to the solution via
/// <see cref="SolutionItemsService.AddFolderAsync"/> and
/// <see cref="SolutionItemsService.AddFilesAsync"/>. <c>sync</c>
/// never removes items; <c>--prune</c> is post-MVP per the issue
/// body.
/// </para>
/// <para>
/// The CLI computes the diff locally via
/// <see cref="SolutionItemsService.ListAsync"/> so no Core API
/// changes beyond the <see cref="SolutionItemsManifest"/> record
/// are required. <c>--dry-run</c> performs a pure read: no
/// <c>Add*</c> or <c>SaveAsync</c> call is made. The folder filter
/// for the <c>Created folder</c> line keeps ancestor records
/// (e.g. <c>/docs/</c>) out of the output so the user only sees
/// the folder they declared.
/// </para>
/// </summary>
public static class SyncCommand
{
    private const string NoSolutionInCwdMessage = "No solution file found in current directory";

    public static Command Create()
    {
        Command command = new("sync", "Apply the .slnx-items.yaml manifest to the solution.");

        Option<string?> manifest = new("--manifest", "-m")
        {
            Description = "Path to a .slnx-items.yaml manifest. Defaults to '.slnx-items.yaml' in the current working directory.",
        };
        command.Options.Add(manifest);

        Option<string?> solution = new("--solution", "-s")
        {
            Description = "Path to a .sln or .slnx file, or a directory containing exactly one.",
        };
        command.Options.Add(solution);

        Option<bool> dryRun = new("--dry-run")
        {
            Description = "Print the changes that would be applied without modifying it.",
        };
        command.Options.Add(dryRun);

        command.SetAction(parseResult => RunAsync(parseResult, manifest, solution, dryRun));
        return command;
    }

    private static async Task<int> RunAsync(
        ParseResult parseResult,
        Option<string?> manifestOption,
        Option<string?> solutionOption,
        Option<bool> dryRunOption)
    {
        try
        {
            return await RunCoreAsync(parseResult, manifestOption, solutionOption, dryRunOption);
        }
        catch (SolutionItemsException ex)
        {
            (string message, int exitCode) = ErrorMapper.Map(ex);
            Console.Error.WriteLine(message);
            return exitCode;
        }
    }

    private static async Task<int> RunCoreAsync(
        ParseResult parseResult,
        Option<string?> manifestOption,
        Option<string?> solutionOption,
        Option<bool> dryRunOption)
    {
        string? explicitManifest = parseResult.GetValue(manifestOption);
        bool dryRun = parseResult.GetValue(dryRunOption);

        string manifestPath = explicitManifest
            ?? Path.Combine(Environment.CurrentDirectory, ".slnx-items.yaml");

        if (!File.Exists(manifestPath))
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.ManifestNotFound,
                $"Manifest file not found: {manifestPath}");
        }

        SolutionItemsManifest manifest = YamlManifestReader.Read(manifestPath);

        // Pre-flight validation. CLI-owned: Core's validation lives
        // in AddFolderAsync/AddFilesAsync and returns InvalidFolderPath
        // (exit 6) which is the wrong shape for sync — sync is
        // declarative and an InvalidManifest (exit 1) is more honest
        // about what the user got wrong. File existence check happens
        // here too; failing before any Add* call is what guarantees
        // the "no partial state" rule from docs/design/manifest-schema.md.
        string manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))
            ?? Environment.CurrentDirectory;

        Dictionary<string, List<string>> filesByFolder = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<string>> entry in manifest.Folders)
        {
            string folderKey = entry.Key;

            if (string.IsNullOrWhiteSpace(folderKey)
                || folderKey.Contains("..", StringComparison.Ordinal)
                || folderKey.StartsWith("/", StringComparison.Ordinal))
            {
                throw new SolutionItemsException(
                    SolutionItemsErrorKind.InvalidManifest,
                    $"Invalid manifest: folder key '{folderKey}' is empty, contains '..', or is an absolute path.");
            }

            IReadOnlyList<string> files = entry.Value ?? Array.Empty<string>();
            foreach (string file in files)
            {
                if (Path.IsPathRooted(file))
                {
                    throw new SolutionItemsException(
                        SolutionItemsErrorKind.InvalidManifest,
                        $"Invalid manifest: file path '{file}' must be relative to the manifest directory.");
                }

                string absolutePath = Path.GetFullPath(Path.Combine(manifestDir, file));
                if (!File.Exists(absolutePath))
                {
                    throw new SolutionItemsException(
                        SolutionItemsErrorKind.FileReferencedInManifestNotFound,
                        $"File referenced in manifest does not exist: {file}");
                }

                if (!filesByFolder.TryGetValue(folderKey, out List<string>? _fileList))
                {
                    _fileList = new List<string>();
                    filesByFolder[folderKey] = _fileList;
                }
                _fileList.Add(file);
            }
        }

        // Resolve target solution. Explicit --solution wins;
        // otherwise the cwd resolver returns the path, the cwd (when
        // >1), or null (when 0). In the null case Core never gets
        // called.
        string? explicitSolution = parseResult.GetValue(solutionOption);
        string? target = explicitSolution ?? SolutionResolver.ResolveFromCwd(Environment.CurrentDirectory);
        if (target is null)
        {
            Console.Error.WriteLine(NoSolutionInCwdMessage);
            return 2;
        }

        SolutionItemsService service;
        try
        {
            service = await SolutionItemsService.CreateAsync(target);
        }
        catch (SolutionItemsException ex)
        {
            (string message, int exitCode) = ErrorMapper.Map(ex);
            Console.Error.WriteLine(message);
            return exitCode;
        }

        // Partition current state via ListAsync. The stream is flat:
        // each Folder record is followed by its File records in the
        // order Core emits them. A per-folder HashSet tracks which
        // files are already there.
        IReadOnlyList<SolutionItemInfo> allItems;
        try
        {
            allItems = await service.ListAsync();
        }
        catch (SolutionItemsException ex)
        {
            (string message, int exitCode) = ErrorMapper.Map(ex);
            Console.Error.WriteLine(message);
            return exitCode;
        }

        HashSet<string> existingFolders = new(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> existingFiles = new(StringComparer.Ordinal);
        string? currentFolder = null;
        foreach (SolutionItemInfo item in allItems)
        {
            if (item.Kind == SolutionItemKind.Folder)
            {
                currentFolder = item.Path;
                existingFolders.Add(item.Path);
            }
            else if (currentFolder is not null)
            {
                if (!existingFiles.TryGetValue(currentFolder, out HashSet<string>? set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    existingFiles[currentFolder] = set;
                }
                set.Add(item.Path);
            }
        }

        // Diff: for each manifest entry, decide whether the folder
        // and each file need to be created/added.
        List<string> foldersToCreate = new();
        Dictionary<string, List<string>> filesToAdd = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<string>> entry in manifest.Folders)
        {
            string folderKey = entry.Key;
            string normalizedKey = "/" + folderKey.Trim('/') + "/";
            bool folderExists = existingFolders.Contains(normalizedKey);
            if (!folderExists)
            {
                foldersToCreate.Add(folderKey);
            }

            foreach (string file in filesByFolder.TryGetValue(folderKey, out List<string>? list)
                ? list
                : new List<string>())
            {
                bool fileExists = folderExists
                    && existingFiles.TryGetValue(normalizedKey, out HashSet<string>? existing)
                    && existing.Contains(file);
                if (!fileExists)
                {
                    if (!filesToAdd.TryGetValue(folderKey, out List<string>? toAdd))
                    {
                        toAdd = new List<string>();
                        filesToAdd[folderKey] = toAdd;
                    }
                    toAdd.Add(file);
                }
            }
        }

        // Empty manifest (no top-level keys) is a true no-op: emit
        // no output and exit 0. Non-empty manifests with zero changes
        // still print the zero summary per issue body example.
        if (manifest.Folders.Count == 0)
        {
            return 0;
        }

        if (dryRun)
        {
            foreach (string folderKey in foldersToCreate)
            {
                string normalized = "/" + folderKey.Trim('/') + "/";
                Console.Out.WriteLine($"[DRY-RUN] Would create folder '{normalized}'.");
            }
            foreach (KeyValuePair<string, List<string>> kvp in filesToAdd)
            {
                foreach (string file in kvp.Value)
                {
                    Console.Out.WriteLine($"[DRY-RUN] Would add file '{kvp.Key}/{file}'.");
                }
            }

            int totalFolders = foldersToCreate.Count;
            int totalFiles = filesToAdd.Values.Sum(f => f.Count);
            Console.Out.WriteLine($"[DRY-RUN] {FormatSummary("Would sync", totalFolders, totalFiles)}");
            return 0;
        }

        // Apply: folders first (so each AddFilesAsync finds its
        // folder), then files, then a single SaveAsync.
        int foldersCreated = 0;
        foreach (string folderKey in foldersToCreate)
        {
            IReadOnlyList<FolderAddResult> records = await service.AddFolderAsync(folderKey);
            string normalizedKey = "/" + folderKey.Trim('/') + "/";
            foreach (FolderAddResult record in records)
            {
                if (record.Created && record.Path == normalizedKey)
                {
                    Console.Out.WriteLine($"Created folder '{record.Path}'.");
                    foldersCreated++;
                }
            }
        }

        int filesAdded = 0;
        foreach (KeyValuePair<string, List<string>> kvp in filesToAdd)
        {
            string folderKey = kvp.Key;
            List<string> absoluteFilePaths = new(kvp.Value.Count);
            foreach (string file in kvp.Value)
            {
                absoluteFilePaths.Add(Path.GetFullPath(Path.Combine(manifestDir, file)));
            }

            try
            {
                await service.AddFilesAsync(absoluteFilePaths, folderKey);
            }
            catch (SolutionItemsException ex) when (ex.Kind == SolutionItemsErrorKind.InvalidFilePath)
            {
                throw new SolutionItemsException(
                    SolutionItemsErrorKind.InvalidManifest,
                    $"Invalid manifest: file path in folder '{folderKey}' is outside the solution directory.",
                    ex);
            }

            foreach (string file in kvp.Value)
            {
                Console.Out.WriteLine($"Added file '{folderKey}/{file}'.");
                filesAdded++;
            }
        }

        try
        {
            await service.SaveAsync();
        }
        catch (SolutionItemsException ex)
        {
            (string message, int exitCode) = ErrorMapper.Map(ex);
            Console.Error.WriteLine(message);
            return exitCode;
        }

        Console.Out.WriteLine(FormatSummary("Synced", foldersCreated, filesAdded));
        return 0;
    }

    private static string FormatSummary(string prefix, int folders, int files)
    {
        string folderWord = folders == 1 ? "folder" : "folders";
        string fileWord = files == 1 ? "file" : "files";
        return $"{prefix} {folders} {folderWord} and {files} {fileWord}.";
    }
}