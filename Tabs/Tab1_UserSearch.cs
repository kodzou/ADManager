using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;
using ADManager.Dialogs;

namespace ADManager.Tabs;

public static class Tab1_UserSearch
{
    private static DataGridView? _grid;
    private static DataGridViewRow? _selectedRow;
    private static Button? _btnUnlock;
    private static Button? _btnChangePwd;
    private static Label?  _lblSelectedUser;

    public static TabPage Create()
    {
        var tab = new TabPage("1. Поиск пользователя")
        {
            BackColor = Color.FromArgb(240, 242, 245)
        };

        // --- Поля ввода ---
        var lblSn = UiFactory.MakeLabel("Фамилия (обязательно):");
        lblSn.Location = new Point(8, 6);

        var txtSn = UiFactory.MakeTextBox("Напр.: Иванов", 200);
        txtSn.Location = new Point(8, 26);

        var lblGn = UiFactory.MakeLabel("Имя (необязательно):");
        lblGn.Location = new Point(218, 6);

        var txtGn = UiFactory.MakeTextBox("Напр.: Иван", 180);
        txtGn.Location = new Point(218, 26);

        // --- Кнопки ---
        var btnSearch = UiFactory.MakeActionButton("Найти", 100);
        btnSearch.Location = new Point(408, 24);

        _btnUnlock = UiFactory.MakeActionButton("Разблокировать", 140);
        _btnUnlock.Location = new Point(514, 24);
        _btnUnlock.Enabled = false;
        _btnUnlock.BackColor = Color.FromArgb(120, 125, 140);

        _btnChangePwd = UiFactory.MakeActionButton("Сменить пароль", 140);
        _btnChangePwd.Location = new Point(660, 24);
        _btnChangePwd.Enabled = false;
        _btnChangePwd.BackColor = Color.FromArgb(120, 125, 140);

        _lblSelectedUser = new Label
        {
            Location  = new Point(808, 28),
            Size      = new Size(180, 22),
            ForeColor = Color.FromArgb(50, 80, 160),
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        // --- DataGridView ---
        _grid = UiFactory.MakeGrid();
        _grid.Location = new Point(5, 58);
        _grid.Anchor =
            AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right | AnchorStyles.Bottom;

        // --- Контекстное меню ---
        var ctxMenu = new ContextMenuStrip();
        var menuGroups  = new ToolStripMenuItem("Группы пользователя");
        var menuDetails = new ToolStripMenuItem("Подробная информация");
        ctxMenu.Items.AddRange(new ToolStripItem[] { menuGroups, menuDetails });
        _grid.ContextMenuStrip = ctxMenu;

        // --- Events ---
        _grid.MouseDown       += OnGridMouseDown;
        _grid.SelectionChanged += OnSelectionChanged;
        menuGroups.Click += (_, _) =>
        {
            if (_selectedRow == null) return;
            string sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
            string domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
            // Находим MainForm → TabControl → переходим на Tab2
            var form = tab.FindForm() as MainForm;
            if (form == null) return;
            Tab2_Groups.SetSearch(sam, domain);
            form.SelectTab(1);          // индекс Tab2
            Tab2_Groups.TriggerSearch();
        };
        btnSearch.Click += (_, _) => DoSearch(txtSn.Text.Trim(), txtGn.Text.Trim());

        _btnUnlock.Click += (_, _) => DoUnlock();
        _btnChangePwd.Click += (_, _) =>
        {
            if (_selectedRow == null) return;
            var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
            var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
            using var dlg = new ChangePasswordDialog(domain, sam);
            dlg.ShowDialog(tab.FindForm());
        };

        menuDetails.Click += (_, _) =>
        {
            if (_selectedRow == null) return;
            var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
            var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
            using var dlg = new UserDetailsDialog(domain, sam);
            dlg.ShowDialog(tab.FindForm());
        };

        // Ctrl+A / Ctrl+C / Ctrl+Shift+C
        _grid.KeyDown += UiFactory.GridCopyHandler;

        tab.Controls.AddRange(new Control[]
        {
            lblSn, txtSn, lblGn, txtGn,
            btnSearch, _btnUnlock, _btnChangePwd,
            _lblSelectedUser, _grid
        });

        return tab;
    }

    // -------------------------------------------------------
    private static void DoSearch(string sn, string gn)
    {
        if (sn.Length < 2)
        {
            Logger.Write("Введите фамилию (минимум 2 символа).", LogType.Warning);
            return;
        }

        Logger.Write($"Поиск: фамилия='{sn}' имя='{gn}'", LogType.Info);

        string filter = string.IsNullOrEmpty(gn)
            ? $"(&(objectClass=user)(sn={sn}*))"
            : $"(&(objectClass=user)(sn={sn}*)(givenName={gn}*))";

        string[] props =
        {
            "sAMAccountName", "displayName", "sn", "givenName",
            "userAccountControl", "lockoutTime", "pwdLastSet",
            "accountExpires", "msDS-UserPasswordExpiryTimeComputed"
        };

        var results = new List<SearchResultRow>();

        foreach (var domain in MainForm.Domains)
        {
            try
            {
                var searcher = LdapHelper.CreateSearcher(domain, filter, props);
                if (searcher == null) continue;

                foreach (System.DirectoryServices.SearchResult r in searcher.FindAll())
                {
                    int uac = (int)LdapHelper.GetPropLong(r, "userAccountControl");
                    bool enabled        = (uac & 2) == 0;
                    long lockoutTime    = LdapHelper.GetPropLong(r, "lockoutTime");
                    bool lockedOut      = lockoutTime != 0;
                    long pwdLastSetFT   = LdapHelper.GetPropLong(r, "pwdLastSet");
                    bool pwdNeverExpires = (uac & 65536) != 0;
                    bool mustChange     = pwdLastSetFT == 0 && !pwdNeverExpires;

                    long expFT = LdapHelper.GetPropLong(r, "msDS-UserPasswordExpiryTimeComputed");
                    bool pwdExpired   = false;
                    string expDateStr = "";

                    var expDT = LdapHelper.FileTimeToDateTime(expFT);
                    if (expDT.HasValue)
                    {
                        pwdExpired  = expDT.Value < DateTime.Now;
                        expDateStr  = expDT.Value.ToString("dd.MM.yyyy HH:mm");
                    }

                    var pwdLastSetDT  = LdapHelper.FileTimeToDateTime(pwdLastSetFT);
                    var accountExpDT  = LdapHelper.FileTimeToDateTime(
                        LdapHelper.GetPropLong(r, "accountExpires"));

                    results.Add(new SearchResultRow
                    {
                        Domain               = domain,
                        SamAccountName       = LdapHelper.GetProp(r, "sAMAccountName"),
                        DisplayName          = LdapHelper.GetProp(r, "displayName"),
                        Enabled              = enabled ? "Да" : "Нет",
                        LockedOut            = lockedOut ? "Да" : "Нет",
                        PasswordExpired      = pwdExpired ? "Да" : "Нет",
                        PasswordLastSet      = pwdLastSetDT?.ToString("dd.MM.yyyy HH:mm") ?? "",
                        ExpirationDate       = expDateStr,
                        PasswordNeverExpires = pwdNeverExpires ? "Да" : "Нет",
                        AccountExpires       = accountExpDT?.ToString("dd.MM.yyyy HH:mm") ?? "Не задано",
                        MustChangePwd        = mustChange ? "Да" : "Нет"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Ошибка поиска в {domain}: {ex.Message}", LogType.Error);
            }
        }

        GridFiller.Fill(_grid!, results);
        Logger.Write($"Найдено: {results.Count} записей.", LogType.Summary);
    }

    private static void DoUnlock()
    {
        if (_selectedRow == null) return;
        var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
        var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";

        var searcher = LdapHelper.CreateSearcher(
            domain,
            $"(&(objectClass=user)(sAMAccountName={sam}))",
            new[] { "distinguishedName" });
        if (searcher == null) return;

        var res = searcher.FindOne();
        if (res == null) { Logger.Write($"Не найден: {sam}", LogType.Error); return; }

        var dn = LdapHelper.GetProp(res, "distinguishedName");
        bool ok = LdapHelper.InvokeADOperation(domain, dn, entry =>
        {
            entry.Properties["lockoutTime"].Value = 0;
            entry.CommitChanges();
        }, _grid!.FindForm()!);

        if (ok)
        {
            Logger.Write($"Учётная запись {sam} разблокирована.", LogType.OK);
            _selectedRow.Cells["LockedOut"].Value = "Нет";
            _btnUnlock!.Enabled   = false;
            _btnUnlock.BackColor  = Color.FromArgb(120, 125, 140);
        }
    }

    private static void OnGridMouseDown(object? s, MouseEventArgs e)
    {
        if (_grid == null || e.Button != MouseButtons.Right) return;

        var hit = _grid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0) return;

        if (!_grid.Focused) _grid.Focus();

        if (_grid.CurrentCell?.RowIndex != hit.RowIndex)
        {
            _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[0];
        }
    }

    private static void OnSelectionChanged(object? s, EventArgs e)
    {
        if (_grid == null) return;
        DataGridViewRow? row = null;
        if (_grid.SelectedRows.Count > 0)
            row = _grid.SelectedRows[0];
        else if (_grid.SelectedCells.Count > 0)
            row = _grid.Rows[_grid.SelectedCells[0].RowIndex];

        _selectedRow = row;

        if (row == null)
        {
            _btnUnlock!.Enabled   = false;
            _btnChangePwd!.Enabled = false;
            _lblSelectedUser!.Text = "";
            return;
        }

        var sam = row.Cells["SamAccountName"].Value?.ToString() ?? "";
        _lblSelectedUser!.Text = sam;

        bool locked = row.Cells["LockedOut"].Value?.ToString() == "Да";
        _btnUnlock!.Enabled   = locked;
        _btnUnlock.BackColor  = locked
            ? Color.FromArgb(200, 120, 30)
            : Color.FromArgb(120, 125, 140);

        _btnChangePwd!.Enabled   = true;
        _btnChangePwd.BackColor  = Color.FromArgb(50, 100, 200);
    }

    // Модель строки результата
    private record SearchResultRow
    {
        public string Domain               { get; init; } = "";
        public string SamAccountName       { get; init; } = "";
        public string DisplayName          { get; init; } = "";
        public string Enabled              { get; init; } = "";
        public string LockedOut            { get; init; } = "";
        public string PasswordExpired      { get; init; } = "";
        public string PasswordLastSet      { get; init; } = "";
        public string ExpirationDate       { get; init; } = "";
        public string PasswordNeverExpires { get; init; } = "";
        public string AccountExpires       { get; init; } = "";
        public string MustChangePwd        { get; init; } = "";
    }
}