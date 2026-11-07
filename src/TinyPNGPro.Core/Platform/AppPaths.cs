using System;
using System.IO;

namespace TinyPNGPro.Core.Platform
{
    /// <summary>便携路径约定：配置、账本、日志全部位于 exe 同级目录。</summary>
    public static class AppPaths
    {
        public static string BaseDirectory
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(BaseDirectory, "config.ini"); }
        }

        public static string StatePath
        {
            get { return Path.Combine(BaseDirectory, "processed_state.json"); }
        }

        public static string LogDirectory
        {
            get { return Path.Combine(BaseDirectory, "logs"); }
        }
    }
}
