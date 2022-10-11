using System.Text.RegularExpressions;
using OkTools.ProcMonUtils;

// ReSharper disable StringLiteralTypo

class SymbolicateTests : PmlTestFixtureBase
{
    NPath _pmlBakedPath = null!, _pmlDebugBakedPath = null!;
    SymbolicatedEventsDb _eventsDb = null!;

    // crap tests just to get some basic sanity..

    [OneTimeSetUp]
    public new void OneTimeSetUp()
    {
        _pmlBakedPath = PmlPath.ChangeExtension(".pmlbaked");
        _pmlDebugBakedPath = _pmlBakedPath.ChangeExtension(".dbg.pmlbaked");;

        var options = new SymbolicateOptions
        {
            IgnorePmipCreateTimes = true, // don't use filesystem timestamps to determine if an event is in a pmip or not
            MonoPmipPaths = new[] { PmipPath.ToString() },
            BakedPath = _pmlBakedPath,
            NtSymbolPath = "",
        };

        using var pmlReader = new PmlReader(PmlPath);

        PmlUtils.Symbolicate(pmlReader, options);

        options.DebugFormat = true;
        options.BakedPath = _pmlDebugBakedPath;
        PmlUtils.Symbolicate(pmlReader, options);

        _eventsDb = new SymbolicatedEventsDb(_pmlBakedPath);
    }

    [TestCase(
        "06 K [ntoskrnl.exe] ObOpenObjectByNameEx + 0xdc7 (0xfffff807644decb7)",
        FrameType.Kernel, "ntoskrnl.exe", "ObOpenObjectByNameEx", 0xdc7, 0xfffff807644decb7u)]
    [TestCase(
        "28 M [mscorlib.dll] System.Reflection.RuntimeMethodInfo.Invoke(object, System.Reflection.BindingFlags, System.Reflection.Binder, object[], System.Globalization.CultureInfo) + 0x11b (0x1e8dc29937b)",
        FrameType.Mono, "mscorlib.dll", "System.Reflection.RuntimeMethodInfo.Invoke(object, System.Reflection.BindingFlags, System.Reflection.Binder, object[], System.Globalization.CultureInfo)", 0x11b, 0x1e8dc29937bu)]
    [TestCase(
        "14 U 0x7ff6b6a9deed",
        FrameType.User, null, null, 0, 0x7ff6b6a9deedu)]
    public void TryParseDebugFrameRecord(string line, FrameType type, string? module, string? symbol, int offset, ulong addr)
    {
        DebugFrameRecord.TryParse(line, out var record).ShouldBeTrue();
        record.Type.ShouldBe(type);
        record.Module.ShouldBe(module);
        record.Symbol.ShouldBe(symbol);
        record.Offset.ShouldBe(offset);
        record.Address.ShouldBe(addr);
    }

    [Test]
    public void BinarySerialization_Matches()
    {
        var dbgStacks = File.ReadAllText(_pmlDebugBakedPath).Split("Event #")[1..];
        for (var istack = 0; istack != dbgStacks.Length; ++istack)
        {
            var dbgTexts = dbgStacks[istack].Split('\n').Select(l => l.Trim()).Where(l => l.Any()).ToArray();
            var dbgFrames = dbgTexts.Skip(1).Select(line =>
            {
                DebugFrameRecord.TryParse(line, out var record).ShouldBeTrue($"`{line}` did not match regex");;
                return record;
            }).ToArray();

            var eventIndex = int.Parse(dbgTexts[0][..dbgTexts[0].IndexOf(' ')]);
            var record = _eventsDb.GetRecord(eventIndex)!.Value;

            record.Frames.Length.ShouldBe(dbgFrames.Length);
            for (var iframe = 0; iframe < dbgFrames.Length; ++iframe)
            {
                var bin = record.Frames[iframe];
                var dbg = dbgFrames[iframe];

                bin.Type.ShouldBe(dbg.Type);
                (bin.AddressOrOffset == dbg.Address || bin.AddressOrOffset == (ulong)dbg.Offset).ShouldBeTrue();
                _eventsDb.GetString(bin.ModuleStringIndex).ShouldBe(dbg.Module);
                _eventsDb.GetString(bin.SymbolStringIndex).ShouldBe(dbg.Symbol);
            }
        }
    }

    [Test, Category("TODO"), Ignore("Have to disable this until figure out a way to make it stable")]
    public void WriteAndParse()
    {
        var frame = _eventsDb.GetRecord(36)!.Value.Frames[2];
        _eventsDb.GetString(frame.ModuleStringIndex).ShouldBe("FLTMGR.SYS");
        frame.Type.ShouldBe(FrameType.Kernel);
        _eventsDb.GetString(frame.SymbolStringIndex).ShouldBe("FltGetFileNameInformation");

        // TODO: also test other frame types, at least User

        // TODO: this is unstable; as the OS gets updated, offsets change..pack in the PDB probably..?
        frame.AddressOrOffset.ShouldBe(0x752u);
    }

    [Test, Ignore("WIP!!!")]
    public void Match()
    {
        // find all events where someone is calling a dotnet generic
        var matches = _eventsDb
            .MatchRecordsBySymbol(new Regex("`"))
            .OrderBy(seq => seq)
            .ToList();

        matches.First().ShouldBe(3);
        matches.Last().ShouldBe(311);
    }
}
