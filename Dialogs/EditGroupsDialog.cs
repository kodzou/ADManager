using System.DirectoryServices;
using System.Drawing;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Dialogs;

public class EditGroupsDialog : Form
{
    private readonly string            _domain;
    private readonly string            _userDN;
    private readonly string            _infoLabel;
    private readonly NetworkCredential _credential;

    private TextBox _txtSearch  = null!;
    private ListBox _lstFound   = null!;
    private ListBox _lstCurrent = null!;

    private readonly List<GroupItem> _originalGroups;
    private readonly List<GroupItem> _workingGroups;
    private readonly List<GroupItem> _foundGroups = new();

    private record GroupItem(string CN, string DN);

    public EditGroupsDialog(
        string domain,
        string sam,
        string userDN,
        string displayName,
        NetworkCredential credential,
        IEnumerable<(string CN, string DN)> currentGroups)
    {
        _domain     = domain;
        _userDN     = userDN;
        _infoLabel  = $"{displayName} | {sam} @ {domain}";
        _credential = credential;

        _originalGroups = currentGroups
            .Select(g => new GroupItem(g.CN, g.DN))
            .OrderBy(g => g.CN, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _workingGroups = new List<GroupItem>(_originalGroups);

        BuildUI();
    }

    private void BuildUI()
    {
        // Геометрия (все константы в одном месте):
        //   lstFound: y=116, h=27*15=405, bottom=521
        //   lstCurrent: y=66 (= txtSearch.Top), bottom=521, h=455
        //   lstCurrentW = clientW - lstCurrentX - rightMargin = 860-452-10 = 398
        //   Кнопки > <: midX=376 (центр gap 92px между полями), вертикально по центру overlap-зоны (y=318)
        //   Нижние кнопки: y=531 (lstBottom+10), h=32; gap ниже = 573-563 = 10 = leftMargin
        Text            = "Редактирование групп";
        ClientSize      = new Size(860, 573);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = Color.FromArgb(240, 242, 245);

        const int leftMargin  = 10;
        const int txtY        = 66;
        const int lstFoundX   = leftMargin;
        const int lstFoundY   = 116;
        const int lstFoundH   = 405;   // 27 строк × 15px
        const int lstBottom   = lstFoundY + lstFoundH;   // 521
        const int lstCurrentX = 452;
        const int lstWidth    = 350;
        const int lstCurrentW = 398;   // 860 - 452 - 10
        const int btnY        = 531;   // lstBottom + 10
        const int btnH        = 32;

        // ── Информация о пользователе ──────────────────────────
        var lblUser = new Label
        {
            Text      = _infoLabel,
            Location  = new Point(leftMargin, 12),
            Size      = new Size(830, 22),
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 55, 70)
        };

        // ── Левая панель: поиск ────────────────────────────────
        var lblSearch = UiFactory.MakeLabel("Поиск групп:");
        lblSearch.Location = new Point(lstFoundX, 46);

        _txtSearch = UiFactory.MakeTextBox("Название группы, Enter - найти", lstWidth);
        _txtSearch.Location = new Point(lstFoundX, txtY);

        _lstFound = new ListBox
        {
            Location            = new Point(lstFoundX, lstFoundY),
            Size                = new Size(lstWidth, lstFoundH),
            HorizontalScrollbar = true,
            IntegralHeight      = false,
            Font                = new Font("Segoe UI", 9f)
        };

        // ── Средние кнопки ─────────────────────────────────────
        // midX: центр gap'а между lstFound.Right(360) и lstCurrentX(452) → 360+(92-60)/2=376
        // midY: центр overlap-зоны обоих списков (y=116..521) → 318
        const int midX      = lstFoundX + lstWidth + (lstCurrentX - lstFoundX - lstWidth - 60) / 2; // 376
        const int overlapCY = lstFoundY + lstFoundH / 2;  // 318
        const int midBtnH   = 34;
        const int midBtnGap = 8;
        var btnAdd = new Button
        {
            Text      = ">",
            Location  = new Point(midX, overlapCY - midBtnH - midBtnGap / 2),
            Size      = new Size(60, midBtnH),
            BackColor = Color.FromArgb(50, 55, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold)
        };
        btnAdd.FlatAppearance.BorderSize = 0;

        var btnRemove = new Button
        {
            Text      = "<",
            Location  = new Point(midX, overlapCY + midBtnGap / 2),
            Size      = new Size(60, midBtnH),
            BackColor = Color.FromArgb(50, 55, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold)
        };
        btnRemove.FlatAppearance.BorderSize = 0;

        var toolTip = new ToolTip();
        toolTip.SetToolTip(btnAdd,    "Добавить");
        toolTip.SetToolTip(btnRemove, "Убрать");

        // ── Правая панель: текущие группы ─────────────────────
        var lblCurrent = UiFactory.MakeLabel("Текущие группы:");
        lblCurrent.Location = new Point(lstCurrentX, 46);

        _lstCurrent = new ListBox
        {
            Location            = new Point(lstCurrentX, txtY),
            Size                = new Size(lstCurrentW, lstBottom - txtY),
            HorizontalScrollbar = true,
            IntegralHeight      = false,
            Font                = new Font("Segoe UI", 9f)
        };

        RefreshCurrentList();

        // ── Нижние кнопки ──────────────────────────────────────
        // Отменить: прижата к левому краю lstCurrent
        // Применить: прижата к правому краю lstCurrent
        var btnCancel = new Button
        {
            Text         = "Отменить",
            Location     = new Point(lstCurrentX, btnY),
            Size         = new Size(120, btnH),
            BackColor    = Color.FromArgb(50, 55, 70),
            ForeColor    = Color.White,
            FlatStyle    = FlatStyle.Flat,
            Font         = new Font("Segoe UI", 9f, FontStyle.Bold),
            DialogResult = DialogResult.Cancel
        };
        btnCancel.FlatAppearance.BorderSize = 0;

        var btnApply = new Button
        {
            Text      = "Применить",
            Location  = new Point(lstCurrentX + lstCurrentW - 120, btnY),
            Size      = new Size(120, btnH),
            BackColor = Color.FromArgb(50, 100, 200),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnApply.FlatAppearance.BorderSize = 0;

        Controls.AddRange(new Control[]
        {
            lblUser,
            lblSearch, _txtSearch, _lstFound,
            btnAdd, btnRemove,
            lblCurrent, _lstCurrent,
            btnCancel, btnApply
        });

        CancelButton = btnCancel;

        _txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            SearchGroups();
        };

        btnAdd.Click    += OnAdd;
        btnRemove.Click += OnRemove;
        btnApply.Click  += OnApply;
    }

    private void RefreshCurrentList()
    {
        _lstCurrent.BeginUpdate();
        _lstCurrent.Items.Clear();
        foreach (var g in _workingGroups)
            _lstCurrent.Items.Add(g.CN);
        _lstCurrent.EndUpdate();
    }

    private void SearchGroups()
    {
        string query = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        _foundGroups.Clear();
        _lstFound.BeginUpdate();
        _lstFound.Items.Clear();

        try
        {
            string escaped = LdapHelper.EscapeLdapFilter(query);
            var searcher = LdapHelper.CreateSearcher(
                _domain,
                $"(&(objectClass=group)(cn=*{escaped}*))",
                new[] { "cn", "distinguishedName" },
                _credential);

            if (searcher == null) return;
            searcher.SizeLimit = 200;

            var results = searcher.FindAll();
            foreach (SearchResult r in results)
            {
                string cn = LdapHelper.GetProp(r, "cn");
                string dn = LdapHelper.GetProp(r, "distinguishedName");
                if (string.IsNullOrEmpty(cn) || string.IsNullOrEmpty(dn)) continue;
                _foundGroups.Add(new GroupItem(cn, dn));
                _lstFound.Items.Add(cn);
            }

            Logger.Write($"Найдено групп по запросу «{query}»: {_foundGroups.Count}", LogType.Info);
        }
        catch (Exception ex)
        {
            Logger.Write($"Ошибка поиска групп: {ex.Message}", LogType.Error);
        }
        finally
        {
            _lstFound.EndUpdate();
        }
    }

    private void OnAdd(object? sender, EventArgs e)
    {
        if (_lstFound.SelectedIndex < 0) return;
        var group = _foundGroups[_lstFound.SelectedIndex];

        if (_workingGroups.Any(g => string.Equals(g.DN, group.DN, StringComparison.OrdinalIgnoreCase)))
        {
            Logger.Write($"Пользователь уже является членом группы «{group.CN}».", LogType.Warning);
            return;
        }

        _workingGroups.Add(group);
        _workingGroups.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.CN, b.CN));
        RefreshCurrentList();
    }

    private void OnRemove(object? sender, EventArgs e)
    {
        if (_lstCurrent.SelectedIndex < 0) return;
        int idx = _lstCurrent.SelectedIndex;
        _workingGroups.RemoveAt(idx);
        RefreshCurrentList();
    }

    private void OnApply(object? sender, EventArgs e)
    {
        var toAdd = _workingGroups
            .Where(g => !_originalGroups.Any(o => string.Equals(o.DN, g.DN, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var toRemove = _originalGroups
            .Where(g => !_workingGroups.Any(w => string.Equals(w.DN, g.DN, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            Logger.Write("Нет изменений для применения.", LogType.Info);
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        int ok = 0, fail = 0;

        foreach (var g in toAdd)
        {
            if (ModifyGroupMembership(g.DN, add: true, g.CN)) ok++;
            else fail++;
        }

        foreach (var g in toRemove)
        {
            if (ModifyGroupMembership(g.DN, add: false, g.CN)) ok++;
            else fail++;
        }

        if (ok > 0)
        {
            string failPart = fail > 0 ? $", {fail} с ошибками" : "";
            Logger.Write($"Изменения применены: {ok} успешно{failPart}.", LogType.OK);
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool ModifyGroupMembership(string groupDN, bool add, string groupCN)
    {
        try
        {
            using var groupEntry = new DirectoryEntry(
                $"LDAP://{_domain}/{groupDN}",
                _credential.UserName, _credential.Password,
                AuthenticationTypes.Secure);

            if (add)
                groupEntry.Properties["member"].Add(_userDN);
            else
                groupEntry.Properties["member"].Remove(_userDN);

            groupEntry.CommitChanges();

            string action = add ? "Добавлен в" : "Удалён из";
            Logger.Write($"{action} группу «{groupCN}».", LogType.OK);
            return true;
        }
        catch (Exception ex) when (IsAccessDenied(ex))
        {
            string action = add ? "добавления в" : "удаления из";
            Logger.Write($"Недостаточно прав для {action} группы «{groupCN}».", LogType.Warning);
            return false;
        }
        catch (Exception ex)
        {
            string action = add ? "добавлении в" : "удалении из";
            Logger.Write($"Ошибка при {action} группу «{groupCN}»: {ex.Message}", LogType.Error);
            return false;
        }
    }

    private static readonly uint[] _accessDeniedCodes =
    {
        0x80070005,  // E_ACCESSDENIED
        0x80072032,  // LDAP_INSUFFICIENT_ACCESS_RIGHTS
        0x8007052E,  // ERROR_LOGON_FAILURE
        0x80005000,  // E_ADS_BAD_PATHNAME
    };

    private static bool IsAccessDenied(Exception ex)
    {
        if (ex is UnauthorizedAccessException) return true;
        if (ex is COMException c && Array.IndexOf(_accessDeniedCodes, (uint)c.ErrorCode) >= 0) return true;
        if (ex is System.Reflection.TargetInvocationException { InnerException: not null } tie)
        {
            return tie.InnerException is UnauthorizedAccessException ||
                   (tie.InnerException is COMException comEx &&
                    Array.IndexOf(_accessDeniedCodes, (uint)comEx.ErrorCode) >= 0);
        }
        return false;
    }
}
