namespace TinyPNGPro.App
{
    /// <summary>版本与产品信息（本项目为 .NET 便携版，文案只描述本实现）。</summary>
    public static class AppInfo
    {
        public const string Title = "TinyPNG Pro";
        public const string Version = "1.0.16";
        public const string ReleaseDate = "2026-11-07";

        public static string AboutText()
        {
            return "v" + Version + " · Windows 10 / 11 便携版 · .NET Framework 4.8 · 单个 exe\r\n" +
                   "\r\n" +
                   "▍这是什么\r\n" +
                   "  面向 Windows 的 TinyPNG 桌面客户端：把本地图片提交给 TinyPNG\r\n" +
                   "  在线压缩服务，再把结果写回本地。解压即用，配置与日志都在 exe\r\n" +
                   "  同目录，不依赖安装程序。\r\n" +
                   "\r\n" +
                   "▍能力范围\r\n" +
                   "  · 拖放或选择 PNG / JPEG / WebP 图片压缩\r\n" +
                   "  · 监控文件夹：启动后新增的图片自动入队（非递归）\r\n" +
                   "  · 最多保存 3 个 TinyPNG 密钥，可切换并在线验证\r\n" +
                   "  · 替换原文件，或输出到指定目录（文件名加 _compressed）\r\n" +
                   "  · 每月 500 次额度进度，读取 Compression-Count 响应头\r\n" +
                   "  · 手动任务优先的单线程队列，批量结束汇总节省率与耗时\r\n" +
                   "  · 夜间 / 明亮双主题，切换后立即生效\r\n" +
                   "  · 托盘常驻、窗口置顶、开机自启\r\n" +
                   "  · 按日期切分日志，可在应用内查看与清空\r\n" +
                   "\r\n" +
                   "▍技术栈\r\n" +
                   "  · 语言 / 运行时：C# 5.0，.NET Framework 4.8（系统内置）\r\n" +
                   "  · 界面：WinForms + GDI+ 全自绘控件，无 NuGet、无第三方库\r\n" +
                   "  · 网络：HttpClient 直连 api.tinify.com，无第三方 SDK\r\n" +
                   "  · 产物：单个 exe（约 120 KB），不附带 DLL\r\n" +
                   "\r\n" +
                   "▍构建方式\r\n" +
                   "  · 运行 build-nosdk.cmd，调用系统自带 csc.exe 编译\r\n" +
                   "  · 不需要安装 SDK 或 NuGet，/target:winexe 输出单文件\r\n" +
                   "  · 生成位置：dist\\TinyPNG Pro.exe\r\n" +
                   "\r\n" +
                   "▍数据与配置（均与 exe 同目录）\r\n" +
                   "  · config.ini：密钥、压缩与界面设置\r\n" +
                   "  · processed_state.json：自动压缩去重账本\r\n" +
                   "  · logs\\：按日期命名的日志\r\n" +
                   "  · 删除上述文件即可恢复默认设置\r\n" +
                   "\r\n" +
                   "▍交互与托盘\r\n" +
                   "  · 无边框窗口，四边四角拖动可缩放\r\n" +
                   "  · 左键 / 中键按住窗口任意位置可拖动窗口\r\n" +
                   "  · 任意位置右键：设置 / 日志 / 最小化到托盘 / 退出\r\n" +
                   "  · 关闭按钮等于隐藏到托盘，退出请用「退出」\r\n" +
                   "  · 托盘单击或双击恢复窗口，托盘菜单含设置与日志\r\n" +
                   "\r\n" +
                   "压缩服务与每月免费额度由 TinyPNG 官方提供。\r\n";
        }
    }
}
