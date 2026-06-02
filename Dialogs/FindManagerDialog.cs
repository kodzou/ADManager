using System.DirectoryServices;
using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Dialogs;

public class FindManagerDialog : Form
{
    private TextBox       _txtSearch = null!;
    private DataGridView  _grid      = null!;
    private Button        _btnSelect = null!;

    private readonly List<(string DN, string Display)> _results = new();

    public string SelectedDN      { get; private set; } = "";
    public string SelectedDisplay { get; private set; } = "";

    public FindManagerDialog()
    {
        Text            = "Поиск руководителя";
        Size            = new Size(820, 480);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterParent;
        MinimumSize     = new Size(600, 380);
        MaximizeBox     = true;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = Color.FromArgb(240, 242, 245);

        BuildUI();
    }

    private void BuildUI()
    {
        // Search bar
        var searchPanel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 42,
            Padding   = new Padding(8, 8, 8, 0)
        };

        var lblSearch = new Label
        {
            Text      = "Фамилия / Имя:",
            Location  = new Point(8, 10),
            AutoSize  = true,
            ForeColor = Color.FromArgb(90, 100, 120)
        };

        _txtSearch = new TextBox
        {
            Location        = new Point(130, 8),
            Width           = 340,
            PlaceholderText = "Введите фамилию или имя",
            Font            = new Font("Segoe UI", 9.5f)
        };

        var btnFind = new Button
        {
            Text      = "Найти",
            Location  = new Point(478, 7),
            Size      = new Size(90, 26),
            BackColor = Color.FromArgb(50, 55, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnFind.FlatAppearance.BorderSize = 0;

        searchPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch, btnFind });

        // Grid
        _grid = UiFactory.MakeGrid();
        _grid.Dock = DockStyle.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Domain",  HeaderText = "Домен",          Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Display", HeaderText = "Выводимое имя",  Width = 200 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Login",   HeaderText = "Логин",          Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title",   HeaderText = "Должность",      Width = 250 });

        // Bottom bar
        var bottomPanel = new Panel
        {
            Dock    = DockStyle.Bottom,
            Height  = 44,
            Padding = new Padding(8, 8, 8, 0)
        };

        _btnSelect = new Button
        {
            Text      = "Выбрать",
            Location  = new Point(8, 8),
            Size      = new Size(120, 28),
            BackColor = Color.FromArgb(34, 139, 75),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            Enabled   = false
        };
        _btnSelect.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button
        {
            Text         = "Отмена",
            Location     = new Point(136, 8),
            Size         = new Size(100, 28),
            DialogResult = DialogResult.Cancel
        };

        bottomPanel.Controls.AddRange(new Control[] { _btnSelect, btnCancel });

        Controls.Add(_grid);
        Controls.Add(searchPanel);
        Controls.Add(bottomPanel);
        CancelButton = btnCancel;

        // Events
        btnFind.Click += (_, _) => DoSearch();
        _txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            DoSearch();
        };

        _grid.SelectionChanged += (_, _) =>
            _btnSelect.Enabled = _grid.SelectedRows.Count > 0;

        _grid.CellDoubleClick += (_, _) => AcceptSelection();
        _btnSelect.Click      += (_, _) => AcceptSelection();
    }

    private void DoSearch()
    {
        string query = _txtSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            Logger.Write("Введите фамилию или имя для поиска.", LogType.Warning);
            return;
        }

        _grid.Rows.Clear();
        _results.Clear();

        string safe   = LdapHelper.EscapeLdapFilter(query);
        string filter = $"(&(objectClass=user)(!(objectClass=computer))" +
                        $"(|(sn=*{safe}*)(givenName=*{safe}*)(displayName=*{safe}*)))";

        Logger.Write($"Поиск руководителя: «{query}»...", LogType.Info);

        int total = 0;
        foreach (var domain in MainForm.Domains)
        {
            try
            {
                var searcher = LdapHelper.CreateSearcher(domain, filter,
                    new[] { "distinguishedName", "displayName", "sAMAccountName", "title" });
                if (searcher == null) continue;

                foreach (SearchResult r in searcher.FindAll())
                {
                    string dn      = LdapHelper.GetProp(r, "distinguishedName");
                    string display = LdapHelper.GetProp(r, "displayName");
                    string login   = LdapHelper.GetProp(r, "sAMAccountName");
                    string title   = LdapHelper.GetProp(r, "title");

                    _results.Add((dn, display));
                    _grid.Rows.Add(domain, display, login, title);
                    total++;
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Ошибка поиска в {domain}: {ex.Message}", LogType.Error);
            }
        }

        Logger.Write($"Найдено: {total} пользователей.", LogType.Summary);
    }

    private void AcceptSelection()
    {
        if (_grid.SelectedRows.Count == 0) return;
        int idx = _grid.SelectedRows[0].Index;
        if (idx < 0 || idx >= _results.Count) return;

        SelectedDN      = _results[idx].DN;
        SelectedDisplay = _results[idx].Display;
        DialogResult    = DialogResult.OK;
        Close();
    }
}
