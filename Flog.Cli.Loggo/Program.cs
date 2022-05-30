using System.Text;
using DocoptNet;
using OkTools.Core;

public static class Program
{
    const string k_programName = "loggo";
    const string k_docName = $"{k_programName}, the logfile generator";
    const string k_docVersion = "0.1";
    const string k_docUsage = $@"{k_docName}

Usage:
  {k_programName}  [options] PATH
  {k_programName}  --version

Options:
  --speed MINMAX  Delay between each line being added to the log in ms [default: 1,5]
  --size MINMAX   Will stop generating after log gets to this size (can postfix SIZE with kb mb gb) [default: 100MB]
  --width MINMAX  Width of generated lines [default: 1,200]

Notes:
  MINMAX can be min and max entries separated by a comma (for example `1MB,50GB`). Both min and max will be the same if there is only one entry.
";

    static (float min, float max) ParseMinMax(string str)
    {

    }

    static (long min, long max) ParseMinMaxSize(string str)
    {

    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
                throw new DocoptExitException(k_docUsage);

            var opt = new Docopt().Apply(k_docUsage, args, version: $"{k_docName} {k_docVersion}", help: false);

            return (int)LoggIt(
                ParseMinMax(opt["--speed"].ToString()),
                ParseMinMaxSize(opt["--size"].ToString()),
                ParseMinMax(opt["--width"].ToString()),
                opt["PATH"].ToString());
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

    static CliExitCode LoggIt(
        (float min, float max) speed,
        (long min, long max) size,
        (float min, float max) width,
        string path)
    {
        using var writer = File.CreateText(path);
        var sb = new StringBuilder();

        var written = 0;


        return CliExitCode.Success;
    }
}
