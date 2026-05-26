using System.Drawing;
using System.Windows.Forms;

namespace ADManager.Helpers;

public enum LogType { Normal, OK, Error, Info, Warning, Inactive, Summary }

public static class Logger
{
    private static RichTextBox? _logBox;
    private static string?      _logFilePath;
    private static readonly object _fileLock = new();

    public static void Init(RichTextBox box) => _logBox = box;

    public static void Write(string message, LogType type = LogType.Normal)
    {
        if (type is LogType.Error or LogType.Warning)
        {
            string? justCreated = WriteToFile(message, type);
            if (justCreated != null)
                WriteToUi($"Лог ошибок создан: {justCreated}", LogType.Info);
        }
        WriteToUi(message, type);
    }

    // Возвращает путь к файлу, если файл был создан в этом вызове; иначе null.
    private static string? WriteToFile(string message, LogType type)
    {
        string? newPath = null;
        lock (_fileLock)
        {
            try
            {
                if (_logFilePath == null)
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ADManager");
                    Directory.CreateDirectory(dir);
                    _logFilePath = Path.Combine(dir, $"admanager_{DateTime.Now:yyyy-MM-dd}.log");
                    newPath = _logFilePath;
                }

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] {message}";
                File.AppendAllText(_logFilePath, line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Сбрасываем путь, чтобы следующий вызов попробовал снова
                _logFilePath = null;
                newPath      = null;
                WriteToUi($"Не удалось записать лог в файл: {ex.Message}", LogType.Warning);
            }
        }
        return newPath;
    }

    private static void WriteToUi(string message, LogType type)
    {
        if (_logBox == null) return;

        if (_logBox.InvokeRequired)
        {
            _logBox.Invoke(() => WriteToUi(message, type));
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

        _logBox.SelectionStart  = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor  = color;
        _logBox.AppendText(line + "\n");
        _logBox.ScrollToCaret();
    }
}
