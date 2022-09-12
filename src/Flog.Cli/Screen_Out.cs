using System.Drawing;

partial class Screen
{
    readonly ControlBuilder _cb = new();
    #if ENABLE_SCREEN_RECORDER
    readonly ScreenRecorder _recorder = new();
    #endif

    public void OutFlush()
    {
        if (_cb.Span.IsEmpty)
            return;

        _terminal.Out(_cb.Span);
        #if ENABLE_SCREEN_RECORDER
        _recorder.Process(_cb.Span);
        #endif
        _cb.Clear(50000);
    }

    public void OutClearScreen(ClearMode mode = ClearMode.Full) => _cb.ClearScreen(mode);
    public void OutClearLine(ClearMode mode = ClearMode.Full) => _cb.ClearLine(mode);
    public void OutClearChars(int count) => _cb.EraseCharacters(count);

    public void OutInsertChars(int count) => _cb.InsertCharacters(count);
    public void OutDeleteChars(int count) => _cb.DeleteCharacters(count);

    public void OutSetForegroundColor(Color color) => _cb.SetForegroundColor(color.R, color.G, color.B);
    public void OutSetBackgroundColor(Color color) => _cb.SetBackgroundColor(color.R, color.G, color.B);
    public void OutResetAttributes() => _cb.ResetAttributes();

    public void OutShowCursor(bool visible) => _cb.SetCursorVisibility(visible);
    public void OutSetCursorPos(int x, int y) => _cb.MoveCursorTo(x, y);

    public void OutMoveCursorLeft(int count = 1) => _cb.MoveCursorLeft(count);
    public void OutMoveCursorRight(int count = 1) => _cb.MoveCursorRight(count);
    public void OutSaveCursorPos() => _cb.SaveCursorState();
    public void OutRestoreCursorPos() => _cb.RestoreCursorState();

    public void OutSetScrollMargins(int top, int bottom) => _cb.SetScrollMargin(top, bottom);
    public void OutScrollBufferUp(int count = 1) => _cb.MoveBufferUp(count);
    public void OutScrollBufferDown(int count = 1) => _cb.MoveBufferDown(count);

    public void OutPrint(char chr) => _cb.Print(chr);
    public void OutPrint(ReadOnlySpan<char> span) => _cb.Print(span);

    public void OutPrint(ReadOnlySpan<char> span, int fillWidth, bool useClearAfter = false)
    {
        var len = Math.Min(span.Length, fillWidth);
        OutPrint(span[..len]);
        if (len < fillWidth)
        {
            if (useClearAfter)
                OutClearLine(ClearMode.After);
            else
                OutClearChars(fillWidth - len);
        }
    }

    public void OutTruncateMarker()
    {
        OutSetForegroundColor(Options.TruncateMarkerColor);
        OutPrint(Options.TruncateMarkerText);
        OutResetAttributes();
    }
}
