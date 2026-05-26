using System.Data;
using System.Windows.Forms;

namespace ADManager.Helpers;

public static class GridFiller
{
    // Для типизированных record-моделей (Tab1–Tab5)
    // Использует DataTable как DataSource, чтобы сортировка работала по реальным типам (DateTime, int, TimeSpan)
    public static void Fill<T>(DataGridView grid, IEnumerable<T> rows) where T : class
    {
        grid.SuspendLayout();
        grid.DataSource = null;
        grid.Columns.Clear();

        var list = rows.ToList();
        if (list.Count == 0) { grid.ResumeLayout(); return; }

        var props = typeof(T).GetProperties();
        var dt    = new DataTable();

        foreach (var prop in props)
        {
            var colType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            dt.Columns.Add(prop.Name, colType);
        }

        foreach (var row in list)
        {
            var dr = dt.NewRow();
            foreach (var prop in props)
                dr[prop.Name] = prop.GetValue(row) ?? DBNull.Value;
            dt.Rows.Add(dr);
        }

        grid.DataSource = dt;

        grid.ResumeLayout();

        // AutoResizeColumn вызывается ПОСЛЕ ResumeLayout — иначе появление горизонтального
        // скроллбара не учитывается при расчёте диапазона вертикального скроллбара
        for (int i = 0; i < grid.Columns.Count; i++)
            grid.AutoResizeColumn(i, DataGridViewAutoSizeColumnMode.AllCells);
    }

    // Для динамических словарей (Tab6, Tab7)
    public static void FillDynamic(
        DataGridView grid,
        IEnumerable<string> columns,
        IEnumerable<Dictionary<string, string>> rows)
    {
        grid.SuspendLayout();
        grid.DataSource = null;
        grid.Columns.Clear();

        var colList = columns.ToList();
        var rowList = rows.ToList();

        foreach (var col in colList)
            grid.Columns.Add(col, col);

        foreach (var row in rowList)
        {
            var values = colList
                .Select(c => (object)(row.TryGetValue(c, out var v) ? v : ""))
                .ToArray();
            grid.Rows.Add(values);
        }

        grid.ResumeLayout();

        // AutoResizeColumn вызывается ПОСЛЕ ResumeLayout — иначе появление горизонтального
        // скроллбара не учитывается при расчёте диапазона вертикального скроллбара
        for (int i = 0; i < grid.Columns.Count; i++)
            grid.AutoResizeColumn(i, DataGridViewAutoSizeColumnMode.AllCells);
    }
}
