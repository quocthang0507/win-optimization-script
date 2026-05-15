# Windows System Maintenance Tool 🛠️

Một script PowerShell mạnh mẽ giúp tối ưu hóa, dọn dẹp và sửa lỗi hệ thống Windows một cách nhanh chóng và hiệu quả.

![GitHub last commit](https://img.shields.io/github/last-commit/quoct/win-optimization-script)
![PowerShell](https://img.shields.io/badge/PowerShell-%3E%3D%205.1-blue.svg)

## 🌟 Tính năng chính

Công cụ được chia thành các nhóm chức năng rõ ràng:

### 1. Dọn dẹp nhanh (Quick Cleanup)
- Xóa file tạm hệ thống (Temp files).
- Xóa cache trình duyệt (Edge, Chrome).
- Làm trống Thùng rác (Recycle Bin).
- Xóa bộ nhớ đệm Clipboard.

### 2. Dọn dẹp chuyên sâu (Deep Cleanup)
- Chạy Disk Cleanup (Tự động hoặc Giao diện).
- Dọn dẹp cache của lập trình viên (NuGet, pip, npm, yarn).
- Xóa bộ nhớ đệm Windows Update.
- Xóa Windows Event Logs để giải phóng không gian.
- Gỡ bỏ các file cài đặt Windows cũ (`Windows.old`).

### 3. Sửa lỗi hệ thống (Repair)
- Chạy DISM Repair để sửa lỗi Windows Image.
- Chạy System File Checker (SFC Scan) để kiểm tra tính toàn vẹn của file hệ thống.
- Khởi động lại Windows Explorer.

### 4. Phần mềm & Tiện ích
- Cập nhật tất cả phần mềm qua `winget upgrade --all`.
- Hỗ trợ biên dịch script sang file `.exe`.

### 5. Mạng & Thông tin
- Làm mới bộ nhớ đệm DNS (Flush DNS).
- Kiểm tra thời gian hệ thống đã hoạt động (Uptime).

### 6. Quyền riêng tư (Privacy)
- Xóa lịch sử lệnh PowerShell.
- Xóa danh sách các mục đã mở gần đây trên Start/Taskbar (Windows 11).

## 📋 Yêu cầu hệ thống

- **Hệ điều hành:** Windows 10 hoặc Windows 11.
- **Quyền hạn:** Cần chạy dưới quyền **Administrator**.
- **PowerShell:** Phiên bản 5.1 trở lên.

## 🚀 Cách sử dụng

1. Tải về hoặc clone repository này.
2. Chuột phải vào file `Utilities.ps1` và chọn **Run with PowerShell**.
3. Nếu script yêu cầu quyền Admin, hãy chọn **Yes**.
4. Chọn các tùy chọn theo số tương ứng trong menu.

**Mẹo:** Bạn có thể sử dụng file `Utilities.exe` (nếu đã được biên dịch) để chạy trực tiếp mà không cần mở PowerShell.

## 🛠️ Biên dịch sang EXE

Nếu bạn muốn tạo file thực thi riêng:
1. Mở script bằng PowerShell.
2. Chọn mục **Software** -> **Quick compile this tool to EXE**.
3. Script sẽ tự động cài đặt module `PS2EXE` (nếu chưa có) và tạo file `.exe` cho bạn.

## ⚠️ Cảnh báo (Disclaimer)

Script này thực hiện các thay đổi sâu vào hệ thống. Mặc dù nó đã được thiết kế an toàn, hãy lưu ý:
- Tác giả không chịu trách nhiệm về bất kỳ sự cố mất dữ liệu hoặc lỗi hệ thống nào phát sinh.
- Nên đóng các trình duyệt trước khi chạy tính năng dọn dẹp cache trình duyệt.
- Nên tạo điểm khôi phục hệ thống (Restore Point) trước khi thực hiện các thay đổi lớn.

---
*Phát triển bởi [quoct](https://github.com/quoct)*
