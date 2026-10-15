namespace grapher.Common
{
    /// <summary>Display names only. Persisted theme identifiers stay compatible with upstream.</summary>
    internal static class Chinese
    {
        public static string ErrorMessage(string message)
        {
            if (message.Contains("Raw Accel is not installed, run installer.exe"))
                return "Raw Accel 驱动尚未安装或尚未生效。请以管理员身份运行同目录中的 installer.exe，然后重启电脑。";
            if (message.Contains("newer driver is installed"))
                return "当前驱动版本比此界面更新，请使用与驱动配套的新版界面。";
            return message;
        }

        public static string ThemeName(string name)
        {
            switch (name)
            {
                case "Light Theme": return "浅色";
                case "Light Streamer Theme": return "浅色直播";
                case "Dark Theme": return "深色";
                case "Accented Dark Theme": return "强调色深色";
                case "Dark Streamer Theme": return "深色直播";
                default: return name;
            }
        }
    }
}
