using System.Dynamic;

namespace OkTools.Unity;

public enum StructuredOutputLevel
{
    Minimal, Typical, Full, Debug
}

public interface IStructuredOutput
{
    dynamic Output(StructuredOutputLevel level);
}
