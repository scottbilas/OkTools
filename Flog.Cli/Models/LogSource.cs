using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Channels;

static class LogSource
{
    public static async Task TailFileAsync(string path, ChannelWriter<LogChange> writer, CancellationToken cancel)
    {
        try
        {
            var pipe = new Pipe();
            await Task.WhenAll(
                TailFileBytesAsync(path, writer, pipe.Writer, cancel),
                ReadLinesAsync(writer, pipe.Reader, cancel));
        }
        finally
        {
            writer.Complete();
        }
    }

    static async Task TailFileBytesAsync(string path, ChannelWriter<LogChange> logWriter, PipeWriter pipeWriter, CancellationToken cancel)
    {
        await logWriter.WriteAsync(new[] { $"! Waiting for file {path}..." }, cancel);
        await using var file = await OpenFileAsync(path, cancel);
        await logWriter.WriteAsync(LogChange.Clear, cancel);

        var eof = 0L;
        while (!cancel.IsCancellationRequested)
        {
            if (file.Length == eof)
            {
                // TODO: option to decide how to handle file deleted underneath us.. show eof? restart? error? status update?
                if (file.WasFileDeleted())
                {
                    await logWriter.WriteAsync(new[] { "", $"! File {path} was deleted!" }, cancel);
                    return;
                }

                await Task.Delay(100, cancel);
                continue;
            }

            // TODO: how to handle file truncation? (such as when overwriting a file without deleting first) - can detect by seeing if new eof < old eof

            while (!cancel.IsCancellationRequested)
            {
                var block = pipeWriter.GetMemory(4096);
                var read = await file.ReadAsync(block, cancel);
                if (read == 0)
                    break;

                pipeWriter.Advance(read);
                await pipeWriter.FlushAsync(cancel);
            }

            eof = file.Position;
        }
    }

    static async Task<FileStream> OpenFileAsync(string path, CancellationToken cancel)
    {
        for (;;)
        {
            cancel.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                try
                {
                    return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                }
                catch (FileNotFoundException) { /* we hit a race condition */ }
            }

            await Task.Delay(100, cancel);
        }
    }

    static async Task ReadLinesAsync(ChannelWriter<LogChange> logWriter, PipeReader pipeReader, CancellationToken cancel)
    {
        // TODO: use StreamReader just on first block to detect encoding
        //       then change TailFileBytesAsync to TailFileCharsAsync so ReadLinesAsync can work on chars
        //       (or have a TailFileCharsAsync in the middle..). probably also switch the logentry stuff to run on
        //       Memory<Memory<char>> or whatev.

        while (!cancel.IsCancellationRequested)
        {
            var result = await pipeReader.ReadAsync(cancel);
            var buffer = result.Buffer;

            for (;;)
            {
                var position = buffer.PositionOf((byte)'\n');
                if (position == null)
                    break;

                var line = buffer.Slice(0, position.Value);
                buffer = buffer.Slice(buffer.GetPosition(1, position.Value));

                // TODO: alloc; perf
                var str = Encoding.UTF8.GetString(line);
                await logWriter.WriteAsync(new[] { str }, cancel);
            }

            pipeReader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
                break;
        }
    }
}
