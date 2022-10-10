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

        _pmlPath = testDataPath.Combine("events.pml").FileMustExist();
        _pmipPath = testDataPath.Files("pmip*.txt").Single();
        _pmlBakedPath = _pmlPath.ChangeExtension(".pmlbaked");
    }

    void Symbolicate()
    {
        var options = new SymbolicateOptions
        {
            IgnorePmipCreateTimes = true, // don't use filesystem timestamps to determine if an event is in a pmip or not
            MonoPmipPaths = new[] { _pmipPath.ToString() },
            BakedPath = _pmlBakedPath,
            NtSymbolPath = "",
        };

        using var pmlReader = new PmlReader(_pmlPath);

        PmlUtils.Symbolicate(pmlReader, options);

        options.DebugFormat = true;
        options.BakedPath = _pmlBakedPath.ChangeExtension(".dbg.pmlbaked");
        PmlUtils.Symbolicate(pmlReader, options);
    }

    [Test]
    public void PmipIndexing()
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

    [TestCase("", "")]
    [TestCase( // basic substitution
        "Mono.SafeGPtrArrayHandle:get_Item (int)",
        "Mono.SafeGPtrArrayHandle.get_Item(int)")]
    [TestCase( // more substitution
        "System.RuntimeType/ListBuilder`1<T_REF>:Add (T_REF)",
        "System.RuntimeType.ListBuilder`1<T_REF>.Add(T_REF)")]
    [TestCase( // target longer than source
        "System.Text.StringBuilder:.ctor (int,int,int,int,int,int,int,int,int)",
        "System.Text.StringBuilder.ctor(int, int, int, int, int, int, int, int, int)")]
    [TestCase( // really long one with many substitutions
        "System.Collections.Generic.Dictionary`2<string, UnityEditor.Scripting.ScriptCompilation.CachedVersionRangesFactory`1/CacheEntry<UnityEditor.Scripting.ScriptCompilation.UnityVersion>>:.ctor (int,System.Collections.Generic.IEqualityComparer`1<string>)",
        "System.Collections.Generic.Dictionary`2<string, UnityEditor.Scripting.ScriptCompilation.CachedVersionRangesFactory`1.CacheEntry<UnityEditor.Scripting.ScriptCompilation.UnityVersion>>.ctor(int, System.Collections.Generic.IEqualityComparer`1<string>)")]
    [TestCase( // extra text at the front
        "(wrapper runtime-invoke) <Module>:runtime_invoke_void__this___object (object,intptr,intptr,intptr)",
        "(wrapper runtime-invoke) <Module>.runtime_invoke_void__this___object(object, intptr, intptr, intptr)")]
    public void NormalizeMonoSymbolName(string test, string expected)
    {
        MonoJitSymbolDb.NormalizeMonoSymbolName(test).ShouldBe(expected);
    }

    [TestCase("mscorlib.dll", "Mono.SafeGPtrArrayHandle:get_Item (int)", // line 83
        new[] { 0x000001E8BB737830u, 0x000001E8BB73784Fu, 0x000001E8BB737858u },
        new[] { 0x000001E8BB73782Fu, 0x000001E8BB737859u })]
    [TestCase("UnityEngine.UIElementsNativeModule.dll", "(wrapper managed-to-native) UnityEngine.Yoga.Native:YGConfigFreeInternal (intptr)", // line 17534
        new[] { 0x000001E6AD2E55A0u, 0x000001E6AD2E55FFu, 0x000001E6AD2E5600u, 0x000001E6AD2E56CCu },
        new[] { 0x000001E6AD2E559Fu, 0x000001E6AD2E56CDu, 0x000001E6AD2E56CEu })]
    public void PmipParsing(string dllName, string symbol, ulong[] valid, ulong[] invalid)
    {
        var mono = new MonoJitSymbolDb(_pmipPath);

        foreach (var ivalid in valid)
        {
            mono.TryFindSymbol(ivalid, out var sym).ShouldBeTrue();
            {
                sym.ShouldNotBeNull();
                sym.AssemblyName.ShouldBe(dllName);
                sym.Symbol.ShouldBe(MonoJitSymbolDb.NormalizeMonoSymbolName(symbol));
            }
        }

        foreach (var iinvalid in invalid)
        {
            mono.TryFindSymbol(iinvalid, out var sym).ShouldBeFalse();
            sym.ShouldBeNull();
        }
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
