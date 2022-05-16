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
using Vezel.Cathode.Text.Control;
using Term = Vezel.Cathode.Terminal;
#pragma warning disable RS0030 // TODO: remove me
// TODO: if this works out ok, add Vezel.Cathode.Analyzers nuget package too...though do I need it? getting RS0030 above without it being in the project
#else
#pragma warning disable RS0030
#endif
#if SPECTRE
using Spectre.Console;
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
        var result = FlogItAsync(path);
        result.Wait();
        return result.Result;
    }

    static async Task<CliExitCode> FlogItAsync(NPath path)
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

        // $$$ REPLACE
        Console.CancelKeyPress += (_, _) => Shutdown();

        // TODO: react to resize events

        var x = 0;
        var y = 0;

        void Refresh(int beginScreenY, int endScreenY)
        {
            cb.SaveCursorState();
            cb.SetCursorVisibility(false);

            cb.MoveCursorTo(0, beginScreenY);
            for (var i = beginScreenY; i < endScreenY; ++i)
            {
                cb.ClearLine();
                cb.PrintLine(text[i+y][x..]);
            }

            cb.RestoreCursorState();
        }
        Refresh(0, Term.Size.Height);

        Out(cb);

        Term.EnableRawMode();

        try
        {
            for (;;)
            {
                var keys = new byte[10];
                Term.Read(keys);
                if (keys[0] == 0x1b) // ESC
                {
                    await Term.ReadAsync(keys, new CancellationTokenSource(15).Token);
                    if (keys[0] == '[')
                    {
                        await Term.ReadAsync(keys, new CancellationTokenSource(15).Token);
                        if (keys[0] == 'A')
                        {
                            cb.MoveBufferDown(1);
                            y = Math.Max(y-1, 0);
                            Refresh(0, 1);
                            Out(cb);
                        }
                        else if (keys[0] == 'B')
                        {
                            cb.MoveBufferUp(1);
                            y = Math.Min(y+1, text.Length);
                            Refresh(Term.Size.Height-1, Term.Size.Height);
                            Out(cb);
                        }
                    }

                    continue;
                }

                var key = Console.ReadKey(true);
                if (key.KeyChar == 'q')
                    return CliExitCode.Success;

                // TODO: shift-j/k/down/up should do half page

                if (key.KeyChar == 'j' || key.Key == ConsoleKey.DownArrow)
                {
                    cb.MoveBufferUp(1);
                    y = Math.Min(y+1, text.Length);
                    Refresh(Term.Size.Height-1, Term.Size.Height);
                    Out(cb);
                }
                else if (key.KeyChar == 'k' || key.Key == ConsoleKey.UpArrow)
                {
                    cb.MoveBufferDown(1);
                    y = Math.Max(y-1, 0);
                    Refresh(0, 1);
                    Out(cb);
                }
                else if (key.Key == ConsoleKey.PageDown)
                {
                    y = Math.Min(y+Term.Size.Height, text.Length);
                    Refresh(0, Term.Size.Height);
                    Out(cb);
                }
                else if (key.Key == ConsoleKey.PageUp)
                {
                    y = Math.Max(y-Term.Size.Height, 0);
                    Refresh(0, Term.Size.Height);
                    Out(cb);
                }
            }
        }
        finally
        {
            Shutdown();
        }
        //return CliExitCode.Success;
        #endif

        #if SPECTRE

        if (!AnsiConsole.Profile.Capabilities.Interactive)
            throw new CliExitException($"{k_programName} requires an interactive console", CliExitCode.ErrorUsage);
        if (!AnsiConsole.Profile.Capabilities.Ansi)
            throw new CliExitException($"{k_programName} requires an ANSI console", CliExitCode.ErrorUsage);

        AlternateScreen(true);
        AnsiConsole.Cursor.SetPosition(0, 0);

        void Shutdown()
        {
            AlternateScreen(false);
        }

        // $$$ REPLACE
        Console.CancelKeyPress += (_, _) => Shutdown();

        // TODO: react to resize events

        var x = 0;
        var y = 0;

        void Refresh(int beginScreenY, int endScreenY)
        {
            //cb.SaveCursorState();
            //cb.SetCursorVisibility(false);

            AnsiConsole.Cursor.SetPosition(0, beginScreenY);
            for (var i = beginScreenY; i < endScreenY; ++i)
            {
                //AnsiConsole.ClearLine();
                AnsiConsole.WriteLine(text[i+y][x..]);
            }

            //cb.RestoreCursorState();
        }
        Refresh(0, AnsiConsole.Console.Profile.Height);

        Out(cb);

        Term.EnableRawMode();

        try
        {
            for (;;)
            {
                var keys = new byte[1];
                Term.Read(keys);
                if (keys[0] == 0x1b) // ESC
                {
                    await Term.ReadAsync(keys, new CancellationTokenSource(15).Token);
                    if (keys[0] == '[')
                    {
                        await Term.ReadAsync(keys, new CancellationTokenSource(15).Token);
                        if (keys[0] == 'A')
                        {
                            cb.MoveBufferDown(1);
                            y = Math.Max(y-1, 0);
                            Refresh(0, 1);
                            Out(cb);
                        }
                        else if (keys[0] == 'B')
                        {
                            cb.MoveBufferUp(1);
                            y = Math.Min(y+1, text.Length);
                            Refresh(Term.Size.Height-1, Term.Size.Height);
                            Out(cb);
                        }
                    }

                    continue;
                }

                var key = Console.ReadKey(true);
                if (key.KeyChar == 'q')
                    return CliExitCode.Success;

                // TODO: shift-j/k/down/up should do half page

                if (key.KeyChar == 'j' || key.Key == ConsoleKey.DownArrow)
                {
                    cb.MoveBufferUp(1);
                    y = Math.Min(y+1, text.Length);
                    Refresh(Term.Size.Height-1, Term.Size.Height);
                    Out(cb);
                }
                else if (key.KeyChar == 'k' || key.Key == ConsoleKey.UpArrow)
                {
                    cb.MoveBufferDown(1);
                    y = Math.Max(y-1, 0);
                    Refresh(0, 1);
                    Out(cb);
                }
                else if (key.Key == ConsoleKey.PageDown)
                {
                    y = Math.Min(y+Term.Size.Height, text.Length);
                    Refresh(0, Term.Size.Height);
                    Out(cb);
                }
                else if (key.Key == ConsoleKey.PageUp)
                {
                    y = Math.Max(y-Term.Size.Height, 0);
                    Refresh(0, Term.Size.Height);
                    Out(cb);
                }
            }
        }
        finally
        {
            Shutdown();
        }
        //return CliExitCode.Success;
        #endif
    }

    #if CATHODE

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

    #endif

    #if SPECTRE

    static void AlternateScreen(bool alternate)
    {
        // oh well..
        if (!AnsiConsole.Profile.Capabilities.AlternateBuffer)
            return; // TODO: maybe need to clear the screen too?

        if (alternate)
            AnsiConsole.Write("\u001b[?1049h\u001b[H");
        else
            AnsiConsole.Write("\u001b[?1049l");
    }

    #endif
}
