using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace DevLiteServer.Core;

public class DnsServer : IDisposable
{
    private static DnsServer? _instance;
    public static DnsServer Instance => _instance ??= new DnsServer();

    private UdpClient? _udpListener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isRunning;

    public bool IsRunning => _isRunning;
    public int Port => 53;
    public string UpstreamDns { get; set; } = "1.1.1.1";

    public event Action<bool>? StatusChanged;

    public bool Start()
    {
        if (_isRunning) return true;

        try
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 53);
            _udpListener = new UdpClient(endpoint);
            _cts = new CancellationTokenSource();
            _isRunning = true;

            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            StatusChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DnsServer] Failed to bind 127.0.0.1:53: {ex.Message}");
            _isRunning = false;
            _udpListener?.Dispose();
            _udpListener = null;
            StatusChanged?.Invoke(false);
            return false;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cts?.Cancel();

        try
        {
            _udpListener?.Close();
            _udpListener?.Dispose();
        }
        catch { }

        _udpListener = null;
        StatusChanged?.Invoke(false);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isRunning && _udpListener != null)
        {
            try
            {
                var result = await _udpListener.ReceiveAsync(ct);
                _ = Task.Run(() => HandleDnsQueryAsync(result.Buffer, result.RemoteEndPoint, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DnsServer] Receive error: {ex.Message}");
            }
        }
    }

    private async Task HandleDnsQueryAsync(byte[] queryBytes, IPEndPoint clientEndpoint, CancellationToken ct)
    {
        if (queryBytes.Length < 12) return;

        try
        {
            var (domain, qtype, questionEndOffset) = ParseQuery(queryBytes);

            // Check if domain is wildcard *.test, *.localhost, or localhost
            if (IsLocalDomain(domain))
            {
                byte[] responseBytes = BuildLocalResponse(queryBytes, qtype, questionEndOffset);
                if (_udpListener != null && _isRunning)
                {
                    await _udpListener.SendAsync(responseBytes, responseBytes.Length, clientEndpoint);
                }
            }
            else
            {
                // Forward query to upstream DNS
                byte[]? upstreamResponse = await ForwardToUpstreamAsync(queryBytes, ct);
                if (upstreamResponse != null && _udpListener != null && _isRunning)
                {
                    await _udpListener.SendAsync(upstreamResponse, upstreamResponse.Length, clientEndpoint);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DnsServer] Error handling query: {ex.Message}");
        }
    }

    private static (string domain, int qtype, int questionEndOffset) ParseQuery(byte[] bytes)
    {
        int offset = 12; // Skip 12-byte DNS header
        var sb = new StringBuilder();

        while (offset < bytes.Length && bytes[offset] > 0)
        {
            int len = bytes[offset++];
            if (offset + len > bytes.Length) break;

            if (sb.Length > 0) sb.Append('.');
            sb.Append(Encoding.ASCII.GetString(bytes, offset, len));
            offset += len;
        }

        offset++; // Skip 0x00 terminating byte

        int qtype = 1;
        if (offset + 4 <= bytes.Length)
        {
            qtype = (bytes[offset] << 8) | bytes[offset + 1];
            offset += 4; // qtype (2) + qclass (2)
        }

        return (sb.ToString(), qtype, offset);
    }

    private static bool IsLocalDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return false;
        string lower = domain.Trim().ToLowerInvariant();
        return lower.EndsWith(".test") || lower == "test" ||
               lower.EndsWith(".localhost") || lower == "localhost";
    }

    private static byte[] BuildLocalResponse(byte[] queryBytes, int qtype, int questionEndOffset)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // 1. Transaction ID
        bw.Write(queryBytes[0]);
        bw.Write(queryBytes[1]);

        // 2. Flags: 0x8180 (Standard query response, No error, Recursion Available)
        bw.Write((byte)0x81);
        bw.Write((byte)0x80);

        // 3. Questions: 1
        bw.Write((byte)0x00);
        bw.Write((byte)0x01);

        // 4. Answers: 1 (if A or AAAA) else 0
        bool hasAnswer = (qtype == 1 || qtype == 28);
        bw.Write((byte)0x00);
        bw.Write((byte)(hasAnswer ? 0x01 : 0x00));

        // 5. Authority RRs: 0
        bw.Write((byte)0x00);
        bw.Write((byte)0x00);

        // 6. Additional RRs: 0
        bw.Write((byte)0x00);
        bw.Write((byte)0x00);

        // 7. Echo Question section exactly from query
        int questionLength = Math.Min(questionEndOffset - 12, queryBytes.Length - 12);
        bw.Write(queryBytes, 12, questionLength);

        // 8. Answer Section
        if (hasAnswer)
        {
            // Name pointer to QNAME at offset 12
            bw.Write((byte)0xC0);
            bw.Write((byte)0x0C);

            if (qtype == 1) // Type A (IPv4)
            {
                bw.Write((byte)0x00); bw.Write((byte)0x01); // Type: A
                bw.Write((byte)0x00); bw.Write((byte)0x01); // Class: IN
                bw.Write((byte)0x00); bw.Write((byte)0x00); bw.Write((byte)0x00); bw.Write((byte)0x3C); // TTL: 60s
                bw.Write((byte)0x00); bw.Write((byte)0x04); // RDLENGTH: 4
                bw.Write((byte)127); bw.Write((byte)0); bw.Write((byte)0); bw.Write((byte)1); // RDATA: 127.0.0.1
            }
            else if (qtype == 28) // Type AAAA (IPv6)
            {
                bw.Write((byte)0x00); bw.Write((byte)0x1C); // Type: AAAA
                bw.Write((byte)0x00); bw.Write((byte)0x01); // Class: IN
                bw.Write((byte)0x00); bw.Write((byte)0x00); bw.Write((byte)0x00); bw.Write((byte)0x3C); // TTL: 60s
                bw.Write((byte)0x00); bw.Write((byte)0x10); // RDLENGTH: 16
                for (int i = 0; i < 15; i++) bw.Write((byte)0);
                bw.Write((byte)1); // RDATA: ::1
            }
        }

        return ms.ToArray();
    }

    private async Task<byte[]?> ForwardToUpstreamAsync(byte[] queryBytes, CancellationToken ct)
    {
        try
        {
            using var forwarder = new UdpClient();
            var upstreamEp = new IPEndPoint(IPAddress.Parse(UpstreamDns), 53);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await forwarder.SendAsync(queryBytes, queryBytes.Length, upstreamEp);
            var response = await forwarder.ReceiveAsync(linkedCts.Token);
            return response.Buffer;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsWindowsDnsConfigured()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var ipProps = ni.GetIPProperties();
                foreach (var dns in ipProps.DnsAddresses)
                {
                    if (dns.Equals(IPAddress.Loopback))
                    {
                        return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }

    public static async Task<bool> ConfigureWindowsDnsAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            try
            {
                string script = enable
                    ? "$adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceType -ne 24 }; foreach ($a in $adapters) { Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ServerAddresses ('127.0.0.1','1.1.1.1','8.8.8.8') }"
                    : "$adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceType -ne 24 }; foreach ($a in $adapters) { Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ResetServerAddresses }";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(8000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Checks whether local DNS is operational and Windows is pointed to 127.0.0.1.
    /// If operational, all *.test / *.localhost domains resolve with zero UAC and no hosts file sync needed.
    /// </summary>
    public static bool IsZeroUacActive => Instance.IsRunning && IsWindowsDnsConfigured();

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
