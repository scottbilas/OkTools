namespace OkTools.Core.Terminal;

// TODO: let's also pack in a little 10-char (null-term) array to store the captured pattern, and have little helpers to access it.
// sometimes might be nicer when detecting ctrl-c for example, and may help with diagnostics as well when there is a misfire on the key matcher.

public interface ITerminalEvent {}

[PublicAPI]
public readonly record struct KeyEvent(ConsoleKey Key, char Char, bool Alt = false, bool Shift = false, bool Ctrl = false) : ITerminalEvent
{
    public KeyEvent(ConsoleKey key, bool alt = false, bool shift = false, bool ctrl = false)
        : this(key, default, alt, shift, ctrl) {}
    public KeyEvent(char ch, bool alt = false, bool shift = false, bool ctrl = false)
        : this(default, ch, alt, shift, ctrl) {}
    public KeyEvent(ConsoleKey key, ConsoleModifiers modifiers)
        : this(key, default, (modifiers & ConsoleModifiers.Alt) != 0, (modifiers & ConsoleModifiers.Shift) != 0, (modifiers & ConsoleModifiers.Control) != 0) {}
    public KeyEvent(char ch, ConsoleModifiers modifiers)
        : this(default, ch, (modifiers & ConsoleModifiers.Alt) != 0, (modifiers & ConsoleModifiers.Shift) != 0, (modifiers & ConsoleModifiers.Control) != 0) {}

    public ConsoleModifiers Modifiers => (Alt ? ConsoleModifiers.Alt : 0) | (Shift ? ConsoleModifiers.Shift : 0) | (Ctrl ? ConsoleModifiers.Control : 0);

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

        if (Modifiers == 0)
            return wrapInBrackets ? $"<{str}>" : str;

        return $"<{(Alt?"!":"")}{(Shift?"+":"")}{(Ctrl?"^":"")}{str}>";
    }

    public (string prefix, string special, string normal) ToComponentStrings()
    {
        string special = "", normal = "";

        if (Char is >= ' ' and <= '~')
        {
            if (Char == ' ')
                special = "⎵";
            else
                normal = Char.ToString();
        }
        else if (Key != ConsoleKey.None)
            special = Key.ToString();
        else
        {
            normal = CharUtils.ToNiceString(Char);
            if (normal.Length > 1)
            {
                special = normal;
                normal = "";
            }
        }

        return (
            $"{(Alt ? "!" : "")}{(Shift ? "+" : "")}{(Ctrl ? "^" : "")}",
            special, normal);
    }
}
