# Spec: Windows System Maintenance Tool (WinOptimizationApp)

## Assumptions
1. Ứng dụng Native Windows (WinUI 3 / Windows App SDK v2.1.3) mục tiêu `.NET 10.0-windows10.0.19041.0` chạy trên Windows 10 và Windows 11.
2. Không thêm thư viện NuGet bên ngoài cho SQLite: sử dụng P/Invoke gọi `winsqlite3.dll` tích hợp sẵn trên Windows 10/11 cho tính năng Browser Cookie Keep-List.
3. Cấu hình người dùng mới (`PinnedWingetPackageIds`, `StorageBookmarks`, `CookieKeepDomains`) được lưu chung trong `AppSettings` (`settings.json`) để tự động tương thích với tính năng Export/Import Settings.
4. Quá trình triển khai chia làm 3 Phase tuần tự, giữ `0 Error(s)` và `100% Pass` cho toàn bộ Unit Tests sau mỗi bước, và tách `src/core/WinOptimization.Core.csproj` ở Phase 3.

## Objective
Xây dựng một công cụ tối ưu hóa, dọn dẹp và bảo trì hệ thống Windows mạnh mẽ, toàn diện, thay thế cho các phần mềm rác hoặc lỗi thời. Ứng dụng cung cấp giao diện người dùng hiện đại, tốc độ cao, hỗ trợ đa ngôn ngữ, và an toàn bằng cách sử dụng các API gốc của Windows, WinGet và PowerShell. Lấy cảm hứng từ Winhance, FluentCleaner, Win11Debloat, Optimizer, QDirStat và WinMole.

Các tính năng cốt lõi & mở rộng:
- **Dọn dẹp hệ thống (Cleanup)**: Tích hợp Winapp2, xóa file rác, dọn dẹp không gian lưu trữ, tối ưu hóa Privacy và **Browser Cookie Keep-List** (giữ lại cookie các trang được chọn qua `winsqlite3.dll`).
- **Bảo trì & Sửa lỗi (Repair & Storage Sense)**: Cung cấp các công cụ sửa lỗi Windows tích hợp sẵn (SFC, DISM), cấu hình **Windows Storage Sense** và nén hệ thống **CompactOS**.
- **Quản lý Ứng dụng (App Management)**:
  - Tự động hóa cài đặt ứng dụng (Software Installer) qua WinGet.
  - Cập nhật ứng dụng có hỗ trợ **Pin / Ignore Package** và kiểm soát **Uninstall Timeout** kèm báo cáo từng phần (partial batch report).
  - Trình gỡ cài đặt Bloatware mạnh mẽ (Gỡ AppxPackage).
- **Tùy chỉnh hệ thống (System Tweaks & Builder)**:
  - Đọc trạng thái trực tiếp qua C# Registry (`Microsoft.Win32.Registry`) tốc độ cao.
  - Lưu **Exact Typed Registry Snapshot** (kiểu dữ liệu và giá trị gốc trước khi sửa) và tự động dọn snapshot cũ theo cấu hình retention.
  - Chế độ **Configuration Builder** cho phép thiết kế và xuất gói cấu hình tổng hợp (Tweaks + WinGet Apps + Cleanup Tasks) mà không áp dụng lên máy hiện tại.
- **Bộ công cụ nâng cao (Toolbox & Storage Analyzer)**:
  - **Storage Analyzer**: Hỗ trợ **Storage Bookmarks** và tìm kiếm đệ quy có giới hạn bộ nhớ (**Bounded Recursive Search**) theo biểu thức tên/kích thước/tuổi file.
  - **Context Menu Manager**: Chuyển đổi menu chuột phải Win11/Classic và bật/tắt an toàn các Shell Extensions bên thứ ba.
  - **Battery Health & Power Plan Manager**: Phân tích độ chai pin (`DesignCapacity` vs `FullChargeCapacity`), chuyển đổi Power Plan và kiểm tra tiến trình giữ máy thức (`powercfg /requests`).
- **Giao thức IPC song công có `RequestId`**: Hỗ trợ hủy tác vụ từ xa (`CancelRequest`) mà không đóng Named Pipe.
- **Đồng bộ PowerShell CLI (`Utilities.ps1`)**: Hỗ trợ lệnh chính chủ cho Developer Cache, chế độ `--Preview`, hiển thị Risk Badge và ghi báo cáo JSON vào `logs/`.

## Tech Stack
- **Framework**: WinUI 3 / Windows App SDK (v2.1.3)
- **Language**: C# 12 (.NET 10.0), PowerShell 5.1+
- **Native Interop**: `Microsoft.Win32.Registry`, `winsqlite3.dll` (P/Invoke), Win32 Named Pipes
- **Architecture**: Mô hình `WinOptimization.Core` (Class Library dùng chung) + `WinOptimizationApp` (WinUI 3 + Runner IPC) + `Utilities.ps1` (PowerShell CLI)
- **Testing Framework**: xUnit + Coverlet

## Commands
```powershell
# Build toàn bộ solution
dotnet build src/WinOptimizationApp.slnx

# Build dự án ứng dụng chính
dotnet build src/app/WinOptimizationApp.csproj

# Chạy toàn bộ Unit Tests
dotnet test src/WinOptimizationApp.Tests/WinOptimizationApp.Tests.csproj

# Chạy ứng dụng ở chế độ Debug
dotnet run --project src/app/WinOptimizationApp.csproj

# Chạy CLI ở chế độ Preview (Dry-Run)
powershell -ExecutionPolicy Bypass -File .\src\cli\Utilities.ps1 -Preview

# Xuất bản (Publish) ứng dụng
dotnet publish src/app/WinOptimizationApp.csproj -c Release -r win-x64 --self-contained
```

## Project Structure
```text
.
├── src/
│   ├── core/                         → Class Library dùng chung (Models & Services không phụ thuộc WinUI)
│   │   ├── Models/                   → Lớp dữ liệu (AppSettings, SystemTweak, WingetPackage...)
│   │   ├── Services/                 → Lõi xử lý nghiệp vụ (TweakService, CleanupService, IpcClient...)
│   │   └── WinOptimization.Core.csproj
│   ├── app/                          → Mã nguồn giao diện WinUI 3 và điểm khởi chạy Runner/UI
│   │   ├── Assets/Langs/             → Các file ngôn ngữ JSON (en.json, vi.json)
│   │   ├── Views/                    → Giao diện WinUI Pages (OptimizePage, ToolboxPage, StoragePage...)
│   │   ├── MainWindow.cs             → Cửa sổ và Routing chính
│   │   └── WinOptimizationApp.csproj
│   ├── cli/                          → Script PowerShell tối ưu hệ thống (Utilities.ps1)
│   └── WinOptimizationApp.Tests/     → Các bài kiểm tra Unit Tests (xUnit)
│       └── Unit/                     → Kiểm thử đơn vị cho toàn bộ Services & Models
├── docs/                             → Tài liệu kiến trúc, kế hoạch và báo cáo kiểm toán
└── SPEC.md                           → Tài liệu đặc tả kỹ thuật dự án (File này)
```

## Code Style
1. **Sử dụng DAMP cho Testing và SOLID cho Services**:
   - Mọi class `Service` chịu một trách nhiệm duy nhất và cho phép truyền abstraction (như `CommandRunner`, `IRegistryAccessor` hoặc đường dẫn tạm) để test độc lập.
2. **Ví dụ chuẩn hóa Service**:
```csharp
public sealed class TweakService
{
    private readonly CommandRunner _commands;
    private readonly IRegistryAccessor _registry;

    public TweakService(CommandRunner commands, IRegistryAccessor? registry = null)
    {
        _commands = commands;
        _registry = registry ?? new WindowsRegistryAccessor();
    }

    public async Task<TweakStateResponse> CheckTweakStateAsync(string id, CancellationToken cancellationToken = default)
    {
        // Ưu tiên đọc trực tiếp qua _registry trước khi fallback sang PowerShell
    }
}
```
3. **Nguyên tắc Async/Await & Cancellation**:
   - Tất cả các tác vụ I/O, IPC, tiến trình ngoài phải nhận `CancellationToken` và không làm treo Main UI Thread.

## Testing Strategy
- **Framework**: xUnit.
- **Test Locations**: `src/WinOptimizationApp.Tests/Unit/`.
- **Test Levels**:
  - **Unit Tests (80%+)**: Phủ sóng toàn bộ Logic `Services` (Registry evaluation, Snapshot serialization, IPC RequestId correlation, Recursive Storage Search, Cookie Keep-List SQL generation, Battery/Power parsing, Context Menu discovery, Setup Builder export/import).
  - Test độc lập hoàn toàn với môi trường máy thật (sử dụng In-Memory Fake Registry, Temp Directory, và Mock/Fake `CommandRunner`).
- **Quy tắc (The Beyonce Rule)**: Mọi chức năng Service mới bắt buộc có Unit Test viết theo chu trình TDD (RED -> GREEN -> REFACTOR).

## Boundaries
- **Always do**:
  - Chạy toàn bộ Unit Tests trước và sau mỗi hạng mục (`dotnet test`).
  - Validation đầu vào của chuỗi dòng lệnh và tham số SQLite/Registry để ngăn chặn Injection.
  - Bổ sung đầy đủ khóa dịch tiếng Anh (`en.json`) và tiếng Việt (`vi.json`) cho mọi tính năng mới.
- **Ask first**:
  - Bổ sung thư viện/NuGet package mới ngoài Base .NET.
  - Thay đổi hành vi xóa mặc định sang xóa vĩnh viễn mà không qua xác nhận.
- **Never do**:
  - Gọi các lệnh PowerShell dạng String Concatenation mà không escape chuỗi.
  - Đóng cưỡng bức trình duyệt của người dùng khi dọn Cookie hoặc Cache.
  - Xóa vĩnh viễn khóa Registry của Context Menu khi người dùng chỉ chọn Disable.

## Success Criteria
- [x] **Phase 1**:
  - `TweakService` đọc trạng thái Registry trực tiếp bằng C# (`IRegistryAccessor`) không cần gọi PowerShell cho các tweak Registry chuẩn.
  - `TweakSnapshotService` lưu và khôi phục chính xác kiểu dữ liệu/giá trị Registry gốc (`Exact Typed Registry Snapshot`) và hỗ trợ tự động dọn snapshot cũ (`PruneSnapshots`).
  - `UpdatesPage` và `WingetService` hỗ trợ ghim/bỏ qua gói cập nhật (`PinnedWingetPackageIds` trong `AppSettings`) và `UninstallerService` xử lý timeout rõ ràng kèm báo cáo từng phần khi hủy.
- [x] **Phase 2**:
  - `IpcClient` / `IpcServer` truyền `RequestId` trên mỗi `IpcMessage` và hỗ trợ `CancelRequest` hủy tác vụ đang chạy trên Runner mà không ngắt Named Pipe.
  - `StoragePage` và `DiskAnalysisService` hỗ trợ `StorageBookmarks` và tìm kiếm đệ quy có giới hạn (`Bounded Recursive Search`) theo từ khóa, kích thước tối thiểu và tuổi file.
  - Bổ sung `PowerManagementService` (Battery Health, Power Plans, Power Requests) và `StorageSenseService` (cấu hình Windows Storage Sense & CompactOS).
- [x] **Phase 3**:
  - Tách `src/core/WinOptimization.Core.csproj` dùng chung cho `WinOptimizationApp` và `WinOptimizationApp.Tests`.
  - Bổ sung **Configuration Builder** (`SetupConfigurationService`), **Context Menu Manager** (`ContextMenuService`), và **Browser Cookie Keep-List** (`BrowserCookieService` dùng `winsqlite3.dll`).
  - Nâng cấp `src/cli/Utilities.ps1` với `-Preview`, Risk Badges, lệnh chính chủ cho Developer Cache và xuất báo cáo JSON vào `logs/`.
  - Toàn bộ solution build `0 Warning(s)`, `0 Error(s)` và pass 100% Unit Tests.
