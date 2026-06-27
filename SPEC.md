# Spec: Windows System Maintenance Tool (WinOptimizationApp)

## Objective
Xây dựng một công cụ tối ưu hóa, dọn dẹp và bảo trì hệ thống Windows mạnh mẽ, toàn diện, thay thế cho các phần mềm rác hoặc lỗi thời. Ứng dụng cung cấp giao diện người dùng hiện đại, tốc độ cao, hỗ trợ đa ngôn ngữ, và an toàn bằng cách sử dụng các API gốc của Windows, WinGet và PowerShell. Lấy cảm hứng từ Winhance, FluentCleaner và CCleaner.

Các tính năng cốt lõi:
- **Dọn dẹp hệ thống (Cleanup)**: Tích hợp Winapp2, xóa file rác, dọn dẹp không gian lưu trữ và tối ưu hóa Privacy.
- **Bảo trì & Sửa lỗi (Repair)**: Cung cấp các công cụ sửa lỗi Windows tích hợp sẵn (SFC, DISM).
- **Quản lý Ứng dụng (App Management)**: 
  - Tự động hóa cài đặt ứng dụng (Software Installer) thông qua hệ thống WinGet (Silent Install).
  - Trình gỡ cài đặt Bloatware mạnh mẽ (Gỡ AppxPackage).
- **Tùy chỉnh hệ thống (System Tweaks)**: Các công tắc bật/tắt để tối ưu hóa Gaming, Privacy, UI/Taskbar và vô hiệu hóa Telemetry với khả năng Import/Export Profile.

## Tech Stack
- **Framework**: WinUI 3 / Windows App SDK (v2.1.3)
- **Language**: C# (.NET 10.0)
- **Architecture**: Mô hình Service-based với IPC (Inter-Process Communication) chạy quyền Administrator thông qua Named Pipes.
- **Testing Framework**: xUnit + Moq + Coverlet.

## Commands
*Sử dụng .NET CLI để thao tác với dự án.*
```powershell
# Build dự án chính
dotnet build src/app

# Chạy toàn bộ Unit Tests
dotnet test src/WinOptimizationApp.Tests

# Chạy ứng dụng ở chế độ Debug
dotnet run --project src/app

# Xuất bản (Publish) ứng dụng
dotnet publish src/app -c Release -r win-x64 --self-contained
```

## Project Structure
```text
.
├── src/
│   ├── app/                      → Mã nguồn ứng dụng WinUI 3 chính
│   │   ├── Assets/Langs/         → Các file ngôn ngữ JSON (en.json, vi.json)
│   │   ├── Models/               → Lớp dữ liệu (SystemTweak, WingetPackage...)
│   │   ├── Services/             → Lõi xử lý logic (TweakService, WingetService...)
│   │   ├── Views/                → Giao diện WinUI Pages (OptimizePage, SoftwareInstallerPage...)
│   │   └── MainWindow.cs         → Cửa sổ và Routing chính
│   │
│   └── WinOptimizationApp.Tests/ → Các bài kiểm tra Unit Tests (xUnit)
│       └── Unit/                 → Tách biệt các bài kiểm thử tương ứng với Services
├── .agents/                      → Thư mục cấu hình agent (Skills, Rules)
└── SPEC.md                       → Tài liệu đặc tả kỹ thuật dự án (File này)
```

## Code Style
1. **Sử dụng DAMP cho Testing và SOLID cho Services**:
   - Mọi class `Service` nên chịu một trách nhiệm duy nhất.
   - Code giao tiếp bên ngoài (PowerShell, Winget) phải sử dụng Dependency Injection hoặc Wrapper để dễ dàng Test.

2. **Ví dụ chuẩn hóa Service**:
```csharp
public sealed class TweakService
{
    private readonly CommandRunner _commands;
    
    public TweakService(CommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<bool> ApplyTweakAsync(string id, bool enable)
    {
        // Thực thi Logic
    }
}
```

3. **Nguyên tắc Async/Await**: 
   - Tất cả các tác vụ tốn thời gian (I/O, PowerShell) phải sử dụng `await Task` hoặc `Task.Run` để không block Main UI Thread.

## Testing Strategy
- **Framework**: xUnit.
- **Test Locations**: Nằm trong `src/WinOptimizationApp.Tests/Unit/`. Do WinUI khó tham chiếu trực tiếp, sử dụng cơ chế `<Compile Include="..." Link="..." />` trong file `.csproj` để tái sử dụng mã nguồn.
- **Test Levels**:
  - **Unit Tests (80%)**: Phủ sóng toàn bộ Logic `Services` (Parsing, Argument Generation, Data Filtering).
  - Test phải độc lập hoàn toàn, không phụ thuộc môi trường (No side effects).
- **Quy tắc (The Beyonce Rule)**: Mọi chức năng Service mới (ví dụ `WingetService`, `TweakService`) bắt buộc phải có Unit Test đi kèm để chứng minh nó hoạt động theo logic mong đợi.

## Boundaries
- **Always do**: 
  - Chạy toàn bộ Unit Tests trước khi merge tính năng mới (`dotnet test`).
  - Validation đầu vào của chuỗi dòng lệnh (Sử dụng `QuoteArgument` trong PowerShell/WinGet) để ngăn chặn Command Injection.
  - Tách rời mã ngôn ngữ (`.json`) khỏi mã nguồn C#.
- **Ask first**: 
  - Bổ sung thư viện/NuGet package mới (Chỉ thêm nếu thực sự không có giải pháp từ Base .NET).
  - Thay đổi cấu trúc cơ sở dữ liệu/File hệ thống quan trọng.
- **Never do**: 
  - Gọi các lệnh PowerShell dạng String Concatenation mà không escape chuỗi.
  - Sử dụng các vòng lặp Synchronous làm treo UI thread (Freeze app).
  - Hardcode đường dẫn tuyệt đối (Nên dùng `AppContext.BaseDirectory` hoặc `Environment.GetFolderPath`).

## Success Criteria
- [x] Giao diện người dùng mượt mà, hỗ trợ Dark/Light Theme.
- [x] `IpcServer` chạy ngầm, gọi PowerShell bằng quyền Admin thành công.
- [x] Tính năng Cleanup, Uninstall Bloatware, Install Winget hoạt động tự động.
- [x] Tính năng System Tweaks có thể Import/Export JSON Profile, cập nhật trạng thái theo thời gian thực.
- [x] Build thành công `0 Error(s)` và vượt qua 100% các bài Unit Tests.

## Open Questions
- Tính năng Tối ưu hoá (Tweaks) hiện đang gọi PowerShell từng dòng lệnh để check trạng thái. Về lâu dài có nên chuyển sang truy vấn trực tiếp bằng C# Registry (`Microsoft.Win32.Registry`) cho các thao tác đọc nhằm tăng tối đa hiệu suất khởi động giao diện?
