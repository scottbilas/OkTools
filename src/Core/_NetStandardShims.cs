#if NETSTANDARD

using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;
}

[PublicAPI]
public class ArgumentNullException : System.ArgumentNullException
{
    public ArgumentNullException() {}
    public ArgumentNullException(string? paramName) : base(paramName) {}
    public ArgumentNullException(string? message, Exception? innerException) : base(message, innerException) {}
    public ArgumentNullException(string? paramName, string? message) : base(paramName, message) {}

    public static void ThrowIfNull(object? argument, string? paramName = null)
    {
        if (argument is null)
            Throw(paramName);
    }

    [DoesNotReturn]
    static void Throw(string? paramName) =>
        throw new System.ArgumentNullException(paramName);
}

#endif
