using NiceIO;
using OkTools.Core;

public static partial class Program
{
    static async Task<CliExitCode> FlogIt(NPath path)
    {
        using var screen = new Screen();
        screen.OutShowCursor(false);

        var textViews = new List<TextView>();
        textViews.Add(new TextView(screen, new StreamLogSource(path))); // TODO: remove path, start up threaded reader, have main thread feed view(s) with chunks from reader
        var currentFilter = textViews[0];

        var command = new InputView(screen);
        var inCommand = false;

        // TODO: scrolling in a later view should also (optionally) update views in base views

        void UpdateLayout()
        {
            var bottom = screen.Size.Height;

            if (inCommand)
            {
                screen.OutShowCursor(false);
                currentFilter.SetBounds(screen.Size.Width, 0, bottom - 1);
                command.SetBounds(screen.Size.Width, bottom - 1);
                screen.OutShowCursor(true);
            }
            else
            {
                screen.OutShowCursor(false);
                currentFilter.SetBounds(screen.Size.Width, 0, bottom);
            }
        }

        UpdateLayout();

        void ToggleCommandMode()
        {
            inCommand = !inCommand;
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
                if (inCommand)
                {
                    switch (evt)
                    {
                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                            ToggleCommandMode();
                            break;

                        default:
                            if (!command.HandleEvent(evt))
                            {
                                currentFilter.HandleEvent(evt,
                                    // ReSharper disable AccessToDisposedClosure
                                    () =>
                                    {
                                        screen.OutSaveCursorPos();
                                        screen.OutShowCursor(false);
                                    },
                                    () =>
                                    {
                                        screen.OutRestoreCursorPos();
                                        screen.OutShowCursor(true);
                                    });
                                    // ReSharper restore AccessToDisposedClosure
                            }
                            break;
                    }
                }
                else
                {
                    switch (evt)
                    {
                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                        case CharEvent { Char: 'd', Alt: false, Ctrl: true }:
                        case CharEvent { Char: 'q', NoModifiers: true }:
                            return CliExitCode.Success;

                        case KeyEvent { Key: ConsoleKey.Enter, NoModifiers: true }:
                        case CharEvent { Char: ':', NoModifiers: true }:
                            ToggleCommandMode();
                            break;

                        default:
                            currentFilter.HandleEvent(evt);
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
