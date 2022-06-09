using System.ComponentModel;
using System.Runtime.InteropServices;

namespace OkTools.Core;

public static class FileStreamNativeExtensions
{
    // originally from https://stackoverflow.com/a/54657277/14582

    // TODO: contribute upstream to https://github.com/dotnet/pinvoke/tree/main/src/Kernel32
    static class Native
    {
        // ReSharper disable InconsistentNaming

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetFileInformationByHandleEx(IntPtr hFile, int FileInformationClass, IntPtr lpFileInformation, uint dwBufferSize);

        public struct FILE_STANDARD_INFO
        {
            #pragma warning disable CS0649
            public long AllocationSize;
            public long EndOfFile;
            public uint NumberOfLinks;
            public byte DeletePending;
            public byte Directory;
            #pragma warning restore CS0649
        }

        public const int FileStandardInfo = 1;

        // ReSharper restore InconsistentNaming
    }

    // use this when opening a FileStream with FileShare.Delete. if someone deletes the underlying file, the stream will
    // just report that it's at eof and give no errors, but this function will detect that case.
    public static bool WasFileDeleted(this FileStream @this)
    {
        var size = Marshal.SizeOf<Native.FILE_STANDARD_INFO>();
        var buf = Marshal.AllocHGlobal(size);
        
        try
        {
            var handle = @this.SafeFileHandle.DangerousGetHandle();
            if (!Native.GetFileInformationByHandleEx(handle, Native.FileStandardInfo, buf, (uint)size))
                throw new Win32Exception("GetFileInformationByHandleEx() failed");

            var info = Marshal.PtrToStructure<Native.FILE_STANDARD_INFO>(buf);
            return info.DeletePending != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }
}
