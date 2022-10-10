using System.Runtime.InteropServices;
using System.Text;
using OkTools.ProcMonUtils.FileSystem;

// PML format: https://github.com/eronnen/procmon-parser/blob/master/docs/PML%20Format.md

namespace OkTools.ProcMonUtils;

public sealed class PmlReader : IDisposable
{
    readonly BinaryReader _reader;
    readonly int _eventCount;
    readonly long _eventOffsetsOffset;
    readonly Dictionary<int, PmlProcess> _processesByPmlIndex = new();
    readonly string[] _strings;

    public void Dispose() => _reader.Dispose();

    public NPath PmlPath { get; }
    public int EventCount => _eventCount;

    public PmlReader(NPath pmlPath)
    {
        PmlPath = pmlPath;
        _reader = new BinaryReader(new FileStream(pmlPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete), Encoding.Unicode);

        if (_reader.ReadByte() != 'P' || // Signature - "PML_"
            _reader.ReadByte() != 'M' ||
            _reader.ReadByte() != 'L' ||
            _reader.ReadByte() != '_')
        {
            throw new FileLoadException("Not a PML file", PmlPath);
        }

        const int expectedVersion = 9;
        var version = _reader.ReadInt32();
        if (version != expectedVersion) // The version of the PML file. I assume its 9
            throw new FileLoadException($"PML has version {version}, expected {expectedVersion}", PmlPath);

        var is64Bit = _reader.ReadInt32(); // 1 if the system is 64 bit, 0 otherwise
        if (is64Bit != 1)
            throw new FileLoadException("PML must be 64-bit", PmlPath);

        SeekCurrent(
            0x20 +  // The computer name
            0x208); // The system root path (like "C:\Windows")

        _eventCount = _reader.ReadInt32();

        SeekCurrent(8); // Unknown
        var eventsDataOffset = _reader.ReadInt64();
        _eventOffsetsOffset = _reader.ReadInt64();

        // don't pskill your procmon or it won't update the header; use /terminate instead
        if (_eventOffsetsOffset == 0)
            throw new FileLoadException("File was not closed cleanly during capture and is corrupt", PmlPath);

        var processesOffset = _reader.ReadInt64();
        var stringsOffset = _reader.ReadInt64();

        // minor sanity check
        SeekBegin(_eventOffsetsOffset);
        var eventOffset0 = _reader.ReadInt32();
        if (eventOffset0 != eventsDataOffset)
            throw new FileLoadException($"PML has mismatched first event offset ({eventOffset0} and {eventsDataOffset})", PmlPath);

        _strings = ReadStringData(stringsOffset);
        ReadProcessData(processesOffset);
    }

    string[] ReadStringData(long stringsOffset)
    {
        SeekBegin(stringsOffset);

        var stringDataOffsets = new int[_reader.ReadInt32()];
        for (var istring = 0; istring < stringDataOffsets.Length; ++istring)
            stringDataOffsets[istring] = _reader.ReadInt32();
        var strings = new string[stringDataOffsets.Length];
        for (var istring = 0; istring < stringDataOffsets.Length; ++istring)
        {
            SeekBegin(stringsOffset + stringDataOffsets[istring]);
            var strlen = _reader.ReadInt32() / 2;
            strings[istring] = new string(_reader.ReadChars(strlen), 0, Math.Max(0, strlen - 1)); // drop null-term, except empty string that doesn't include one (don't know why)
        }

        return strings;
    }

    void ReadProcessData(long processesOffset)
    {
        SeekBegin(processesOffset);

        var processCount = _reader.ReadInt32();
        SeekCurrent(processCount * 4); // jump over the process indexes array
        var processDataOffsets = new int[processCount];
        for (var iprocess = 0; iprocess < processDataOffsets.Length; ++iprocess)
            processDataOffsets[iprocess] = _reader.ReadInt32();
        PmlProcess? systemProcess = null;
        for (var iprocess = 0; iprocess < processDataOffsets.Length; ++iprocess)
        {
            var processIndex = _reader.ReadInt32(); // The process index (for events to use as a reference to the process)
            var processId = _reader.ReadInt32(); // Process id

            SeekCurrent(
                4 + // Parent process id
                4 + // Unknown
                8 + // Authentication id
                4 + // Session number
                4 + // Unknown
                8 + // The starting time of the process.
                8 + // The ending time of the process.
                4 + // 1 if the process is virtualized, 0 otherwise.
                4 + // 1 if this process is 64 bit, 0 if WOW64.
                4 + // Integrity - as a string index
                4); // the user - as a string index

            var processName = _strings[_reader.ReadInt32()]; // the process name - as a string index

            SeekCurrent(
                4 + // the image path - as a string index
                4 + // the command line - as a string index
                4 + // company of the executable - as a string index
                4 + // version of the executable - as a string index
                4 + // description of the executable - as a string index
                4 + // Icon index small (0x10 pixels)
                4 + // Icon index big (0x20 pixels)
                8); // Unknown

            var moduleCount = _reader.ReadInt32(); // number of modules in the process
            var totalModuleCount = moduleCount;
            if (systemProcess != null)
                totalModuleCount += systemProcess.Modules.Count;

            var modules = new PmlModule[totalModuleCount];
            for (var imodule = 0; imodule < moduleCount; ++imodule)
            {
                SeekCurrent(8); // Unknown

                var baseAddress = _reader.ReadUInt64(); // Base address of the module.
                var size = _reader.ReadInt32(); // Size of the module.
                var imagePath = _strings[_reader.ReadInt32()]; // image path - as a string index
                modules[imodule] = new PmlModule(imagePath, new AddressRange(baseAddress, size));

                SeekCurrent(
                    4 + // version of the executable - as a string index
                    4 + // company of the executable - as a string index
                    4 + // description of the executable - as a string index
                    4 + // timestamp of the executable
                    8 * 3); // Unknown
            }

            // kernel modules are loaded into every process, and procmon only records them in the System process. just drop them in each process too, and don't care about the duplication.
            if (systemProcess != null)
            {
                for (var i = 0; i < systemProcess.Modules.Count; ++i)
                    modules[moduleCount + i] = systemProcess.Modules[i];
            }

            var process = new PmlProcess(processId, processName, modules);
            _processesByPmlIndex.Add(processIndex, process);

            // remember the System process because it contains all the kernel modules
            if (process.ProcessName == "System")
                systemProcess = process;
        }
    }

    public IEnumerable<PmlProcess> Processes => _processesByPmlIndex.Values;

    [Flags]
    public enum Filter
    {
        Process    = 1 << 0,
        Registry   = 1 << 1,
        FileSystem = 1 << 2,
        Profiling  = 1 << 3,
        Network    = 1 << 4,
        AllEventClasses = Process | Registry | FileSystem | Profiling | Network,

        Stacks     = 1 << 5,
        Details    = 1 << 6,

        Everything = AllEventClasses | Stacks | Details,
    }

    public IEnumerable<PmlEvent> SelectEvents(Filter filter = Filter.Everything, Range? range = null)
    {
        var startAtIndex = 0;
        var count = _eventCount;

        if (range != null)
            (startAtIndex, count) = range.Value.GetOffsetAndLength(count);

        var offsets = new long[count];
        SeekBegin(_eventOffsetsOffset);
        SeekCurrent(startAtIndex * 5);
        for (var ievent = 0; ievent < offsets.Length; ++ievent)
        {
            offsets[ievent] = _reader.ReadInt32();
            SeekCurrent(1); // Unknown flags
        }

        for (var ioffset = 0; ioffset < offsets.Length; ++ioffset)
        {
            var eventIndex = ioffset + startAtIndex;
            var eventStartOffset = offsets[ioffset];
            SeekBegin(eventStartOffset);

            var rawEvent = ReadRawEvent();

            if (rawEvent.EventClass switch
            {
                PmlProcessEvent.EventClass    => (filter & Filter.Process)    == 0,
                PmlRegistryEvent.EventClass   => (filter & Filter.Registry)   == 0,
                PmlFileSystemEvent.EventClass => (filter & Filter.FileSystem) == 0,
                PmlProfilingEvent.EventClass  => (filter & Filter.Profiling)  == 0,
                PmlNetworkEvent.EventClass    => (filter & Filter.Network)    == 0,
                _ => true,
            }) continue;

            ulong[]? frames = null;
            if ((filter & Filter.Stacks) != 0)
                frames = ReadArray<ulong>(rawEvent.StackTraceDepth);
            else
                SeekCurrent(rawEvent.StackTraceDepth * 8);

            var pmlInit = new PmlEventInit(eventIndex, rawEvent, frames);
            PmlEvent? pmlEvent = null;

            if ((filter & Filter.Details) != 0 && rawEvent.DetailsSize != 0)
            {
                #pragma warning disable CS8509
                pmlEvent = rawEvent.EventClass switch
                #pragma warning restore CS8509
                {
                    //PmlProcessEvent.EventClass    => ,
                    //PmlRegistryEvent.EventClass   => ,
                    PmlFileSystemEvent.EventClass => ReadFileSystemDetailedEvent(eventStartOffset, pmlInit),
                    //PmlProfilingEvent.EventClass  => ,
                    //PmlNetworkEvent.EventClass    => ,
                };
            }

            yield return pmlEvent ?? new PmlEvent(pmlInit);
        }
    }

    public IEnumerable<PmlEvent> SelectEvents(Range eventRange) => SelectEvents(Filter.Everything, eventRange);

    public PmlEvent GetEvent(int index)
    {
        var pmlEvent = SelectEvents(Range.StartAt(index)).First();
        if (pmlEvent.EventIndex != index)
            throw new InvalidOperationException("Unexpected mismatch of found event and requested index");
        return pmlEvent;
    }

    public PmlProcess ResolveProcess(int processIndex) => _processesByPmlIndex[processIndex];

    unsafe PmlRawEvent ReadRawEvent()
    {
        var rawEvent = new PmlRawEvent();

        if (_reader.BaseStream.Read(new Span<byte>(&rawEvent, sizeof(PmlRawEvent))) != sizeof(PmlRawEvent))
            throw new IOException("Unexpected EOF");

        return rawEvent;
    }

    unsafe T[] ReadArray<T>(int count) where T : unmanaged
    {
        var array = new T[count];
        if (count == 0)
            return array;

        if (_reader.BaseStream.Read(MemoryMarshal.Cast<T, byte>(array)) != sizeof(T)*count)
            throw new IOException("Unexpected EOF");

        return array;
    }

    record StringInfo(bool IsAscii, int Length)
    {
        public StringInfo(ushort raw) : this((raw & 0x8000) != 0, raw & 0x7fff) {}
    }

    StringInfo ReadDetailStringInfo() => new(_reader.ReadUInt16());

    string ReadDetailString(StringInfo stringInfo)
    {
        if (!stringInfo.IsAscii)
            throw new ArgumentException("Only ascii supported currently");
            //return read_utf16(io, character_count * 2)

        return Encoding.ASCII.GetString(_reader.ReadBytes(stringInfo.Length));
    }

    public PmlProcess? FindProcessByProcessId(int processId) =>
        _processesByPmlIndex.Values.FirstOrDefault(p => p.ProcessId == processId);

    PmlFileSystemDetailedEvent ReadFileSystemDetailedEvent(long eventStartOffset, PmlEventInit init)
    {
        var subOperation = _reader.ReadByte();
        SeekCurrent(3); // padding

        var detailsPathPos = _reader.BaseStream.Position + 8*5 + 0x14; // past the filesystem details block

        string SeekAndReadDetailsPath()
        {
            SeekBegin(detailsPathPos);
            var pathInfo = ReadDetailStringInfo();
            SeekCurrent(2); // padding
            return ReadDetailString(pathInfo);
        }

        switch ((FileSystemOperation)init.RawEvent.Operation)
        {
            case FileSystemOperation.CreateFile:
                // TODO: get_filesystem_create_file_details
                break;

            case FileSystemOperation.ReadFile:
            case FileSystemOperation.WriteFile:
                SeekCurrent(4);
                var ioFlagsAndPriority = _reader.ReadInt32();
                var ioFlags = (FileOperationIoFlags)(ioFlagsAndPriority & 0xe000ff);
                var priority = (FileOperationPriority)((ioFlagsAndPriority >> 0x11) & 7);
                SeekCurrent(4);
                var length = _reader.ReadInt64();
                SeekCurrent(8);
                var offset = _reader.ReadInt64();

                if (init.RawEvent.ExtraDetailsOffset != 0)
                {
                    SeekBegin(eventStartOffset + init.RawEvent.ExtraDetailsOffset);
                    var extraSize = _reader.ReadInt16();
                    var extraPos = _reader.BaseStream.Position;

                    length = _reader.ReadInt32();

                    if (_reader.BaseStream.Position > extraPos + extraSize)
                        throw new InvalidOperationException("Unexpected offset");
                }

                return new PmlFileSystemReadWriteEvent(
                    init, subOperation, SeekAndReadDetailsPath(),
                    ioFlags, priority, length, offset);

            case FileSystemOperation.FileSystemControl:
            case FileSystemOperation.DeviceIoControl:
                // TODO: get_filesystem_ioctl_details
                break;

            case FileSystemOperation.DirectoryControl:
                var directoryControlOperation = (DirectoryControlOperation)subOperation;
                if (directoryControlOperation == DirectoryControlOperation.QueryDirectory)
                {
                    // TODO: get_filesystem_query_directory_details
                }
                else if (directoryControlOperation == DirectoryControlOperation.NotifyChangeDirectory)
                {
                    // TODO: get_filesystem_notify_change_directory_details
                }
                break;

            case FileSystemOperation.QueryInformationFile:
                var queryInformationFileOperation = (QueryInformationFileOperation)subOperation;
                if (queryInformationFileOperation is QueryInformationFileOperation.QueryIdInformation or QueryInformationFileOperation.QueryRemoteProtocolInformation)
                {
                    // TODO: get_filesystem_read_metadata_details
                }
                break;

            case FileSystemOperation.SetInformationFile when (SetInformationFileOperation)subOperation is SetInformationFileOperation.SetDispositionInformationFile:
                // TODO: get_filesystem_setdispositioninformation_details
                break;
        }

        return new PmlFileSystemDetailedEvent(init, subOperation, SeekAndReadDetailsPath());
    }

    void SeekBegin(long offset) => _reader.BaseStream.Seek(offset, SeekOrigin.Begin);
    void SeekCurrent(int offset) => _reader.BaseStream.Seek(offset, SeekOrigin.Current);
}
