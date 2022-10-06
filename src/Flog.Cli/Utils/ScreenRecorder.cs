using PInvoke;

#if ENABLE_SCREEN_RECORDER

class UnexpectedEscapeException : Exception {}

class ScreenRecorder
{
    public enum ShowType { None, Count, Chars };

    (int count, int chars)[] _cells = Array.Empty<(int, int)>();
    ShowType _show;
    Int2 _size = Int2.Zero;
    Int2 _nextSize; // from OnResize, which may fire on another thread
    Int2 _cursor, _savedCursor;

    void SetCursor(Int2 newCursor)
    {
        _cursor = (
            Math.Clamp(newCursor.X, 0, _size.X - 1),
            Math.Clamp(newCursor.Y, 0, _size.Y - 1));
    }

    public ShowType Show
    {
        get => _show;

        set
        {
            _show = value;
            if (_show != ShowType.None)
                Draw();
        }
    }

    public void OnResized(TerminalSize size)
    {
        _nextSize = new Int2(size.Width, size.Height);
    }

    public void Process(ReadOnlySpan<char> span)
    {
        var nextSize = _nextSize;
        if ((nextSize != _size).Any())
        {
            _cells = new (int, int)[nextSize.X * nextSize.Y];
            _size = nextSize;
            _cursor = _cursor.Min(_size - 1); // clamp to screen
        }

        Span<int> nums = stackalloc int[10];
        int pos = 0, startPos, startCursor;

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

        for (; pos != span.Length; ++_cells[startCursor].count, _cells[startCursor].chars += pos - startPos)
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

                // nums

                var numsUsed = 0;

                for (var numStart = pos;; ++pos)
                {
                    if (span[pos] >= '0' && span[pos] <= '9')
                        continue;

                    if (pos != numStart)
                        nums[numsUsed++] = int.Parse(span.Slice(numStart, pos - numStart));
                    else if (span[pos] == ';')
                        nums[numsUsed++] = 0;

                    if (span[pos] != ';')
                        break;

                    numStart = pos + 1;
                }

                int GetNum(ReadOnlySpan<int> c, int i = 0) => numsUsed > i ? c[i] : 0;
                int GetNumOneBased(ReadOnlySpan<int> c, int i = 0) => Math.Max(GetNum(c, i), 1);
                int GetNumZeroBased(ReadOnlySpan<int> c, int i = 0) => GetNumOneBased(c, i) - 1;

                // code

                switch (span[pos++])
                {
                    case 'A': // CUU (move the cursor up n rows)
                        SetCursor(_cursor - (0, GetNumOneBased(nums)));
                        break;

                    case 'B': // CUD (move the cursor down n rows)
                        SetCursor(_cursor + (0, GetNumOneBased(nums)));
                        break;

                    case 'C': // CUF (move the cursor right n columns)
                        SetCursor(_cursor + (GetNumOneBased(nums), 0));
                        break;

                    case 'D': // CUB (move the cursor left n columns)
                        SetCursor(_cursor - (GetNumOneBased(nums), 0));
                        break;

                    case 'E': // CNL (move the cursor to the beginning of line n down)
                        SetCursor((0, _cursor.Y + GetNumOneBased(nums)));
                        break;

                    case 'F': // CPL (move the cursor to the beginning of line n up)
                        SetCursor((0, _cursor.Y - GetNumOneBased(nums)));
                        break;

                    case 'G': // CHA (move the cursor to absolute column n)
                        SetCursor((GetNumZeroBased(nums), _cursor.Y));
                        break;

                    case 'd': // VPA (move the cursor to absolute row n)
                        SetCursor((_cursor.X, GetNumZeroBased(nums)));
                        break;

                    case 'H': // CUP (move the cursor to absolute position)
                    case 'f': // HVP (same as CUP but older)
                        SetCursor((GetNumZeroBased(nums, 1), GetNumZeroBased(nums)));
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
                        if (GetNum(nums) > 6)
                            throw new UnexpectedEscapeException();
                        break; // ignore

                    case 'S': // SU
                    case 'T': // SD
                    case '@': // ICH (insert spaces, shifting+clipping current and after to right)
                    case 'P': // DCH (delete chars, removing+shifting left from current, replacing from right end with spaces)
                    case 'X': // ECH (overwrite chars from current with spaces)
                    case 'L': // IL (insert lines, shifting+clipping current and below down)
                    case 'M': // DL (delete lines, removing+shifting up from current, replacing from bottom end with blank lines)
                        break; // ignore

                    case 'J': // ED (replace text on screen according to n mode with spaces)
                        // special: reset tracking on a full screen reset
                        if (GetNum(nums) == 2)
                            Array.Fill(_cells, default);
                        break; // ignore

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

        if (_show != ShowType.None)
            Draw();
    }

    unsafe void Draw()
    {
        var stdout = Kernel32.GetStdHandle(Kernel32.StdHandle.STD_OUTPUT_HANDLE);
        var cells = new Kernel32.CHAR_INFO[_size.X * _size.Y];
        var rect = new SMALL_RECT { Left = 0, Top = 0, Right = (short)_size.X, Bottom = (short)_size.Y };

        fixed (Kernel32.CHAR_INFO* cellsPtr = cells)
        {
            Kernel32.ReadConsoleOutput(stdout, cellsPtr,
                new COORD { X = (short)_size.X, Y = (short)_size.Y },
                new COORD { X = 0, Y = 0 },
                ref rect);
        }

        for (var y = 0; y < _size.Y; ++y)
        {
            for (var x = 0; x < _size.X; ++x)
            {
                var off = y * _size.X + x;
                var count = _show == ShowType.Count ? _cells[off].count : _cells[off].chars;
                cells[off].Char.UnicodeChar = (char)(count switch
                {
                    0 => ' ',
                    < 10 => '0' + count,
                    < 36 => 'A' + count-10,
                    _ => '#',
                });
            }
        }

        fixed (Kernel32.CHAR_INFO* cellsPtr = cells)
        {
            Kernel32.WriteConsoleOutput(stdout, cellsPtr,
                new COORD { X = (short)_size.X, Y = (short)_size.Y },
                new COORD { X = 0, Y = 0 },
                &rect);
        }
    }
}

#endif // ENABLE_SCREEN_RECORDER
