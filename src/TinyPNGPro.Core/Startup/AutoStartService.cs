using System;
using System.Diagnostics;
using Microsoft.Win32;
using TinyPNGPro.Core.Logging;

namespace TinyPNGPro.Core.Startup
{
    /// <summary>开机自启：HKCU Run 键，值名与 Python 版一致。</summary>
    public static class AutoStartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TinyPNG Pro";

        public static string ExecutablePath
        {
            get
            {
                try
                {
                    return Process.GetCurrentProcess().MainModule.FileName;
                }
                catch
                {
                    return AppDomain.CurrentDomain.BaseDirectory;
                }
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    object value = key.GetValue(ValueName);
                    return value != null &&
                           string.Equals(value.ToString(), ExecutablePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error checking auto-start: " + ex.Message);
                return false;
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    if (enabled)
                    {
                        key.SetValue(ValueName, ExecutablePath, RegistryValueKind.String);
                        AppLogger.Info("Auto-start enabled");
                    }
                    else
                    {
                        try
                        {
                            key.DeleteValue(ValueName, false);
                        }
                        catch
                        {
                        }
                        AppLogger.Info("Auto-start disabled");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error setting auto-start: " + ex.Message);
                return false;
            }
        }
    }
}
