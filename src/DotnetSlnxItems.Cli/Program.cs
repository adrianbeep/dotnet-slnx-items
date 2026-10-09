namespace DotnetSlnxItems.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Real command dispatch lands in issue #6 (CLI: add file).
        // Until then, this placeholder lets the project build and the tool package cleanly.
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            Console.WriteLine("slnx-items 0.1.0");
            return 0;
        }

        Console.Error.WriteLine("slnx-items: no commands implemented yet. Run with --version to check the binary.");
        return 1;
    }
}
