#if ENABLE_SCREEN_RECORDER

class UnexpectedEscapeException : Exception
{
}

class ScreenRecorder
{
    int[] _counts = Array.Empty<int>();
    Int2 _size = Int2.Zero;
    Int2 _cursor, _savedCursor;

    void SetCursor(Int2 newCursor)
    {
        _cursor = (
            Math.Clamp(newCursor.X, 0, _size.X - 1),
            Math.Clamp(newCursor.Y, 0, _size.Y - 1));
    }

    public void OnResized(TerminalSize size)
    {
        _counts = new int[size.Width * size.Height];
        _size = (size.Width, size.Height);
    }

    public void Process(ReadOnlySpan<char> span)
    {
        Span<int> counts = stackalloc int[10];
        int pos = 0, startPos = 0, startCursor = 0;

        bool Accept(ReadOnlySpan<char> s, char c)
        {
            if (s[pos] != c)
                return false;

            ++pos;
            return true;
        }

        void Expect(ReadOnlySpan<char> s, char c)
        {
            if (s[pos++] != c)
                throw new UnexpectedEscapeException();
        }

        for (; pos != span.Length; _counts[startCursor] += pos - startPos)
        {
            startPos = pos;
            startCursor = _cursor.Y * _size.X + _cursor.X;

            // ordinary text

            if (!Accept(span, (char)0x1B))
            {
                ++pos;

                if (++_cursor.X == _size.X)
                {
                    _cursor.X = 0;
                    if (_cursor.Y < _size.Y - 1)
                        ++_cursor.Y;
                }

                continue;
            }

            // escape codes (https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences)

            // CSI

            if (Accept(span, '['))
            {
                // prefix

                var prefix = span[pos];
                if (prefix is '!' or '?')
                    ++pos;
                else
                    prefix = (char)0;

                // counts

                var countsUsed = 0;

                for (var numStart = pos;; ++pos)
                {
                    if (span[pos] >= '0' && span[pos] <= '9')
                        continue;

                    if (pos != numStart)
                        counts[countsUsed++] = int.Parse(span.Slice(numStart, pos - numStart));
                    else if (span[pos] == ';')
                        counts[countsUsed++] = 0;

                    if (span[pos] != ';')
                        break;

                    numStart = pos + 1;
                }

                int GetCount(ReadOnlySpan<int> c, int i = 0) => countsUsed > i ? c[i] : 0;
                int GetCountOB(ReadOnlySpan<int> c, int i = 0) => Math.Max(GetCount(c, i), 1);
                int GetCountZB(ReadOnlySpan<int> c, int i = 0) => GetCountOB(c, i) - 1;

                // code

                switch (span[pos++])
                {
                    case 'A': // CUU (move the cursor up n rows)
                        SetCursor(_cursor - (0, GetCountOB(counts)));
                        break;

                    case 'B': // CUD (move the cursor down n rows)
                        SetCursor(_cursor + (0, GetCountOB(counts)));
                        break;

                    case 'C': // CUF (move the cursor right n columns)
                        SetCursor(_cursor + (GetCountOB(counts), 0));
                        break;

                    case 'D': // CUB (move the cursor left n columns)
                        SetCursor(_cursor - (GetCountOB(counts), 0));
                        break;

                    case 'E': // CNL (move the cursor to the beginning of line n down)
                        SetCursor((0, _cursor.Y + GetCountOB(counts)));
                        break;

                    case 'F': // CPL (move the cursor to the beginning of line n up)
                        SetCursor((0, _cursor.Y - GetCountOB(counts)));
                        break;

                    case 'G': // CHA (move the cursor to absolute column n)
                        SetCursor((GetCountZB(counts), _cursor.Y));
                        break;

                    case 'd': // VPA (move the cursor to absolute row n)
                        SetCursor((_cursor.X, GetCountZB(counts)));
                        break;

                    case 'H': // CUP (move the cursor to absolute position)
                    case 'f': // HVP (same as CUP but older)
                        SetCursor((GetCountZB(counts, 1), GetCountZB(counts)));
                        break;

                    case 's': // ANSISYSSC
                        _savedCursor = _cursor;
                        break;
                    case 'u': // ANSISYSRC
                        _cursor = _savedCursor;
                        break;

                    case 'p': // DECSTR (soft reset)
                        if (prefix != '!')
                            throw new UnexpectedEscapeException();
                        _cursor = Int2.Zero;
                        break;

                    // none of these move the cursor

                    case 'h': // ATT160/DECTCEM/DECCKM
                    case 'l': // ATT160/DECTCEM/DECCKM
                        if (prefix != '?')
                            throw new UnexpectedEscapeException();
                        break; // ignore

                    case ' ': // DECSCUSR
                        Expect(span, 'q');
                        if (GetCount(counts) > 6)
                            throw new UnexpectedEscapeException();
                        break; // ignore

                    case 'S': // SU
                    case 'T': // SD
                    case '@': // ICH (insert spaces, shifting+clipping current and after to right)
                    case 'P': // DCH (delete chars, removing+shifting left from current, replacing from right end with spaces)
                    case 'X': // ECH (overwrite chars from current with spaces)
                    case 'L': // IL (insert lines, shifting+clipping current and below down)
                    case 'M': // DL (delete lines, removing+shifting up from current, replacing from bottom end with blank lines)
                    case 'J': // ED (replace text on screen according to n mode with spaces)
                    case 'K': // ED (replace text in current line according to n mode with spaces)
                    case 'm': // SGR (set format of text)
                    case 'r': // DECSTBM (set scrolling region)
                        break; // ignore

                    default:
                        throw new UnexpectedEscapeException();
                }

                continue;
            }

            // simple code

            switch (span[pos++])
            {
                case 'M': // RI
                    if (_cursor.Y > 0)
                        --_cursor.Y;
                    break;
                case '7': // DECSC
                    _savedCursor = _cursor;
                    break;
                case '8': // DECSR
                    _cursor = _savedCursor;
                    break;

                // none of these move the cursor

                case '=': // DECKPAM
                case '>': // DECKPNM
                    break; // ignore

                default:
                    throw new UnexpectedEscapeException();
            }
        }
    }
}
#endif // ENABLE_SCREEN_RECORDER
