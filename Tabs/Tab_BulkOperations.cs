using System.DirectoryServices;
using System.Drawing;
using System.Windows.Forms;
using ADManager.Dialogs;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab_BulkOperations : UserControl
{
    private readonly List<BulkUserEntry> _users = new();
    private string _targetOUDN     = "";
    private string _targetOUDomain = "";
    private string _managerDN      = "";

    public Tab_BulkOperations()
    {
        InitializeComponent();
        WireEvents();
    }

    // ── Public API ────────────────────────────────────────────

    public void AddUsers(IEnumerable<BulkUserEntry> entries)
    {
        bool changed = false;
        foreach (var e in entries)
        {
            if (_users.Any(u =>
                    string.Equals(u.DomainFull, e.DomainFull, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(u.Login, e.Login, StringComparison.OrdinalIgnoreCase)))
                continue;
            _users.Add(e);
            changed = true;
        }
        if (changed) { RefreshGrid(); UpdateRunButton(); }
    }

    public void SetTargetOU(string dn, string domain, string displayPath)
    {
        _targetOUDN     = dn;
        _targetOUDomain = domain;
        _txtOU.Text     = displayPath;
        _chkOU.Checked  = true;
        RefreshGrid();
        UpdateRunButton();
    }

    public bool IsUserAdded(string domain, string login) =>
        _users.Any(u =>
            string.Equals(u.Domain, domain.Replace(".local", ""), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase));

    // ── Event wiring ──────────────────────────────────────────

    private void WireEvents()
    {
        _chkPosition.Checked   = false;
        _chkDepartment.Checked = false;
        _chkManager.Checked    = true;
        _chkOU.Checked         = false;

        _chkPosition.CheckedChanged   += (_, _) => { UpdateColumnVisibility(); UpdateRunButton(); };
        _chkDepartment.CheckedChanged += (_, _) => { UpdateColumnVisibility(); UpdateRunButton(); };
        _chkManager.CheckedChanged    += (_, _) => { UpdateColumnVisibility(); UpdateRunButton(); };
        _chkOU.CheckedChanged         += (_, _) => { UpdateColumnVisibility(); UpdateRunButton(); };
        _chkShowAll.CheckedChanged    += (_, _) => UpdateColumnVisibility();

        _btnFindManager.Click += (_, _) =>
        {
            using var dlg = new FindManagerDialog();
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _managerDN       = dlg.SelectedDN;
                _txtManager.Text = dlg.SelectedDisplay;
            }
        };

        _btnOUInfo.Click += (_, _) =>
            MessageBox.Show(
                "Здесь находится адрес OU, для перемещения выбранных\n" +
                "учётных записей.\n\n" +
                "Для выбора OU:\n" +
                "- перейдите на вкладку Создание пользователя\n" +
                "- найдите OU\n" +
                "- добавьте OU через контекстное меню:\n" +
                "Добавить в Массовые операции",
                "Подсказка", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _btnDelete.Click += (_, _) =>
        {
            var indices = _grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.Index)
                .OrderByDescending(i => i)
                .ToList();

            foreach (int idx in indices)
                if (idx >= 0 && idx < _users.Count)
                    _users.RemoveAt(idx);

            RefreshGrid();
            UpdateRunButton();
        };

        _btnRun.Click += (_, _) => DoRun();

        _btnInfo.Click += (_, _) =>
            MessageBox.Show(
                "Для добавления пользователей в таблицу используйте\n" +
                "пункт контекстного меню Добавить в Массовые операции\n" +
                "на вкладках:\n\n" +
                "1. Поиск пользователя\n" +
                "4. Поиск по OU\n" +
                "5. Поиск по списку",
                "Подсказка", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _grid.KeyDown += UiFactory.GridCopyHandler;

        UpdateColumnVisibility();
        UpdateRunButton();
    }

    // ── Grid ──────────────────────────────────────────────────

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        string ouDisplay = !string.IsNullOrEmpty(_targetOUDN) ? FormatOuPath(_targetOUDN) : "";
        foreach (var u in _users)
            _grid.Rows.Add(u.Domain, u.Login, u.DisplayName,
                           u.Position, u.Department, u.Manager, ouDisplay, u.DN);
        UpdateColumnVisibility();
    }

    private void UpdateColumnVisibility()
    {
        if (_grid.Columns.Count == 0) return;
        bool showAll = _chkShowAll.Checked;

        (string name, CheckBox chk)[] conditional =
        {
            ("Position",   _chkPosition),
            ("Department", _chkDepartment),
            ("Manager",    _chkManager),
            ("OUTarget",   _chkOU)
        };

        foreach (var (name, chk) in conditional)
        {
            if (!_grid.Columns.Contains(name)) continue;
            var col = _grid.Columns[name];
            col.Visible = chk.Checked || showAll;
            col.HeaderCell.Style.BackColor = chk.Checked
                ? Color.FromArgb(255, 255, 153)
                : Color.FromArgb(50, 55, 70);
            col.HeaderCell.Style.ForeColor = chk.Checked
                ? Color.Black
                : Color.White;
        }
    }

    private void UpdateRunButton()
    {
        bool hasUsers  = _users.Count > 0;
        bool hasFields = _chkPosition.Checked || _chkDepartment.Checked ||
                         _chkManager.Checked  || _chkOU.Checked;
        bool active = hasUsers || hasFields;
        _btnRun.Enabled   = active;
        _btnRun.BackColor = active
            ? Color.FromArgb(50, 100, 200)
            : Color.FromArgb(120, 125, 140);
    }

    // ── Run ───────────────────────────────────────────────────

    private void DoRun()
    {
        if (_users.Count == 0)
        {
            Logger.Write("Нет пользователей для операции.", LogType.Warning);
            return;
        }
        bool anyChecked = _chkPosition.Checked || _chkDepartment.Checked ||
                          _chkManager.Checked  || _chkOU.Checked;
        if (!anyChecked)
        {
            Logger.Write("Не выбрано ни одного поля для изменения.", LogType.Warning);
            return;
        }

        Logger.Write($"Запуск массовой операции: {_users.Count} пользователей...", LogType.Info);

        string newTitle = _txtPosition.Text.Trim();
        string newDept  = _txtDepartment.Text.Trim();
        string newMgr   = _managerDN;
        bool doPos  = _chkPosition.Checked;
        bool doDept = _chkDepartment.Checked;
        bool doMgr  = _chkManager.Checked;
        bool doOU   = _chkOU.Checked && !string.IsNullOrEmpty(_targetOUDN);

        int ok = 0, fail = 0, skip = 0;

        foreach (var u in _users.ToList())
        {
            if (string.IsNullOrEmpty(u.DN))
            {
                Logger.Write($"Пропущен {u.Login}: DN не определён.", LogType.Warning);
                skip++;
                continue;
            }

            bool success = LdapHelper.InvokeADOperation(u.DomainFull, u.DN, entry =>
            {
                if (doPos)
                {
                    entry.Properties["title"].Clear();
                    if (!string.IsNullOrEmpty(newTitle))
                        entry.Properties["title"].Value = newTitle;
                }
                if (doDept)
                {
                    entry.Properties["department"].Clear();
                    if (!string.IsNullOrEmpty(newDept))
                        entry.Properties["department"].Value = newDept;
                }
                if (doMgr)
                {
                    entry.Properties["manager"].Clear();
                    if (!string.IsNullOrEmpty(newMgr))
                        entry.Properties["manager"].Value = newMgr;
                }
                entry.CommitChanges();

                if (doOU)
                {
                    if (string.Equals(u.DomainFull, _targetOUDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        using var targetOU = new DirectoryEntry($"LDAP://{_targetOUDomain}/{_targetOUDN}");
                        entry.MoveTo(targetOU);
                    }
                    else
                    {
                        Logger.Write(
                            $"[{u.Login}] Перемещение пропущено: домен пользователя " +
                            $"({u.DomainFull}) не совпадает с доменом OU ({_targetOUDomain}).",
                            LogType.Warning);
                    }
                }
            }, FindForm()!);

            if (success)
            {
                ok++;
                Logger.Write($"[OK] {u.Login} ({u.Domain})", LogType.OK);
            }
            else
            {
                fail++;
            }
        }

        string summary = $"Массовая операция завершена. Успешно: {ok}";
        if (fail > 0) summary += $", ошибок: {fail}";
        if (skip > 0) summary += $", пропущено: {skip}";
        summary += ".";
        Logger.Write(summary, fail == 0 ? LogType.OK : LogType.Error);
    }

    private static string FormatOuPath(string dn) =>
        string.Join(" > ",
            dn.Split(',')
              .Where(p => p.TrimStart().StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
              .Reverse()
              .Select(p => p.TrimStart()[3..]));
}
