using System.Diagnostics;

namespace OkTools.Unity;

/// <summary>
/// The entry point to Unity-related queries.
/// </summary>
[PublicAPI]
public static class Unity
{
    // TODO: consider if we should auto-add common subpaths like 'Editor' or for build like 'build/*Editor*/*/*'
    // but probably have a pathspec prefix option to disable this automation, if it's not opt-in..
    static IEnumerable<UnityToolchain> FindToolchains(NPath pathSpec, UnityToolchainOrigin? origin, bool throwOnInvalidPathSpec) => Globbing
        .Find(pathSpec, UnityConstants.UnityExeName, throwOnInvalidPathSpec)
        .Select(p => UnityToolchain.TryCreateFromPath(p.Parent, origin)) // drop filename for TryCreateFromPath, which will add it back (see Globbing.Find for why we must do this hack)
        .WhereNotNull();

    // this just walks the default install dir, but the user may have customized where the hub goes, which affects
    // its default install path for unity installations it manages.
    //
    // TODO: include user-customized Hub install folder if set
    //   * %APPDATA%\UnityHub\secondaryInstallPath.json contains a quoted string
    //   * this is empty if the user hasn't customized "installs location" in hub prefs
    //   * need to test and detect the sub-spec to use..also Editor/*/Editor..?
    //
    // TODO: include Hub-known custom installs
    //   * %APPDATA%\Roaming\UnityHub\editors.json contains all installs manually added to Hub
    public static IEnumerable<UnityToolchain> FindHubInstalledToolchains() =>
        FindToolchains(UnityConstants.HubInstalledToolchainNPathSpec, UnityToolchainOrigin.UnityHub, false);

    // this just walks the default install dir
    // TODO: go walk the installer stuff in the registry and discover all manually installations that way.
    public static IEnumerable<UnityToolchain> FindManuallyInstalledToolchains() =>
        FindToolchains(UnityConstants.ManuallyInstalledToolchainsNPathSpec, UnityToolchainOrigin.ManuallyInstalled, false);

    public static IEnumerable<UnityToolchain> FindCustomToolchains(string pathSpec, bool throwOnInvalidPathSpec) =>
        FindToolchains(pathSpec.ToNPath(), null, throwOnInvalidPathSpec);

    public static IReadOnlyList<Process> FindUnityProcessesForProject(string projectPath)
        => FindUnityProcessesForProject(projectPath.ToNPath());
    internal static IReadOnlyList<Process> FindUnityProcessesForProject(NPath projectPath)
    {
        var matches = new List<Process>();

        foreach (var unityProcess in Process.GetProcessesByName(UnityConstants.UnityProcessName))
        {
            var workingDir = NativeWindows.SafeGetProcessCurrentDirectory(unityProcess.Id)?.ToNPath();
            if (workingDir == projectPath)
                matches.Add(unityProcess);
            else
                unityProcess.Dispose();
        }

        return matches;
    }

    public static Process? TryFindMainUnityProcess(IEnumerable<Process> unityProcesses)
    {
        foreach (var unityProcess in unityProcesses.Where(p => p.MainWindowHandle != IntPtr.Zero))
        {
            var unityCommandLine = NativeWindows.SafeGetProcessCommandLine(unityProcess.Id);
            if (unityCommandLine == null)
                continue;

            var unityArgs = CliUtility.ParseCommandLineArgs(unityCommandLine);
            if (unityArgs.Any(a => a.EqualsIgnoreCase("-batchmode") || a.EqualsIgnoreCase("-ump")))
                continue;

            return unityProcess;
        }

        return null;
    }

    #if NOTYET
    public static bool TryParseUnityHubUrl(string unityHubUrl)
    {
        // example: unityhub://2020.3.25f1-foo/123456789ab << last part is the hash, first part is the build

        // what the hub does..
        // 1. download "https://download.unity3d.com/download_unity/123456789ab/Windows64EditorInstaller/UnitySetup64-2020.3.25f1-foo.exe"
        //    to "C:\Users\scott\AppData\Local\Temp\unityhub-62a10bf0-9324-11ec-8e36-fd2c0e80f6f8\UnitySetup64-2020.3.25f1-foo.exe"
        // 1a. additional modules have custom installers like "UnitySetup-WebGL-Support-for-Editor-2020.3.25f1-foo.exe" or a zip like "UnityDocumentation.zip"
        //     and they will go to peer guid-named folders
        // 2. run that exe with "/S /D=C:\Program Files\Unity\Hub\Editor\2020.3.25f1-foo"
        //    ^ this causes problems when we have different hashes but otherwise same versions
        // 3. installer runs (it will do an unnecessary UAC popup if path is to a user-controlled folder) and hub waits for it to complete
    }
    #endif
}
