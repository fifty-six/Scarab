using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
        HttpResponseMessage resp = await self.SendAsync
        (
            new HttpRequestMessage {
                Version = self.DefaultRequestVersion,
                Method = HttpMethod.Get,
                RequestUri = uri
            },
            cts
        ).ConfigureAwait(false);

        Debug.Assert(resp is not null);

        resp.EnsureSuccessStatusCode();

        HttpContent content = resp.Content;

        await using Stream stream = await content.ReadAsStreamAsync(cts);

        Memory<byte> buf = ArrayPool<byte>.Shared.Rent(4096);

        MemoryStream memory = content.Headers.ContentLength is long value
            ? new MemoryStream((int) value)
            : new MemoryStream(1024);

        var args = new DownloadProgressArgs {
            TotalBytes = (int?) content.Headers.ContentLength,
        };

        progress.Report(args);

        while (true)
        {
            cts.ThrowIfCancellationRequested();

            int read = await stream.ReadAsync(buf, cts);

            await memory.WriteAsync(buf[..read], cts);

            if (read == 0)
                break;

            args.BytesRead += read;

            progress.Report(args);
        }

        args.Completed = true;

        progress.Report(args);

        ArraySegment<byte> res_segment = memory.TryGetBuffer(out ArraySegment<byte> out_buffer)
            ? out_buffer
            : memory.ToArray();

        return (res_segment, resp);
    }
}