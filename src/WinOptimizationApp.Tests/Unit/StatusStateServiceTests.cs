using WinOptimizationApp.Services;

namespace WinOptimizationApp.Tests.Unit;

public sealed class StatusStateServiceTests
{
    [Theory]
    // The exact bug reported by user: 1-click finished with 1 failure
    [InlineData("Đã kết thúc: giải phóng 0 B, xóa 0 file, 1 tác vụ thất bại.", false)]
    [InlineData("Đã kết thúc: giải phóng 1.2 GB, xóa 45 file, 0 tác vụ thất bại.", false)]
    [InlineData("Finished: 0 B freed, 0 file(s) removed, 1 action(s) failed.", false)]
    [InlineData("Finished: 1.2 GB freed, 45 file(s) removed, 0 action(s) failed.", false)]
    // Ready / Idle states
    [InlineData("Sẵn sàng", false)]
    [InlineData("Sẵn sàng (Quyền Admin)", false)]
    [InlineData("Ready", false)]
    [InlineData("Ready (Admin)", false)]
    // Other completion states
    [InlineData("Đã hoàn tất", false)]
    [InlineData("Hoàn tất với cảnh báo", false)]
    [InlineData("Đã gỡ bỏ 2 ứng dụng, 0 lỗi.", false)]
    [InlineData("Đã gỡ bỏ 2 ứng dụng, 1 lỗi.", false)]
    [InlineData("Removed 2 apps, 0 failed.", false)]
    [InlineData("Đã cài xong. Thành công: 1, lỗi: 0.", false)]
    [InlineData("Installation finished. Success: 1, failed: 0.", false)]
    [InlineData("Đã áp dụng xong. Thành công: 5, lỗi: 0.", false)]
    [InlineData("Preset finished. Success: 5, lỗi: 0.", false)]
    [InlineData("Cập nhật xong. Thành công: 3, lỗi: 0.", false)]
    [InlineData("Updates finished. Success: 3, failed: 0.", false)]
    [InlineData("Đã lưu cài đặt", false)]
    [InlineData("Đã xuất báo cáo bảng điều khiển sang C:\\test.md", false)]
    [InlineData("Đã xóa lịch sử", false)]
    [InlineData("Hoàn tất hoàn tác", false)]
    [InlineData("Đã hủy trước khi dọn dẹp.", false)]
    [InlineData("Đã hủy sau khi dọn dẹp một phần.", false)]
    [InlineData("Cancelled. No cleanup or performance action was run.", false)]
    // Active progress states MUST be busy
    [InlineData("Đang quét...", true)]
    [InlineData("Đang dọn dẹp...", true)]
    [InlineData("Đang chạy tác vụ...", true)]
    [InlineData("Đang phân tích dọn dẹp 1-Click…", true)]
    [InlineData("Đang phân tích Hệ thống (1/5)...", true)]
    [InlineData("Đang chạy Dọn dẹp (2/5)...", true)]
    [InlineData("Đang quét lỗi Registry...", true)]
    [InlineData("Scanning...", true)]
    [InlineData("Running Cleanup...", true)]
    [InlineData("Cleaning temporary files...", true)]
    [InlineData("Analyzing selected actions…", true)]
    // Empty or whitespace
    [InlineData("", false)]
    [InlineData(null, false)]
    public void InferIsBusy_ReturnsExpectedBusyState(string? statusText, bool expectedBusy)
    {
        var actual = StatusStateService.InferIsBusy(statusText);
        Assert.Equal(expectedBusy, actual);
    }

    [Theory]
    // The exact bug from user: Finished with 1 failure -> Warning (DarkOrange)
    [InlineData("Đã kết thúc: giải phóng 0 B, xóa 0 file, 1 tác vụ thất bại.", StatusVisualState.Warning)]
    // Finished with 0 failure -> Success (MediumSeaGreen)
    [InlineData("Đã kết thúc: giải phóng 100 MB, xóa 5 file, 0 tác vụ thất bại.", StatusVisualState.Success)]
    [InlineData("Finished: 0 B freed, 0 file(s) removed, 1 action(s) failed.", StatusVisualState.Warning)]
    [InlineData("Finished: 100 MB freed, 5 file(s) removed, 0 action(s) failed.", StatusVisualState.Success)]
    // Active scanning -> Busy (DeepSkyBlue)
    [InlineData("Đang quét...", StatusVisualState.Busy)]
    [InlineData("Đang dọn dẹp...", StatusVisualState.Busy)]
    [InlineData("Đang quét lỗi Registry...", StatusVisualState.Busy)]
    [InlineData("Scanning...", StatusVisualState.Busy)]
    // Stopping / Cancelled -> Warning (DarkOrange)
    [InlineData("Đang dừng quét...", StatusVisualState.Warning)]
    [InlineData("Stopping...", StatusVisualState.Warning)]
    [InlineData("Đã hủy trước khi dọn dẹp.", StatusVisualState.Warning)]
    [InlineData("Cancelled before run.", StatusVisualState.Warning)]
    // Completed with 0 errors -> Success
    [InlineData("Cập nhật xong. Thành công: 5, lỗi: 0.", StatusVisualState.Success)]
    [InlineData("Updates finished. Success: 5, failed: 0.", StatusVisualState.Success)]
    [InlineData("Đã gỡ bỏ 2 ứng dụng, 0 lỗi.", StatusVisualState.Success)]
    // Completed with >0 errors -> Warning
    [InlineData("Cập nhật xong. Thành công: 4, lỗi: 1.", StatusVisualState.Warning)]
    [InlineData("Updates finished. Success: 4, failed: 1.", StatusVisualState.Warning)]
    [InlineData("Đã gỡ bỏ 2 ứng dụng, 1 lỗi.", StatusVisualState.Warning)]
    // Ready -> Success/Ready
    [InlineData("Sẵn sàng", StatusVisualState.Success)]
    [InlineData("Ready", StatusVisualState.Success)]
    [InlineData("Sẵn sàng (Quyền Admin)", StatusVisualState.Success)]
    public void GetVisualState_ReturnsExpectedVisualState(string statusText, StatusVisualState expected)
    {
        var actual = StatusStateService.GetVisualState(statusText);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Đã kết thúc: giải phóng 0 B, xóa 0 file, 1 tác vụ thất bại.", true)]
    [InlineData("Đã kết thúc: giải phóng 100 MB, xóa 5 file, 0 tác vụ thất bại.", false)]
    [InlineData("Finished: 0 B freed, 0 file(s) removed, 1 action(s) failed.", true)]
    [InlineData("Finished: 100 MB freed, 5 file(s) removed, 0 action(s) failed.", false)]
    [InlineData("Đang quét lỗi Registry...", false)] // In-progress is not a completed failure
    [InlineData("Cập nhật xong. Thành công: 5, lỗi: 0.", false)]
    [InlineData("Cập nhật xong. Thành công: 4, lỗi: 1.", true)]
    [InlineData("Sẵn sàng", false)]
    public void HasFailure_DetectsActualFailuresExcludingZeroCounters(string statusText, bool expected)
    {
        var actual = StatusStateService.HasFailure(statusText);
        Assert.Equal(expected, actual);
    }
}
