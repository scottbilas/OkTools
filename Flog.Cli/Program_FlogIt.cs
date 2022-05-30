using NiceIO;
using OkTools.Core;

public static partial class Program
{
    static async Task<CliExitCode> FlogIt(NPath path)
    {
        using var screen = new Screen();
        screen.OutShowCursor(false);

        var filterViews = new List<TextView> { new(screen, new StreamLogSource(path)) };
        var filterPane = filterViews[0];
        var filteringPane = default(TextView);

        var commandPane = new InputView(screen);
        var commandMode = false;

        // TODO: scrolling in a later view should also (optionally) update views in base views

        void Pre()
        {
            screen.OutSaveCursorPos();
            screen.OutShowCursor(false);
        }

        void Post()
        {
            screen.OutRestoreCursorPos();
            screen.OutShowCursor(true);
        }

        void UpdateLayout()
        {
            var bottom = screen.Size.Height;

            if (commandMode)
            {
                screen.OutShowCursor(false);
                filterPane.SetBounds(screen.Size.Width, 0, bottom - 1);
                commandPane.SetBounds(screen.Size.Width, bottom - 1);
                screen.OutShowCursor(true);
            }
            else
            {
                screen.OutShowCursor(false);
                filterPane.SetBounds(screen.Size.Width, 0, bottom);
            }
        }

        UpdateLayout();

        void ToggleCommandMode()
        {
            commandMode = !commandMode;
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
                if (commandMode)
                {
                    switch (evt)
                    {
                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                            ToggleCommandMode();
                            break;

                        case FilterUpdatedEvent filterUpdatedEvt:

                            if (filteringPane == null)
                            {
                                filteringPane = new TextView(screen, new FilterLogSource(filterPane.LogSource));
                                // TODO: not loving this.
                                filteringPane.SetBounds(filterViews[0].Width, filterViews[0].Top, filterViews[0].Bottom);
                                filterPane = filteringPane;
                                filterViews.Add(filteringPane);
                            }

                            // TODO: avoid this "reaching in" cast stuff
                            filteringPane.LogSource.To<FilterLogSource>().SetFilter(filterUpdatedEvt.NewFilter);

                            // TODO: refresh should be automatic via channels
                            Pre();
                            filteringPane.Refresh();
                            Post();

                            break;

                        case FilterCommittedEvent:
                            filteringPane = null;
                            ToggleCommandMode();
                            break;

                        default:
                            if (!commandPane.HandleEvent(evt))
                            {
                                // allow some scrolling operations while command mode open
                                filterPane.HandleEvent(evt, Pre, Post);
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
                            filterPane.HandleEvent(evt);
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
