using System.Reflection;
using System.Text.RegularExpressions;

static class MiscUtils
{
    public static IEnumerable<string> SequenceDuplicatesAsDots(IEnumerable<string> source)
    {
        var last = "";
        var count = 0;

        string GetLast() => last + new string('.', count == 1 ? 0 : count);

        foreach (var item in source)
        {
            if (item == last)
                ++count;
            else
            {
                if (count > 0)
                    yield return GetLast();

                count = 1;
                last = item;
            }
        }

        if (count > 0)
            yield return GetLast();
    }

    public static string ToNiceMethodName(MethodBase method)
    {
        // TODO: make this not crap code

        // not in love with this code. ideally would go look at .net source and try to build a simple little
        // parser, but who's got time for that. i'm just going to hack on this as needed. probably will have to
        // adjust it every new .net release anyway...

        var methodName = method.Name;

        // base name
        var name = (method.DeclaringType!.FullName! + '.' + methodName);

        // not useful to me to have nesting vs namespace differentiation
        name = name.Replace('+', '.');

        // clean up generated code a little
        var found = Regex.Match(name, @"<>c__DisplayClass\d+_\d+\.");
        var isLambda = found.Success;
        if (found.Success)
        {
            var replaced = name[(found.Index + found.Length)..]
                .RegexReplace(@"<<(.*)>b__\d+>d", "$1")
                .RegexReplace(@"<(.*)>b__\d+", "$1");

            // detect when we have a top level function
            var last = replaced;
            replaced = replaced.RegexReplace(@"<(.*)>g__(.*)\|\d+", "$1.$2");
            if (replaced != last)
                isLambda = false;

            name = name[..found.Index] + replaced;
        }

        // more simplifying of naming
        name = name
            .RegexReplace(@"^Program\.", "")
            .RegexReplace(@"^<Main>\$\.", "")
            .Replace("<Main>$", "main");

        // a bit of markup
        name = name.RegexReplace(@"(^|\.)MoveNext$", "⇓");
        if (isLambda)
            name = 'λ' + name;

        return name;
    }
}
