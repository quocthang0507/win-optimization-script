# Spec: WinOptimizationApp (Tài liệu Đặc tả Hệ thống)

Dưới đây là tài liệu đặc tả (Specification Document) rà soát lại toàn bộ nghiệp vụ hiện tại của hệ thống dựa trên chuẩn `/spec-driven-development`. Tài liệu này sẽ đóng vai trò là "Source of Truth" cho toàn bộ quá trình phát triển tiếp theo.

## ASSUMPTIONS I'M MAKING (Các giả định hiện tại):
1. Đây là ứng dụng Native Windows (WinUI 3 / Windows App SDK) chỉ chạy trên Windows 10/11.
2. Ứng dụng kết hợp giữa C# (UI & Background Services) và PowerShell (Thực thi các lệnh can thiệp hệ thống sâu) để đạt hiệu năng tối đa.
3. Không sử dụng Database ngoài (chỉ dùng `settings.json` lưu cấu hình).

---

## 1. Objective (Mục tiêu cốt lõi)
Xây dựng một công cụ tối ưu hóa Windows toàn diện, nhẹ, nhanh và an toàn. Giúp người dùng dọn dẹp hệ thống, tăng tốc độ xử lý mạng/tiến trình, và quản lý phần mềm/khởi động dễ dàng mà không đi kèm quảng cáo hay bloatware.

**Các Nghiệp Vụ (Business Logic) Đã Có:**
1. **Dashboard & Monitoring:** Hiển thị tổng quan tình trạng máy tính (CPU, RAM, Disk, Network) theo thời gian thực.
2. **Storage Cleanup (Dọn rác ổ đĩa):** Quét và xóa an toàn các file tạm (Temp), cache trình duyệt, file Windows Update cũ.
3. **Registry Cleaner:** Phát hiện và dọn dẹp các khóa Registry rác (Obsolete keys).
4. **Startup Manager:** Liệt kê, đánh giá mức độ ảnh hưởng (Impact) và bật/tắt các chương trình khởi động cùng Windows.
5. **App Uninstaller (Tích hợp Winget):** Gỡ cài đặt ứng dụng Win32/UWP và cập nhật phần mềm thông qua Winget.
6. **Network & Process Optimization:** Dọn DNS (Flush DNS), Reset Winsock, làm trống bộ nhớ RAM dự phòng, áp dụng EcoQoS cho các tiến trình nền.
7. **Mini Widget (Toolbar):** Widget nổi trên màn hình hỗ trợ xem nhanh trạng thái máy tính và truy cập tiện ích nhanh.

---

## 2. Tech Stack (Công nghệ sử dụng)
- **Ngôn ngữ:** C# 12, PowerShell 7/5.1.
- **Framework:** .NET 10.0, WinUI 3 (Windows App SDK).
- **Thư viện chính:** `System.Text.Json` (Cấu hình), `xUnit` (Kiểm thử).

---

## 3. Commands (Các lệnh thao tác dự án)
- **Build (Biên dịch):** `dotnet build src/app`
- **Test (Kiểm thử):** `dotnet test src/WinOptimizationApp.Tests`
- **Format Code:** `dotnet format src/app`

---

## 4. Project Structure (Cấu trúc dự án)
```text
/src
  /app/               → Chứa toàn bộ mã nguồn WinUI 3 (Mô hình MVVM/Services)
    /Models/          → Định nghĩa cấu trúc dữ liệu (AppSettings, SystemMetrics)
    /Services/        → Logic nghiệp vụ chính (CleanupService, StartupService)
    /Views/           → Các giao diện XAML (MainWindow, DashboardPage)
  /cli/               → Chứa mã nguồn PowerShell (Utilities.ps1)
  /WinOptimizationApp.Tests/ → Các test case (Unit & Integration)
```

---

## 5. Testing Strategy (Chiến lược kiểm thử)
- **Framework:** `xUnit`.
- **Vị trí Test:** Tách biệt trong `src/WinOptimizationApp.Tests/` (chia thành `Unit` và `Integration`).
- **Chiến lược (TDD):** Theo nguyên tắc RED-GREEN-REFACTOR. Các Service xử lý logic nền (như `CommandRunner`, `AppSettingsService`) phải được mock file hoặc dùng thư mục tạm (Temp Directory) để không ảnh hưởng đến máy tính thật. Mọi tính năng mới/sửa lỗi phải có test đi kèm.

---

## 6. Boundaries (Ranh giới & Quy tắc)
- **Always do (Luôn luôn):** 
  - Áp dụng kiểm thử `dotnet test` trước khi commit.
  - Sử dụng tham số an toàn trong PowerShell (không dán trực tiếp biến vào command line để tránh Command Injection).
  - Gom các thay đổi UI vào luồng chính (DispatcherQueue).
- **Ask first (Hỏi trước ý kiến):** 
  - Chạy các lệnh có tính phá hủy (ví dụ: Xóa Registry diện rộng, Xóa thư mục System).
  - Cập nhật thư viện hoặc Framework sang phiên bản mới.
- **Never do (Tuyệt đối không):** 
  - Vô hiệu hóa tính năng Single-Instance Guard (Bảo vệ một phiên chạy).
  - Nuốt lỗi ẩn (Dùng `catch {}` không mục đích) khiến lỗi bị che giấu.
