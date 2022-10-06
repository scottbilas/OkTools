using System.Text.RegularExpressions;

namespace OkTools.ProcMonUtils;

public struct NtSymbolPath
{
    public const string EnvVarName = "_NT_SYMBOL_PATH"; // docs at https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/symbol-path

    public NtSymbolPath(string? value = null) => Value = value;

    public static implicit operator NtSymbolPath(string path) => new (path);
    public static implicit operator string?(NtSymbolPath path) => path.Value;

    public string? Value { get; set; } // NULL == let dbghelp decide
    public bool HasDownloadPaths => Value?.Contains("http") == true;

    public override string ToString()
    {
        if (Value != null)
            return Value;

        var value = FromEnvironment.Value;
        if (value != null)
            return $"{EnvVarName}={value}";

        return "<unconfigured>";
    }

    public static NtSymbolPath FromEnvironment
    {
        get
        {
            var path = Environment.GetEnvironmentVariable(EnvVarName);
            if (path != null)
                path = Environment.ExpandEnvironmentVariables(path);

            return new NtSymbolPath(path);
        }
    }

    public void StripDownloadPaths()
    {
        if (Value == null)
            return;

        Value = Regex.Replace(Value, @"\bSRV\*([^*]+)\*http[^;]+", "$1", RegexOptions.IgnoreCase);
    }

    public void AddPath(string path)
    {
        Value ??= FromEnvironment.Value ?? string.Empty;

        if (Value != "")
            Value += ';';
        Value += path;
    }
}
