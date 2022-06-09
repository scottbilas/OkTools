using System.Threading.Channels;
using DocoptNet;
using OkTools.Core;

class FlogApp : IDisposable
{
    readonly Screen _screen = new();
    readonly ChannelReader<LogChange> _logFileReader;
    readonly CancellationTokenSource _logFileCancel = new();
    readonly LogFilterChain _logFilterChain;
    readonly LogFilterChainView _logFilterPane;
    readonly StatusView _statusPane;
    readonly InputView _commandPane, _editFilterPane;

    bool _editingExistingFilter;
    string _originalFilter = "";

    enum State
    {
        LogView,
        // TODO: LogSelect (activate with '>' and it does micro level work like bookmarking, clipping, etc.)
        InputCommand,
        InputEditFilter,
    }

    State _state = State.LogView;

    public FlogApp(IDictionary<string, ValueObject> options)
    {
        _screen.OutShowCursor(false);

        // tail

        var path = options["PATH"].ToString();
        var channel = Channel.CreateUnbounded<LogChange>();
        Task.Run(() => LogSource.TailFileAsync(path, channel.Writer, _logFileCancel.Token));

        // filter chain

        _logFileReader = channel.Reader;
        _logFilterChain = new LogFilterChain(_logFileReader);
        var passThruProcessor = new PassThruProcessor();
        _logFilterChain.Add(passThruProcessor);

        _logFilterPane = new LogFilterChainView(_screen) { Enabled = true };
        _logFilterPane.AddAndActivate(passThruProcessor);

        // status

        _statusPane = new StatusView(_screen) { Enabled = true };
        _statusPane.SetLogPath(path);

        // input

        _commandPane = new InputView(_screen) { Prompt = ":" };
        _editFilterPane = new InputView(_screen) { Prompt = "=" };
    }

    void IDisposable.Dispose()
    {
        _screen.Dispose();
        _logFileCancel.Cancel();
    }

    readonly struct AutoCursor : IDisposable
    {
        readonly FlogApp _owner;

        public AutoCursor(FlogApp owner)
        {
            _owner = owner;
            _owner._screen.OutShowCursor(false);
        }

        public void Dispose()
        {
            if (_owner._commandPane.Enabled)
                _owner._commandPane.UpdateCursorPos();
            else if (_owner._editFilterPane.Enabled)
                _owner._editFilterPane.UpdateCursorPos();
            else
                return;

            _owner._screen.OutShowCursor(true);
        }
    }

    public async Task<CliExitCode> Run()
    {
        // TODO: save session state on any kind of exit (or periodically save it as we go..); make sure to do this atomically

        var events = new EventBuffer<ITerminalEvent>();

        for (;;)
        {
            events.Clear();
            await Task.WhenAny(
                _screen.GetEvents(events),
                _logFileReader.WaitToReadAsync(_logFileCancel.Token).AsTask());

            using var _ = new AutoCursor(this);

            // do any global events from the batch first
            var exitCode = ProcessGlobalEvents(events);
            if (exitCode != null)
                return exitCode.Value;

            // now process the rest of the batch
            exitCode = ProcessNormalEvents(events);
            if (exitCode != null)
                return exitCode.Value;

            // update filter chain
            _logFilterChain.Process();

            // update visible filter
            _logFilterPane.UpdateAndDrawIfChanged();

            // update status
            _statusPane.SetFilterStatus(_logFilterChain, _logFilterPane);
            _statusPane.DrawIfChanged();
        }
    }

    void UpdateLayout()
    {
        var y = _screen.Size.Height;

        if (_statusPane.Enabled)
            _statusPane.SetBounds(_screen.Size.Width, --y, y+1);

        if (_commandPane.Enabled)
            _commandPane.SetBounds(_screen.Size.Width, --y, y+1);
        else if (_editFilterPane.Enabled)
            _editFilterPane.SetBounds(_screen.Size.Width, --y, y+1);

        _logFilterPane.SetBounds(_screen.Size.Width, 0, y);
    }

    void Refresh()
    {
        using var x = new AutoCursor(this);

        _logFilterPane.Draw();
        if (_statusPane.Enabled)
            _statusPane.Draw();

        if (_commandPane.Enabled)
            _commandPane.Draw();
        else if (_editFilterPane.Enabled)
            _editFilterPane.Draw();
    }

    CliExitCode? ProcessGlobalEvents(EventBuffer<ITerminalEvent> events)
    {
        foreach (var evt in events)
        {
            var accept = true;

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
                    UpdateLayout();
                    Refresh();
                    break;

                case ResizeEvent:
                    UpdateLayout();
                    break;

                default:
                    accept = false;
                    break;
            }

            if (accept)
                evt.Accept();
        }

        return null;
    }

    void StateChange_LogView_InputCommand()
    {
        _commandPane.Enabled = true;
        UpdateLayout();
        _statusPane.Draw();
        _commandPane.Draw();

        _state = State.InputCommand;
    }

    void StateChange_LogView_InputEditFilter()
    {
        _editFilterPane.Enabled = true;
        _editFilterPane.Text = _originalFilter = _logFilterPane.Current.Processor.To<SimpleFilterProcessor>().Filter;
        UpdateLayout();
        _statusPane.Draw();
        _editFilterPane.Draw();

        _state = State.InputEditFilter;
    }

    void StateChange_Input_LogView()
    {
        _commandPane.Enabled = false;
        _editFilterPane.Enabled = false;
        UpdateLayout();
        _statusPane.Draw();

        // clean other state
        _editingExistingFilter = false;
        _originalFilter = "";

        _state = State.LogView;
    }

    void AddAndActivateFilter()
    {
        var filter = new SimpleFilterProcessor();
        _logFilterChain.Add(filter);
        _logFilterPane.AddAndActivate(filter);
    }

    CliExitCode? ProcessNormalEvents(EventBuffer<ITerminalEvent> events)
    {
        foreach (var evt in events)
        {
            var accept = true;

            switch (_state)
            {
                case State.LogView:

                    switch (evt.Value)
                    {
                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                            // TODO: maybe some kind of confirmation..for when someone hammers esc but doesn't want to exit app by accident
                            return CliExitCode.Success;

                        case CharEvent { Char: 'd', Alt: false, Ctrl: true }:
                        case CharEvent { Char: 'q', NoModifiers: true }:
                            return CliExitCode.Success;

                        case CharEvent { Char: >= '1' and <= '9', Alt: true, Ctrl: false } cevt:
                            var index = cevt.Char - '1';
                            if (index < _logFilterPane.Filters.Count)
                                _logFilterPane.SetCurrentIndex(index);
                            break;

                        // micro search
                        case CharEvent { Char: '/', NoModifiers: true }:
                            break;

                        // new child filter
                        case CharEvent { Char: 't', Alt: false, Ctrl: true }:
                        case CharEvent { Char: '+', NoModifiers: true }:
                            AddAndActivateFilter();
                            StateChange_LogView_InputEditFilter();
                            break;

                        // edit existing filter
                        case KeyEvent { Key: ConsoleKey.F2, NoModifiers: true }:
                        case KeyEvent { Key: ConsoleKey.Enter, NoModifiers: true }:
                        case CharEvent { Char: '=', NoModifiers: true }:
                            if (_logFilterPane.CurrentIndex == 0)
                                AddAndActivateFilter();
                            else
                                _editingExistingFilter = true;
                            StateChange_LogView_InputEditFilter();
                            break;

                        // prev filter
                        case CharEvent { Char: ',', NoModifiers: true }:
                            _logFilterPane.ActivatePrev();
                            break;

                        // next filter
                        case CharEvent { Char: '.', NoModifiers: true }:
                            _logFilterPane.ActivateNext();
                            break;

                        // command mode
                        case CharEvent { Char: ':', NoModifiers: true }:
                            StateChange_LogView_InputCommand();
                            break;

                        default:
                            accept = _logFilterPane.HandleEvent(evt.Value);
                            break;
                    }

                    break;

                case State.InputCommand:
                    switch (evt.Value)
                    {
                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                            StateChange_Input_LogView();
                            break;

                        case KeyEvent { Key: ConsoleKey.Enter, NoModifiers: true }:
                            // TODO: execute command
                            _commandPane.Accept();
                            StateChange_Input_LogView();
                            break;

                        default:
                            accept = _commandPane.HandleEvent(evt.Value).accepted || _logFilterPane.HandleEvent(evt.Value);
                            break;
                    }
                    break;

                case State.InputEditFilter:
                    switch (evt.Value)
                    {
                        case KeyEvent { Key: ConsoleKey.Escape, NoModifiers: true }:
                            if (_editingExistingFilter)
                                _logFilterPane.Current.Processor.To<SimpleFilterProcessor>().Filter = _originalFilter;
                            else
                                _logFilterPane.RemoveLast();

                            _logFilterPane.Draw();
                            StateChange_Input_LogView();
                            break;

                        case KeyEvent { Key: ConsoleKey.Enter, NoModifiers: true }:
                            _editFilterPane.Accept();
                            StateChange_Input_LogView();
                            break;

                        default:
                            var handled = _editFilterPane.HandleEvent(evt.Value);
                            if (handled.inputChanged)
                                _logFilterPane.Current.Processor.To<SimpleFilterProcessor>().Filter = _editFilterPane.Text;

                            accept = handled.accepted || _logFilterPane.HandleEvent(evt.Value);
                            break;
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException();

            }

            if (accept)
                evt.Accept();
        }

        return null;
    }
}
