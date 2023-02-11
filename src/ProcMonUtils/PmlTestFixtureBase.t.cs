class PmlTestFixtureBase
{
    protected NPath PmlPath { get; private set; } = null!;
    protected NPath PmipPath { get; private set; } = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var testDataPath = TestContext.CurrentContext
            .TestDirectory.ToNPath()
            .ParentContaining("src", true)
            .DirectoryMustExist()
            .Combine("ProcMonUtils/testdata")
            .DirectoryMustExist();

        PmlPath = testDataPath.Combine("events.pml").FileMustExist();
        PmipPath = testDataPath.Files("pmip*.txt").Single();
    }
}
