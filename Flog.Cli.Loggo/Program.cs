﻿using DocoptNet;
using OkTools.Core;

public static partial class Program
{
    const string k_programName = "loggo";
    const string k_docName = $"{k_programName}, the logfile generator";
    const string k_docVersion = "0.1";
    const string k_docUsage = $@"{k_docName}

Usage:
  {k_programName}  [options] [PATH]
  {k_programName}  --version

Options:
  --stdout           Mirror to stdout as well (defaults to true if PATH not specified)
  --linenums         Prefix each line with a line number
  --delay DELAY      Delay between each line being added to the log in ms [default: 1,5]
  --width WIDTH      Width of generated lines [default: 1,200]
  --size SIZE        Stop generating after total log output gets to this size (can postfix SIZE with kb mb gb)
  --lines LINES      Stop generating after LINES lines
  --pattern PATTERN  Pattern to use for generated lines (one of: lorem, nums, loggy, unity) [default: nums]
  --eol EOL          End of line character to use (one of: crlf, lf, mix) [default: lf]

Notes:
  DELAY and WIDTH can be ""min,max"" entries separated by a comma (for example `1MB,50GB`). Both min and max will be the same if there is only one entry.

Alternative:
  go install github.com/mingrammer/flog@latest
";

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
                throw new DocoptExitException(k_docUsage);

            var opt = new Docopt().Apply(k_docUsage, args, version: $"{k_docName} {k_docVersion}", help: false);

            return (int)LoggIt(opt);
        }
        catch (DocoptInputErrorException x)
        {
            Console.Error.WriteLine("bad command line: " + args.StringJoin(' '));
            Console.Error.WriteLine(x.Message);
            return (int)CliExitCode.ErrorUsage;
        }
        catch (DocoptExitException x)
        {
            Console.WriteLine(DocoptUtility.Reflow(x.Message, Console.WindowWidth));
            return (int)CliExitCode.Help;
        }
        catch (Exception x)
        {
            Console.Error.WriteLine("Internal error!");
            Console.Error.WriteLine();
            Console.Error.WriteLine(x);
            return (int)CliExitCode.ErrorSoftware;
        }
    }

    static CliExitCode LoggIt(IDictionary<string, ValueObject> options)
    {
        StreamWriter? fileWriter = null;

        try
        {
            var optStdout = options["--stdout"].IsTrue;

            var optPath = options["PATH"].ToString();
            if (optPath.IsNullOrEmpty())
                optStdout = true;
            else
                fileWriter = File.CreateText(optPath);

            var optLineNums = options["--linenums"].IsTrue;
            var optDelay = ParseMinMaxInt(options["--delay"].ToString());
            var optWidth = ParseMinMaxInt(options["--width"].ToString());
            var optSize = TryParseSize(options["--size"].ToString());
            var optLines = TryParseInt(options["--lines"].ToString());
            var optPattern = options["--pattern"].ToString();
            var optEol = options["--eol"].ToString();

            var csb = new CharSpanBuilder(optWidth.max + 2); // room for \r\n

            for (var (written, lineNum) = (0L, 0); ; ++lineNum)
            {
                if (optLines != null && lineNum >= optLines)
                    break;
                if (optSize != null && written >= optSize)
                    break;

                csb.Clear();
                var width = Random.Shared.Next(optWidth.min, optWidth.max+1);

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
                                csb.Append((char)(group%26 + 'a'));
                            }
                            else
                                csb.Append((char)(digit + '0'));
                        }
                        break;

                    case "loggy":
                        // TODO: output log-looking stuff (or maybe just use that go tool 'flog'...)
                        // ALSO: maybe disable width constraints? will ruin json matchers otherwise
                        throw new NotImplementedException();

                    case "unity":
                        // TODO: output unity-log-looking stuff (probably reprocess an existing unity log copied alongside the exe..)
                        // ALSO: maybe disable width constraints? will ruin unity pattern matchers otherwise
                        throw new NotImplementedException();

                    default:
                        throw new DocoptInputErrorException($"Invalid --pattern option `{optPattern}`");
                }

                switch (optEol)
                {
                    case "crlf":
                        csb.Append("\r\n");
                        break;
                    case "lf":
                        csb.Append('\n');
                        break;
                    case "mix":
                        csb.Append(Random.Shared.Next() % 2 == 0 ? "\r\n" : "\n");
                        break;
                    default:
                        throw new DocoptInputErrorException($"Invalid --eol option `{optEol}`");
                }

                var span = csb.Span.Truncate(width);
                fileWriter?.Write(span);
                written += span.Length;

                if (optStdout)
                {
                    var (chars, count) = csb.Chars;
                    Console.Write(chars, 0, count);
                }

                if (optDelay.min != 0 || optDelay.max != 0)
                {
                    var delay = Random.Shared.Next(optDelay.min, optDelay.max+1);
                    Thread.Sleep(delay);
                }
            }

            return CliExitCode.Success;
        }
        finally
        {
            fileWriter?.Dispose();
        }
    }
}

static class Extensions
{
    public static Span<T> Truncate<T>(this Span<T> span, int length) =>
        span[..Math.Min(length, span.Length)];
    public static ReadOnlySpan<T> Truncate<T>(this ReadOnlySpan<T> span, int length) =>
        span[..Math.Min(length, span.Length)];
}
