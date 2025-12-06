using System.Buffers;
using System.Diagnostics;

namespace Scarab.Util;

public static class HttpClientExt
{
    public static async Task<(ArraySegment<byte>, HttpResponseMessage)> DownloadBytesWithProgressAsync
    (
        this HttpClient self,
        Uri uri,
        IProgress<DownloadProgressArgs> progress,
        CancellationToken cts = default
    )
    {
        var resp = await self.SendAsync
        (
            new HttpRequestMessage {
                Version = self.DefaultRequestVersion,
                Method = HttpMethod.Get,
                RequestUri = uri
            },
            HttpCompletionOption.ResponseHeadersRead,
            cts
        ).ConfigureAwait(false);

        Debug.Assert(resp is not null);

        resp.EnsureSuccessStatusCode();

        var content = resp.Content;

        await using var stream = await content.ReadAsStreamAsync(cts).ConfigureAwait(false);

        var dl_size = content.Headers.ContentLength is { } len
            ? (int) len
            : 65536;

        var pool_buffer = ArrayPool<byte>.Shared.Rent(65536);
        
        Memory<byte> buf = pool_buffer;

        var memory = new MemoryStream(dl_size);

        var args = new DownloadProgressArgs {
            TotalBytes = (int?) content.Headers.ContentLength
        };

        progress.Report(args);

        try
        {
            while (true)
            {
                cts.ThrowIfCancellationRequested();

                var read = await stream.ReadAsync(buf, cts).ConfigureAwait(false);
                
                await memory.WriteAsync(buf[..read], cts).ConfigureAwait(false);

                if (read == 0)
                    break;

                args.BytesRead += read;

                progress.Report(args);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pool_buffer);
        }

        args.Completed = true;

        progress.Report(args);
        
        var res_segment = memory.TryGetBuffer(out var out_buffer)
            ? out_buffer
            : memory.ToArray();

        return (res_segment, resp);
    }
}