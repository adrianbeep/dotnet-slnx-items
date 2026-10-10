using System.CommandLine;
using System.Globalization;

using DotnetSlnxItems.Cli.Commands;

namespace DotnetSlnxItems.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Force invariant UI culture so System.CommandLine's help
        // output is stable for substring assertions regardless of the
        // host's LANG/LC_ALL. The CLI's user-facing copy is always
        // English-only per spec.
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

        RootCommand root = new("slnx-items — manage Solution Items in .sln and .slnx");

        // Drop System.CommandLine's default --version option so we can
        // emit the project's own "slnx-items 0.1.0" line instead of
        // the assembly informational version. AGENTS.md locks the MVP
        // version to 0.1.0; bumping requires a Semver review bump.
        Option? autoVersion = null;
        foreach (Option option in root.Options)
        {
            if (option.Name == "--version")
            {
                autoVersion = option;
                break;
            }
        }
        if (autoVersion is not null)
        {
            root.Options.Remove(autoVersion);
        }

        Option<bool> versionOption = new("--version", "-v")
        {
            Description = "Print version information and exit.",
            Arity = ArgumentArity.Zero,
        };
        root.Options.Add(versionOption);

        root.SetAction(parseResult =>
        {
            if (parseResult.GetValue(versionOption))
            {
                Console.Out.WriteLine("slnx-items 0.1.0");
                return 0;
            }
            Console.Error.WriteLine("slnx-items: missing subcommand. Use --help for usage.");
            return 1;
        });

        Command add = new("add", "Add Solution Items to the solution.");
        add.Subcommands.Add(AddFileCommand.Create());
        add.Subcommands.Add(AddFolderCommand.Create());
        root.Subcommands.Add(add);

        return await root.Parse(args).InvokeAsync();
    }
}