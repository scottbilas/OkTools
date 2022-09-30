using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.DbgHelp;

namespace OkTools.ProcMonUtils;

// this is a cut down DebugHelp.SymbolHandler that doesn't do anything with slow exceptions
public sealed class SimpleSymbolHandler : IDisposable
{
    static int s_serial;
    readonly HPROCESS _handle;

    static SimpleSymbolHandler()
    {
        // system32 has a dbghelp.dll, but we also want symsrv.dll to enable auto-download of symbols if the
        // user has _NT_SYMBOL_PATH set up to do so. i copied dbghelp.dll/symsrv.dll out of C:\Program Files (x86)\Windows Kits\10\Debuggers\x64.
        // (there's probably a better way to do this..)

        // recursive check because debug build is in a subdir of builds folder
        var dbgHelpPath = Assembly.GetExecutingAssembly().Location.ToNPath().ParentContaining("dbghelp.dll", true);
        if (dbgHelpPath != null!)
            NativeLibrary.Load(dbgHelpPath);
    }

    public SimpleSymbolHandler(string? ntSymbolPath = null)
    {
        _handle = new IntPtr(++s_serial);

        var options = SYMOPT.SYMOPT_UNDNAME | SYMOPT.SYMOPT_LOAD_LINES;
        if (Debugger.IsAttached)
            options |= SYMOPT.SYMOPT_DEBUG;

        SymSetOptions(options);

        if (!SymInitialize(_handle, ntSymbolPath, false))
            throw new Win32Exception();
    }

    public void Dispose()
    {
        SymCleanup(_handle);
    }

    // FUTURE: check the DLL at `imageName` against a fingerprint. We don't get a checksum in the PML, but can do the
    // next best thing at least, which is check size, version, timestamp.
    public Win32Error LoadModule(string imageName, ulong dllBase = 0, bool skipSymbols = false)
    {
        var flags = skipSymbols ? SLMFLAG.SLMFLAG_NO_SYMBOLS : 0;

        var rc = SymLoadModuleEx(_handle, HFILE.NULL, imageName, null, dllBase, 0, default, flags) != 0
            ? Win32Error.ERROR_SUCCESS
            : Win32Error.GetLastError(); // note that a zero return but ERROR_SUCCESS after that is how SymLoadModuleEx signifies "already loaded"

        // recommendation from msdn docs is to always call this after loading the module to force a deferred load to
        // happen. not caring for now whether it succeeds (it won't for crowdstrike files for example).
        var moduleInfo = new IMAGEHLP_MODULE64();
        moduleInfo.SizeOfStruct = (uint)Marshal.SizeOf(moduleInfo);
        SymGetModuleInfo64(_handle, dllBase, ref moduleInfo);

        return rc;
    }

    public Win32Error GetSymbolFromAddress(ulong address, ref SYMBOL_INFO2 symbol, out ulong offset)
    {
        symbol.MaxNameLen = 500;
        symbol.SizeOfStruct = (uint)Marshal.SizeOf(symbol) - symbol.MaxNameLen*2;
        var rc = SymFromAddr(_handle, address, out offset, ref symbol)
            ? Win32Error.ERROR_SUCCESS
            : Win32Error.GetLastError();
        return rc;
    }

    // TODO: remove after https://github.com/dahall/Vanara/discussions/324#discussioncomment-3764178 makes it to nuget
    [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SymFromAddr(HPROCESS hProcess, ulong Address, out ulong Displacement, ref SYMBOL_INFO2 Symbol);

}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
#pragma warning disable CA1707
public struct SYMBOL_INFO2
#pragma warning restore CA1707
{
    public uint SizeOfStruct;
    public uint TypeIndex;
    public ulong Reserved0;
    ulong Reserved1;
    public uint Index;
    public uint Size;
    public ulong ModBase;
    public SYMFLAG Flags;
    public ulong Value;
    public ulong Address;
    public uint Register;
    public uint Scope;
    public uint Tag;
    public uint NameLen;
    public uint MaxNameLen;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 500)]
    public string Name;
}
