partial class Screen
{
    readonly ControlBuilder _cb = new();

    public void OutFlush()
    {
        if (_cb.Span.IsEmpty)
            return;

        _terminal.Out(_cb.Span);
        _cb.Clear(100000);
    }

    public void OutClearScreen(ClearMode mode = ClearMode.Full) => _cb.ClearScreen(mode);
    public void OutClearLine(ClearMode mode = ClearMode.Full) => _cb.ClearLine(mode);
    public void OutClearChars(int count) => _cb.EraseCharacters(count);

    public void OutInsertChars(int count) => _cb.InsertCharacters(count);
    public void OutDeleteChars(int count) => _cb.DeleteCharacters(count);

    public void OutResetAttributes() => _cb.ResetAttributes();

    public void OutShowCursor(bool visible) => _cb.SetCursorVisibility(visible);
    public void OutSetCursorPos(int x, int y) => _cb.MoveCursorTo(x, y);
    public void OutSaveCursorPos() => _cb.SaveCursorState();
    public void OutRestoreCursorPos() => _cb.RestoreCursorState();

    public void OutSetScrollMargins(int top, int bottom) => _cb.SetScrollMargin(top, bottom);
    public void OutScrollBufferUp(int count) => _cb.MoveBufferUp(count);
    public void OutScrollBufferDown(int count) => _cb.MoveBufferDown(count);

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
}
