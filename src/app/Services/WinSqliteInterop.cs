using System.Runtime.InteropServices;

namespace WinOptimizationApp.Services;

internal static class WinSqliteInterop
{
    private const string LibraryName = "winsqlite3.dll";

    public const int SQLITE_OK = 0;
    public const int SQLITE_ROW = 100;
    public const int SQLITE_DONE = 101;

    public const int SQLITE_OPEN_READONLY = 0x00000001;
    public const int SQLITE_OPEN_READWRITE = 0x00000002;
    public const int SQLITE_OPEN_CREATE = 0x00000004;

    public static readonly IntPtr SQLITE_TRANSIENT = new(-1);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_open_v2(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filename,
        out IntPtr ppDb,
        int flags,
        IntPtr zVfs);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_close_v2(IntPtr db);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_exec(
        IntPtr db,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sql,
        IntPtr callback,
        IntPtr arg,
        out IntPtr errmsg);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_prepare_v2(
        IntPtr db,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string zSql,
        int nByte,
        out IntPtr ppStmt,
        IntPtr pzTail);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_bind_text(
        IntPtr pStmt,
        int index,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
        int nBytes,
        IntPtr xDel);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_step(IntPtr pStmt);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr sqlite3_column_text(IntPtr pStmt, int iCol);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_column_int(IntPtr pStmt, int iCol);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_finalize(IntPtr pStmt);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int sqlite3_changes(IntPtr db);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern IntPtr sqlite3_errmsg(IntPtr db);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void sqlite3_free(IntPtr ptr);

    public static string GetErrorMessage(IntPtr db)
    {
        if (db == IntPtr.Zero)
        {
            return "SQLite database handle is null.";
        }

        var ptr = sqlite3_errmsg(db);
        return ptr == IntPtr.Zero ? "Unknown SQLite error." : Marshal.PtrToStringUTF8(ptr) ?? "Unknown SQLite error.";
    }

    public static void ExecuteNonQuery(IntPtr db, string sql)
    {
        var rc = sqlite3_exec(db, sql, IntPtr.Zero, IntPtr.Zero, out var errPtr);
        if (rc != SQLITE_OK)
        {
            var msg = errPtr != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(errPtr) ?? GetErrorMessage(db)
                : GetErrorMessage(db);
            if (errPtr != IntPtr.Zero)
            {
                sqlite3_free(errPtr);
            }

            throw new InvalidOperationException($"SQLite error ({rc}): {msg}");
        }
    }
}
