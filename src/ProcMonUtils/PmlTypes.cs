using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using OkTools.ProcMonUtils.FileSystem;

namespace OkTools.ProcMonUtils;

[PublicAPI, DebuggerDisplay("{ProcessName}")]
public class PmlProcess
{
    public readonly int ProcessId;
    public readonly string ProcessName;
    public IReadOnlyList<PmlModule> Modules => _modules;

    readonly PmlModule[] _modules;

    public PmlProcess(int processId, string processName, PmlModule[] takeModules)
    {
        ProcessId = processId;
        ProcessName = processName;
        _modules = takeModules;

        // keep sorted for bsearch
        Array.Sort(_modules, (a, b) => a.Address.Base.CompareTo(b.Address.Base));
    }

    public bool TryFindModule(long address, [NotNullWhen(returnValue: true)] out PmlModule? module) =>
        _modules.TryFindAddressIn(address, out module);

    // may return null for process names like "System" and "Idle"
    public string? GetImagePath()
    {
        PmlModule? found = null;
        foreach (var module in _modules)
        {
            if (!Path.GetFileName(module.ImagePath).EqualsIgnoreCase(ProcessName))
                continue;

            if (found?.ImagePath.EqualsIgnoreCase(module.ImagePath) == false)
                throw new InvalidOperationException($"Unexpected process name found at multiple image paths: '{found.ImagePath}' and '{module.ImagePath}'");

            found = module;
        }

        return found?.ImagePath;
    }
}

[PublicAPI, DebuggerDisplay("{ImagePath}")]
public class PmlModule : IAddressRange
{
    public readonly string ImagePath;
    public readonly string ModuleName;
    public readonly AddressRange Address;

    public PmlModule(string imagePath, AddressRange address)
    {
        ImagePath = imagePath;
        ModuleName = Path.GetFileName(ImagePath);
        Address = address;
    }

    ref readonly AddressRange IAddressRange.AddressRef => ref Address;
}

// ReSharper disable NotAccessedPositionalProperty.Global
[StructLayout(LayoutKind.Sequential, Pack=1)]
record struct PmlRawEvent(         // {from PML Format.md}
    uint   ProcessIndex,        // 0x0    | Uint32    | The index to the process of the event.
    uint   ThreadId,            // 0x4    | Uint32    | Thread Id.
    uint   EventClass,          // 0x8    | Uint32    | Event class - see ```class EventClass(enum.IntEnum)``` in [consts.py](../procmon_parser/consts.py)
    ushort Operation,           // 0xC    | Uint16    | Operation type - see `ProcessOperation`, `RegistryOperation`, `NetworkOperation`, `FilesystemOperation` in [consts.py](../procmon_parser/consts.py)
    ushort Dummy0,              // 0xE    | Byte[6]   | Unknown.
    uint   Dummy1,
    ulong  Duration,            // 0x14   | Uint64    | Duration of the operation in 100 nanoseconds interval.
    ulong  CaptureTime,         // 0x1C   | FILETIME  | The time when the event was captured.
    uint   Result,              // 0x24   | Uint32    | The value of the event result.
    ushort StackTraceDepth,     // 0x28   | Uint16    | The depth of the captured stack trace.
    ushort Dummy2,              // 0x2A   | Uint16    | Unknown
    uint   DetailsSize,         // 0x2C   | Uint32    | The size of the specific **detail** structure (contains path and other details)
    uint   ExtraDetailsOffset); // 0x30   | Uint32    | The offset from the start of the event to **extra detail** structure (not necessarily continuous with this structure).
                                // 0x34   | PVoid[]   | Array of the addresses of the stack frames.
                                // 0x34+n | Byte[]    | A **detail** structure based on the operation type.
// ReSharper restore NotAccessedPositionalProperty.Global

record struct PmlEventInit(int EventIndex, PmlRawEvent RawEvent, long[]? Frames);

[PublicAPI, DebuggerDisplay("#{EventIndex}")]
public class PmlEvent
{
    readonly long _duration;

    public readonly int     EventIndex;
    public readonly int     ProcessIndex;
    public readonly int     ThreadId;
    public readonly long    CaptureTime;  // FILETIME (100ns intervals since 1601-01-01 UTC)
    public readonly int     Result;       // HRESULT (probably)
    public readonly long[]? Frames;

    public DateTime CaptureDateTimeUtc => DateTime.FromFileTimeUtc(CaptureTime);
    public DateTime CaptureDateTime => DateTime.FromFileTime(CaptureTime);
    public TimeSpan DurationSpan => new(_duration);

    internal PmlEvent(PmlEventInit init)
    {
        EventIndex   = init.EventIndex;
        ProcessIndex = (int)init.RawEvent.ProcessIndex;
        ThreadId     = (int)init.RawEvent.ThreadId;
        CaptureTime  = (long)init.RawEvent.CaptureTime;
        _duration    = (long)init.RawEvent.Duration;
        Result       = (int)init.RawEvent.Result;
        Frames       = init.Frames;
    }
}

[PublicAPI]
public class PmlProcessEvent : PmlEvent
{
    internal const int EventClass = 1;

    internal PmlProcessEvent(PmlEventInit init) : base(init) {}
}

[PublicAPI]
public class PmlRegistryEvent : PmlEvent
{
    internal const int EventClass = 2;

    internal PmlRegistryEvent(PmlEventInit init) : base(init) {}
}

[PublicAPI]
public class PmlFileSystemEvent : PmlEvent
{
    protected readonly FileSystemOperation FileSystemOperation;

    internal const int EventClass = 3;

    public virtual object Operation => FileSystemOperation;

    internal PmlFileSystemEvent(PmlEventInit init) : base(init)
    {
        FileSystemOperation = (FileSystemOperation)init.RawEvent.Operation;
    }
}

[PublicAPI]
public class PmlProfilingEvent : PmlEvent
{
    internal const int EventClass = 4;

    internal PmlProfilingEvent(PmlEventInit init) : base(init) {}
}

[PublicAPI]
public class PmlNetworkEvent : PmlEvent
{
    internal const int EventClass = 5;

    internal PmlNetworkEvent(PmlEventInit init) : base(init) {}
}

[PublicAPI]
public class PmlFileSystemDetailedEvent : PmlFileSystemEvent
{
    readonly byte _subOperation;

    public readonly NPath Path;

    internal PmlFileSystemDetailedEvent(PmlEventInit init, byte subOperation, string path) : base(init)
    {
        _subOperation = subOperation;
        Path = path;
    }

    public override object Operation => FileSystemOperation switch
    {
        FileSystemOperation.QueryVolumeInformation when _subOperation != 0 => (QueryVolumeInformationOperation)_subOperation,
        FileSystemOperation.SetVolumeInformation   when _subOperation != 0 => (SetVolumeInformationOperation)  _subOperation,
        FileSystemOperation.QueryInformationFile   when _subOperation != 0 => (QueryInformationFileOperation)  _subOperation,
        FileSystemOperation.SetInformationFile     when _subOperation != 0 => (SetInformationFileOperation)    _subOperation,
        FileSystemOperation.DirectoryControl       when _subOperation != 0 => (DirectoryControlOperation)      _subOperation,
        FileSystemOperation.PlugAndPlay            when _subOperation != 0 => (PlugAndPlayOperation)           _subOperation,
        FileSystemOperation.LockUnlockFile         when _subOperation != 0 => (LockUnlockFileOperation)        _subOperation,
        _ => base.Operation
    };
}

[PublicAPI]
public class PmlFileSystemReadWriteEvent : PmlFileSystemDetailedEvent
{
    public readonly FileOperationIoFlags  FileOperationIoFlags;
    public readonly FileOperationPriority FileOperationPriority;
    public readonly long                  Length;
    public readonly long                  Offset;

    internal PmlFileSystemReadWriteEvent(
        PmlEventInit init, byte subOperation, string path,
        FileOperationIoFlags fileOperationIoFlags, FileOperationPriority fileOperationPriority,
        long length, long offset)
        : base(init, subOperation, path)
    {
        FileOperationIoFlags  = fileOperationIoFlags;
        FileOperationPriority = fileOperationPriority;
        Length                = length;
        Offset                = offset;
    }
}
