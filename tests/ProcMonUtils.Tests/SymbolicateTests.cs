using System.Text.RegularExpressions;
using OkTools.ProcMonUtils;

// ReSharper disable StringLiteralTypo

class SymbolicateTests
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
        _pmlBakedPath = _pmlPath.ChangeExtension(".pmlbaked");
    }

    void Symbolicate()
    {
        using var pmlReader = new PmlReader(_pmlPath);
        PmlUtils.Symbolicate(pmlReader, new SymbolicateOptions
        {
            IgnorePmipCreateTimes = true, // don't use filesystem timestamps to determine if an event is in a pmip or not
            MonoPmipPaths = new[] { _pmipPath.ToString() },
            BakedPath = _pmlBakedPath,
            NtSymbolPath = "",
        });
    }

    [Test]
    public void PmipBasics()
    {
        var mono = new MonoJitSymbolDb(_pmipPath);
        foreach (var symbol in mono.Symbols)
        {
            mono.TryFindSymbol(symbol.Address.Base + (ulong)symbol.Address.Size / 2, out var sym0).ShouldBeTrue();
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

        var eventsDb = new SymbolicatedEventsDb(_pmlBakedPath);

        var frame = eventsDb.GetRecord(36)!.Value.Frames[2];
        eventsDb.GetString(frame.ModuleStringIndex).ShouldBe("FLTMGR.SYS");
        frame.Type.ShouldBe(FrameType.Kernel);
        eventsDb.GetString(frame.SymbolStringIndex).ShouldBe("FltGetFileNameInformation");

        // TODO: also test other frame types, at least User

        // TODO: this is unstable; as the OS gets updated, offsets change..pack in the PDB probably..?
        frame.AddressOrOffset.ShouldBe(0x752u);
    }

    [Test]
    public void Match()
    {
        Symbolicate();

        var eventsDb = new SymbolicatedEventsDb(_pmlBakedPath);

        // find all events where someone is calling a dotnet generic
        var matches = eventsDb
            .MatchRecordsBySymbol(new Regex("`"))
            .OrderBy(seq => seq)
            .ToList();

        matches.First().ShouldBe(3);
        matches.Last().ShouldBe(311);
    }
}
