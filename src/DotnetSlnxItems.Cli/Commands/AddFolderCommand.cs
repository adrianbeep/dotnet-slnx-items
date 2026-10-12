using System.CommandLine;

using DotnetSlnxItems.Cli.Errors;
using DotnetSlnxItems.Cli.Paths;
using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Cli.Commands;

/// <summary>
/// Builds the <c>slnx-items add folder &lt;name&gt;</c>
/// subcommand and wires it to <see cref="RunAsync"/>. The body of
/// <see cref="RunAsync"/> is delegated to a private static so the
/// <c>SetAction</c> lambda stays single-expression and
/// <c>dotnet format</c> does not reformat a multi-statement lambda.
/// </summary>
public static class AddFolderCommand
{
    private const string NoSolutionInCwdMessage = "No solution file found in current directory";

    public static Command Create()
    {
        Command command = new("folder", "Add a virtual folder to the solution.");

        Argument<string> name = new("name")
        {
            Description = "Folder name, e.g. 'Build' or 'a/b/c'.",
            Arity = ArgumentArity.ExactlyOne,
        };
        command.Arguments.Add(name);

        Option<string?> solution = new("--solution", "-s")
        {
            Description = "Path to a .sln or .slnx file, or a directory containing exactly one.",
        };
        command.Options.Add(solution);

        command.SetAction(parseResult => RunAsync(parseResult, name, solution));
        return command;
    }

    private static async Task<int> RunAsync(
        ParseResult parseResult,
        Argument<string> nameArgument,
        Option<string?> solutionOption)
    {
        string name = parseResult.GetValue(nameArgument)!;
        string? explicitSolution = parseResult.GetValue(solutionOption);

        // CLI-level pre-flight: reject .. and absolute paths here so
        // the user gets a clear error before any service call. Core
        // also has folder-name validation but for a different
        // failure mode (empty/whitespace/slashes-only); both share
        // CliExitCodes.InvalidFolderPath per the locked CLI contract.
        if (name.Contains("..", StringComparison.Ordinal)
            || name.StartsWith("/", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"Invalid folder name: '{name}'. " +
                $"Folder names cannot contain '..' or be absolute paths.");
            return CliExitCodes.InvalidFolderPath;
        }

        // Resolve target solution. Explicit --solution wins;
        // otherwise the cwd resolver returns the path, the cwd (when
        // >1), or null (when 0). In the null case Core never gets
        // called.
        string? target = explicitSolution ?? SolutionResolver.ResolveFromCwd(Environment.CurrentDirectory);
        if (target is null)
        {
            Console.Error.WriteLine(NoSolutionInCwdMessage);
            return CliExitCodes.NoSolutionInCwd;
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

        IReadOnlyList<FolderAddResult> records;
        try
        {
            records = await service.AddFolderAsync(name);
        }
        catch (SolutionItemsException ex)
        {
            (string message, int exitCode) = ErrorMapper.Map(ex);
            Console.Error.WriteLine(message);
            return exitCode;
        }

        foreach (FolderAddResult record in records)
        {
            if (record.Created)
            {
                Console.Out.WriteLine($"Folder '{record.Path}' added to the solution.");
            }
            else
            {
                Console.Out.WriteLine($"Folder '{record.Path}' already exists in the solution.");
            }
        }

        Console.Out.WriteLine($"Created {records.Count(r => r.Created)} folder(s).");

        await service.SaveAsync();
        return 0;
    }
}