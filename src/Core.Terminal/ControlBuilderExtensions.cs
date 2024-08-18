using System.Text;
using Vezel.Cathode.Text.Control;
using static Vezel.Cathode.Text.Control.ControlConstants;

namespace OkTools.Core.Terminal;

public readonly struct AutoSaveRestoreCursorState : IDisposable
{
    readonly ControlBuilder _cb;

    public AutoSaveRestoreCursorState(ControlBuilder cb) =>
        (_cb = cb).SaveCursorState();
    public void Dispose() =>
        _cb.RestoreCursorState();
}

public enum CursorConstrainMode
{
    // [Default] Origin is upper-left of screen. Cursor positioning will ignore any configured margin, though some
    // operations such as MoveCursorTo() will constrain to the margin, if configured.
    Screen,

    // Origin is upper-left of margin. All cursor operations will be constrained to the current margin.
    Margin
}

[PublicAPI]
public static class ControlBuilderExtensions
{
    // for a good time, go to https://vt100.net/docs/vt100-ug/chapter3.html

    public static ControlBuilder Print(this ControlBuilder @this, StringBuilder sb)
    {
        foreach (var chunk in sb.GetChunks())
            @this.Print(chunk);
        return @this;
    }

    public static ControlBuilder Print(this ControlBuilder @this, ReadOnlyMemory<char> mem) =>
        @this.Print(mem.Span);

    public static void PrintLine(this ControlBuilder @this) =>
        @this.Print("\r\n");
    public static void PrintLine(this ControlBuilder @this, ReadOnlySpan<char> span)
    {
        @this.Print(span);
        @this.PrintLine();
    }

    public static void ShowCursor(this ControlBuilder @this, bool visible = true) =>
        @this.SetCursorVisibility(visible);
    public static void HideCursor(this ControlBuilder @this) =>
        @this.SetCursorVisibility(false);

    // these are affected by current CursorOrigin
    public static ControlBuilder MoveCursorHome(this ControlBuilder @this) =>
        @this.Print(CSI).Print('H');
    public static ControlBuilder MoveCursorEnd(this ControlBuilder @this) =>
        @this.MoveCursorTo(10000, 10000);

    public static void MoveCursorLineStart(this ControlBuilder @this) =>
        @this.CarriageReturn();
    public static void MoveCursorLineEnd(this ControlBuilder @this) =>
        @this.MoveCursorRight(10000);

    public static ControlBuilder MoveCursorUp(this ControlBuilder @this) =>
        @this.MoveCursorUp(1);
    public static ControlBuilder MoveCursorDown(this ControlBuilder @this) =>
        @this.MoveCursorDown(1);
    public static ControlBuilder MoveCursorLeft(this ControlBuilder @this) =>
        @this.MoveCursorLeft(1);
    public static ControlBuilder MoveCursorRight(this ControlBuilder @this) =>
        @this.MoveCursorRight(1);

    public static ControlBuilder MoveBufferUp(this ControlBuilder @this) =>
        @this.MoveBufferUp(1);
    public static ControlBuilder MoveBufferDown(this ControlBuilder @this) =>
        @this.MoveBufferDown(1);

    public static ControlBuilder ResetScrollMargin(this ControlBuilder @this) =>
        @this.Print(CSI).Print(";r");
    public static ControlBuilder ReverseLineFeed(this ControlBuilder @this) =>
        @this.Print(ESC).Print(['M']); // this is reverse index (M), not reverse line feed (H), but this is supported much better and does the same thing according to docs

    public static AutoSaveRestoreCursorState SaveRestoreCursor(this ControlBuilder @this) =>
        new(@this);

    public static AutoSaveRestoreCursorState SaveRestoreCursor(this ControlBuilder @this, int y, int x)
    {
        var saver = @this.SaveRestoreCursor();
        @this.MoveCursorTo(y, x);
        return saver;
    }

    // note that this will cause the cursor to move to home position. can't save/restore cursor pos because it is tied
    // to cursor state, and restoring state will kill the new constrain mode.
    public static ControlBuilder SetCursorConstrainMode(this ControlBuilder @this, CursorConstrainMode mode) => @this
        .Print(CSI)
        .Print(mode switch
        {
            CursorConstrainMode.Screen => "?6l",
            CursorConstrainMode.Margin => "?6h",
            _ => throw new InvalidOperationException("Illegal flag: " + mode)
        });
}
