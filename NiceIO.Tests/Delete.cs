﻿namespace NiceIO.Tests
{
	[TestFixture]
	public class Delete : TestWithTempDir
	{
		[Test]
		public void DeleteFile()
		{
			PopulateTempDir(new[] {"somefile"});

			var path = _tempPath.Combine("somefile");
			Assert.IsTrue(path.FileExists());
			path.Delete();

			AssertTempDir(new string[0]);
		}

		[Test]
		public void DeleteDirectory()
		{
			PopulateTempDir(new[]
			{
				"somedir/",
				"somedir/somefile"
			});

			var path = _tempPath.Combine("somedir");
			Assert.IsTrue(path.DirectoryExists());

			path.Delete();

			AssertTempDir(new string[0]);
		}

		[Test]
		public void DeleteRelativePath()
		{
			var path = new NPath("mydir/myfile.txt");
			Assert.Throws<ArgumentException>(() => path.Delete());
		}

		[Test]
		public void DeleteDirectoryWhileItIsLocked()
		{
			PopulateTempDir(new[] {"somedir/", "somedir/myfile"});

			var directory = _tempPath.Combine("somedir");

			//create a file in the directory and keep an open filehandle to it
			using (new FileStream(directory.Combine("somefile").ToString(), FileMode.Create))
			{
				directory.Delete(DeleteMode.Soft);
			}
			Assert.IsFalse(directory.Combine("myfile").FileExists());
		}


		[Test]
		public void DeleteOnMultiplePaths()
		{
			PopulateTempDir(new[] { "somefile","somedir/","somedir/myfile","somefile2" });

			var twoPaths = new[] {_tempPath.Combine("somefile"), _tempPath.Combine("somedir")};

			var result = twoPaths.Delete();

			CollectionAssert.AreEqual(twoPaths, result);

			AssertTempDir(new [] {"somefile2"});
		}

		[Test]
		public void DeleteContentsOfDirectory()
		{
			PopulateTempDir(new[]
			{
				"somedir/",
				"somedir/somefile"
			});

			var path = _tempPath.Combine("somedir");
			Assert.IsTrue(path.DirectoryExists());

			path.DeleteContents();

			AssertTempDir(new[] { "somedir/" });
		}

		[Test]
		[Platform(Include = "Win")]
		public void DeleteContentsOfDirectoryThrowsWhenFileCannotBeDeleted()
		{
			PopulateTempDir(new[]
			{
				"somedir/",
				"somedir/somefile"
			});

			var path = _tempPath.Combine("somedir");
			Assert.IsTrue(path.DirectoryExists());

			using (var writer = File.Open(path.Combine("somefile").ToString(), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
			{
				Assert.Throws<IOException>(() => path.DeleteContents());
			}
		}

		public void DeleteIfExistsOnFileThatExists()
		{
			PopulateTempDir(new[] { "somefile" });

			var path = _tempPath.Combine("somefile");
			path.DeleteIfExists();

			AssertTempDir(new string[0]);
		}

		[Test]
		public void DeleteIfExistsOnFileThatDoesNotExist()
		{
			var path = _tempPath.Combine("somefile");
			path.DeleteIfExists();

			AssertTempDir(new string[0]);
		}
	}
}