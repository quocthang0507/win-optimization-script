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
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    public static string? PickFolder(IntPtr ownerHwnd, string? initialFolder = null)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        IShellItem? initialItem = null;
        IShellItem? resultItem = null;
        try
        {
            dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM | FOS.FOS_NOCHANGEDIR);
            if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
            {
                try
                {
                    var shellItemId = typeof(IShellItem).GUID;
                    if (SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref shellItemId, out initialItem) == 0)
                    {
                        dialog.SetDefaultFolder(initialItem);
                        dialog.SetFolder(initialItem);
                    }
                }
                catch (COMException)
                {
                    // The picker can still open normally if the suggested folder is unavailable.
                }
            }

            var hr = dialog.Show(ownerHwnd);
            if (hr != 0) // User cancelled or error
            {
                return null;
            }

            dialog.GetResult(out resultItem);
            resultItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var pathPointer);
            try
            {
                return Marshal.PtrToStringUni(pathPointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (resultItem != null)
            {
                Marshal.ReleaseComObject(resultItem);
            }
            if (initialItem != null)
            {
                Marshal.ReleaseComObject(initialItem);
            }
            Marshal.ReleaseComObject(dialog);
        }
    }

    internal static string GetDownloadsFolder()
    {
        var folderId = DownloadsFolderId;
        if (SHGetKnownFolderPath(ref folderId, 0, IntPtr.Zero, out var pathPointer) == 0)
        {
            try
            {
                var path = Marshal.PtrToStringUni(pathPointer);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    // --- COM interop declarations ---

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog { }

    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint fileTypeCount, IntPtr filterSpecifications);
        void SetFileTypeIndex(uint fileTypeIndex);
        void GetFileTypeIndex(out uint fileTypeIndex);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(FOS fos);
        void GetOptions(out FOS fos);
        void SetDefaultFolder(IShellItem shellItem);
        void SetFolder(IShellItem shellItem);
        void GetFolder(out IShellItem shellItem);
        void GetCurrentSelection(out IShellItem shellItem);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName(out IntPtr name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem ppsi);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem shellItem, uint hint, out int order);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        ref Guid interfaceId,
        out IShellItem shellItem);

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHGetKnownFolderPath(
        ref Guid folderId,
        uint flags,
        IntPtr userToken,
        out IntPtr path);

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
