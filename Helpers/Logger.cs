using System.Drawing;
using System.Windows.Forms;

namespace ADManager.Helpers;

public enum LogType { Normal, OK, Error, Info, Warning, Inactive, Summary }

public static class Logger
{
    private static RichTextBox? _logBox;

    public static void Init(RichTextBox box) => _logBox = box;

    public static void Write(string message, LogType type = LogType.Normal)
    {
        if (_logBox == null) return;

        // Если вызов не из UI-потока — инвокация
        if (_logBox.InvokeRequired)
        {
            _logBox.Invoke(() => Write(message, type));
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{time}] {message}";

        var color = type switch
        {
            LogType.OK       => Color.FromArgb(80, 200, 120),
            LogType.Error    => Color.FromArgb(255, 100, 100),
            LogType.Info     => Color.FromArgb(100, 180, 255),
            LogType.Warning  => Color.FromArgb(255, 170, 50),
            LogType.Inactive => Color.FromArgb(130, 140, 160),
            LogType.Summary  => Color.FromArgb(180, 140, 255),
            _                => Color.FromArgb(200, 210, 230)
        };

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = color;
        _logBox.AppendText(line + "\n");
        _logBox.ScrollToCaret();
    }
}