using System.CommandLine;
using DotnetSlnxItems.Cli.Errors;
using DotnetSlnxItems.Cli.Paths;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Commands;

/// <summary>
/// Builds the <c>slnx-items add file &lt;paths...&gt;</c> subcommand
/// and wires it to <see cref="RunAsync"/>. The body of
/// <see cref="RunAsync"/> is delegated to a private static so the
/// <c>SetAction</c> lambda stays single-expression and
/// <c>dotnet format</c> does not reformat a multi-statement lambda.
/// </summary>
public static class AddFileCommand
{
    private const string NoSolutionInCwdMessage = "No solution file found in current directory";

    public static Command Create()
    {
        Command command = new("file", "Add one or more files to the solution's Solution Items folder.");

        Argument<string[]> paths = new("paths")
        {
            Description = "One or more file paths to add.",
            Arity = ArgumentArity.OneOrMore,
        };
        command.Arguments.Add(paths);

        Option<string?> solution = new("--solution", "-s")
        {
            Description = "Path to a .sln or .slnx file, or a directory containing exactly one.",
        };
        command.Options.Add(solution);

        Option<string?> solutionFolder = new("--solution-folder")
        {
            Description = "Destination Solution Items folder. Defaults to /Solution Items/.",
        };
        command.Options.Add(solutionFolder);

        command.SetAction(parseResult => RunAsync(parseResult, paths, solution, solutionFolder));
        return command;
    }

    private static async Task<int> RunAsync(
        ParseResult parseResult,
        Argument<string[]> pathsArgument,
        Option<string?> solutionOption,
        Option<string?> solutionFolderOption)
    {
        string[] paths = parseResult.GetValue(pathsArgument)!;
        string? explicitSolution = parseResult.GetValue(solutionOption);
        string? solutionFolderOptionValue = parseResult.GetValue(solutionFolderOption);

        // Pre-flight: every input file must exist on disk. This is a
        // CLI-level guarantee; Core accepts missing paths silently.
        foreach (string path in paths)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"File not found: '{path}'");
                return 1;
            }
        }

        // Resolve target solution. Explicit --solution wins; otherwise
        // the cwd resolver returns the path, the cwd (when >1), or
        // null (when 0). In the null case Core never gets called.
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

        // Partition the input paths into "already present" (silent
        // no-op) and "to add". Core dedupes by the relative path it
        // stores; replicating the relative-path computation here lets
        // the success message name only the genuinely new files.
        string solutionDir = Path.GetDirectoryName(target) ?? Environment.CurrentDirectory;
        HashSet<string> existingRelative = new(StringComparer.CurrentCulture);
        foreach (SolutionItemInfo item in await service.ListAsync())
        {
            if (item.Kind == SolutionItemKind.File)
            {
                existingRelative.Add(item.Path);
            }
        }

        var newRelativePaths = new List<string>();
        var newInputPaths = new List<string>();
        foreach (string path in paths)
        {
            string relative = ToRelativeFilePath(solutionDir, path);
            if (!existingRelative.Contains(relative))
            {
                newRelativePaths.Add(relative);
                newInputPaths.Add(path);
            }
        }

        if (newInputPaths.Count > 0)
        {
            try
            {
                await service.AddFilesAsync(newInputPaths, solutionFolderOptionValue);
                await service.SaveAsync();
            }
            catch (SolutionItemsException ex)
            {
                (string message, int exitCode) = ErrorMapper.Map(ex);
                Console.Error.WriteLine(message);
                return exitCode;
            }

            foreach (string relative in newRelativePaths)
            {
                Console.Out.WriteLine($"Added: {relative}");
            }
        }

        Console.Out.WriteLine($"Added {newRelativePaths.Count} file(s).");
        return 0;
    }

    /// <summary>
    /// Replicates <c>SolutionItemsService.ResolveRelativeFilePath</c>
    /// (private to Core) so the CLI can decide which input paths are
    /// already present before calling <c>AddFilesAsync</c>. Kept in
    /// sync with Core's implementation; if Core changes its relative
    /// storage rules, this must change too.
    /// </summary>
    private static string ToRelativeFilePath(string solutionDir, string filePath)
    {
        string fullPath = Path.IsPathRooted(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetFullPath(Path.Combine(solutionDir, filePath));

        string relative = Path.GetRelativePath(solutionDir, fullPath);
        return relative.Replace('\\', '/');
    }
}