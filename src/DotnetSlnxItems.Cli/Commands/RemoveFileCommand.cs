using System.CommandLine;

using DotnetSlnxItems.Cli.Errors;
using DotnetSlnxItems.Cli.Paths;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Commands;

/// <summary>
/// Builds the <c>slnx-items remove file &lt;paths...&gt;</c>
/// subcommand and wires it to <see cref="RunAsync"/>. The body of
/// <see cref="RunAsync"/> is delegated to a private static so the
/// <c>SetAction</c> lambda stays single-expression and
/// <c>dotnet format</c> does not reformat a multi-statement lambda.
/// The command is the symmetric inverse of <see cref="AddFileCommand"/>:
/// the same resolution, the same option flags, the same error
/// table — but it partitions the input into "present" vs "absent"
/// via <c>ListAsync</c> and calls <c>RemoveFilesAsync</c> instead of
/// <c>AddFilesAsync</c>.
/// </summary>
public static class RemoveFileCommand
{
    private const string NoSolutionInCwdMessage = "No solution file found in current directory";

    public static Command Create()
    {
        Command command = new("file", "Remove one or more files from the solution's Solution Items folder.");

        Argument<string[]> paths = new("paths")
        {
            Description = "One or more file paths to remove.",
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
            Description = "Source Solution Items folder. Defaults to /Solution Items/.",
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

        // Partition the input paths into "present" (will be removed)
        // and "absent" (no-op). Core's RemoveFilesAsync returns a
        // plain Task; the only signal of "was present" comes from
        // reading the solution up-front via ListAsync.
        string solutionDir = Path.GetDirectoryName(target) ?? Environment.CurrentDirectory;
        HashSet<string> existingRelative = new(StringComparer.CurrentCulture);
        foreach (SolutionItemInfo item in await service.ListAsync())
        {
            if (item.Kind == SolutionItemKind.File)
            {
                existingRelative.Add(item.Path);
            }
        }

        // Print one line per input, in input order, so callers
        // running 'remove file A.md B.md' get results in the same
        // order. Each line says either removed-from-solution or
        // was-not-in-solution; no summary line because removal is
        // naturally idempotent and every input is reported exactly
        // once.
        foreach (string path in paths)
        {
            string relative = ToRelativeFilePath(solutionDir, path);
            if (existingRelative.Contains(relative))
            {
                Console.Out.WriteLine($"File '{relative}' removed from the solution.");
            }
            else
            {
                Console.Out.WriteLine($"File '{relative}' was not in the solution.");
            }
        }

        // Unconditionally call RemoveFilesAsync with the full input.
        // Core no-ops on absent paths (folder.Files is null or the
        // file is missing), so calling it on all paths keeps the
        // call site symmetric with AddFileCommand and avoids a
        // second pass.
        try
        {
            await service.RemoveFilesAsync(paths, solutionFolderOptionValue);
            await service.SaveAsync();
        }
        catch (SolutionItemsException ex)
        {
            (string message, int exitCode) = ErrorMapper.Map(ex);
            Console.Error.WriteLine(message);
            return exitCode;
        }

        return 0;
    }

    /// <summary>
    /// Replicates <c>SolutionItemsService.ResolveRelativeFilePath</c>
    /// (private to Core) so the CLI can decide which input paths are
    /// already present before calling <c>RemoveFilesAsync</c>. Kept
    /// in sync with Core's implementation; if Core changes its
    /// relative storage rules, this must change too. Duplicated from
    /// <see cref="AddFileCommand"/>; extract to
    /// <c>Paths/RelativePathResolver.cs</c> when a third command
    /// needs a copy.
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