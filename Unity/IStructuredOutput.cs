using System.Dynamic;

namespace OkTools.Unity;

public enum StructuredOutputDetail
{
    Minimal, Typical, Full, Debug
}

public interface IStructuredOutput
{
    dynamic Output(StructuredOutputDetail detail);
}
