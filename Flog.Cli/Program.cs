using DocoptNet;
using NiceIO;
using OkTools.Core;

// TODO: this is temporary until remove Console.* calls
#pragma warning disable RS0030

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
        screen.OutShowCursor(false);

        var options = new Options();
        var scrollView = new ScrollView(screen, options, path); // TODO: remove path, start up threaded reader, have main thread feed view(s) with chunks from reader
        var promptView = new PromptView(screen);
        var inPrompt = false;

        void UpdateLayout()
        {
            var bottom = screen.Size.Height;

            if (inPrompt)
            {
                screen.OutShowCursor(false);
                scrollView.SetBounds(screen.Size.Width, 0, bottom - 1);
                promptView.SetBounds(screen.Size.Width, bottom - 1);
                screen.OutShowCursor(true);
            }
            else
            {
                screen.OutShowCursor(false);
                scrollView.SetBounds(screen.Size.Width, 0, bottom);
            }
        }

        UpdateLayout();

        void TogglePrompt()
        {
            inPrompt = !inPrompt;
            UpdateLayout();
        }

        var events = new List<IEvent>();
        for (;;)
        {
            events.Clear();
            screen.GetEvents(events);

            // process global events from the batch first

            foreach (var evt in events)
            {
                switch (evt)
                {
                    case SignalEvent signalEvt:
                        return (signalEvt.Signal switch
                        {
                            TerminalSignal.Close     /*SIGHUP*/  => UnixSignal.Lost,
                            TerminalSignal.Interrupt /*SIGINT*/  => UnixSignal.KeyboardInterrupt,
                            TerminalSignal.Quit      /*SIGQUIT*/ => UnixSignal.KeyboardQuit,
                            TerminalSignal.Terminate /*SIGTERM*/ => UnixSignal.Terminate,
                            _ => (UnixSignal)0
                        }).AsCliExitCode();

                    case CharEvent { Char: 'c', Alt: false, Ctrl: true }:
                        return UnixSignal.KeyboardInterrupt.AsCliExitCode();

                    case ResizeEvent:
                        UpdateLayout();
                        break;
                }
            }

            // now ordinary events

            foreach (var evt in events)
            {
                if (inPrompt)
                {
                    void PreservePrompt(Action scrollAction)
                    {
                        screen.OutSaveCursorPos();
                        screen.OutShowCursor(false);
                        scrollAction();
                        screen.OutRestoreCursorPos();
                        screen.OutShowCursor(true);
                    }

                    switch (evt)
                    {
                        case KeyEvent { Key: ConsoleKey.UpArrow, NoModifiers: true }:
                            PreservePrompt(() => scrollView.ScrollDown());
                            break;
                        case KeyEvent { Key: ConsoleKey.DownArrow, NoModifiers: true }:
                            PreservePrompt(() => scrollView.ScrollUp());
                            break;

                        case KeyEvent { Key: ConsoleKey.PageUp, NoModifiers: true }:
                            PreservePrompt(() => scrollView.ScrollPageDown());
                            break;
                        case KeyEvent { Key: ConsoleKey.PageDown, NoModifiers: true }:
                            PreservePrompt(() => scrollView.ScrollPageUp());
                            break;

                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                            TogglePrompt();
                            break;

                        default:
                            promptView.HandleEvent(evt);
                            break;
                    }
                }
                else
                {
                    switch (evt)
                    {
                        case KeyEvent { Key: ConsoleKey.Home, NoModifiers: true }:
                            scrollView.ScrollToTop();
                            break;
                        case KeyEvent { Key: ConsoleKey.End, NoModifiers: true }:
                            scrollView.ScrollToBottom();
                            break;

                        case KeyEvent { Key: ConsoleKey.UpArrow, NoModifiers: true }:
                        case CharEvent { Char: 'k', NoModifiers: true }:
                            scrollView.ScrollDown();
                            break;
                        case KeyEvent { Key: ConsoleKey.DownArrow, NoModifiers: true }:
                        case CharEvent { Char: 'j', NoModifiers: true }:
                            scrollView.ScrollUp();
                            break;

                        case CharEvent { Char: 'K', NoModifiers: true }:
                            scrollView.ScrollHalfPageDown();
                            break;
                        case CharEvent { Char: 'J', NoModifiers: true }:
                            scrollView.ScrollHalfPageUp();
                            break;

                        case KeyEvent { Key: ConsoleKey.LeftArrow, NoModifiers: true }:
                        case CharEvent { Char: 'h', NoModifiers: true }:
                            scrollView.ScrollRight();
                            break;
                        case KeyEvent { Key: ConsoleKey.RightArrow, NoModifiers: true }:
                        case CharEvent { Char: 'l', NoModifiers: true }:
                            scrollView.ScrollLeft();
                            break;

                        case CharEvent { Char: 'H', NoModifiers: true }:
                            scrollView.ScrollToX(0);
                            break;

                        case KeyEvent { Key: ConsoleKey.PageUp, NoModifiers: true }:
                            scrollView.ScrollPageDown();
                            break;
                        case KeyEvent { Key: ConsoleKey.PageDown, NoModifiers: true }:
                            scrollView.ScrollPageUp();
                            break;

                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                        case CharEvent { Char: 'd', Alt: false, Ctrl: true }:
                        case CharEvent { Char: 'q', NoModifiers: true }:
                            return CliExitCode.Success;

                        case CharEvent { Char: ':', NoModifiers: true }:
                            TogglePrompt();
                            break;
                    }
                }
            }
        }
    }
}
