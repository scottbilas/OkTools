using System.Collections;
using DocoptNet;

// this file is just here to validate that docopt.net options work the way i think they do.
// the grammar is tweaky and easy for me to mess up.
//
// note that this site is helpful: http://try.docopt.org/

#if DEBUG
class DocOptNetTests
{
    [TestCaseSource(nameof(Usages))]
    public void Validate(string usage, string[] args, bool valid, bool hasOpt, string[] inc)
    {
        var docOpt = new Docopt();
        if (valid)
        {
            var opt = docOpt.Apply(usage, args)!;
            try
            {
                opt["f"].IsTrue.ShouldBeTrue();
                opt["-o"].IsTrue.ShouldBe(hasOpt);
                opt["-i"].AsList.Cast<string>().ShouldBe(inc.OrEmpty());
            }
            catch
            {
                opt.DumpConsole();
                throw;
            }
        }
        else
        {
            Should.Throw<DocoptInputErrorException>(() =>
            {
                var opt = docOpt.Apply(usage, args);
                opt.DumpConsole();
            });
        }
    }

    static IEnumerable Usages
    {
        get
        {
            static string Trim(string usage) =>
                usage.RegexReplace(@"\s+", " ").Replace("\n", "; ");
            static TestCaseData Valid(string usage, string args, bool hasOpt, params string[] inc) =>
                new TestCaseData(usage, args.Split(' '), true, hasOpt, inc)
                    .SetName($"{args} // {Trim(usage)}");
            static TestCaseData Invalid(string usage, string args) =>
                new TestCaseData(usage, args.Split(' '), false, false, null)
                    .SetName($"(!) {args} // {Trim(usage)}");

            const string usage = @"
                usage: exe f [-o] [-i V]...

                options:
                  -o    Option
                  -i V  Repeatable
                ";

            yield return Valid  (usage, "f", false);
            yield return Valid  (usage, "f -o", true);
            yield return Valid  (usage, "f -i abc", false, "abc");
            yield return Invalid(usage, "f -i");
            yield return Invalid(usage, "f -i abc def");
            yield return Valid  (usage, "f -i abc -i def", false, "abc", "def");
            yield return Valid  (usage, "f -i abc -o", true, "abc");
            yield return Valid  (usage, "f -i abc -o -i def", true, "abc", "def");
            yield return Invalid(usage, "f -i -o abc -i def");
        }
    }

    [Test]
    public void OptionsFirst()
    {
        Should.Throw<DocoptInputErrorException>(() =>
            new Docopt().Apply("usage: exename command [-n]", new[] { "command", "-n" }, optionsFirst: true));

        var opt = new Docopt().Apply("usage: exename command [-n]", new[] { "command", "-n" })!;
        opt["-n"].IsTrue.ShouldBeTrue();
    }
}
#endif
