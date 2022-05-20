using DocoptNet;
using NiceIO;
using OkTools.Core;

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
            Console.Error.WriteLine(x.Message);
            return (int)CliExitCode.ErrorUsage;
        }
        catch (DocoptExitException x)
        {
            // TODO: wrap help to terminal width. can probably get away with a simple parser aimed just at reflowing
            // aligned/indented content, with some knowledge of the dash-prefixed options table..

            Console.WriteLine(DocoptUtility.Reflow(x.Message, Console.WindowWidth));
            return (int)CliExitCode.Help;
        }
        catch (TerminalNotInteractiveException)
        {
            Console.Error.WriteLine("this app requires an interactive terminal");
            return (int)CliExitCode.ErrorUsage;
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
        using var screen = new Screen();

        var options = new Options();
        var view = new View(screen.Terminal, options, path); // TODO: remove path, start up threaded reader, have main thread feed view(s) with chunks from reader
        screen.Terminal.Resized += _ => view.Resized();

        view.Refresh();

        for (;;)
        {
            // TODO: shift-j/k/down/up should do half page

            switch (screen.TryGetEvent())
            {
                // TODO: need to ensure these aren't modified..

                case KeyEvent { Key.Key: ConsoleKey.Home, Key.Modifiers: 0 }:
                    view.ScrollToBegin();
                    break;
                case KeyEvent { Key.Key: ConsoleKey.End, Key.Modifiers: 0 }:
                    view.ScrollToEnd();
                    break;

                case KeyEvent { Key.Key: ConsoleKey.UpArrow, Key.Modifiers: 0 }:
                case KeyEvent { Key.KeyChar: 'k', Key.Modifiers: 0 }:
                    view.ScrollDown();
                    break;
                case KeyEvent { Key.Key: ConsoleKey.DownArrow, Key.Modifiers: 0 }:
                case KeyEvent { Key.KeyChar: 'j', Key.Modifiers: 0 }:
                    view.ScrollUp();
                    break;

                case KeyEvent { Key.Key: ConsoleKey.LeftArrow, Key.Modifiers: 0 }:
                case KeyEvent { Key.KeyChar: 'l', Key.Modifiers: 0 }:
                    view.ScrollRight();
                    break;
                case KeyEvent { Key.Key: ConsoleKey.RightArrow, Key.Modifiers: 0 }:
                case KeyEvent { Key.KeyChar: 'h', Key.Modifiers: 0 }:
                    view.ScrollLeft();
                    break;

                case KeyEvent { Key.Key: ConsoleKey.PageUp, Key.Modifiers: 0 }:
                    view.ScrollPageDown();
                    break;
                case KeyEvent { Key.Key: ConsoleKey.PageDown, Key.Modifiers: 0 }:
                    view.ScrollPageUp();
                    break;

                case KeyEvent { Key.KeyChar: 'c', Key.Modifiers: ConsoleModifiers.Control }:
                    return UnixSignal.KeyboardInterrupt.AsCliExitCode();

                case KeyEvent { Key.KeyChar: 'q', Key.Modifiers: 0 }:
                case KeyEvent { Key.KeyChar: 'd', Key.Modifiers: ConsoleModifiers.Control }:
                    return CliExitCode.Success;

                case null:
                    Thread.Sleep(10);
                    break;
            }
        }
    }
}

static class ConsoleKeyInfoExtensions
{
    public static bool HasCtrl (this ConsoleKeyInfo @this) => @this.Modifiers.HasCtrl();
    public static bool HasAlt  (this ConsoleKeyInfo @this) => @this.Modifiers.HasAlt();
    public static bool HasShift(this ConsoleKeyInfo @this) => @this.Modifiers.HasShift();
}

static class ConsoleModifiersExtensions
{
    public static bool HasCtrl (this ConsoleModifiers @this) => (@this & ConsoleModifiers.Control) != 0;
    public static bool HasAlt  (this ConsoleModifiers @this) => (@this & ConsoleModifiers.Alt    ) != 0;
    public static bool HasShift(this ConsoleModifiers @this) => (@this & ConsoleModifiers.Shift  ) != 0;
}
