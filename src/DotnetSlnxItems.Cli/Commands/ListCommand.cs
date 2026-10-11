using System.CommandLine;

using DotnetSlnxItems.Cli.Errors;
using DotnetSlnxItems.Cli.Paths;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Commands;

/// <summary>
/// Builds the <c>slnx-items list</c> subcommand and wires it to
/// <see cref="RunAsync"/>. The body of <see cref="RunAsync"/> is
/// delegated to a private static so the <c>SetAction</c> lambda
/// stays single-expression and <c>dotnet format</c> does not
/// reformat a multi-statement lambda.
/// <para>
/// <c>list</c> is the symmetric read operation to <see
/// cref="AddFileCommand"/> / <see cref="RemoveFileCommand"/> / <see
/// cref="AddFolderCommand"/> / <see cref="RemoveFolderCommand"/>.
/// It walks the flat <see cref="SolutionItemInfo"/> stream that
/// <see cref="SolutionItemsService.ListAsync"/> produces and prints
/// a depth-aware tree: folder headers on their own line, files
/// indented under the most recently seen folder.
/// </para>
/// <para>
/// <c>--folder X</c> filters the output to files under the named
/// folder, omitting the folder header itself. The CLI replicates
/// Core's <c>NormalizeFolderPath</c> rule ("/" + Trim('/') + "/")
/// rather than calling into Core because the rule is a three-line
/// expression and exposing a Core helper would force a public-API
/// addition disproportionate to the trivia. Empty and
/// slashes-only inputs throw <see cref="SolutionItemsException"/>
/// with <see cref="SolutionItemsErrorKind.InvalidFolderPath"/>,
/// which <see cref="ErrorMapper"/> routes to exit 6. A folder that
/// exists nowhere in the solution throws
/// <see cref="SolutionItemsErrorKind.FolderNotFound"/>, routed to
/// exit 1.
/// </para>
/// <para>
/// This is a read-only command: it never calls
/// <see cref="SolutionItemsService.SaveAsync"/>. The folder-header
/// lines are indented by <c>currentFolderDepth * 2</c> spaces, the
/// same formula applied to files; the depth tracker is updated from
/// each Folder record's <c>Path.Count(c =&gt; c == '/') - 1</c>
/// AFTER the folder is printed so the folder's own line lands at
/// the correct indentation for its level.
/// </para>
/// </summary>
public static class ListCommand
{
    private const string NoSolutionInCwdMessage = "No solution file found in current directory";

    public static Command Create()
    {
        Command command = new("list", "List items in the solution.");

        Option<string?> solution = new("--solution", "-s")
        {
            Description = "Path to a .sln or .slnx file, or a directory containing exactly one.",
        };
        command.Options.Add(solution);

        Option<string?> folder = new("--folder")
        {
            Description = "Filter to a single folder. Top-level (e.g. 'Build') or nested (e.g. 'docs/infrastructure').",
        };
        command.Options.Add(folder);

        command.SetAction(parseResult => RunAsync(parseResult, solution, folder));
        return command;
    }

    private static async Task<int> RunAsync(
        ParseResult parseResult,
        Option<string?> solutionOption,
        Option<string?> folderOption)
    {
        string? explicitSolution = parseResult.GetValue(solutionOption);
        string? folderValue = parseResult.GetValue(folderOption);

        // Resolve target solution. Explicit --solution wins;
        // otherwise the cwd resolver returns the path, the cwd
        // (when >1), or null (when 0). In the null case Core never
        // gets called and we emit the CLI-owned message.
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

        // Branch on --folder up-front. The entire --folder branch
        // (validation, normalization, walk-and-print) lives inside a
        // single try/catch so the CLI throws-and-maps pattern from
        // the siblings stays consistent: a folder name that fails
        // validation lands on stderr with the right exit code
        // instead of bubbling out as an unhandled exception.
        if (folderValue is not null)
        {
            try
            {
                string folderInput = folderValue;

                // CLI replicates Core's NormalizeFolderPath: "/" +
                // trim('/') + "/". Empty/slashes-only input
                // triggers InvalidFolderPath (exit 6) — the same
                // route Core would take if the validation lived
                // there.
                if (folderInput.Trim('/').Length == 0)
                {
                    throw new SolutionItemsException(
                        SolutionItemsErrorKind.InvalidFolderPath,
                        $"Invalid folder name: '{folderInput}'. " +
                        "Folder names cannot be empty or contain only slashes.");
                }

                string normalized = "/" + folderInput.Trim('/') + "/";

                IReadOnlyList<SolutionItemInfo> items = await service.ListAsync();

                // Detect first: walk items looking for a Folder
                // record whose Path equals the normalized target.
                // Throw FolderNotFound BEFORE printing anything so a
                // bad lookup never leaks partial output.
                bool folderFound = false;
                string currentFolderPath = string.Empty;
                int currentFolderDepth = 0;
                foreach (SolutionItemInfo item in items)
                {
                    if (item.Kind == SolutionItemKind.Folder)
                    {
                        currentFolderPath = item.Path;
                        currentFolderDepth = item.Path.Count(c => c == '/') - 1;
                        if (item.Path == normalized)
                        {
                            folderFound = true;
                        }
                        continue;
                    }

                    // File record: only print if it lives under the
                    // targeted folder (the immediately preceding
                    // Folder record is its parent).
                    if (folderFound && currentFolderPath == normalized)
                    {
                        string indent = new string(' ', currentFolderDepth * 2);
                        Console.Out.WriteLine($"{indent}{item.Path}");
                    }
                }

                if (!folderFound)
                {
                    throw new SolutionItemsException(
                        SolutionItemsErrorKind.FolderNotFound,
                        $"Folder '{normalized}' was not found in the solution.");
                }

                return 0;
            }
            catch (SolutionItemsException ex)
            {
                (string message, int exitCode) = ErrorMapper.Map(ex);
                Console.Error.WriteLine(message);
                return exitCode;
            }
        }

        // No --folder: walk items printing both Folder and File
        // records. The depth tracker is recomputed from each
        // Folder record's Path.Count('/') - 1 AFTER the folder is
        // printed, so /Solution Items/ (depth 1) lands at 0 spaces
        // and README.md (its child file) lands at 2 spaces.
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

        int depth = 0;
        foreach (SolutionItemInfo item in allItems)
        {
            string indent = new string(' ', depth * 2);
            if (item.Kind == SolutionItemKind.Folder)
            {
                Console.Out.WriteLine($"{indent}{item.Path}");
                depth = item.Path.Count(c => c == '/') - 1;
            }
            else
            {
                Console.Out.WriteLine($"{indent}{item.Path}");
            }
        }

        // list is read-only; no SaveAsync call.
        return 0;
    }
}
