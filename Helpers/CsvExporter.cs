using System.Text;
using System.Windows.Forms;

namespace ADManager.Helpers;

public static class CsvExporter
{
    public static void ExportLast(string defaultName = "ad_export.csv")
    {
        if (AppState.LastResults.Count == 0)
        {
            Logger.Write("Нет данных для экспорта.", LogType.Warning);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Filter   = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
            FileName = defaultName
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var sb = new StringBuilder();

            // Заголовки
            var first = AppState.LastResults[0];
            List<string> headers;

            if (first is Dictionary<string, string> dict)
            {
                headers = dict.Keys.ToList();
            }
            else
            {
                headers = first.GetType().GetProperties().Select(p => p.Name).ToList();
            }
            sb.AppendLine(string.Join(",", headers.Select(Escape)));

            // Строки
            foreach (var item in AppState.LastResults)
            {
                IEnumerable<string> values;
                if (item is Dictionary<string, string> d)
                    values = headers.Select(h => d.TryGetValue(h, out var v) ? v : "");
                else
                    values = headers.Select(h => item.GetType().GetProperty(h)?.GetValue(item)?.ToString() ?? "");

                sb.AppendLine(string.Join(",", values.Select(Escape)));
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            Logger.Write($"Экспортировано {AppState.LastResults.Count} записей в: {dlg.FileName}", LogType.OK);
        }
        catch (Exception ex)
        {
            Logger.Write($"Ошибка экспорта: {ex.Message}", LogType.Error);
        }
    }

    private static string Escape(string val) =>
        val.Contains(',') || val.Contains('"') || val.Contains('\n')
            ? $"\"{val.Replace("\"", "\"\"")}\""
            : val;
}