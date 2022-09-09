using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using OkTools.ProcMonUtils.FileSystem;

namespace OkTools.ProcMonUtils;

[PublicAPI, DebuggerDisplay("{ProcessName}")]
public class PmlProcess
{
    public readonly uint ProcessId;
    public readonly string ProcessName;
    public IReadOnlyList<PmlModule> Modules => _modules;

    readonly PmlModule[] _modules;

    public PmlProcess(uint processId, string processName, PmlModule[] takeModules)
    {
        ProcessId = processId;
        ProcessName = processName;
        _modules = takeModules;

        // keep sorted for bsearch
        Array.Sort(_modules, (a, b) => a.Address.Base.CompareTo(b.Address.Base));
    }

    public bool TryFindModule(ulong address, [NotNullWhen(returnValue: true)] out PmlModule? module) =>
        _modules.TryFindAddressIn(address, out module);
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

record struct PmlEventInit(uint EventIndex, PmlRawEvent RawEvent, ulong[]? Frames);

[PublicAPI, DebuggerDisplay("#{EventIndex}")]
public class PmlEvent
{
    readonly ulong _duration;

    public readonly uint     EventIndex;
    public readonly uint     ProcessIndex;
    public readonly uint     ThreadId;
    public readonly ulong    CaptureTime;  // FILETIME (100ns intervals since 1601-01-01 UTC)
    public readonly uint     Result;       // HRESULT (probably)
    public readonly ulong[]? Frames;

    public DateTime CaptureDateTime => DateTime.FromFileTime((long)CaptureTime);
    public TimeSpan DurationSpan => new((long)_duration);

    internal PmlEvent(PmlEventInit init)
    {
        EventIndex   = init.EventIndex;
        ProcessIndex = init.RawEvent.ProcessIndex;
        ThreadId     = init.RawEvent.ThreadId;
        CaptureTime  = init.RawEvent.CaptureTime;
        _duration    = init.RawEvent.Duration;
        Result       = init.RawEvent.Result;
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
    public readonly ulong                 Length;
    public readonly long                  Offset;

    internal PmlFileSystemReadWriteEvent(
        PmlEventInit init, byte subOperation, string path,
        FileOperationIoFlags fileOperationIoFlags, FileOperationPriority fileOperationPriority,
        ulong length, long offset)
        : base(init, subOperation, path)
    {
        FileOperationIoFlags  = fileOperationIoFlags;
        FileOperationPriority = fileOperationPriority;
        Length                = length;
        Offset                = offset;
    }
}
