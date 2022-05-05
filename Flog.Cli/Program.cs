#define CATHODE

using DocoptNet;
using NiceIO;
using OkTools.Core;
#if GUICS
// crazy feature set, especially the input handling
// but slow as FUCK
using Terminal.Gui;
#endif
#if CATHODE
// zero support for keyboard handling..may be able to do my own hotkeys with multiple-Read() but bleh big pain
// and can plug in a ReadLine type thing from one of these:
//  * https://github.com/tonerdo/readline
//  * https://github.com/mono/LineEditor
using Vezel.Cathode;
using Term = Vezel.Cathode.Terminal;
#pragma warning disable RS0030 // TODO: remove me
// TODO: if this works out ok, add Vezel.Cathode.Analyzers nuget package too...though do I need it? getting RS0030 above without it being in the project
#endif

class CliExitException : Exception
{
    public CliExitException(string message, CliExitCode code) : base(message) { Code = code; }
    public CliExitException(string message, Exception innerException, CliExitCode code) : base(message, innerException) { Code = code; }

    public readonly CliExitCode Code;
}

public static class Program
{
    const string k_programName = "okflog";
    const string k_docName = $"{k_programName}, the logfile analyzer";
    const string k_docVersion = "0.1";

    // TODO: add --config prefix before command

    const string k_docUsage = $@"{k_docName}

Usage:
  {k_programName}  [options] PATH
  {k_programName}  --version

Options:
  --debug  Enable extra debug features
";

    public static int Main(string[] args)
    {
        var debugMode = false;

        try
        {
            if (args.Length == 0)
                throw new DocoptExitException(k_docUsage);

            var opt = new Docopt().Apply(k_docUsage, args, version: $"{k_docName} {k_docVersion}", help: false);
            debugMode = opt["--debug"].IsTrue;

            return (int)FlogIt(opt["PATH"].ToString().ToNPath().FileMustExist());
        }
        catch (DocoptInputErrorException x)
        {
            Console.Error.WriteLine("bad command line: " + args.StringJoin(' '));
            Console.WriteLine(x.Message);
            return (int)CliExitCode.ErrorUsage;
        }
        catch (DocoptExitException x)
        {
            // TODO: wrap help to terminal width. can probably get away with a simple parser aimed just at reflowing
            // aligned/indented content, with some knowledge of the dash-prefixed options table..

            Console.WriteLine(DocoptUtility.Reflow(x.Message, Console.WindowWidth));
            return (int)CliExitCode.Help;
        }
        catch (Exception x)
        {
            if (x is CliExitException clix)
            {
                if (x.InnerException != null)
                {
                    Console.Error.WriteLine(x.Message);
                    if (debugMode)
                    {
                        Console.Error.WriteLine();
                        Console.Error.WriteLine(x.InnerException);
                    }
                }
                else
                    Console.Error.WriteLine(x);

                return (int)clix.Code;
            }

            Console.Error.WriteLine("Internal error!");
            Console.Error.WriteLine();
            Console.Error.WriteLine(x);
            return (int)CliExitCode.ErrorSoftware;
        }
    }

    static CliExitCode FlogIt(NPath path)
    {
        var text = path.ReadAllLines(); // TODO: tabs->spaces

        #if GUICS
        try
        {
            Application.Init();
        }
        catch (Exception exception)
        {
            throw new CliExitException($"{k_programName} requires an interactive console", exception, CliExitCode.ErrorUsage);
        }

        var x = 0;
        var y = 0;

        var main = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        void Refresh()
        {
            main.Text = string.Join("\n", text[y..].Select(l => l[Math.Min(l.Length, x)..]));
        }
        Refresh();

        Application.Top.KeyDown += args =>
        {
            switch (args.KeyEvent.Key)
            {
                case Key.q:
                    Application.RequestStop();
                    break;
                case Key.CursorUp:
                    y = Math.Max(0, y - 1);
                    Refresh();
                    break;
                case Key.CursorDown:
                    ++y;
                    Refresh();
                    break;
                case Key.CursorLeft:
                    x = Math.Max(0, x - 10);
                    Refresh();
                    break;
                case Key.CursorRight:
                    x += 10;
                    Refresh();
                    break;
            }
        };

        Application.Top.Add(main);

        Application.Run();
        return CliExitCode.Success;
        #endif

        #if CATHODE

        var cb = new ControlBuilder();
        cb
            .SetScreenBuffer(ScreenBuffer.Alternate)
            .MoveCursorTo(0, 0);

        void Shutdown()
        {
            Out(new ControlBuilder().SetScreenBuffer(ScreenBuffer.Main));
        }

        Console.CancelKeyPress += (_, _) => Shutdown();

        var x = 0;
        var y = 0;

        for (var i = 0; i < Term.Size.Height; ++i)
            cb.PrintLine(text[i+y][x..]);

        Out(cb);

        try
        {
            for (;;)
            {
                var key = Console.ReadKey(true);
                if (key.KeyChar == 'q')
                    return CliExitCode.Success;

                if (key.KeyChar == 'j' || key.Key == ConsoleKey.UpArrow)
                    Out(cb.MoveBufferUp(1));
                else if (key.KeyChar == 'k' || key.Key == ConsoleKey.DownArrow)
                    Out(cb.MoveBufferDown(1));

            }
        }
        finally
        {
            Shutdown();
        }
        //return CliExitCode.Success;
        #endif
    }

    static void Out(ControlBuilder cb)
    {
        Term.Out(cb);
        cb.Clear();
    }
    static void OutLine(ControlBuilder cb)
    {
        Term.OutLine(cb);
        cb.Clear();
    }
    static void Out(string text)
    {
        Term.Out(text);
    }
    static void OutLine(string text)
    {
        Term.OutLine(text);
    }
}
