using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;
using ADManager.Dialogs;

namespace ADManager.Tabs;

public partial class Tab1_UserSearch : UserControl
{
    private DataGridViewRow? _selectedRow;

    public Tab2_Groups?       Tab2    { get; set; }
    public Tab_BulkOperations? TabBulk { get; set; }

    public Tab1_UserSearch()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        _ctxMenu.Opening += (_, _) =>
        {
            if (_selectedRow == null)
            {
                _menuExpiry.Enabled = _menuBulkAdd.Enabled = _menuUnlock.Enabled =
                _menuLock.Enabled   = _menuEnable.Enabled  = _menuDisable.Enabled = false;
                return;
            }
            bool isDisabled = _selectedRow.Cells["Enabled"].Value?.ToString() == "Нет" ||
                              (_selectedRow.Cells["DistinguishedName"].Value?.ToString() ?? "")
                                  .IndexOf("OU=DisabledAccounts", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isLocked   = _selectedRow.Cells["LockedOut"].Value?.ToString() == "Да";

            _menuExpiry.Enabled   = true;
            _menuBulkAdd.Enabled  = true;
            _menuUnlock.Enabled   = isLocked;
            _menuLock.Enabled     = !isDisabled && !isLocked;
            _menuEnable.Enabled   = isDisabled;
            _menuDisable.Enabled  = !isDisabled;
        };

        _grid!.MouseDown        += OnGridMouseDown;
        _grid.SelectionChanged  += OnSelectionChanged;
        _grid.CellFormatting    += OnGridCellFormatting;
        _grid.KeyDown           += UiFactory.GridCopyHandler;

        _menuGroups.Click += (_, _) =>
        {
            if (_selectedRow == null) return;
            string sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
            string domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
            var form = this.FindForm() as MainForm;
            if (form == null) return;
            Tab2?.SetSearch(sam, domain);
            form.SelectTab(1);
            Tab2?.TriggerSearch();
        };

        _btnSearch.Click += (_, _) => DoSearch(_txtSn.Text.Trim(), _txtGn.Text.Trim());

        KeyEventHandler searchOnEnter = (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            DoSearch(_txtSn.Text.Trim(), _txtGn.Text.Trim());
        };
        _txtSn.KeyDown += searchOnEnter;
        _txtGn.KeyDown += searchOnEnter;

        _btnUnlock!.Click   += (_, _) => DoUnlock();
        _btnChangePwd!.Click += (_, _) =>
        {
            if (_selectedRow == null) return;
            var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
            var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
            using var dlg = new Dialogs.ChangePasswordDialog(domain, sam);
            dlg.ShowDialog(this.FindForm());
        };

        _menuDetails.Click  += (_, _) =>
        {
            if (_selectedRow == null) return;
            var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
            var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
            using var dlg = new Dialogs.UserDetailsDialog(domain, sam);
            dlg.ShowDialog(this.FindForm());
        };

        _menuExpiry.Click  += (_, _) => DoSetExpiry();
        _menuUnlock.Click  += (_, _) => DoUnlock();
        _menuLock.Click    += (_, _) => DoLock();
        _menuEnable.Click  += (_, _) => DoEnable();
        _menuDisable.Click += (_, _) => DoDisable();

        _menuBulkAdd.Click += (_, _) => DoAddToBulkOps();
    }

    // -------------------------------------------------------
    private void DoSearch(string sn, string gn)
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
            "accountExpires", "msDS-UserPasswordExpiryTimeComputed",
            "distinguishedName"
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
                        MustChangePwd        = mustChange ? "Да" : "Нет",
                        DistinguishedName    = LdapHelper.GetProp(r, "distinguishedName")
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Ошибка поиска в {domain}: {ex.Message}", LogType.Error);
            }
        }

        GridFiller.Fill(_grid!, results);
        if (_grid!.Columns["DistinguishedName"] is { } dnCol) dnCol.Visible = false;

        if (TabBulk != null)
            foreach (DataGridViewRow row in _grid.Rows)
            {
                string d = row.Cells["Domain"].Value?.ToString() ?? "";
                string s = row.Cells["SamAccountName"].Value?.ToString() ?? "";
                if (TabBulk.IsUserAdded(d, s))
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 204);
            }

        Logger.Write($"Найдено: {results.Count} записей.", LogType.Summary);
    }

    private void DoUnlock()
    {
        if (_selectedRow == null) return;
        var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
        var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
        var dn     = _selectedRow.Cells["DistinguishedName"].Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(dn)) { Logger.Write($"DN не найден для {sam}.", LogType.Error); return; }

        bool ok = LdapHelper.InvokeADOperation(domain, dn, entry =>
        {
            entry.Properties["lockoutTime"].Value = 0;
            entry.CommitChanges();
        }, _grid!.FindForm()!);

        if (ok)
        {
            Logger.Write($"Учётная запись {sam} разблокирована.", LogType.OK);
            _selectedRow.Cells["LockedOut"].Value = "Нет";
            _btnUnlock!.Enabled  = false;
            _btnUnlock.BackColor = Color.FromArgb(120, 125, 140);
        }
    }

    private void DoEnable()
    {
        if (_selectedRow == null) return;
        var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
        var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
        var dn     = _selectedRow.Cells["DistinguishedName"].Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(dn)) { Logger.Write($"DN не найден для {sam}.", LogType.Error); return; }

        bool ok = LdapHelper.InvokeADOperation(domain, dn, entry =>
        {
            var uacVal = entry.Properties["userAccountControl"].Value;
            int uac    = uacVal is int i ? i : Convert.ToInt32(uacVal);
            entry.Properties["userAccountControl"].Value = uac & ~2;
            entry.CommitChanges();
        }, _grid!.FindForm()!);

        if (ok)
        {
            Logger.Write($"Учётная запись {sam} включена.", LogType.OK);
            _selectedRow.Cells["Enabled"].Value = "Да";
            _lblDisabledStatus!.Visible = false;
        }
    }

    private void DoSetExpiry()
    {
        if (_selectedRow == null) return;
        var sam         = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
        var domain      = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
        var dn          = _selectedRow.Cells["DistinguishedName"].Value?.ToString() ?? "";
        var displayName = _selectedRow.Cells["DisplayName"].Value?.ToString() ?? sam;
        if (string.IsNullOrEmpty(dn)) { Logger.Write($"DN не найден для {sam}.", LogType.Error); return; }

        using var dlg = new SetExpiryDialog(domain, sam, dn, displayName, _grid!.FindForm()!);
        dlg.ShowDialog(_grid!.FindForm());
    }

    private void DoLock()
    {
        if (_selectedRow == null) return;
        var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
        var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
        var dn     = _selectedRow.Cells["DistinguishedName"].Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(dn)) { Logger.Write($"DN не найден для {sam}.", LogType.Error); return; }

        bool ok = LdapHelper.InvokeADOperation(domain, dn, entry =>
        {
            entry.Properties["lockoutTime"].Value = DateTime.UtcNow.ToFileTimeUtc();
            entry.CommitChanges();
        }, _grid!.FindForm()!);

        if (ok)
        {
            Logger.Write($"Учётная запись {sam} заблокирована.", LogType.OK);
            _selectedRow.Cells["LockedOut"].Value = "Да";
            _btnUnlock!.Enabled  = true;
            _btnUnlock.BackColor = Color.FromArgb(200, 120, 30);
        }
    }

    private void DoDisable()
    {
        if (_selectedRow == null) return;
        var sam    = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
        var domain = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
        var dn     = _selectedRow.Cells["DistinguishedName"].Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(dn)) { Logger.Write($"DN не найден для {sam}.", LogType.Error); return; }

        bool ok = LdapHelper.InvokeADOperation(domain, dn, entry =>
        {
            var uacVal = entry.Properties["userAccountControl"].Value;
            int uac    = uacVal is int i ? i : Convert.ToInt32(uacVal);
            entry.Properties["userAccountControl"].Value = uac | 2;
            entry.CommitChanges();
        }, _grid!.FindForm()!);

        if (ok)
        {
            Logger.Write($"Учётная запись {sam} отключена.", LogType.OK);
            _selectedRow.Cells["Enabled"].Value = "Нет";
            _lblDisabledStatus!.Visible = true;
        }
    }

    private void OnGridMouseDown(object? s, MouseEventArgs e)
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

    private void OnSelectionChanged(object? s, EventArgs e)
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
            _btnUnlock!.Enabled        = false;
            _btnChangePwd!.Enabled     = false;
            _lblSelectedUser!.Text     = "";
            _lblDisabledStatus!.Visible = false;
            return;
        }

        var sam = row.Cells["SamAccountName"].Value?.ToString() ?? "";
        _lblSelectedUser!.Text = sam;

        bool isDisabled = row.Cells["Enabled"].Value?.ToString() == "Нет" ||
                          (row.Cells["DistinguishedName"].Value?.ToString() ?? "")
                              .IndexOf("OU=DisabledAccounts", StringComparison.OrdinalIgnoreCase) >= 0;
        _lblDisabledStatus!.Visible = isDisabled;

        bool locked = row.Cells["LockedOut"].Value?.ToString() == "Да";
        _btnUnlock!.Enabled   = locked;
        _btnUnlock.BackColor  = locked
            ? Color.FromArgb(200, 120, 30)
            : Color.FromArgb(120, 125, 140);

        _btnChangePwd!.Enabled   = true;
        _btnChangePwd.BackColor  = Color.FromArgb(50, 100, 200);
    }

    private void OnGridCellFormatting(object? s, DataGridViewCellFormattingEventArgs e)
    {
        if (_grid == null || e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name != "Enabled") return;

        var row = _grid.Rows[e.RowIndex];
        bool isDisabled = row.Cells["Enabled"].Value?.ToString() == "Нет" ||
                          (row.Cells["DistinguishedName"].Value?.ToString() ?? "")
                              .IndexOf("OU=DisabledAccounts", StringComparison.OrdinalIgnoreCase) >= 0;

        if (isDisabled)
            e.CellStyle.BackColor = Color.FromArgb(255, 146, 92);
    }

    private void DoAddToBulkOps()
    {
        if (_selectedRow == null || TabBulk == null) return;

        string sam         = _selectedRow.Cells["SamAccountName"].Value?.ToString() ?? "";
        string domainFull  = _selectedRow.Cells["Domain"].Value?.ToString() ?? "";
        string displayName = _selectedRow.Cells["DisplayName"].Value?.ToString() ?? "";

        if (string.IsNullOrEmpty(sam)) return;

        var (dn, title, dept, mgr) = LdapHelper.FetchUserProps(domainFull, sam);

        var entry = new BulkUserEntry
        {
            Domain      = domainFull.Replace(".local", ""),
            DomainFull  = domainFull,
            Login       = sam,
            DisplayName = displayName,
            Position    = title,
            Department  = dept,
            Manager     = mgr,
            DN          = dn
        };

        TabBulk.AddUsers(new[] { entry });
        _selectedRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 204);
        Logger.Write($"Пользователь {sam} добавлен в Массовые операции.", LogType.Info);
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
        public string DistinguishedName    { get; init; } = "";
    }
}