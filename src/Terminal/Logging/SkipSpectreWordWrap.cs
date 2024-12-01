using Spectre.Console;

namespace OkTools.Terminal;

// this works around Spectre apparent lack of ability to pass through "single line" option for rendering. word wrapping
// will break the ability to copy-paste long lines from console. specific example is when i print out a full cli that
// is intended to copy-paste into a debugger. wrapping will require multiple copy-pastes.
//
// see https://github.com/spectreconsole/spectre.console/discussions/471
//

readonly struct SkipSpectreWordWrap : IDisposable
{
    static bool s_inProgress;
    readonly int _oldWidth = AnsiConsole.Profile.Width;

    public SkipSpectreWordWrap()
    {
        if (s_inProgress)
            throw new InvalidOperationException("nested SkipSpectreWordWrap");
        s_inProgress = true;

        AnsiConsole.Profile.Width = 100000;
    }

    public void Dispose()
    {
        if (!s_inProgress)
            throw new InvalidOperationException("unexpected SkipSpectreWordWrap lifetime");

        AnsiConsole.Profile.Width = _oldWidth;
        s_inProgress = false;
    }
}
