using System.Net.NetworkInformation;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class NetworkOptimizationService
{
    private readonly CommandRunner _commands;

    public IpcClient? Client { get; set; }

    public NetworkOptimizationService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<bool> FlushDnsAsync(CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("RunNetworkRepair", "FlushDns", cancellationToken);
            return response == "Success";
        }

        var result = await _commands.RunCaptureAsync("ipconfig", "/flushdns", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> ResetWinsockAsync(CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("RunNetworkRepair", "ResetWinsock", cancellationToken);
            return response == "Success";
        }

        var result = await _commands.RunCaptureAsync("netsh", "winsock reset", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> RenewIpAsync(CancellationToken cancellationToken = default)
    {
        if (Client?.IsConnected == true)
        {
            var response = await Client.SendRequestAsync("RunNetworkRepair", "RenewIp", cancellationToken);
            return response == "Success";
        }

        var releaseResult = await _commands.RunCaptureAsync("ipconfig", "/release", cancellationToken);
        var renewResult = await _commands.RunCaptureAsync("ipconfig", "/renew", cancellationToken);
        return releaseResult.ExitCode == 0 && renewResult.ExitCode == 0;
    }

    public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<NetworkAdapterInfo>>(() =>
        {
            var adapters = new List<NetworkAdapterInfo>();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nInterface in interfaces)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (nInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    var mac = nInterface.GetPhysicalAddress().ToString();
                    var ip = GetIpAddress(nInterface);

                    adapters.Add(new NetworkAdapterInfo
                    {
                        Id = nInterface.Id,
                        Name = nInterface.Name,
                        Description = nInterface.Description,
                        Status = nInterface.OperationalStatus.ToString(),
                        Speed = FormatSpeed(nInterface.Speed),
                        MacAddress = FormatMacAddress(mac),
                        IpAddress = ip
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkOptimizationService] Error getting adapters: {ex.Message}");
            }
            return adapters;
        }, cancellationToken);
    }

    public async Task<NetworkLatencyResult> MeasureLatencyAsync(
        string host,
        int attempts = 4,
        CancellationToken cancellationToken = default)
    {
        host = host.Trim();
        if (!IsValidPingHost(host))
        {
            throw new ArgumentException("Enter a valid DNS host name or IP address.", nameof(host));
        }

        attempts = Math.Clamp(attempts, 1, 10);
        var successfulRoundTrips = new List<long>(attempts);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var ping = new Ping();
            try
            {
                var reply = await ping.SendPingAsync(host, 2500).WaitAsync(cancellationToken);
                if (reply.Status == IPStatus.Success)
                {
                    successfulRoundTrips.Add(reply.RoundtripTime);
                }
            }
            catch (PingException)
            {
                // A failed probe is represented as packet loss; later probes still run.
            }
        }

        return new NetworkLatencyResult(
            host,
            attempts,
            successfulRoundTrips.Count,
            successfulRoundTrips.Count == 0 ? 0 : successfulRoundTrips.Min(),
            successfulRoundTrips.Count == 0 ? 0 : successfulRoundTrips.Max(),
            successfulRoundTrips.Count == 0 ? 0 : successfulRoundTrips.Average());
    }

    public static bool IsValidPingHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || host.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return Uri.CheckHostName(host) is UriHostNameType.Dns
            or UriHostNameType.IPv4
            or UriHostNameType.IPv6;
    }

    private static string GetIpAddress(NetworkInterface nInterface)
    {
        try
        {
            var ips = nInterface.GetIPProperties().UnicastAddresses
                .Select(addr => addr.Address.ToString())
                .ToList();
            return ips.Count > 0 ? string.Join(", ", ips) : "No IP Address";
        }
        catch
        {
            return "N/A";
        }
    }

    private static string FormatSpeed(long speed)
    {
        return speed <= 0
            ? "Unknown"
            : speed >= 1_000_000_000
            ? $"{(double)speed / 1_000_000_000:F1} Gbps"
            : speed >= 1_000_000
            ? $"{(double)speed / 1_000_000:F1} Mbps"
            : speed >= 1000 ? $"{(double)speed / 1000:F1} Kbps" : $"{speed} bps";
    }

    private static string FormatMacAddress(string mac)
    {
        return string.IsNullOrEmpty(mac) || mac.Length != 12
            ? mac
            : string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
    }
}
