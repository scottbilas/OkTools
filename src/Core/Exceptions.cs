namespace OkTools.Core;

public class UnreachableCodeException : Exception
{
    public UnreachableCodeException(string message) : base(message) {}
    public UnreachableCodeException() : this("Internal error: this code should be unreachable!") {}
}
