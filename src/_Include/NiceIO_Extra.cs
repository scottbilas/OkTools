namespace NiceIO;

partial class NPath
{
    public static implicit operator string(NPath path)
    {
        return path.ToString();
    }
}
