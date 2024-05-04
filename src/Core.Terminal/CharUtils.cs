namespace OkTools.Core.Terminal;

public static class CharUtils
{
    public static string ToNiceString(char c, bool alwaysWideHexEscapes = false)
    {
        return c switch
        {
            '\a'      => @"\a", // ctrl-g or ctrl-' (bell)
            '\b'      => @"\b", // ctrl-h (backspace)
            '\t'      => @"\t", // ctrl-i or tab (horizontal tab)
            '\n'      => @"\n", // ctrl-j or ctrl-enter (line feed)
            '\v'      => @"\v", // ctrl-k (vertical tab)
            '\f'      => @"\f", // ctrl-l (form-feed)
            '\r'      => @"\r", // ctrl-m or enter (carriage return)
            '\\'      => @"\\", // need to escape the backslash because of the others
            '\x7f'    => @"\b", // backspace (same as ctrl-h)
            <= '\x1f' => $"^{(char)(c+'A'-1)}",
            <= '~'    => c.ToString(),

            // \x sucks because it is ambiguous (like is \x41B '\x41'+'B' or '\x41B'). the compiler resolves it
            // greedily, so anything printing this c-escape as part of a larger string will need to either pay
            // attention to the following char (is it 0-9a-f) or just set the alwaysWideHexEscapes flag.
            <= '\xff' when !alwaysWideHexEscapes
                => $@"\x{(int)c:x}",
            _
                => $@"\u{(int)c:x4}"
        };
    }
}
