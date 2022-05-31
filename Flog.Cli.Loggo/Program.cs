using DocoptNet;
using OkTools.Core;

public static partial class Program
{
    const string k_programName = "loggo";
    const string k_docName = $"{k_programName}, the logfile generator";
    const string k_docVersion = "0.1";
    const string k_docUsage = $@"{k_docName}

Usage:
  {k_programName}  [options] PATH
  {k_programName}  --version

Options:
  --stdout           Mirror to stdout as well
  --speed MINMAX     Delay between each line being added to the log in ms [default: 1,5]
  --width MINMAX     Width of generated lines [default: 1,200]
  --size SIZE        Will stop generating after log gets to this size (can postfix SIZE with kb mb gb) [default: 100MB]
  --pattern PATTERN  Pattern to use for generated lines (one of: lorem, nums, loggy) [default: nums]
  --eol EOL          End of line character to use (one of: crlf, lf, mix) [default: lf]

Notes:
  MINMAX can be min and max entries separated by a comma (for example `1MB,50GB`). Both min and max will be the same if there is only one entry.
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
        using var writer = File.CreateText(options["PATH"].ToString());

        var stdout = options["--stdout"].IsTrue;
        var speed = ParseMinMaxFloat(options["--speed"].ToString());
        var width = ParseMinMaxInt(options["--width"].ToString());
        var size = ParseSize(options["--size"].ToString());
        var pattern = options["--pattern"].ToString();
        var eol = options["--eol"].ToString();

        var csb = new CharSpanBuilder(width.max + 2); // room for \r\n

        for (var (written, line) = (0L, 0); written < size; ++line)
        {
            csb.Clear();

            switch (pattern)
            {
                case "lorem":
                    throw new NotImplementedException();
                case "nums":
                    csb.Append(line);
                    break;
                case "loggy":
                    throw new NotImplementedException();
                default:
                    throw new DocoptInputErrorException($"Invalid --pattern option `{pattern}`");
            }

            switch (eol)
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
                    throw new DocoptInputErrorException($"Invalid --eol option `{eol}`");
            }

            writer.Write(csb.Span);
            written += csb.Span.Length;

            if (stdout)
            {
                var (chars, count) = csb.Chars;
                Console.Write(chars, 0, count);
            }

            var delay = Random.Shared.NextSingle() * (speed.max - speed.min) + speed.min;
            Thread.Sleep((int)(delay / 1000));
        }

        return CliExitCode.Success;
    }
}
