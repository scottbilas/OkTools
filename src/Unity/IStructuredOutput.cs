namespace OkTools.Unity;

[PublicAPI]
public enum StructuredOutputLevel
{
    Flat, Normal, Detailed
}

[PublicAPI]
public interface IStructuredOutput
{
    object Output(StructuredOutputLevel level, bool debug);
}
