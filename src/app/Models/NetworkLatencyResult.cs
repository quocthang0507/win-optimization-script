namespace WinOptimizationApp.Models;

public sealed record NetworkLatencyResult(
    string Host,
    int Sent,
    int Received,
    long MinimumMilliseconds,
    long MaximumMilliseconds,
    double AverageMilliseconds)
{
    public double PacketLossPercent => Sent == 0 ? 100 : (Sent - Received) * 100d / Sent;
}
