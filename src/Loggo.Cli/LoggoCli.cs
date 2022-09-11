using System.Text.RegularExpressions;
using DocoptNet;

const string programVersion = "0.1";

var (exitCode, opt) = LoggoCliArguments.CreateParser().Parse(args, programVersion, LoggoCliArguments.Help, LoggoCliArguments.Usage);
if (exitCode != null)
    return (int)exitCode.Value;

StreamWriter? fileWriter = null;
var optPath = opt.ArgDestination!;

var rng = opt.OptRngSeed != null ? new Random(int.Parse(opt.OptRngSeed)) : new Random();

// ReSharper disable AccessToModifiedClosure
void Dispose()
{
    if (fileWriter == null)
        return;

    fileWriter.Dispose();
    if (opt.OptDeleteOnExit)
        File.Delete(optPath);
}
// ReSharper restore AccessToModifiedClosure

Console.CancelKeyPress += (_, _) => Dispose();

try
{
    var optStdout = opt.OptStdout;

    if (optPath.IsNullOrEmpty())
        optStdout = true;
    else
    {
        optPath = Path.GetFullPath(optPath);
        fileWriter = new StreamWriter(File.Open(optPath,
            opt.OptOverwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete));
    }

    var optDelay = ParseMinMaxInt(opt.OptDelay);
    var optWidth = ParseMinMaxInt(opt.OptWidth);
    var optSize = TryParseSize(opt.OptSize);
    var optLines = TryParseInt(opt.OptLines);

    string[]? patternLines = null;
    if (File.Exists(opt.OptPattern))
    {
        patternLines = File.ReadAllLines(opt.OptPattern);
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

        if (opt.OptLineNums)
        {
            csb.Append(lineNum);
            csb.Append(": ");
        }

        var minWidth = Math.Max(optWidth.min, csb.Length - 1); // always ensure we print the full line number and colon (minus space)
        var width = rng.Next(minWidth, optWidth.max + 1);

        switch (opt.OptPattern)
        {
            case "lorem":
                LoremIpsum.Fill(ref csb, rng);
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
                    throw new DocoptInputErrorException($"Invalid --pattern option `{opt.OptPattern}`");
                csb.Append(patternLines[lineNum % patternLines.Length]);
                break;
        }

        var eol = opt.OptEol switch
        {
            "crlf" => "\r\n",
            "lf" => "\n",
            "mix" => rng.Next() % 2 == 0 ? "\r\n" : "\n",
            _ => throw new DocoptInputErrorException($"Invalid --eol option `{opt.OptEol}`")
        };

        csb.Length = Math.Min(csb.Length, width);
        csb.Append(eol);

        var delay = rng.Next(optDelay.min, optDelay.max + 1);
        var chars = csb.Chars;

        while (chars.Count > 0)
        {
            var write = chars.Count;

            if (delay > 0 && opt.OptIntraLineDelay)
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

    return (int)CliExitCode.Success;
}
finally
{
    Dispose();
}

static (int min, int max) ParseMinMaxInt(string str)
{
    var split = str.Split(',', 2);
    var min = int.Parse(split[0]);
    var max = split.Length == 2 ? int.Parse(split[1]) : min;
    return (min, max);
}

static int? TryParseInt(string? str)
{
    if (str.IsNullOrEmpty())
        return null;

    return int.Parse(str!);
}

static long? TryParseSize(string? str)
{
    if (str.IsNullOrEmpty())
        return null;

    var match = Regex.Match(str!, @"(\d+)([kmg]b)?$", RegexOptions.IgnoreCase);
    if (!match.Success)
        throw new DocoptInputErrorException($"`{str}` is not a valid size");

    var multiplier = 1;
    if (match.Groups[2].Success)
    {
        multiplier *= match.Groups[2].Value[0] switch
        {
            'k' => 1024,
            'm' => 1024 * 1024,
            'g' => 1024 * 1024 * 1024,
            _ => throw new FormatException("Regex fail")
        };
    }

    return long.Parse(match.Groups[1].Value) * multiplier;
}
