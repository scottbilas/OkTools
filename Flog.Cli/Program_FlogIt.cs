using NiceIO;
using OkTools.Core;

public static partial class Program
{
    static async Task<CliExitCode> FlogIt(NPath path)
    {
        using var screen = new Screen();
        screen.OutShowCursor(false);

        var scrollViews = new List<TextView>();
        scrollViews.Add(new TextView(screen, new StreamLogSource(path))); // TODO: remove path, start up threaded reader, have main thread feed view(s) with chunks from reader

        var scrollView = scrollViews[0];
        var promptView = new InputView(screen);
        var inPrompt = false;

        // TODO: scrolling in a later view should also (optionally) update views in base views

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

        var events = new List<ITerminalEvent>();
        for (;;)
        {
            events.Clear();
            await screen.GetEvents(events);

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

                        case KeyEvent { Key: ConsoleKey.Enter, NoModifiers: true }:
                        case CharEvent { Char: ':', NoModifiers: true }:
                            TogglePrompt();
                            break;
                    }
                }
            }

            // finally any updated filters

            foreach (var filterEvt in events.OfType<FilterUpdatedEvent>())
            {

            }
        }
    }
}
