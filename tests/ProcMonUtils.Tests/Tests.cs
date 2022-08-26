using System.Text.RegularExpressions;
using OkTools.ProcMonUtils;

// ReSharper disable StringLiteralTypo

class Tests
{
    NPath _pmlPath = null!, _pmipPath = null!, _pmlBakedPath = null!;

    // crap tests just to get some basic sanity..

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var testDataPath = TestContext.CurrentContext
            .TestDirectory.ToNPath()
            .ParentContaining("tests", true)
            .DirectoryMustExist()
            .Combine("ProcMonUtils.Tests/testdata")
            .DirectoryMustExist();

        _pmlPath = testDataPath.Combine("basic.pml").FileMustExist();
        _pmipPath = testDataPath.Files("pmip*.txt").Single();
        _pmlBakedPath = _pmlPath.ChangeExtension(".pmlbaked").FileMustExist();
    }

    void Symbolicate()
    {
        PmlUtils.Symbolicate(_pmlPath, new SymbolicateOptions
        {
            MonoPmipPaths = new[] { _pmipPath.ToString() },
            BakedPath = _pmlBakedPath,
            NtSymbolPath = "",
        });
    }

    [Test]
    public void PmipBasics()
    {
        var mono = new MonoSymbolReader(_pmipPath);
        foreach (var symbol in mono.Symbols)
        {
            mono.TryFindSymbol(symbol.Address.Base + symbol.Address.Size / 2, out var sym0).ShouldBeTrue();
            sym0.ShouldBe(symbol);

            mono.TryFindSymbol(symbol.Address.Base, out var sym1).ShouldBeTrue();
            sym1.ShouldBe(symbol);

            mono.TryFindSymbol(symbol.Address.End - 1, out var sym2).ShouldBeTrue();
            sym2.ShouldBe(symbol);
        }

        mono.TryFindSymbol(mono.Symbols[0].Address.Base - 1, out _).ShouldBeFalse();
        mono.TryFindSymbol(mono.Symbols[^1].Address.End, out _).ShouldBeFalse();
    }

    [Test, Category("TODO"), Ignore("Have to disable this until figure out a way to make it stable")]
    public void WriteAndParse()
    {
        Symbolicate();

        var pmlQuery = new PmlQuery(_pmlBakedPath);

        var frame = pmlQuery.FindRecordBySequence(36)!.Value.Frames[2];
        pmlQuery.GetString(frame.ModuleStringIndex).ShouldBe("FLTMGR.SYS");
        frame.Type.ShouldBe(FrameType.Kernel);
        pmlQuery.GetString(frame.SymbolStringIndex).ShouldBe("FltGetFileNameInformation");

        // TODO: this is unstable; as the OS gets updated, offsets change..pack in the PDB probably..?
        frame.Offset.ShouldBe(0x752ul);
    }

    [Test]
    public void Match()
    {
        Symbolicate();

        var pmlQuery = new PmlQuery(_pmlBakedPath);

        // find all events where someone is calling a dotnet generic
        var matches = pmlQuery
            .MatchRecordsBySymbol(new Regex("`"))
            .OrderBy(seq => seq)
            .Select(seq => pmlQuery.FindRecordBySequence(seq))
            .WhereNotNull()
            .ToList();

        matches.First().Sequence.ShouldBe(3);
        matches.Last().Sequence.ShouldBe(311);
    }
}
