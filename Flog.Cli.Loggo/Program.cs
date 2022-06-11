using DocoptNet;
using OkTools.Core;

public static partial class Program
{
    const string k_programName = "loggo";
    const string k_docName = $"{k_programName}, the logfile generator";
    const string k_docVersion = "0.1";
    const string k_docUsage = $@"{k_docName}

Usage:
  {k_programName}  [options] [DESTINATION]
  {k_programName}  --version

Options:
  --stdout            Mirror to stdout as well (defaults to true if PATH not specified)
  --line-nums         Prefix each line with a line number
  --delay DELAY       Delay between each line being added to the log in ms [default: 1,5]
  --width WIDTH       Width of generated lines [default: 1,200]
  --size SIZE         Stop generating after total log output gets to this size (can postfix SIZE with kb mb gb)
  --lines LINES       Stop generating after LINES lines
  --pattern PATTERN   Pattern to use for generated lines (one of: lorem, nums, loggy, PATH) [default: nums]
  --eol EOL           End of line character to use (one of: crlf, lf, mix) [default: lf]
  --overwrite         Overwrite existing log file if it already exists
  --delete-on-exit    Delete log file when program exits for any reason
  --intra-line-delay  Use some of the time from DELAY to delay randomly in the middle of writing each line. This is
                      useful for testing that a tail-follow utility can handle partially written lines.

Notes:
  * DELAY and WIDTH can be ""min,max"" entries separated by a comma (for example `1MB,50GB`). Both min and max will be
    the same if there is only one entry.
  * PATTERN can be the PATH to a file, which would be used as the source material for the generator. If SIZE and/or
    LINES are also specified, then the file at PATH will be repeated as needed until the SIZE/LINES conditions are met.
  * WIDTH is ignored if a file path is specified for PATTERN

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
        var optPath = options["DESTINATION"].ToString();

        // ReSharper disable AccessToModifiedClosure
        void Dispose()
        {
            if (fileWriter == null)
                return;

            fileWriter.Dispose();
            if (options["--delete-on-exit"].IsTrue)
                File.Delete(optPath);
        }
        // ReSharper restore AccessToModifiedClosure

        Console.CancelKeyPress += (_, _) => Dispose();

        try
        {
            var optStdout = options["--stdout"].IsTrue;

            if (optPath.IsNullOrEmpty())
                optStdout = true;
            else
            {
                optPath = Path.GetFullPath(optPath);
                fileWriter = new StreamWriter(File.Open(optPath,
                    options["--overwrite"].IsTrue ? FileMode.Create : FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete));
            }

            var optLineNums = options["--line-nums"].IsTrue;
            var optDelay = ParseMinMaxInt(options["--delay"].ToString());
            var optWidth = ParseMinMaxInt(options["--width"].ToString());
            var optSize = TryParseSize(options["--size"].ToString());
            var optLines = TryParseInt(options["--lines"].ToString());
            var optPattern = options["--pattern"].ToString();
            var optEol = options["--eol"].ToString();
            var optIntraLineDelay = options["--intra-line-delay"].IsTrue;

            var csb = new CharSpanBuilder(optWidth.max + 2); // room for \r\n

            string[]? patternLines = null;
            if (File.Exists(optPattern))
            {
                patternLines = File.ReadAllLines(optPattern);
                var maxWidth = patternLines.Max(l => l.Length) + 50;
                optWidth = (maxWidth, maxWidth);
            }

            for (var (written, lineNum) = (0L, 0); ; ++lineNum)
            {
                if (optLines != null && lineNum >= optLines)
                    break;
                if (optSize != null && written >= optSize)
                    break;
                if (optLines == null && optSize == null && patternLines != null && lineNum == patternLines.Length)
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

                    default:
                        if (patternLines == null)
                            throw new DocoptInputErrorException($"Invalid --pattern option `{optPattern}`");
                        csb.Append(patternLines[lineNum % patternLines.Length]);
                        break;
                }

                var eol = optEol switch
                {
                    "crlf" => "\r\n",
                    "lf"   => "\n",
                    "mix"  => Random.Shared.Next() % 2 == 0 ? "\r\n" : "\n",
                    _      => throw new DocoptInputErrorException($"Invalid --eol option `{optEol}`")
                };

                csb.Length = Math.Min(csb.Length, width);
                csb.Append(eol);

                var delay = Random.Shared.Next(optDelay.min, optDelay.max+1);
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
}
