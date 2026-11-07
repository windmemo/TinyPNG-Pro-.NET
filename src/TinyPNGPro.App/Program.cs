using System;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using TinyPNGPro.Core.Logging;
using TinyPNGPro.Core.Platform;

namespace TinyPNGPro.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
            catch
            {
            }

            if (!SingleInstance.TryAcquire())
            {
                if (!SingleInstance.ActivateExistingWindow(AppInfo.Title))
                {
                    SingleInstance.ShowAlreadyRunningMessage();
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            AppHost host = new AppHost();
            host.Start();
            try
            {
                Application.Run(host.MainForm);
            }
            finally
            {
                host.Shutdown();
            }
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            HandleFatal(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            HandleFatal(ex);
        }

        private static void HandleFatal(Exception ex)
        {
            if (ex == null)
            {
                return;
            }
            string message = ex.Message;
            string full = ex.ToString();
            try
            {
                AppLogger.Error("Unhandled exception: " + full);
            }
            catch
            {
            }
            try
            {
                // 额外落一份到 exe 同目录，便于在日志节流/未初始化时定位。
                File.AppendAllText(
                    Path.Combine(AppPaths.BaseDirectory, "crash.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + full +
                    Environment.NewLine + new string('-', 60) + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
            }
            try
            {
                MessageBox.Show(message, AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
