[AttributeUsage(AttributeTargets.Assembly)]
class TestFilesLocationAttribute : Attribute
{
    public TestFilesLocationAttribute(string location) => Location = location;
    public string Location { get; }
}
