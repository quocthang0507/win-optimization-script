using System.Runtime.InteropServices;

namespace WinOptimizationApp.Services;

/// <summary>
/// Provides a folder-picker dialog that works even when the application is
/// running elevated (as Administrator).  The WinRT <c>FolderPicker</c> relies
/// on a broker process that refuses to enable the "Open" button for certain
/// known folders (e.g. Downloads) when the caller is elevated.  This helper
/// falls back to the classic Win32 <c>IFileOpenDialog</c> with
/// <c>FOS_PICKFOLDERS</c>, which does not have that limitation.
/// </summary>
public static class FolderPickerHelper
{
    public static string? PickFolder(IntPtr ownerHwnd)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        try
        {
            dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM | FOS.FOS_NOCHANGEDIR);
            var hr = dialog.Show(ownerHwnd);
            if (hr != 0) // User cancelled or error
            {
                return null;
            }

            dialog.GetResult(out var item);
            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
            Marshal.FreeCoTaskMem(path);
            return Marshal.PtrToStringUni(path);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    // --- COM interop declarations ---

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog { }

    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes();     // unused placeholder
        void SetFileTypeIndex();  // unused placeholder
        void GetFileTypeIndex();  // unused placeholder
        void Advise();            // unused placeholder
        void Unadvise();          // unused placeholder
        void SetOptions(FOS fos);
        void GetOptions();       // unused placeholder
        void SetDefaultFolder(); // unused placeholder
        void SetFolder();        // unused placeholder
        void GetFolder();        // unused placeholder
        void GetCurrentSelection(); // unused placeholder
        void SetFileName();      // unused placeholder
        void GetFileName();      // unused placeholder
        void SetTitle();         // unused placeholder
        void SetOkButtonLabel(); // unused placeholder
        void SetFileNameLabel(); // unused placeholder
        void GetResult(out IShellItem ppsi);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler();
        void GetParent();
        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
    }

    [Flags]
    private enum FOS : uint
    {
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_NOCHANGEDIR = 0x00000008
    }

    private enum SIGDN : uint
    {
        SIGDN_FILESYSPATH = 0x80058000
    }
}
