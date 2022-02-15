namespace NiceIO.Tests
{
	class TildeExpansion
	{
		[Test]
		public void RelativeWithSoloTilde_ReturnsExpandedHome()
		{
			new NPath("~").TildeExpand().ShouldBe(NPath.HomeDirectory);
		}

		[Test]
		public void RelativeWithOnlyTilde_ReturnsExpandedHome()
		{
			var expected = NPath.HomeDirectory.Combine("some", "other", "file.txt");
			new NPath("~/some/other/file.txt").TildeExpand().ShouldBe(expected);
		}

		[Test]
		public void RelativeWithTildeAndOtherStuff_ReturnsSame()
		{
			new NPath("~x/file.txt").TildeExpand().ShouldBe(new NPath("~x/file.txt"));
			new NPath("x~/file.txt").TildeExpand().ShouldBe(new NPath("x~/file.txt"));
		}

		[Test]
		public void RelativeWithNoTilde_ReturnsSame()
		{
			new NPath("x/file.txt").TildeExpand().ShouldBe(new NPath("x/file.txt"));
		}
		
		[Test]
		public void Absolute_ReturnsSame()
		{
			new NPath("c:/blah/file.txt").TildeExpand().ShouldBe(new NPath("c:/blah/file.txt"));
			new NPath("c:/~/blah/file.txt").TildeExpand().ShouldBe(new NPath("c:/~/blah/file.txt"));
		}
	}
}
