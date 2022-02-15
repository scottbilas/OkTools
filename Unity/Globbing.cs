using Microsoft.Extensions.FileSystemGlobbing;

namespace OkTools.Unity;

static class Globbing
{
    // TODO: add support for searching with Everything if found to be installed
    // (lib can use a configvar to tune whether this is enabled..in this case we'd also
    // want exclusion patterns)

    // this is a set of hacks to deal with limitations on MS.FS.Globbing
    // - fileName is currently required because Matcher can't find dirs, so we enforce this limitation as a parameter instead of `pathSpec` expectation
    // - absolute paths now work, though it restricts the API to only an includespec (fine for my purposes)
    // limitations:
    // - fails on something like "c:/build*/**" (some problem with root)
    //
    internal static IEnumerable<NPath> Find(NPath pathSpec, string fileName)
    {
        if (fileName.Contains('*'))
            throw new ArgumentException("Wildcards not allowed", nameof(fileName));

        pathSpec = pathSpec.Combine(fileName);

        var wild = pathSpec.Elements.IndexOf(e => e.Contains('*'));

        if (wild < 0)
        {
            // just an ordinary path to a folder
            return pathSpec.FileExists()
                ? pathSpec.WrapInEnumerable()
                : Enumerable.Empty<NPath>();
        }

        var (basePath, matchPath) = pathSpec.SplitAtElement(wild);

        var matcher = new Matcher();
        matcher.AddInclude(matchPath);

        return matcher.GetResultsInFullPath(basePath).Select(p => p.ToNPath());
    }
}
