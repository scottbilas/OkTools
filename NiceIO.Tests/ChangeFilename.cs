namespace NiceIO.Tests
{
	[TestFixture]
	public class ChangeFilename
	{
        [Test]
        public void Absolute()
        {
            NPath expected, actual;

            expected = new NPath("/my/other.file");
            actual = new NPath("/my/file.txt").ChangeFilename("other.file");
            Assert.AreEqual(expected, actual);

            expected = new NPath("/my/folder");
            actual = new NPath("/my/path").ChangeFilename("folder");
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Relative()
        {
            NPath expected, actual;

            expected = new NPath("my/other.file");
            actual = new NPath("my/file.txt").ChangeFilename("other.file");
            Assert.AreEqual(expected, actual);

            expected = new NPath("my/folder");
            actual = new NPath("my/path").ChangeFilename("folder");
            Assert.AreEqual(expected, actual);
        }

		[Test]
		public void ToEmptyString()
		{
            NPath expected, actual;

            expected = new NPath("/my/path");
			actual = new NPath("/my/path/file.txt").ChangeFilename("");
			Assert.AreEqual(expected, actual);

            expected = new NPath("my/path");
            actual = new NPath("my/path/file.txt").ChangeFilename("");
            Assert.AreEqual(expected, actual);
        }

		[Test]
		public void CannotChangeTheExtensionOfAWindowsRootDirectory()
		{
			Assert.Throws<ArgumentException>(() => new NPath("C:\\").ChangeFilename("file.txt"));
		}

		[Test]
		public void CannotChangeTheExtensionOfALinuxRootDirectory()
		{
			Assert.Throws<ArgumentException>(() => new NPath("/").ChangeFilename("file.txt"));
		}
	}
}
