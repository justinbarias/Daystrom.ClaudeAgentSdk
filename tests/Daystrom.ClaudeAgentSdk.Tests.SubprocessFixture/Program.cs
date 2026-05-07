using System;
using System.Threading;

// Tiny helper binary backing ProcessGracefulShutdownTests and (later)
// SubprocessCliTransportTests. Modes are selected via the first argv:
//
//   exit-on-eof        Read stdin until EOF, then exit 0.
//   ignore-eof         Sleep forever; ignore stdin completely.
//   echo-line          Read one line from stdin, write it to stdout, exit 0.
//
// Anything else falls through to "ignore-eof" so tests fail loudly rather
// than silently hanging on a typo.

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: SubprocessFixture <mode>");
    Environment.Exit(64);
}

switch (args[0])
{
    case "exit-on-eof":
        // Drain stdin to EOF.
        while (Console.In.ReadLine() is not null) { }
        return 0;

    case "echo-line":
        var line = Console.In.ReadLine();
        Console.Out.WriteLine(line ?? string.Empty);
        Console.Out.Flush();
        return 0;

    case "ignore-eof":
    default:
        // Do not read stdin. Sleep forever; the parent must escalate to
        // Kill() to terminate.
        Thread.Sleep(Timeout.Infinite);
        return 0;
}
