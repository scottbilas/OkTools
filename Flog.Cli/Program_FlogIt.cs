using NiceIO;
using OkTools.Core;

public static partial class Program
{
    static async Task<CliExitCode> FlogIt(NPath path)
    {
        using var screen = new Screen();
        screen.OutShowCursor(false);

        var filterViews = new List<ScrollingTextView> { new(screen, new StreamLogSource(path)) };
        var currentFilterPane = filterViews[0];
        currentFilterPane.Enabled = true;
        var liveFilteringPane = default(ScrollingTextView);

        var statusPane = new StatusView(screen);
        //statusPane.SetLogName(path.FileName);
        //statusPane.Enabled = true;

        var commandPane = new InputView(screen);

        // TODO: scrolling in a later view should also (optionally) update views in base views

        void UpdateLayout(bool forceRedraw)
        {
            var bottom
                = screen.Size.Height
                + (commandPane.Enabled ? -1 : 0)
                + (statusPane.Enabled ? -1 : 0);

            currentFilterPane.SetBounds(screen.Size.Width, 0, bottom, true);
            if (statusPane.Enabled)
                statusPane.SetBounds(screen.Size.Width, bottom, ++bottom, true);
            if (commandPane.Enabled)
                commandPane.SetBounds(screen.Size.Width, bottom, ++bottom, true);
        }

        UpdateLayout(true);

        void ToggleCommandMode()
        {
            commandPane.Enabled = !commandPane.Enabled;
            UpdateLayout(false);
        }

        var events = new EventBuffer<ITerminalEvent>();
        for (;;)
        {
            events.Clear();
            await screen.GetEvents(events);

            // don't want cursor visible for any screen updates

            screen.OutShowCursor(false);

            // process global events from the batch first

            foreach (var evt in events)
            {
                switch (evt.Value)
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

                    case CharEvent { Char: 'l', Alt: false, Ctrl: true }:
                        UpdateLayout(true);
                        evt.Accept();
                        break;

                    case ResizeEvent:
                        UpdateLayout(false);
                        break;
                }
            }

            // now ordinary events

            foreach (var evt in events)
            {
                if (commandPane.Enabled)
                {
                    switch (evt.Value)
                    {
                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                            ToggleCommandMode();
                            evt.Accept();
                            break;

                        default:
                            if (commandPane.HandleEvent(evt.Value) || currentFilterPane.HandleEvent(evt.Value))
                                evt.Accept();
                            break;
                    }
                }
                else
                {
                    switch (evt.Value)
                    {
                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                        case CharEvent { Char: 'd', Alt: false, Ctrl: true }:
                        case CharEvent { Char: 'q', NoModifiers: true }:
                            return CliExitCode.Success;

                        case KeyEvent { Key: ConsoleKey.Enter, NoModifiers: true }:
                        case CharEvent { Char: ':', NoModifiers: true }:
                            ToggleCommandMode();
                            evt.Accept();
                            break;

                        default:
                            if (currentFilterPane.HandleEvent(evt.Value))
                                evt.Accept();
                            break;
                    }
                }
            }

            // finally any updated filters

            foreach (var evt in events)
            {
                switch (evt.Value)
                {
                    case FilterUpdatedEvent updatedEvt:
                        if (liveFilteringPane == null)
                        {
                            liveFilteringPane = new ScrollingTextView(screen, new FilterLogSource(currentFilterPane.LogSource)) { Enabled = true };
                            // TODO: not loving this.
                            liveFilteringPane.SetBounds(filterViews[0].Width, filterViews[0].Top, filterViews[0].Bottom, false);
                            currentFilterPane = liveFilteringPane;
                            filterViews.Last().Enabled = false;
                            filterViews.Add(liveFilteringPane);
                        }

                        // TODO: avoid this "reaching in" cast stuff
                        liveFilteringPane.LogSource.To<FilterLogSource>().SetFilter(updatedEvt.NewFilter);

                        // TODO: refresh should be automatic via channels
                        liveFilteringPane.Draw();

                        evt.Accept();
                        break;

                    case FilterCommittedEvent:
                        liveFilteringPane = null;
                        ToggleCommandMode();
                        evt.Accept();
                        break;
                }
            }

            // ensure we always have correct cursor after any events processed
            if (commandPane.Enabled)
                commandPane.UpdateCursor();
        }
    }
}
