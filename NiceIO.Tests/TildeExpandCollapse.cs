namespace NiceIO.Tests
{
	class TildeExpandCollapse
	{
		[TestCaseSource(nameof(Source))]
		public void Cases((string, string) ab)
		{
            var (a, b) = ab;
            var (an, bn) = (a.ToNPath(), b.ToNPath());

            an.TildeExpand().ShouldBe(bn);
            an.TildeExpand().TildeCollapse().ShouldBe(an);

            bn.TildeCollapse().ShouldBe(an);
            bn.TildeCollapse().TildeExpand().ShouldBe(bn);
		}

        static IEnumerable<(string, string)> Source() => new (string, string)[]
        {
            // basics

            ("~", NPath.HomeDirectory),
			("~/some/other/file.txt", NPath.HomeDirectory.Combine("some", "other", "file.txt")),
            ("~/some/other/file.txt", NPath.HomeDirectory.Combine("some", "other", "file.txt")),

            // unsupported tilde style

            ("~x/file.txt", "~x/file.txt"),
            ("x~/file.txt", "x~/file.txt"),

            // no tilde

            ("x/file.txt", "x/file.txt"),

            // absolutes

            ("c:/blah/file.txt", "c:/blah/file.txt"),
            ("c:/~/blah/file.txt", "c:/~/blah/file.txt"),
        };
    }
}
