using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class PowerManagementServiceTests
{
    [Fact]
    public void ParseBatteryReportXml_ComputesHealthAndWearPercentage()
    {
        const string sampleXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <BatteryReport xmlns="http://schemas.microsoft.com/battery/2012">
              <Batteries>
                <Battery>
                  <Id>DELL-99</Id>
                  <Manufacturer>SMP</Manufacturer>
                  <Chemistry>LiP</Chemistry>
                  <DesignCapacity>60000</DesignCapacity>
                  <FullChargeCapacity>51000</FullChargeCapacity>
                  <CycleCount>142</CycleCount>
                </Battery>
              </Batteries>
            </BatteryReport>
            """;

        var report = PowerManagementService.ParseBatteryReportXml(sampleXml);

        Assert.True(report.HasBattery);
        Assert.Equal("SMP DELL-99", report.BatteryName);
        Assert.Equal("LiP", report.Chemistry);
        Assert.Equal(60000, report.DesignCapacityMwh);
        Assert.Equal(51000, report.FullChargeCapacityMwh);
        Assert.Equal(142, report.CycleCount);
        Assert.Equal(85.0, report.HealthPercent);
        Assert.Equal(15.0, report.WearPercent);
    }

    [Fact]
    public void ParsePowerPlansOutput_ExtractsSchemesAndActiveIndicator()
    {
        const string sampleOutput = """
            Existing Power Schemes (* Active)
            -----------------------------------
            Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *
            Power Scheme GUID: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (High performance)
            Power Scheme GUID: e9a42b02-d5df-448d-aa00-03f14749eb61  (Ultimate Performance)
            """;

        var plans = PowerManagementService.ParsePowerPlansOutput(sampleOutput);

        Assert.Equal(3, plans.Count);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", plans[0].SchemeGuid);
        Assert.Equal("Balanced", plans[0].Name);
        Assert.True(plans[0].IsActive);
        Assert.False(plans[1].IsActive);
        Assert.Equal("Ultimate Performance", plans[2].Name);
    }

    [Fact]
    public void ParsePowerRequestsOutput_ExtractsActiveWakeBlockers()
    {
        const string sampleOutput = """
            DISPLAY:
            None.

            SYSTEM:
            [PROCESS] \Device\HarddiskVolume3\Apps\MediaPlayer.exe
            Playing audio stream

            AWAYMODE:
            None.
            """;

        var requests = PowerManagementService.ParsePowerRequestsOutput(sampleOutput);

        var single = Assert.Single(requests);
        Assert.Equal("SYSTEM", single.Category);
        Assert.Contains("MediaPlayer.exe", single.Requester);
        Assert.Equal("Playing audio stream", single.Reason);
    }
}
