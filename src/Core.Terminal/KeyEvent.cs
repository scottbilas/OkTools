namespace OkTools.Core.Terminal;

// TODO: let's also pack in a little 10-char (null-term) array to store the captured pattern, and have little helpers to access it.
// sometimes might be nicer when detecting ctrl-c for example, and may help with diagnostics as well when there is a misfire on the key matcher.

[PublicAPI]
public readonly record struct KeyEvent(ConsoleKey Key, char Char, bool Alt = false, bool Shift = false, bool Ctrl = false)
{
    public bool AnyModifiers => Alt || Shift || Ctrl;
    public bool NoModifiers  => !Alt && !Shift && !Ctrl;

    public KeyEvent(ConsoleKey key, bool alt = false, bool shift = false, bool ctrl = false)
        : this(key, default, alt, shift, ctrl) {}
    public KeyEvent(char ch, bool alt = false, bool shift = false, bool ctrl = false)
        : this(default, ch, alt, shift, ctrl) {}

    public override string ToString()
    {
        string str;
        bool wrapInBrackets;

        if (Char is >= ' ' and <= '~')
        {
            str = Char.ToString();
            wrapInBrackets = Char == ' ';
        }
        else if (Key != ConsoleKey.None)
        {
            str = Key.ToString();
            wrapInBrackets = true;
        }
        else
        {
            str = CharUtils.ToNiceString(Char);
            wrapInBrackets = str.Length > 1;
        }

        if (NoModifiers)
            return wrapInBrackets ? $"<{str}>" : str;

        return $"<{(Alt?"!":"")}{(Shift?"+":"")}{(Ctrl?"^":"")}{str}>";
    }
}
