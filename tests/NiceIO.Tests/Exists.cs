﻿namespace NiceIO.Tests
{
	[TestFixture]
	public class Exists : TestWithTempDir
	{
		[Test]
		public void FileExists()
		{
			PopulateTempDir(new[] {"somefile"});
			Assert.IsTrue(_tempPath.Combine("somefile").Exists());
			Assert.IsTrue(_tempPath.Combine("somefile").FileExists());
			Assert.IsFalse(_tempPath.Combine("somefile").DirectoryExists());

			Assert.IsTrue(_tempPath.Exists("somefile"));
			Assert.IsTrue(_tempPath.FileExists("somefile"));
			Assert.IsFalse(_tempPath.DirectoryExists("somefile"));
		}

		[Test]
		public void DirectoryExists()
		{
			PopulateTempDir(new[] {"somefile/"});
			Assert.IsTrue(_tempPath.Combine("somefile").Exists());
			Assert.IsFalse(_tempPath.Combine("somefile").FileExists());
			Assert.IsTrue(_tempPath.Combine("somefile").DirectoryExists());
		}

		[Test]
		public void FileWithSpace()
		{
			PopulateTempDir(new[] {"some file"});
			Assert.IsTrue(_tempPath.Combine("some file").FileExists());
			AssertTempDir(new [] {"some file"});
		}

		[Test]
		public void FileMustExistOkWhenExists()
		{
			PopulateTempDir(new[] { "somefile" });
			Assert.That(_tempPath.Combine("somefile"), Is.EqualTo(_tempPath.Combine("somefile").FileMustExist()));
		}

		[Test]
		public void FileMustExistThrowsWhenDoesNotExists()
		{
			Assert.Throws<FileNotFoundException>(() => _tempPath.Combine("somefile").FileMustExist());
		}

		[Test]
		public void DirectoryMustExistOkWhenExists()
		{
			PopulateTempDir(new[] { "somefile/" });
			Assert.That(_tempPath.Combine("somefile"), Is.EqualTo(_tempPath.Combine("somefile").DirectoryMustExist()));
		}

		[Test]
		public void DirectMustExistThrowsWhenDoesNotExists()
		{
			Assert.Throws<DirectoryNotFoundException>(() => _tempPath.Combine("somefile").DirectoryMustExist());
		}
	}
}