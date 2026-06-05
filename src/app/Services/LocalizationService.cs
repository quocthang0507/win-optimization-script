using System.Globalization;
using WinOptimizationApp.Models;

namespace WinOptimizationApp.Services;

public sealed class LocalizationService
{
    private readonly Dictionary<string, string> _english = new(StringComparer.OrdinalIgnoreCase)
    {
        ["app.title"] = "Windows System Maintenance",
        ["app.paneTitle"] = "Maintenance",
        ["common.ready"] = "Ready",
        ["common.loading"] = "Loading...",
        ["common.open"] = "Open",
        ["common.close"] = "Close",
        ["common.cancel"] = "Cancel",
        ["common.run"] = "Run",
        ["common.scan"] = "Scan",
        ["common.stop"] = "Stop",
        ["common.browse"] = "Browse",
        ["common.move"] = "Move",
        ["common.launch"] = "Launch",
        ["common.none"] = "None",
        ["common.hidden"] = "Hidden",
        ["common.system"] = "System",
        ["common.followLinks"] = "Follow links",
        ["common.openLocation"] = "Open location",
        ["common.addCleanupReview"] = "Add to cleanup review",
        ["common.recycleBin"] = "Recycle Bin",
        ["common.delete"] = "Delete",

        ["nav.dashboard"] = "Dashboard",
        ["nav.cleanup"] = "Cleanup",
        ["nav.storage"] = "Storage Analyzer",
        ["nav.startup"] = "Startup",
        ["nav.updates"] = "Updates",
        ["nav.repair"] = "Repair",
        ["nav.history"] = "History",
        ["nav.settings"] = "Settings",

        ["risk.Safe"] = "Safe",
        ["risk.Medium"] = "Medium",
        ["risk.High"] = "High",

        ["group.Cleanup"] = "Cleanup",
        ["group.Privacy"] = "Privacy",
        ["group.Optimization"] = "Optimization",
        ["group.Repair"] = "Repair",

        ["dashboard.title"] = "Dashboard",
        ["dashboard.subtitle"] = "Machine health, safety status and recent maintenance.",
        ["dashboard.windows"] = "Windows",
        ["dashboard.pendingReboot"] = "Pending reboot",
        ["dashboard.noRebootPending"] = "No reboot pending",
        ["dashboard.administrator"] = "Administrator",
        ["dashboard.elevated"] = "Elevated",
        ["dashboard.standardUser"] = "Standard user",
        ["dashboard.highRiskEnabled"] = "High-risk tasks enabled",
        ["dashboard.highRiskNeedAdmin"] = "High-risk tasks need admin",
        ["dashboard.systemDrive"] = "System drive",
        ["dashboard.free"] = "free",
        ["dashboard.uptime"] = "Uptime",
        ["dashboard.wingetAvailable"] = "WinGet available",
        ["dashboard.wingetNotFound"] = "WinGet not found",
        ["dashboard.scanCleanup"] = "Scan Cleanup",
        ["dashboard.analyzeStorage"] = "Analyze Storage",
        ["dashboard.scanUpdates"] = "Scan Updates",
        ["dashboard.openLogs"] = "Open Logs",
        ["dashboard.lastReport"] = "Last Report",

        ["taskPage.subtitle"] = "Preview first, then run selected tasks with risk-aware confirmation.",
        ["startup.title"] = "Startup",
        ["startup.subtitle"] = "Read-only inventory for startup entries.",
        ["startup.scan"] = "Scan Startup",
        ["startup.scanning"] = "Scanning startup entries...",
        ["startup.entries"] = "{0:N0} entries",
        ["startup.enabled"] = "Enabled",
        ["startup.disabled"] = "Disabled",

        ["updates.title"] = "Updates",
        ["updates.subtitle"] = "Preview WinGet packages before upgrading.",
        ["updates.scanWinget"] = "Scan WinGet",
        ["updates.upgradeAll"] = "Upgrade All",
        ["updates.scanning"] = "Scanning WinGet...",
        ["updates.packageUpdates"] = "{0:N0} package updates",

        ["storage.title"] = "Storage Analyzer",
        ["storage.subtitle"] = "Scan a drive or folder, review what uses space, then clean up safely.",
        ["storage.driveOrFolder"] = "Drive or folder",
        ["storage.placeholder"] = "C:\\ or C:\\Users\\You\\Downloads",
        ["storage.drive"] = "Drive",
        ["storage.free"] = "free",
        ["storage.followTooltip"] = "Off by default to avoid loops and double-counting.",
        ["storage.enterPath"] = "Enter a drive or folder to scan.",
        ["storage.pathNotFound"] = "Path not found",
        ["storage.stopping"] = "Stopping scan...",
        ["storage.scanning"] = "Scanning storage...",
        ["storage.progress"] = "{0} scanned / {1:N0} files / {2:N0} folders / {3:N0} skipped\n{4}",
        ["storage.completedIn"] = "Completed in {0:N1}s.",
        ["storage.scanCanceled"] = "Scan canceled.",
        ["storage.scanCanceledDetail"] = "Scan canceled. Previous results are unchanged.",
        ["storage.scanFailed"] = "Scan failed: {0}",
        ["storage.scanned"] = "Scanned",
        ["storage.filesFolders"] = "{0:N0} files / {1:N0} folders",
        ["storage.largestFolder"] = "Largest Folder",
        ["storage.skipped"] = "Skipped",
        ["storage.errors"] = "{0:N0} error(s)",
        ["storage.cleanupReview"] = "Cleanup Review",
        ["storage.reviewSelected"] = "Review Selected",
        ["storage.spaceMap"] = "Space Map",
        ["storage.folderTree"] = "Folder Tree",
        ["storage.largestFiles"] = "Largest Files",
        ["storage.fileTypes"] = "File Types",
        ["storage.skippedErrors"] = "Skipped / Errors",
        ["storage.name"] = "Name",
        ["storage.size"] = "Size",
        ["storage.files"] = "Files",
        ["storage.modified"] = "Modified",
        ["storage.action"] = "Action",
        ["storage.type"] = "Type",
        ["storage.extension"] = "Extension",
        ["storage.count"] = "Count",
        ["storage.lastModified"] = "Last Modified",
        ["storage.largest"] = "Largest",
        ["storage.selectAtLeastOne"] = "Select at least one item.",
        ["storage.itemSummary"] = "{0:N0} item(s), {1}",
        ["storage.moreItems"] = "+ {0:N0} more item(s)",
        ["storage.moveQuestion"] = "Move selected items to Recycle Bin?",
        ["storage.cleaning"] = "Cleaning selected storage items...",
        ["storage.manualReason"] = "Manually selected in Storage Analyzer.",

        ["history.title"] = "History",
        ["history.subtitle"] = "Reports saved after task execution.",
        ["history.empty"] = "No reports yet.",

        ["settings.title"] = "Settings",
        ["settings.subtitle"] = "Paths and Windows entry points.",
        ["settings.language"] = "Language",
        ["settings.languageDescription"] = "Choose the display language for this session.",
        ["settings.cliScript"] = "CLI script",
        ["settings.storageSense"] = "Storage Sense",
        ["settings.storageSenseDescription"] = "Open Windows Storage Sense settings.",
        ["settings.logs"] = "Logs",
        ["settings.repository"] = "Repository",

        ["preview.moreTargets"] = "+ {0:N0} more target(s)",
        ["preview.targetLine"] = "{0}: {1} / {2:N0} files / {3}",
        ["preview.title"] = "{0} preview",
        ["admin.title"] = "Administrator required",
        ["admin.message"] = "{0} needs an elevated app session.",
        ["confirm.risk"] = "Risk: {0}",
        ["confirm.restorePoint"] = "A restore point will be requested before running when possible.",
        ["confirm.runQuestion"] = "Run {0}?",
        ["run.completed"] = "Completed",
        ["run.completedWarnings"] = "Completed with warnings",
        ["run.summary"] = "Freed {0}. Removed {1:N0}, skipped {2:N0}.",
        ["status.scanningTask"] = "Scanning {0}...",
        ["status.runningTask"] = "Running {0}...",

        ["task.cleanup.temp.label"] = "Temporary files",
        ["task.cleanup.temp.description"] = "User and Windows temporary folders.",
        ["task.cleanup.temp.impact"] = "Frees local temporary files.",
        ["task.cleanup.browser.label"] = "Browser cache",
        ["task.cleanup.browser.description"] = "Edge, Chrome, Firefox, Brave and Opera cache folders.",
        ["task.cleanup.browser.impact"] = "Browsers may reload cached assets.",
        ["task.cleanup.dev.label"] = "Developer caches",
        ["task.cleanup.dev.description"] = "NuGet, pip, npm and yarn cache commands.",
        ["task.cleanup.dev.impact"] = "Build tools may download packages again.",
        ["task.cleanup.windowsupdate.label"] = "Windows Update cache",
        ["task.cleanup.windowsupdate.description"] = "SoftwareDistribution and Delivery Optimization caches.",
        ["task.cleanup.windowsupdate.impact"] = "May require services to restart.",
        ["task.cleanup.recyclebin.label"] = "Recycle Bin",
        ["task.cleanup.recyclebin.description"] = "Empties the current user's recycle bin.",
        ["task.cleanup.recyclebin.impact"] = "Removes deleted files permanently.",
        ["task.cleanup.windowsold.label"] = "Old Windows installation",
        ["task.cleanup.windowsold.description"] = "Windows.old from previous upgrades.",
        ["task.cleanup.windowsold.impact"] = "Removes rollback files for old Windows installs.",
        ["task.privacy.clipboard.label"] = "Clipboard",
        ["task.privacy.clipboard.description"] = "Clears clipboard contents.",
        ["task.privacy.clipboard.impact"] = "Removes current clipboard data.",
        ["task.privacy.powershell.label"] = "PowerShell history",
        ["task.privacy.powershell.description"] = "Clears PSReadLine console history.",
        ["task.privacy.powershell.impact"] = "Command history cannot be recovered from this file.",
        ["task.network.dns.label"] = "DNS cache",
        ["task.network.dns.description"] = "Runs ipconfig /flushdns.",
        ["task.network.dns.impact"] = "Refreshes cached DNS records.",
        ["task.repair.dism.label"] = "DISM RestoreHealth",
        ["task.repair.dism.description"] = "Repairs the Windows component store.",
        ["task.repair.dism.impact"] = "Long-running Windows repair command.",
        ["task.repair.sfc.label"] = "System File Checker",
        ["task.repair.sfc.description"] = "Runs sfc /scannow.",
        ["task.repair.sfc.impact"] = "Long-running integrity scan.",
        ["task.repair.explorer.label"] = "Restart Explorer",
        ["task.repair.explorer.description"] = "Restarts Windows Explorer.",
        ["task.repair.explorer.impact"] = "Taskbar and File Explorer windows refresh.",
        ["task.optimization.hibernate.label"] = "Disable hibernation",
        ["task.optimization.hibernate.description"] = "Runs powercfg -h off.",
        ["task.optimization.hibernate.impact"] = "Reclaims hiberfil.sys but disables hibernate.",
        ["task.optimization.drives.label"] = "Optimize drives",
        ["task.optimization.drives.description"] = "Runs Windows drive optimization.",
        ["task.optimization.drives.impact"] = "TRIM/defrag fixed drives.",
        ["task.software.winget.label"] = "WinGet updates",
        ["task.software.winget.description"] = "Scans and upgrades packages through winget.",
        ["task.software.winget.impact"] = "Applications may change versions.",
        ["task.startup.scan.label"] = "Startup inventory",
        ["task.startup.scan.description"] = "Reads startup registry entries and folders.",
        ["task.startup.scan.impact"] = "Read-only inventory.",
        ["task.settings.storage.label"] = "Storage Sense",
        ["task.settings.storage.description"] = "Opens Windows Storage Sense settings.",
        ["task.settings.storage.impact"] = "Uses Windows Settings.",
        ["task.cli.launch.label"] = "Launch CLI tool",
        ["task.cli.launch.description"] = "Starts src/cli/Utilities.ps1 in PowerShell.",
        ["task.cli.launch.impact"] = "Runs the existing console workflow.",
        ["task.storage.cleanup.label"] = "Storage Analyzer Cleanup"
    };

    private readonly Dictionary<string, string> _vietnamese = new(StringComparer.OrdinalIgnoreCase)
    {
        ["app.title"] = "Bảo trì hệ thống Windows",
        ["app.paneTitle"] = "Bảo trì",
        ["common.ready"] = "Sẵn sàng",
        ["common.loading"] = "Đang tải...",
        ["common.open"] = "Mở",
        ["common.close"] = "Đóng",
        ["common.cancel"] = "Hủy",
        ["common.run"] = "Chạy",
        ["common.scan"] = "Quét",
        ["common.stop"] = "Dừng",
        ["common.browse"] = "Chọn thư mục",
        ["common.move"] = "Chuyển",
        ["common.launch"] = "Khởi chạy",
        ["common.none"] = "Không có",
        ["common.hidden"] = "Ẩn",
        ["common.system"] = "Hệ thống",
        ["common.followLinks"] = "Theo liên kết",
        ["common.openLocation"] = "Mở vị trí",
        ["common.addCleanupReview"] = "Thêm vào duyệt dọn dẹp",
        ["common.recycleBin"] = "Thùng rác",
        ["common.delete"] = "Xóa",

        ["nav.dashboard"] = "Tổng quan",
        ["nav.cleanup"] = "Dọn dẹp",
        ["nav.storage"] = "Phân tích lưu trữ",
        ["nav.startup"] = "Khởi động",
        ["nav.updates"] = "Cập nhật",
        ["nav.repair"] = "Sửa lỗi",
        ["nav.history"] = "Lịch sử",
        ["nav.settings"] = "Cài đặt",

        ["risk.Safe"] = "An toàn",
        ["risk.Medium"] = "Trung bình",
        ["risk.High"] = "Cao",

        ["group.Cleanup"] = "Dọn dẹp",
        ["group.Privacy"] = "Quyền riêng tư",
        ["group.Optimization"] = "Tối ưu",
        ["group.Repair"] = "Sửa lỗi",

        ["dashboard.title"] = "Tổng quan",
        ["dashboard.subtitle"] = "Tình trạng máy, trạng thái an toàn và lần bảo trì gần nhất.",
        ["dashboard.windows"] = "Windows",
        ["dashboard.pendingReboot"] = "Cần khởi động lại",
        ["dashboard.noRebootPending"] = "Không cần khởi động lại",
        ["dashboard.administrator"] = "Quyền quản trị",
        ["dashboard.elevated"] = "Đã nâng quyền",
        ["dashboard.standardUser"] = "Người dùng thường",
        ["dashboard.highRiskEnabled"] = "Có thể chạy tác vụ rủi ro cao",
        ["dashboard.highRiskNeedAdmin"] = "Tác vụ rủi ro cao cần admin",
        ["dashboard.systemDrive"] = "Ổ hệ thống",
        ["dashboard.free"] = "trống",
        ["dashboard.uptime"] = "Thời gian chạy",
        ["dashboard.wingetAvailable"] = "Có WinGet",
        ["dashboard.wingetNotFound"] = "Không tìm thấy WinGet",
        ["dashboard.scanCleanup"] = "Quét dọn dẹp",
        ["dashboard.analyzeStorage"] = "Phân tích lưu trữ",
        ["dashboard.scanUpdates"] = "Quét cập nhật",
        ["dashboard.openLogs"] = "Mở nhật ký",
        ["dashboard.lastReport"] = "Báo cáo gần nhất",

        ["taskPage.subtitle"] = "Xem trước trước khi chạy, kèm xác nhận theo mức rủi ro.",
        ["startup.title"] = "Khởi động",
        ["startup.subtitle"] = "Danh sách mục khởi động ở chế độ chỉ đọc.",
        ["startup.scan"] = "Quét khởi động",
        ["startup.scanning"] = "Đang quét mục khởi động...",
        ["startup.entries"] = "{0:N0} mục",
        ["startup.enabled"] = "Đang bật",
        ["startup.disabled"] = "Đã tắt",

        ["updates.title"] = "Cập nhật",
        ["updates.subtitle"] = "Xem trước gói WinGet trước khi nâng cấp.",
        ["updates.scanWinget"] = "Quét WinGet",
        ["updates.upgradeAll"] = "Nâng cấp tất cả",
        ["updates.scanning"] = "Đang quét WinGet...",
        ["updates.packageUpdates"] = "{0:N0} gói cập nhật",

        ["storage.title"] = "Phân tích lưu trữ",
        ["storage.subtitle"] = "Quét ổ đĩa hoặc thư mục, xem nơi chiếm dung lượng và dọn dẹp an toàn.",
        ["storage.driveOrFolder"] = "Ổ đĩa hoặc thư mục",
        ["storage.placeholder"] = "C:\\ hoặc C:\\Users\\You\\Downloads",
        ["storage.drive"] = "Ổ đĩa",
        ["storage.free"] = "trống",
        ["storage.followTooltip"] = "Mặc định tắt để tránh vòng lặp và tính trùng dung lượng.",
        ["storage.enterPath"] = "Nhập ổ đĩa hoặc thư mục cần quét.",
        ["storage.pathNotFound"] = "Không tìm thấy đường dẫn",
        ["storage.stopping"] = "Đang dừng quét...",
        ["storage.scanning"] = "Đang quét lưu trữ...",
        ["storage.progress"] = "Đã quét {0} / {1:N0} file / {2:N0} thư mục / bỏ qua {3:N0}\n{4}",
        ["storage.completedIn"] = "Hoàn tất trong {0:N1}s.",
        ["storage.scanCanceled"] = "Đã hủy quét.",
        ["storage.scanCanceledDetail"] = "Đã hủy quét. Kết quả trước đó được giữ nguyên.",
        ["storage.scanFailed"] = "Quét thất bại: {0}",
        ["storage.scanned"] = "Đã quét",
        ["storage.filesFolders"] = "{0:N0} file / {1:N0} thư mục",
        ["storage.largestFolder"] = "Thư mục lớn nhất",
        ["storage.skipped"] = "Bỏ qua",
        ["storage.errors"] = "{0:N0} lỗi",
        ["storage.cleanupReview"] = "Duyệt dọn dẹp",
        ["storage.reviewSelected"] = "Duyệt mục chọn",
        ["storage.spaceMap"] = "Bản đồ dung lượng",
        ["storage.folderTree"] = "Cây thư mục",
        ["storage.largestFiles"] = "File lớn nhất",
        ["storage.fileTypes"] = "Loại file",
        ["storage.skippedErrors"] = "Bỏ qua / Lỗi",
        ["storage.name"] = "Tên",
        ["storage.size"] = "Dung lượng",
        ["storage.files"] = "File",
        ["storage.modified"] = "Sửa đổi",
        ["storage.action"] = "Thao tác",
        ["storage.type"] = "Loại",
        ["storage.extension"] = "Đuôi file",
        ["storage.count"] = "Số lượng",
        ["storage.lastModified"] = "Sửa đổi gần nhất",
        ["storage.largest"] = "Lớn nhất",
        ["storage.selectAtLeastOne"] = "Chọn ít nhất một mục.",
        ["storage.itemSummary"] = "{0:N0} mục, {1}",
        ["storage.moreItems"] = "+ {0:N0} mục khác",
        ["storage.moveQuestion"] = "Chuyển các mục đã chọn vào Thùng rác?",
        ["storage.cleaning"] = "Đang dọn các mục đã chọn...",
        ["storage.manualReason"] = "Được chọn thủ công trong Phân tích lưu trữ.",

        ["history.title"] = "Lịch sử",
        ["history.subtitle"] = "Báo cáo được lưu sau khi chạy tác vụ.",
        ["history.empty"] = "Chưa có báo cáo.",

        ["settings.title"] = "Cài đặt",
        ["settings.subtitle"] = "Đường dẫn và điểm mở của Windows.",
        ["settings.language"] = "Ngôn ngữ",
        ["settings.languageDescription"] = "Chọn ngôn ngữ hiển thị cho phiên chạy này.",
        ["settings.cliScript"] = "Script CLI",
        ["settings.storageSense"] = "Storage Sense",
        ["settings.storageSenseDescription"] = "Mở cài đặt Storage Sense của Windows.",
        ["settings.logs"] = "Nhật ký",
        ["settings.repository"] = "Repository",

        ["preview.moreTargets"] = "+ {0:N0} mục tiêu khác",
        ["preview.targetLine"] = "{0}: {1} / {2:N0} file / {3}",
        ["preview.title"] = "Xem trước {0}",
        ["admin.title"] = "Cần quyền Administrator",
        ["admin.message"] = "{0} cần chạy app ở quyền nâng cao.",
        ["confirm.risk"] = "Rủi ro: {0}",
        ["confirm.restorePoint"] = "App sẽ yêu cầu tạo điểm khôi phục trước khi chạy nếu có thể.",
        ["confirm.runQuestion"] = "Chạy {0}?",
        ["run.completed"] = "Đã hoàn tất",
        ["run.completedWarnings"] = "Hoàn tất với cảnh báo",
        ["run.summary"] = "Giải phóng {0}. Đã xóa {1:N0}, bỏ qua {2:N0}.",
        ["status.scanningTask"] = "Đang quét {0}...",
        ["status.runningTask"] = "Đang chạy {0}...",

        ["task.cleanup.temp.label"] = "File tạm",
        ["task.cleanup.temp.description"] = "Thư mục tạm của người dùng và Windows.",
        ["task.cleanup.temp.impact"] = "Giải phóng file tạm cục bộ.",
        ["task.cleanup.browser.label"] = "Cache trình duyệt",
        ["task.cleanup.browser.description"] = "Cache Edge, Chrome, Firefox, Brave và Opera.",
        ["task.cleanup.browser.impact"] = "Trình duyệt có thể cần tải lại tài nguyên cache.",
        ["task.cleanup.dev.label"] = "Cache lập trình",
        ["task.cleanup.dev.description"] = "Lệnh dọn cache NuGet, pip, npm và yarn.",
        ["task.cleanup.dev.impact"] = "Công cụ build có thể cần tải lại package.",
        ["task.cleanup.windowsupdate.label"] = "Cache Windows Update",
        ["task.cleanup.windowsupdate.description"] = "Cache SoftwareDistribution và Delivery Optimization.",
        ["task.cleanup.windowsupdate.impact"] = "Có thể cần khởi động lại service.",
        ["task.cleanup.recyclebin.label"] = "Thùng rác",
        ["task.cleanup.recyclebin.description"] = "Làm trống thùng rác của người dùng hiện tại.",
        ["task.cleanup.recyclebin.impact"] = "Xóa vĩnh viễn các file đã xóa.",
        ["task.cleanup.windowsold.label"] = "Bản cài Windows cũ",
        ["task.cleanup.windowsold.description"] = "Windows.old từ các lần nâng cấp trước.",
        ["task.cleanup.windowsold.impact"] = "Xóa file rollback của bản Windows cũ.",
        ["task.privacy.clipboard.label"] = "Clipboard",
        ["task.privacy.clipboard.description"] = "Xóa nội dung clipboard.",
        ["task.privacy.clipboard.impact"] = "Xóa dữ liệu clipboard hiện tại.",
        ["task.privacy.powershell.label"] = "Lịch sử PowerShell",
        ["task.privacy.powershell.description"] = "Xóa lịch sử console PSReadLine.",
        ["task.privacy.powershell.impact"] = "Không thể khôi phục lịch sử lệnh từ file này.",
        ["task.network.dns.label"] = "Cache DNS",
        ["task.network.dns.description"] = "Chạy ipconfig /flushdns.",
        ["task.network.dns.impact"] = "Làm mới bản ghi DNS đã cache.",
        ["task.repair.dism.label"] = "DISM RestoreHealth",
        ["task.repair.dism.description"] = "Sửa Windows component store.",
        ["task.repair.dism.impact"] = "Lệnh sửa Windows chạy lâu.",
        ["task.repair.sfc.label"] = "System File Checker",
        ["task.repair.sfc.description"] = "Chạy sfc /scannow.",
        ["task.repair.sfc.impact"] = "Quét tính toàn vẹn hệ thống, có thể chạy lâu.",
        ["task.repair.explorer.label"] = "Khởi động lại Explorer",
        ["task.repair.explorer.description"] = "Khởi động lại Windows Explorer.",
        ["task.repair.explorer.impact"] = "Taskbar và cửa sổ File Explorer sẽ làm mới.",
        ["task.optimization.hibernate.label"] = "Tắt hibernation",
        ["task.optimization.hibernate.description"] = "Chạy powercfg -h off.",
        ["task.optimization.hibernate.impact"] = "Giải phóng hiberfil.sys nhưng tắt Hibernate.",
        ["task.optimization.drives.label"] = "Tối ưu ổ đĩa",
        ["task.optimization.drives.description"] = "Chạy tối ưu ổ đĩa của Windows.",
        ["task.optimization.drives.impact"] = "TRIM/defrag các ổ cố định.",
        ["task.software.winget.label"] = "Cập nhật WinGet",
        ["task.software.winget.description"] = "Quét và nâng cấp package bằng winget.",
        ["task.software.winget.impact"] = "Ứng dụng có thể thay đổi phiên bản.",
        ["task.startup.scan.label"] = "Danh sách khởi động",
        ["task.startup.scan.description"] = "Đọc registry và folder khởi động.",
        ["task.startup.scan.impact"] = "Chỉ đọc danh sách.",
        ["task.settings.storage.label"] = "Storage Sense",
        ["task.settings.storage.description"] = "Mở cài đặt Storage Sense của Windows.",
        ["task.settings.storage.impact"] = "Dùng Windows Settings.",
        ["task.cli.launch.label"] = "Mở công cụ CLI",
        ["task.cli.launch.description"] = "Chạy src/cli/Utilities.ps1 bằng PowerShell.",
        ["task.cli.launch.impact"] = "Chạy workflow console hiện có.",
        ["task.storage.cleanup.label"] = "Dọn dẹp từ Phân tích lưu trữ"
    };

    public LocalizationService()
    {
        CurrentLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Vietnamese
            : AppLanguage.English;
    }

    public AppLanguage CurrentLanguage { get; set; }

    public string Get(string key)
    {
        var selected = CurrentLanguage == AppLanguage.Vietnamese ? _vietnamese : _english;
        if (selected.TryGetValue(key, out var value))
        {
            return value;
        }

        return _english.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(GetCulture(), Get(key), args);
    }

    public string TaskLabel(string taskId, string fallback)
    {
        var key = $"task.{taskId}.label";
        var value = Get(key);
        return value == key ? fallback : value;
    }

    public string TaskDescription(string taskId, string fallback)
    {
        var key = $"task.{taskId}.description";
        var value = Get(key);
        return value == key ? fallback : value;
    }

    public string TaskImpact(string taskId, string fallback)
    {
        var key = $"task.{taskId}.impact";
        var value = Get(key);
        return value == key ? fallback : value;
    }

    public string GroupName(string group)
    {
        var key = $"group.{group}";
        var value = Get(key);
        return value == key ? group : value;
    }

    public string RiskName(RiskLevel risk)
    {
        return Get($"risk.{risk}");
    }

    private CultureInfo GetCulture()
    {
        return CurrentLanguage == AppLanguage.Vietnamese
            ? CultureInfo.GetCultureInfo("vi-VN")
            : CultureInfo.GetCultureInfo("en-US");
    }
}
