﻿namespace NiceIO.Tests
{
	[TestFixture]
	public class ChangeExtension
	{
		[Test]
		public void WithoutDot()
		{
			var p1 = new NPath("/my/path/file.txt");
			var p2 = new NPath("/my/path/file.mp4").ChangeExtension("txt");
			Assert.AreEqual(p1, p2);
		}

		[Test]
		public void WithDot()
		{
			var p1 = new NPath("/my/path/file.txt");
			var p2 = new NPath("/my/path/file.mp4").ChangeExtension(".txt");
			Assert.AreEqual(p1, p2);
		}

		[Test]
		public void OnPathWithMultipleDots()
		{
			var p1 = new NPath("/my/path/file.something.txt");
			var p2 = new NPath("/my/path/file.something.mp4").ChangeExtension(".txt");
			Assert.AreEqual(p1, p2);
		}

		[Test]
		public void ToEmptyString()
		{
			var expected = new NPath("/my/path/file.something");
			var actual = new NPath("/my/path/file.something.exe").ChangeExtension(string.Empty);
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void CannotChangeTheExtensionOfAWindowsRootDirectory()
		{
			Assert.Throws<ArgumentException>(() => new NPath("C:\\").ChangeExtension(".txt"));
		}

		[Test]
		public void CannotChangeTheExtensionOfALinuxRootDirectory()
		{
			Assert.Throws<ArgumentException>(() => new NPath("/").ChangeExtension(".txt"));
		}
	}
}
