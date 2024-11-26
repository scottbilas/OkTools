using DocoptNet;
using Vezel.Cathode.Text.Control;

namespace OkTools.Terminal;

[PublicAPI]
public readonly struct AutoSaveRestoreCursorState : IDisposable
{
    readonly ControlBuilder _cb;

    public AutoSaveRestoreCursorState(ControlBuilder cb) =>
        (_cb = cb).SaveCursorState();
    public void Dispose() =>
        _cb.RestoreCursorState();
}

[PublicAPI]
public enum CursorConstrainMode
{
    // [Default] Origin is upper-left of screen. Cursor positioning will ignore any configured margin, though some
    // operations such as MoveCursorTo() will constrain to the margin, if configured.
    Screen,

    // Origin is upper-left of margin. All cursor operations will be constrained to the current margin.
    Margin
}

[PublicAPI]
public class HelpCommandResult : IHelpResult
{
    public string Help { get; }

    public HelpCommandResult(string help) { Help = help.TrimStart(); }
}
