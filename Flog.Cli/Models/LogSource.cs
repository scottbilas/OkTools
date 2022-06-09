using System.Threading.Channels;
using OkTools.Core;

static class LogSource
{
    public static async Task TailFileAsync(string path, ChannelWriter<LogChange> writer, CancellationToken token)
    {
        StreamReader? reader = null;
        try
        {
            // TODO: LogChange needs a way to mark up status and errors from the engine itself (LogChange.LogType?)
            await writer.WriteAsync(new[] { $"! Waiting for file {path}..." }, token);
            reader = await OpenFileAsync(path, token);
            await writer.WriteAsync(LogChange.Clear, token);

            var eof = reader.BaseStream.Length;
            while (!token.IsCancellationRequested)
            {
                if (reader.BaseStream.Length == eof)
                {
                    // TODO: option to decide how to handle file deleted underneath us.. show eof? restart? error? status update?
                    if (((FileStream)reader.BaseStream).WasFileDeleted())
                    {
                        await writer.WriteAsync(new[] { "", $"! File {path} was deleted!" }, token);
                        return;
                    }

                    await Task.Delay(100, token);
                    continue;
                }

                // TODO: option to decide how to handle a file truncation (such as when overwriting a file without deleting first) - can detect by seeing if new eof < old eof

                reader.BaseStream.Seek(eof, SeekOrigin.Begin);

                // TODO: use ordinary FileStream and process directly instead of doing multiple async ReadLines (loads of overhead there..)
                //       (and also a partially written line will show up as >= two lines in this..)

                // TODO: implement partial line continuation..will require a bool LogChange.IsContinuation

                while (await reader.ReadLineAsync() is { } line) // TODO: batches (though using 4k blocks or whatever will also throttle just fine)
                    await writer.WriteAsync(new[] { line }, token);

                eof = reader.BaseStream.Position;
            }
        }
        finally
        {
            writer.Complete();
            reader?.Dispose();
        }
    }

    static async Task<StreamReader> OpenFileAsync(string path, CancellationToken token)
    {
        for (;;)
        {
            token.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                try
                {
                    return new StreamReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
                }
                catch (FileNotFoundException) { /* we hit a race condition */ }
            }

            await Task.Delay(100, token);
        }
    }
}
