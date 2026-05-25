using System.Windows.Forms;

namespace ADManager.Helpers;

public static class GridFiller
{
    // Для типизированных record-моделей (Tab1–Tab5)
    public static void Fill<T>(DataGridView grid, IEnumerable<T> rows) where T : class
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        grid.Columns.Clear();

        var list  = rows.ToList();
        if (list.Count == 0) { grid.ResumeLayout(); return; }

        var props = typeof(T).GetProperties();
        foreach (var prop in props)
            grid.Columns.Add(prop.Name, prop.Name);

        foreach (var row in list)
        {
            var values = props.Select(p => p.GetValue(row)?.ToString() ?? "").ToArray<object>();
            grid.Rows.Add(values);
        }

        for (int i = 0; i < grid.Columns.Count; i++)
            grid.AutoResizeColumn(i, DataGridViewAutoSizeColumnMode.AllCells);

        grid.ResumeLayout();
    }

    // Для динамических словарей (Tab6, Tab7)
    public static void FillDynamic(
        DataGridView grid,
        IEnumerable<string> columns,
        IEnumerable<Dictionary<string, string>> rows)
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        grid.Columns.Clear();

        var colList  = columns.ToList();
        var rowList  = rows.ToList();

        foreach (var col in colList)
            grid.Columns.Add(col, col);

        foreach (var row in rowList)
        {
            var values = colList
                .Select(c => (object)(row.TryGetValue(c, out var v) ? v : ""))
                .ToArray();
            grid.Rows.Add(values);
        }

        for (int i = 0; i < grid.Columns.Count; i++)
            grid.AutoResizeColumn(i, DataGridViewAutoSizeColumnMode.AllCells);

        grid.ResumeLayout();
    }
}