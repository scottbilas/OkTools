using System.Text;
using System.Text.RegularExpressions;
using DocoptNet;
using NiceIO;
using OkTools.Core;
using Vezel.Cathode.Text.Control;
using Term = Vezel.Cathode.Terminal;
#pragma warning disable RS0030 // TODO: remove me

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

    class Options
    {
        public readonly int TabWidth = 4;
    }

    class View
    {
        static readonly string k_empty = new(' ', 100);

        readonly Options _options;
        readonly string[] _lines;
        readonly string?[] _processedLines;
        readonly ControlBuilder _cb = new();
        readonly StringBuilder _sb = new();

        int _x, _y;

        public View(Options options, NPath path)
        {
            _options = options;
            _lines = path.ReadAllLines();
            _processedLines = new string?[_lines.Length];
        }

        public void Refresh()
        {
            Refresh(0, Term.Size.Height);
        }

        // https://github.com/chalk/ansi-regex/blob/main/index.js
        static readonly Regex s_ansiEscapes = new(
            "[\\u001B\\u009B][[\\]()#;?]*(?:(?:(?:(?:;[-a-zA-Z\\d\\/#&.:=?%@~_]+)*|[a-zA-Z\\d]+(?:;[-a-zA-Z\\d\\/#&.:=?%@~_]*)*)?\\u0007)|" +
            "(?:(?:\\d{1,4}(?:;\\d{0,4})*)?[\\dA-PR-TZcf-nq-uy=><~]))");

        string ProcessForDisplay(string line)
        {
            var hasEscapeSeqs = false;
            var hasControlChars = false;

            for (var i = 0; i < line.Length; ++i)
            {
                var c = line[i];
                if (c == 0x1b)
                    hasEscapeSeqs = true;
                else if (c <= 0x1f || c == 0x7f)
                    hasControlChars = true;
            }

            if (!hasEscapeSeqs && !hasControlChars)
                return line;

            if (hasEscapeSeqs)
                line = s_ansiEscapes.Replace(line, "");

            if (hasControlChars)
            {
                _sb.Clear();
                for (var i = 0; i < line.Length; ++i)
                {
                    var c = line[i];
                    if (c == '\t')
                    {
                        var indent = _sb.Length % _options.TabWidth;
                        // $$$$
                    }
                    else if (c <= 0x1f || c == 0x7f)
                    {
                        // skip
                    }
                    else
                        _sb.Append(c);
                }
            }
        }

        void Refresh(int beginScreenY, int endScreenY)
        {
            _cb.SaveCursorState();
            _cb.SetCursorVisibility(false);

            var endPrintY = Math.Min(endScreenY, _lines.Length - _y);

            for (var i = beginScreenY; i < endPrintY; ++i)
            {
                _cb.MoveCursorTo(0, i);

                var line = _processedLines[i + _y] ?? (_processedLines[i + _y] = ProcessForDisplay(_lines[i + _y]));

                var len = Math.Min(line.Length - _x, Term.Size.Width);
                if (len > 0)
                {
                    _cb.Print(line.AsSpan(_x, len));

                    for (var remain = Term.Size.Width - len; remain > 0; )
                    {
                        var write = Math.Min(remain, k_empty.Length);
                        _cb.Print(k_empty.AsSpan(0, write));
                        remain -= write;
                    }
                }
                else
                    _cb.ClearLine();
            }

            for (var i = endPrintY; i < endScreenY; ++i)
            {
                _cb.MoveCursorTo(0, i);
                _cb.ClearLine();
            }

            _cb.RestoreCursorState();

            Term.Out(_cb);
            _cb.Clear(Term.Size.Width * Term.Size.Height * 2);
        }

        int ConstrainY(int testY)
        {
            return Math.Max(Math.Min(testY, _lines.Length - 1), 0);
        }

        bool ScrollToY(int y)
        {
            y = ConstrainY(y);

            var (beginScreenY, endScreenY) = (0, Term.Size.Height);
            var offset = y - _y;

            switch (offset)
            {
                case > 0 when offset < Term.Size.Height:
                    _cb.MoveBufferUp(offset);
                    beginScreenY = Term.Size.Height - offset;
                    break;
                case < 0 when -offset < Term.Size.Height:
                    _cb.MoveBufferDown(-offset);
                    endScreenY = -offset;
                    break;
                case 0:
                    return false;
            }

            _y = y;
            Refresh(beginScreenY, endScreenY);
            return true;
        }

        void ScrollY(int offset) => ScrollToY(_y + offset);

        void ScrollToX(int x)
        {
            if (x < 0)
                x = 0;

            if (_x == x)
                return;

            _x = x;
            Refresh();
        }

        void ScrollX(int offset) => ScrollToX(_x + offset);

        public void ScrollDown() => ScrollY(1);
        public void ScrollUp() => ScrollY(-1);
        public void ScrollPageDown() => ScrollY(Term.Size.Height);
        public void ScrollPageUp() => ScrollY(-Term.Size.Height);

        public void ScrollLeft() => ScrollX(10);
        public void ScrollRight() => ScrollX(-10);

        public void ScrollToBegin()
        {
            if (!ScrollToY(0))
                ScrollToX(0);
        }

        public void ScrollToEnd()
        {
            var target = _lines.Length - (Term.Size.Height / 2);
            ScrollToY(_y < target ? target : _lines.Length);
        }

        public void Resized()
        {
            _y = ConstrainY(_y);
            Refresh();
        }
    }

    static CliExitCode FlogIt(NPath path)
    {
        Term.EnableRawMode();
        Term.Out(new ControlBuilder().SetScreenBuffer(ScreenBuffer.Alternate));
        void Shutdown() => Term.Out(new ControlBuilder().SetScreenBuffer(ScreenBuffer.Main));
        Term.Signaled += _ => Shutdown();

        var options = new Options();

        var view = new View(options, path);
        view.Refresh();

        Term.Resized += newSize =>
        {
            view.Resized();
        };

        try
        {
            for (;;)
            {
                // TODO: shift-j/k/down/up should do half page

                var keys = new byte[10];
                Term.Read(keys);
                if (keys[0] == 0x1b) // ESC
                {
                    Term.Read(keys, new CancellationTokenSource(15).Token);
                    if (keys[0] == '[')
                    {
                        Term.Read(keys, new CancellationTokenSource(15).Token);
                        if (keys[0] == 'A')
                            view.ScrollUp();
                        else if (keys[0] == 'B')
                            view.ScrollDown();
                        else if (keys[0] == 'C')
                            view.ScrollLeft();
                        else if (keys[0] == 'D')
                            view.ScrollRight();
                        else if (keys[0] == 'H')
                            view.ScrollToBegin();
                        else if (keys[0] == 'F')
                            view.ScrollToEnd();
                        else if (keys[0] == '5')
                        {
                            Term.Read(keys);
                            if (keys[0] == '~')
                                view.ScrollPageUp();
                        }
                        else if (keys[0] == '6')
                        {
                            Term.Read(keys);
                            if (keys[0] == '~')
                                view.ScrollPageDown();
                        }
                    }
                }
                else if (keys[0] == 3) // ctrl-c
                    return UnixSignal.KeyboardInterrupt.AsCliExitCode();
                else if (keys[0] == 4) // ctrl-d
                    return CliExitCode.Success;
                else if (keys[0] == 'q')
                    return CliExitCode.Success;
                else if (keys[0] == 'j')
                    view.ScrollDown();
                else if (keys[0] == 'k')
                    view.ScrollUp();
                else if (keys[0] == 'l')
                    view.ScrollLeft();
                else if (keys[0] == 'h')
                    view.ScrollRight();
            }
        }
        finally
        {
            Shutdown();
        }

        //return CliExitCode.Success;
    }
}
