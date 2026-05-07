using System;
using System.Threading;

// Tiny helper binary backing ProcessGracefulShutdownTests and
// SubprocessCliTransportTests. Modes are selected via the first argv:
//
//   exit-on-eof        Read stdin until EOF, then exit 0.
//   ignore-eof         Sleep forever; ignore stdin completely.
//   echo-line          Read one line from stdin, write it to stdout, exit 0.
//   emit-ndjson <n>    Emit n NDJSON objects to stdout then exit 0.
//   echo-ndjson        Read NDJSON lines from stdin, echo each back to stdout
//                      with an "echoed" field appended; exits on stdin EOF.
//   stderr-then-exit   Write a banner line to stderr, exit 0 with no stdout.
//   exit-nonzero       Exit with code 7 immediately.
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

    case "emit-ndjson":
        var count = args.Length >= 2 && int.TryParse(args[1], out var parsed) ? parsed : 1;
        for (var i = 0; i < count; i++)
        {
            Console.Out.WriteLine($"{{\"index\":{i},\"type\":\"sample\"}}");
        }
        Console.Out.Flush();
        return 0;

    case "echo-ndjson":
        string? incoming;
        while ((incoming = Console.In.ReadLine()) is not null)
        {
            var trimmed = incoming.TrimEnd('\r');
            if (trimmed.Length == 0)
            {
                continue;
            }
            // Append an "echoed":true field to each object. Keep it cheap —
            // the test only inspects that the round-trip happened.
            var withEcho = trimmed.EndsWith('}') ? trimmed[..^1] + ",\"echoed\":true}" : trimmed;
            Console.Out.WriteLine(withEcho);
            Console.Out.Flush();
        }
        return 0;

    case "stderr-then-exit":
        Console.Error.WriteLine("[fixture] hello from stderr");
        Console.Error.Flush();
        return 0;

    case "exit-nonzero":
        return 7;

    case "ignore-eof":
    default:
        // Do not read stdin. Sleep forever; the parent must escalate to
        // Kill() to terminate.
        Thread.Sleep(Timeout.Infinite);
        return 0;
}
