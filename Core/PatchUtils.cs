using System.Text.RegularExpressions;

namespace OkTools.Core;

[PublicAPI]
public static class PatchUtils
{
    public static bool IsDiff(string candidate)
    {
        const string detectDiffPattern = @"(?mx)
                ^
                ---\ [^\n]+\n
                \+\+\+\ [^\n]+\n
                @@\ ";

        return Regex.IsMatch(candidate, detectDiffPattern);
    }
}
