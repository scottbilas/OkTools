using OkTools.ProcMonUtils;

class PmipTests : PmlTestFixtureBase
{
    [Test]
    public void PmipIndexing()
    {
        var mono = new MonoJitSymbolDb(PmipPath);
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
        var mono = new MonoJitSymbolDb(PmipPath);

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
}
