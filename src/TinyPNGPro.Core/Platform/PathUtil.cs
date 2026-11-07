using System;
using System.Globalization;
using System.IO;

namespace TinyPNGPro.Core.Platform
{
    /// <summary>路径归一化、文件类型判定与体积格式化（与 Python 版语义保持一致）。</summary>
    public static class PathUtil
    {
        public const string CompressedSuffix = "_compressed";

        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

        /// <summary>统一为绝对路径并消除大小写与分隔符差异。</summary>
        public static string NormKey(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            try
            {
                string full = Path.GetFullPath(path);
                return full.Replace('/', '\\').ToLowerInvariant();
            }
            catch
            {
                return path.ToLowerInvariant();
            }
        }

        public static bool IsPngFile(string path)
        {
            return path != null && path.ToLowerInvariant().EndsWith(".png");
        }

        public static bool IsSupportedFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            string lower = path.ToLowerInvariant();
            for (int i = 0; i < SupportedExtensions.Length; i++)
            {
                if (lower.EndsWith(SupportedExtensions[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsCompressedOutput(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            bool supported = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp";
            return supported && name.EndsWith(CompressedSuffix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAllowedFile(string path, bool onlyPng)
        {
            return onlyPng ? IsPngFile(path) : IsSupportedFile(path);
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            }
            if (bytes < 1024 * 1024)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:F2} KB", bytes / 1024.0);
            }
            return string.Format(CultureInfo.InvariantCulture, "{0:F2} MB", bytes / (1024.0 * 1024.0));
        }

        public static string FormatRatio(long original, long compressed)
        {
            if (original <= 0)
            {
                return "0%";
            }
            double ratio = (1.0 - (double)compressed / (double)original) * 100.0;
            return string.Format(CultureInfo.InvariantCulture, "{0:F2}%", ratio);
        }

        public static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
