using System;
using System.IO;

namespace WinOptimizationApp.Services;

public static class PathExpander
{
    public static string Expand(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        // Standard Windows environment variables
        var expanded = Environment.ExpandEnvironmentVariables(path);

        // Winapp2 specific custom variables
        if (expanded.Contains("%Documents%"))
            expanded = expanded.Replace("%Documents%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            
        if (expanded.Contains("%CommonAppData%"))
            expanded = expanded.Replace("%CommonAppData%", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

        if (expanded.Contains("%LocalAppDataLow%"))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = expanded.Replace("%LocalAppDataLow%", Path.Combine(userProfile, "AppData", "LocalLow"));
        }

        if (expanded.Contains("%ProgramFiles%"))
            expanded = expanded.Replace("%ProgramFiles%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

        if (expanded.Contains("%ProgramFiles(x86)%"))
            expanded = expanded.Replace("%ProgramFiles(x86)%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        return expanded;
    }
}
