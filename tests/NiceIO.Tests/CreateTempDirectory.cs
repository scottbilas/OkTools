﻿namespace NiceIO.Tests
{
	[TestFixture]
	public class CreateTempDirectory
	{
		[Test]
		public void Test()
		{
			var path = NPath.CreateTempDirectory("myprefix");
			Assert.IsTrue(Directory.Exists(path.ToString()));
			Assert.IsFalse(path.IsRelative);
			StringAssert.Contains("myprefix", path.ToString());
		}
	}
}