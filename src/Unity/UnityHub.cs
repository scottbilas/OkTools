using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace OkTools.Unity;

[SupportedOSPlatform("windows")]
public static class UnityHub
{
    /// <summary>
    /// Look for Unity Hub processes and kill any that are found.
    /// </summary>
    /// <returns>Number of processes found and killed.</returns>
    public static int KillHubIfRunning(bool dryRun)
    {
        var processes = Process.GetProcessesByName("Unity Hub").ToArray();
        if (!dryRun)
        {
            // TODO: make a utility method that will refresh the tray icons. some ideas:
            // https://www.codeproject.com/Articles/19620/LP-TrayIconBuster
            // http://maruf-dotnetdeveloper.blogspot.com/2012/08/c-refreshing-system-tray-icon.html
            // https://forums.codeguru.com/showthread.php?150081-Forcing-the-System-Tray-to-redraw

            foreach (var process in processes)
                process.Kill();
        }
        return processes.Length;
    }

    public enum HubHiddenState
    {
        NotFound,
        AlreadyHidden,
        NowHidden,
        NeedSudo,
    }

    /// <summary>
    /// Do whatever hacks are required to prevent Unity from finding the Hub, which it uses to auto-launch it.
    /// If `dryRun` is true, it won't make any changes, just will return the status.
    /// </summary>
    public static HubHiddenState HideHubFromUnity(bool dryRun)
    {
        var hubInfoPath = NPath.RoamingAppDataDirectory.Combine("UnityHub", "hubInfo.json");
        var hubInfoBakPath = hubInfoPath.ChangeExtension(".json.bak");

        var state = HubHiddenState.NotFound;

        if (hubInfoPath.FileExists())
        {
            if (!dryRun)
                File.Move(hubInfoPath, hubInfoBakPath, true);

            state = HubHiddenState.NowHidden;
        }
        else if (hubInfoBakPath.FileExists())
            state = HubHiddenState.AlreadyHidden;

        try
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Unity Technologies - Hub";

            using var readKey = Registry.LocalMachine.OpenSubKey(keyPath);
            if (readKey != null)
            {
                var value = readKey.GetValue("UninstallString");
                if (value != null)
                {
                    using var writeKey = Registry.LocalMachine.OpenSubKey(keyPath, true)!; // force a security exception even in dry-run, so caller will know they need sudo
                    if (!dryRun)
                    {
                        writeKey.SetValue("UninstallString.bak", value);
                        writeKey.DeleteValue("UninstallString");
                    }

                    state = HubHiddenState.NowHidden;
                }
                else if (readKey.GetValue("UninstallString.bak") != null && state == HubHiddenState.NotFound)
                    state = HubHiddenState.AlreadyHidden;
            }
        }
        catch (SecurityException)
        {
            return HubHiddenState.NeedSudo;
        }

        return state;
    }
}
