using System.Drawing;
using System.Windows.Forms;

namespace ADManager.Helpers;

public static class UiFactory
{
    public static DataGridView MakeGrid()
    {
        var grid = new DataGridView
        {
            ReadOnly                = true,
            AllowUserToAddRows      = false,
            AllowUserToDeleteRows   = false,
            BackgroundColor         = Color.White,
            BorderStyle             = BorderStyle.Fixed3D,
            RowHeadersVisible       = false,
            AutoSizeColumnsMode     = DataGridViewAutoSizeColumnsMode.None,
            ScrollBars              = ScrollBars.Both,
            ClipboardCopyMode       = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
            SelectionMode           = DataGridViewSelectionMode.CellSelect,
            MultiSelect             = true,
            AllowUserToResizeRows   = false,
            Font                    = new Font("Segoe UI", 9f)
        };

        // Header style
        grid.ColumnHeadersDefaultCellStyle.BackColor       = Color.FromArgb(50, 55, 70);
        grid.ColumnHeadersDefaultCellStyle.ForeColor       = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font            = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 55, 70);
        grid.ColumnHeadersHeight                           = 28;
        grid.ColumnHeadersHeightSizeMode                   = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.EnableHeadersVisualStyles                     = false;

        // Row styles
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 220, 240);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);

        return grid;
    }

    public static Button MakeActionButton(string text, int width = 130, int height = 28)
    {
        var btn = new Button
        {
            Text      = text,
            Size      = new Size(width, height),
            BackColor = Color.FromArgb(50, 55, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    public static Button MakeExportButton(string text = "↓ Экспорт CSV")
    {
        var btn = new Button
        {
            Text      = text,
            Size      = new Size(120, 28),
            BackColor = Color.FromArgb(34, 139, 75),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    public static Label MakeLabel(string text)
    {
        return new Label
        {
            Text      = text,
            ForeColor = Color.FromArgb(90, 100, 120),
            Font      = new Font("Segoe UI", 8.5f),
            AutoSize  = true
        };
    }

    public static TextBox MakeTextBox(string placeholder = "", int width = 200)
    {
        var txt = new TextBox
        {
            Size            = new Size(width, 22),
            PlaceholderText = placeholder
        };
        return txt;
    }

    public static ComboBox MakeComboBox(string[] items, int selectedIndex = 0, int width = 200)
    {
        var cb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size          = new Size(width, 22)
        };
        foreach (var item in items) cb.Items.Add(item);
        if (cb.Items.Count > selectedIndex) cb.SelectedIndex = selectedIndex;
        return cb;
    }

    // Общий обработчик Ctrl+C / Ctrl+A для гридов
    public static void GridCopyHandler(object? sender, KeyEventArgs e)
    {
        if (sender is not DataGridView grid) return;

        if (e.KeyCode == Keys.A && e.Control)
        {
            grid.SelectAll();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.C && e.Control && e.Shift)
        {
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            Clipboard.SetDataObject(grid.GetClipboardContent());
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.C && e.Control)
        {
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            Clipboard.SetDataObject(grid.GetClipboardContent());
            e.Handled = true;
        }
    }
}