using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

static class FileOps
{
    [InitializeOnLoadMethod]
    static void DoFileOps()
    {
        // do some dir walking

        var files = Directory.GetFiles(".", "*", SearchOption.AllDirectories);
        Debug.Log($"Directory enumeration: {files.Length}");

        // trigger some reads (depending on buffering)

        static string GetThisFile([CallerFilePath] string path = "") => Path.GetFileName(path);
        var thisFile = GetThisFile();

        var match = files.Single(f => string.Equals(Path.GetFileName(f), thisFile, StringComparison.OrdinalIgnoreCase));

        var bytes = 0;
        var data = new byte[10];

        using (var stream = File.OpenRead(match))
        {
            for (;;)
            {
                var read = stream.Read(data, 0, data.Length);
                if (read == 0)
                    break;
                bytes += read;
            }
        }

        Debug.Log($"File size: {bytes} (size on disk is {new FileInfo(match).Length})");
    }
}
