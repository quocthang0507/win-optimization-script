namespace WinOptimizationApp.Services;

public enum StatusVisualState
{
    Inactive,
    Ready,
    Success,
    Warning,
    Busy
}

public static class StatusStateService
{
    public static bool InferIsBusy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        // 1. Terminal / completion / finished states must NEVER be busy
        if (IsTerminalState(trimmed))
        {
            return false;
        }

        // 2. Active progress patterns
        if (trimmed.StartsWith("Đang ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("...", StringComparison.Ordinal) ||
            trimmed.EndsWith("…", StringComparison.Ordinal) ||
            trimmed.StartsWith("Running", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Scanning", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Loading", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Cleaning", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Analyzing", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Uninstalling", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Removing", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Stopping", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Applying", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static StatusVisualState GetVisualState(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return StatusVisualState.Inactive;
        }

        var trimmed = text.Trim();

        // 1. Stopping states (active or completed cancellation)
        if (trimmed.Contains("dừng", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Stopping", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Canceled", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Cancelled", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Hủy", StringComparison.OrdinalIgnoreCase))
        {
            return StatusVisualState.Warning;
        }

        // 2. Active scanning/running/cleaning/loading (not stopping)
        if (InferIsBusy(trimmed))
        {
            return StatusVisualState.Busy;
        }

        // 3. Completed operations with failure/error
        if (HasFailure(trimmed))
        {
            return StatusVisualState.Warning;
        }

        // 4. Ready / Success states
        var lower = trimmed.ToLowerInvariant();
        if (lower.Contains("sẵn sàng") ||
            lower.Contains("ready") ||
            lower.Contains("saved") ||
            lower.Contains("đã lưu") ||
            lower.Contains("latest") ||
            lower.Contains("kết thúc") ||
            lower.Contains("hoàn tất") ||
            lower.Contains("finished") ||
            lower.Contains("completed") ||
            lower.Contains("success") ||
            lower.Contains("succeeded") ||
            lower.Contains("thành công") ||
            lower.Contains("xong") ||
            trimmed.StartsWith("Đã ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Hoàn tất", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Finished", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return StatusVisualState.Success;
        }

        return StatusVisualState.Inactive;
    }

    public static bool HasFailure(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // If it's an active in-progress scan, don't classify as failure
        // e.g. "Đang quét lỗi Registry..."
        if (text.StartsWith("Đang ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Running", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Scanning", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Cleaning", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();

        var hasFailureKeyword = lower.Contains("thất bại") ||
                                lower.Contains("failed") ||
                                lower.Contains("lỗi") ||
                                lower.Contains("error");

        if (!hasFailureKeyword)
        {
            return false;
        }

        // Check for explicit zero-failure indicators
        var isZeroFailures = lower.Contains("0 tác vụ thất bại") ||
                             lower.Contains("0 lỗi") ||
                             lower.Contains("lỗi: 0") ||
                             lower.Contains("lỗi 0") ||
                             lower.Contains("0 failed") ||
                             lower.Contains("failed: 0") ||
                             lower.Contains("0 action(s) failed") ||
                             lower.Contains("0 actions failed") ||
                             lower.Contains("0 errors") ||
                             lower.Contains("errors: 0");

        return !isZeroFailures;
    }

    private static bool IsTerminalState(string text)
    {
        var lower = text.ToLowerInvariant();

        // Explicit ready / saved
        if (lower.Contains("sẵn sàng") ||
            lower.Contains("ready") ||
            lower.Contains("đã lưu") ||
            lower.Contains("saved") ||
            lower.Contains("latest") ||
            lower.Contains("mới nhất"))
        {
            return true;
        }

        // Vietnamese completion prefixes and keywords
        if (text.StartsWith("Đã ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Hoàn tất", StringComparison.OrdinalIgnoreCase) ||
            lower.Contains("kết thúc") ||
            lower.Contains("hoàn tất") ||
            lower.Contains("thất bại") ||
            lower.Contains("hủy") ||
            lower.Contains("xong"))
        {
            return true;
        }

        // English completion prefixes and keywords
        if (text.StartsWith("Finished", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Completed", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Removed", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Installed", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Applied", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Exported", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Cleared", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Stopped", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Cancelled", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Canceled", StringComparison.OrdinalIgnoreCase) ||
            lower.Contains("finished") ||
            lower.Contains("completed") ||
            lower.Contains("failed") ||
            lower.Contains("succeeded") ||
            lower.Contains("canceled") ||
            lower.Contains("cancelled"))
        {
            return true;
        }

        return false;
    }
}
