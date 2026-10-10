using System.CommandLine;

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

        command.SetAction(static (ParseResult pr) => RunAsync(pr));
        return command;
    }

    private static Task<int> RunAsync(ParseResult parseResult)
    {
        throw new NotImplementedException();
    }
}