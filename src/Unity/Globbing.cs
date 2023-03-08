using Microsoft.Extensions.FileSystemGlobbing;

namespace OkTools.Unity;

static class Globbing
{
    // TODO: TESTS
    // TODO: consider adding support for searching with Everything if found to be installed..if given certain option
    // (lib can use a configvar to tune whether this is enabled..in this case we'd also
    // want exclusion patterns)

    // this is a set of hacks to deal with limitations on MS.FS.Globbing
    // - fileName is currently required because Matcher can't find dirs, so we enforce this limitation as a parameter instead of `pathSpec` expectation
    // - absolute paths now work, though it restricts the API to only an includespec (fine for my purposes)
    // limitations:
    // - fails on something like "c:/build*/**" (some problem with root)
    //
    // TODO: throwOnInvalidPathSpec isn't great on its own - the user won't know where the path came from. needs some kind
    // of source/origin passed in as well. either that, or have caller catch and rethrow, attaching more context..
    internal static IEnumerable<NPath> Find(NPath pathSpec, string fileName, bool throwOnInvalidPathSpec)
    {
        if (fileName.Contains('*'))
            throw new ArgumentException("Wildcards not allowed", nameof(fileName));

        var combinedSpec = pathSpec.Combine(fileName);

        var wild = combinedSpec.Elements.IndexOf(e => e.Contains('*'));

        if (wild < 0)
        {
            // just an ordinary path to a folder
            if (combinedSpec.FileExists())
                return combinedSpec.WrapInEnumerable();

            if (throwOnInvalidPathSpec)
                throw new FileNotFoundException($"Invalid glob pathspec '{pathSpec}', filename does not exist: {combinedSpec}");

            return Enumerable.Empty<NPath>();
        }

        var (basePath, matchPath) = combinedSpec.SplitAtElement(wild);
        if (throwOnInvalidPathSpec && !basePath.DirectoryExists())
            throw new DirectoryNotFoundException($"Invalid glob pathspec '{pathSpec}', base path does not exist: {basePath}");

        var matcher = new Matcher();
        matcher.AddInclude(matchPath);

        return matcher.GetResultsInFullPath(basePath).Select(p => p.ToNPath());
    }
}
