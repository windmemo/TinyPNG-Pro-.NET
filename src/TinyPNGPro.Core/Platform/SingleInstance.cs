using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TinyPNGPro.Core.Platform
{
    /// <summary>命名互斥量单实例保护；已存在实例时唤回其主窗口。</summary>
    public static class SingleInstance
    {
        private const string MutexName = "TinyPNGPro_SingleInstance_v1";
        private static Mutex _mutex;

        public static bool TryAcquire()
        {
            try
            {
                bool createdNew;
                _mutex = new Mutex(true, MutexName, out createdNew);
                if (createdNew)
                {
                    return true;
                }
                _mutex.Dispose();
                _mutex = null;
                return false;
            }
            catch
            {
                // 获取失败时放行，避免误伤正常启动
                return true;
            }
        }

        public static bool ActivateExistingWindow(string title)
        {
            try
            {
                IntPtr hwnd = NativeMethods.FindWindowW(null, title);
                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }
                NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
                NativeMethods.SetForegroundWindow(hwnd);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ShowAlreadyRunningMessage()
        {
            try
            {
                NativeMethods.MessageBoxW(IntPtr.Zero,
                    "TinyPNG Pro 已在运行中，请在系统托盘中查看。",
                    "TinyPNG Pro",
                    NativeMethods.MbIconInformation);
            }
            catch
            {
            }
        }

        private static class NativeMethods
        {
            public const int SwRestore = 9;
            public const int MbIconInformation = 0x40;

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
        }
    }
}
