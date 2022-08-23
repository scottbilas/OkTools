using DocoptNet;
using OkTools.Core;

const string programVersion = "0.1";

try
{
    return LoggoCliArguments.CreateParser()
        .EnableHelp()
        .WithVersion(programVersion)
        .Parse(args)
        .Match(
            argsResult => (int)Main(argsResult),
            _ /*helpResult*/ =>
            {
                var helpText = FormatHelp(LoggoCliArguments.Help);
                Console.WriteLine(DocoptUtility.Reflow(helpText, Console.WindowWidth));
                return (int)CliExitCode.Help;
            },
            _ /*versionResult*/ =>
            {
                var shortDescription = FormatHelp(LoggoCliArguments.Help[..LoggoCliArguments.Help.IndexOf('\n')].Trim());
                Console.WriteLine(shortDescription);
                return (int)CliExitCode.Help;
            },
            errorResult =>
            {
                var printed = false;

                if (args.Length != 0)
                {
                    Console.Error.WriteLine("Bad command line: " + args.StringJoin(' '));
                    printed = true;
                }

                if (errorResult.Error.Any())
                {
                    Console.Error.WriteLine(errorResult.Error);
                    printed = true;
                }

                if (printed)
                    Console.Error.WriteLine();

                var usageText = FormatHelp(LoggoCliArguments.Usage);
                Console.Error.WriteLine(DocoptUtility.Reflow(usageText, Console.WindowWidth));

                return (int)CliExitCode.ErrorUsage;
            });

    static string FormatHelp(string helpText)
    {
        var programName = Path.GetFileNameWithoutExtension(Environment.ProcessPath!);
        return string.Format(helpText, programName, programVersion);
    }
}
catch (Exception x)
{
    Console.Error.WriteLine("Internal error!");
    Console.Error.WriteLine();
    Console.Error.WriteLine(x);
    return (int)CliExitCode.ErrorSoftware;
}

static CliExitCode Main(LoggoCliArguments args)
{
    StreamWriter? fileWriter = null;
    var optPath = args.ArgDestination!;

    // ReSharper disable AccessToModifiedClosure
    void Dispose()
    {
        if (fileWriter == null)
            return;

        fileWriter.Dispose();
        if (args.OptDeleteOnExit)
            File.Delete(optPath);
    }
    // ReSharper restore AccessToModifiedClosure

    Console.CancelKeyPress += (_, _) => Dispose();

    try
    {
        var optStdout = args.OptStdout;

        if (optPath.IsNullOrEmpty())
            optStdout = true;
        else
        {
            optPath = Path.GetFullPath(optPath);
            fileWriter = new StreamWriter(File.Open(optPath,
                args.OptOverwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete));
        }

        var optLineNums = args.OptLineNums;
        var optDelay = ParseMinMaxInt(args.OptDelay);
        var optWidth = ParseMinMaxInt(args.OptWidth);
        var optSize = TryParseSize(args.OptSize);
        var optLines = TryParseInt(args.OptLines);
        var optPattern = args.OptPattern;
        var optEol = args.OptEol;
        var optIntraLineDelay = args.OptIntraLineDelay;

        string[]? patternLines = null;
        if (File.Exists(optPattern))
        {
            patternLines = File.ReadAllLines(optPattern);
            var maxWidth = patternLines.Max(l => l.Length) + 50; // leave room for prefixes
            optWidth = (maxWidth, maxWidth);
        }

        var csb = new CharSpanBuilder(optWidth.max + 2); // room for \r\n

        for (var (written, lineNum) = (0L, 0);; ++lineNum)
        {
            if (optLines != null && lineNum >= optLines)
                break;
            if (optSize != null && written >= optSize)
                break;
            if (optLines == null && optSize == null && patternLines != null && lineNum == patternLines.Length)
                break;

            csb.Clear();
            var width = Random.Shared.Next(optWidth.min, optWidth.max + 1);

            if (optLineNums)
            {
                csb.Append(lineNum);
                csb.Append(": ");
            }

            switch (optPattern)
            {
                case "lorem":
                    LoremIpsum.Fill(ref csb);
                    csb.Length = width;
                    break;

                case "nums":
                    while (csb.Length < width)
                    {
                        var digit = csb.Length % 10;
                        if (digit == 0)
                        {
                            var group = csb.Length / 10;
                            csb.Append((char)(group % 26 + 'a'));
                        }
                        else
                            csb.Append((char)(digit + '0'));
                    }

                    break;

                case "loggy":
                    // TODO: output log-looking stuff (or maybe just use that go tool 'flog'...)
                    // ALSO: maybe disable width constraints? will ruin json matchers otherwise
                    throw new NotImplementedException();

                default:
                    if (patternLines == null)
                        throw new DocoptInputErrorException($"Invalid --pattern option `{optPattern}`");
                    csb.Append(patternLines[lineNum % patternLines.Length]);
                    break;
            }

            var eol = optEol switch
            {
                "crlf" => "\r\n",
                "lf" => "\n",
                "mix" => Random.Shared.Next() % 2 == 0 ? "\r\n" : "\n",
                _ => throw new DocoptInputErrorException($"Invalid --eol option `{optEol}`")
            };

            csb.Length = Math.Min(csb.Length, width);
            csb.Append(eol);

            var delay = Random.Shared.Next(optDelay.min, optDelay.max + 1);
            var chars = csb.Chars;

            while (chars.Count > 0)
            {
                var write = chars.Count;

                if (delay > 0 && optIntraLineDelay)
                {
                    // TODO: implement me
                }

                fileWriter?.Write(chars.Array!, chars.Offset, write);
                if (optStdout)
                    Console.Write(chars.Array!, chars.Offset, write);

                written += write;
                chars = chars[write..];
            }

            fileWriter?.Flush();

            if (delay > 0)
                Thread.Sleep(delay);
        }

        return CliExitCode.Success;
    }
    finally
    {
        Dispose();
    }
}
