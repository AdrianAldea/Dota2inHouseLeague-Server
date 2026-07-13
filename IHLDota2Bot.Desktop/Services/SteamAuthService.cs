using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IHLDota2Bot.Desktop.Services;

/// <summary>
/// Drives the "Sign in through Steam" desktop flow (RFC 8252 loopback pattern):
/// opens a one-shot loopback listener, launches the system browser at the backend's
/// <c>/api/auth/steam/login</c>, and waits for the backend to redirect back with
/// <c>?token=...</c>. Uses a raw <see cref="TcpListener"/> rather than HttpListener to
/// avoid Windows URL-ACL prompts.
/// </summary>
public sealed class SteamAuthService(AppSettings settings)
{
    /// <summary>Result of the loopback round-trip.</summary>
    public sealed record SignInResult(string Token, bool Registered);

    public async Task<SignInResult?> SignInAsync(CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var returnUrl = $"http://127.0.0.1:{port}/callback";
            var loginUrl =
                $"{settings.ApiBaseUrl.TrimEnd('/')}/api/auth/steam/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

            OpenBrowser(loginUrl);

            // Give the user up to 5 minutes to complete Steam login in the browser.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));

            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();

            var requestLine = await ReadRequestLineAsync(stream, timeout.Token);
            var query = ParseQuery(requestLine);

            query.TryGetValue("token", out var token);
            query.TryGetValue("error", out var error);
            var registered = query.TryGetValue("registered", out var reg) && reg == "true";

            await WriteResponseAsync(
                stream,
                token is not null ? SuccessHtml : FailHtml(error),
                timeout.Token
            );

            return string.IsNullOrEmpty(token) ? null : new SignInResult(token, registered);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string> ReadRequestLineAsync(
        NetworkStream stream,
        CancellationToken ct
    )
    {
        // The browser sends the request immediately; the first line carries the URL.
        var buffer = new byte[8192];
        var read = await stream.ReadAsync(buffer, ct);
        var text = Encoding.ASCII.GetString(buffer, 0, read);
        var newline = text.IndexOf('\r');
        return newline >= 0 ? text[..newline] : text;
    }

    private static Dictionary<string, string> ParseQuery(string requestLine)
    {
        // requestLine looks like: GET /callback?token=...&registered=true HTTP/1.1
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var parts = requestLine.Split(' ');
        if (parts.Length < 2)
            return result;

        var target = parts[1];
        var q = target.IndexOf('?');
        if (q < 0)
            return result;

        foreach (var pair in target[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            result[Uri.UnescapeDataString(kv[0])] =
                kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
        }
        return result;
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        string html,
        CancellationToken ct
    )
    {
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/html; charset=utf-8\r\n"
                + $"Content-Length: {body.Length}\r\n"
                + "Connection: close\r\n\r\n"
        );
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(body, ct);
        await stream.FlushAsync(ct);
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // If the shell can't open a browser there's nothing more we can do here.
        }
    }

    private const string SuccessHtml = """
        <!doctype html><html><head><meta charset="utf-8"><title>Signed in</title>
        <style>body{background:#0B0C10;color:#F5F7FA;font-family:Segoe UI,Arial,sans-serif;
        display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
        .card{text-align:center}.a{color:#FF8A3D}</style></head>
        <body><div class="card"><h1>You're signed in <span class="a">✓</span></h1>
        <p>You can close this tab and return to <b>Dota IHL</b>.</p></div></body></html>
        """;

    private static string FailHtml(string? error) =>
        $$"""
            <!doctype html><html><head><meta charset="utf-8"><title>Sign-in failed</title>
            <style>body{background:#0B0C10;color:#F5F7FA;font-family:Segoe UI,Arial,sans-serif;
            display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
            .card{text-align:center}.e{color:#FF5C5C}</style></head>
            <body><div class="card"><h1 class="e">Sign-in failed</h1>
            <p>{{(
                string.IsNullOrEmpty(error) ? "Please try again from the app." : error
            )}}</p></div></body></html>
            """;
}
