using System.Net.Sockets;

namespace Scarab.Util;

public static class WorkaroundHttpClient
{
    public enum Settings
    {
        OnlyWorkaround,

        // ReSharper disable once UnusedMember.Global
        TryBoth
    }

    public class ResultInfo<T>
    {
        public T          Result           { get; }
        public HttpClient Client           { get; }
        public bool       NeededWorkaround { get; }

        public ResultInfo(T result, HttpClient client, bool neededWorkaround)
        {
            Result = result;
            Client = client;
            NeededWorkaround = neededWorkaround;
        }
    }

    /// <summary>
    /// Re-try an action with the IPv4 workaround client
    /// </summary>
    /// <param name="settings">Whether or not to skip trying the normal client</param>
    /// <param name="f">The action to try</param>
    /// <param name="config">A configurator for the HttpClient</param>
    /// <typeparam name="T">Return type of the action</typeparam>
    /// <remarks>It is expected that the action has a timeout, otherwise this will run indefinitely.</remarks>
    /// <returns>A result info containing the client, result, and whether the workaround was used.</returns>
    public static async Task<ResultInfo<T>> TryWithWorkaroundAsync<T>(
        Settings settings,
        Func<HttpClient, Task<T>> f,
        Action<HttpClient> config
    )
    {
        if (settings != Settings.OnlyWorkaround)
        {
            var hc = new HttpClient();

            try
            {
                config(hc);

                return new ResultInfo<T>
                (
                    await f(hc),
                    hc,
                    false
                );
            }
            catch (TaskCanceledException)
            {
                hc.Dispose();
                Log.Warning("Failed with normal client, trying workaround.");
            }
        }

        var workaround = CreateWorkaroundClient();

        try
        {
            config(workaround);

            return new ResultInfo<T>
            (
                await f(workaround),
                workaround,
                true
            );
        }
        catch
        {
            workaround.Dispose();
            throw;
        }
    }

    // .NET has a thing with using IPv6 for IPv4 stuff, so on
    // networks and/or drivers w/ poor support this fails.
    // https://github.com/dotnet/runtime/issues/47267
    // https://github.com/fifty-six/Scarab/issues/47
    private static HttpClient CreateWorkaroundClient()
    {
        return new HttpClient(new SocketsHttpHandler {
            ConnectCallback = IPv4ConnectAsync
        });

        static async ValueTask<Stream> IPv4ConnectAsync(SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            // By default, we create dual-mode sockets:
            // Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            // Defaults to dual-mode sockets, which uses IPv6 for IPv4 stuff.
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;

            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}