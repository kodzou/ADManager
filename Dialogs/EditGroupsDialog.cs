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

    private TextBox  _txtSearch     = null!;
    private ListBox  _lstFound      = null!;
    private ListBox  _lstCurrent    = null!;
    private ComboBox _cbLists       = null!;
    private Button   _btnDeleteList = null!;
    private Button   _btnApply      = null!;

    private readonly List<GroupItem> _originalGroups;
    private readonly List<GroupItem> _workingGroups;
    private readonly List<GroupItem> _foundGroups = new();

    private GroupPreset?    _selectedPreset;
    private HashSet<string> _listGroupDNs        = new(StringComparer.OrdinalIgnoreCase);
    private bool            _deleteListActive     = false;

    private record GroupItem(string CN, string DN);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

    private static void SetCueBanner(ComboBox cb, string hint)
    {
        IntPtr edit = FindWindowEx(cb.Handle, IntPtr.Zero, "Edit", null);
        if (edit != IntPtr.Zero)
            SendMessage(edit, 0x1501u /* EM_SETCUEBANNER */, (IntPtr)1, hint);
    }

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
        // Layout constants
        const int leftMargin  = 10;
        const int lstFoundX   = leftMargin;
        const int lstFoundY   = 116;
        const int lstFoundH   = 405;
        const int lstCurrentX = 452;
        const int lstWidth    = 350;
        const int lstCurrentW = 398;
        const int btnY        = 531;
        const int btnH        = 32;

        // Middle buttons: 4 stacked (^, v, >, <), each h=34, gap=8
        // Center at y=318 (midpoint of listboxes overlap zone 116..521)
        const int midX      = lstFoundX + lstWidth + (lstCurrentX - lstFoundX - lstWidth - 60) / 2; // 376
        const int midBtnW   = 60;
        const int midBtnH   = 34;
        const int midBtnGap = 8;
        const int midStart  = lstFoundY + lstFoundH / 2 - (4 * midBtnH + 3 * midBtnGap) / 2; // 238

        Text            = "Редактирование групп";
        ClientSize      = new Size(860, 573);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = Color.FromArgb(240, 242, 245);

        // ── Пользователь ──────────────────────────────────────────
        var lblUser = new Label
        {
            Text      = _infoLabel,
            Location  = new Point(leftMargin, 12),
            Size      = new Size(830, 22),
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 55, 70)
        };

        // ── Левая панель: поиск ───────────────────────────────────
        var lblSearch = UiFactory.MakeLabel("Поиск групп:");
        lblSearch.Location = new Point(lstFoundX, 46);

        _txtSearch = UiFactory.MakeTextBox("Название группы, Enter — найти", lstWidth);
        _txtSearch.Location = new Point(lstFoundX, 66);

        _lstFound = new ListBox
        {
            Location            = new Point(lstFoundX, lstFoundY),
            Size                = new Size(lstWidth, lstFoundH),
            HorizontalScrollbar = true,
            IntegralHeight      = false,
            Font                = new Font("Segoe UI", 9f)
        };

        // ── Средние кнопки ────────────────────────────────────────
        var toolTip = new ToolTip();

        Button MidBtn(string text)
        {
            var b = new Button
            {
                Size      = new Size(midBtnW, midBtnH),
                Text      = text,
                BackColor = Color.FromArgb(50, 55, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        var btnAddToPreset = MidBtn("↑");
        var btnLoadUser    = MidBtn("↓");
        var btnAdd         = MidBtn(">");
        var btnRemove      = MidBtn("<");

        int py = midStart;
        btnAddToPreset.Location = new Point(midX, py); py += midBtnH + midBtnGap;
        btnLoadUser.Location    = new Point(midX, py); py += midBtnH + midBtnGap;
        btnAdd.Location         = new Point(midX, py); py += midBtnH + midBtnGap;
        btnRemove.Location      = new Point(midX, py);

        toolTip.SetToolTip(btnAddToPreset, "Добавить в список");
        toolTip.SetToolTip(btnLoadUser,    "Загрузить группы пользователя");
        toolTip.SetToolTip(btnAdd,         "Добавить");
        toolTip.SetToolTip(btnRemove,      "Убрать");

        // ── Правая панель ─────────────────────────────────────────
        // Строка 1: Списки групп [ComboBox 364px][✕ 28px]
        // Строка 2: Текущие группы
        // Строка 3: ListBox (y=lstFoundY, та же высота что левый)
        const int cbListsW = lstCurrentW - 28 - 6; // 364

        var lblLists = UiFactory.MakeLabel("Списки групп:");
        lblLists.Location = new Point(lstCurrentX, 46);

        _cbLists = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Location      = new Point(lstCurrentX, 66),
            Size          = new Size(cbListsW, 24),
            Font          = new Font("Segoe UI", 9f)
        };
        // Подсказка внутри поля через EM_SETCUEBANNER (WinForms не имеет PlaceholderText на ComboBox)
        _cbLists.HandleCreated += (_, _) => SetCueBanner(_cbLists, "Выберите пункт, нажмите Enter");

        // Enabled = true всегда, иначе ToolTip не показывается на отключённых кнопках.
        // Реальная «активность» управляется флагом _deleteListActive + цветом.
        _btnDeleteList = new Button
        {
            Text      = "✕",
            Location  = new Point(lstCurrentX + cbListsW + 6, 66),
            Size      = new Size(28, 24),
            BackColor = Color.FromArgb(160, 165, 175),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 10f)
        };
        _btnDeleteList.FlatAppearance.BorderSize = 0;
        toolTip.SetToolTip(_btnDeleteList, "Удалить список");

        var lblCurrent = UiFactory.MakeLabel("Текущие группы:");
        lblCurrent.Location = new Point(lstCurrentX, 96);

        _lstCurrent = new ListBox
        {
            Location            = new Point(lstCurrentX, lstFoundY),
            Size                = new Size(lstCurrentW, lstFoundH),
            HorizontalScrollbar = true,
            IntegralHeight      = false,
            Font                = new Font("Segoe UI", 9f),
            DrawMode            = DrawMode.OwnerDrawFixed
        };

        // ── Нижние кнопки ──────────────────────────────────────────
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

        _btnApply = new Button
        {
            Text      = "Применить",
            Location  = new Point(lstCurrentX + lstCurrentW - 120, btnY),
            Size      = new Size(120, btnH),
            BackColor = Color.FromArgb(160, 165, 175),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            Enabled   = false
        };
        _btnApply.FlatAppearance.BorderSize = 0;

        Controls.AddRange(new Control[]
        {
            lblUser,
            lblSearch, _txtSearch, _lstFound,
            btnAddToPreset, btnLoadUser, btnAdd, btnRemove,
            lblLists, _cbLists, _btnDeleteList,
            lblCurrent, _lstCurrent,
            btnCancel, _btnApply
        });

        CancelButton = btnCancel;

        RefreshPresetsCombo();
        RefreshCurrentList();

        _txtSearch.KeyDown                    += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SearchGroups(); } };
        _lstCurrent.DrawItem                  += OnDrawItem;
        _cbLists.SelectionChangeCommitted     += OnListSelected;
        _cbLists.KeyDown                      += OnListKeyDown;
        _btnDeleteList.Click                  += OnDeleteList;
        btnAddToPreset.Click                  += OnAddToPreset;
        btnLoadUser.Click                     += OnLoadUserGroups;
        btnAdd.Click                          += OnAdd;
        btnRemove.Click                       += OnRemove;
        _btnApply.Click                       += OnApply;
    }

    private void SetDeleteListActive(bool active)
    {
        _deleteListActive          = active;
        _btnDeleteList.BackColor   = active ? Color.FromArgb(50, 55, 70) : Color.FromArgb(160, 165, 175);
        _btnDeleteList.Cursor      = active ? Cursors.Default : Cursors.Default;
    }

    // ── Preset combo ──────────────────────────────────────────────

    private void RefreshPresetsCombo()
    {
        string current = _cbLists.Text;
        _cbLists.Items.Clear();
        foreach (var p in GroupListStorage.ForDomain(_domain))
            _cbLists.Items.Add(p.Name);
        _cbLists.Items.Add("Новый список");

        int idx = _cbLists.Items.IndexOf(current);
        if (idx >= 0) _cbLists.SelectedIndex = idx;
        else if (!string.IsNullOrEmpty(current)) _cbLists.Text = current;
    }

    private void OnListSelected(object? sender, EventArgs e)
    {
        string sel = _cbLists.Text;
        if (sel == "Новый список")
        {
            _cbLists.Text          = "";
            _selectedPreset        = null;
            _listGroupDNs          = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            SetDeleteListActive(false);
            _lstCurrent.Invalidate();
        }
        else
        {
            LoadPresetByName(sel);
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;

        string name = _cbLists.Text.Trim();
        if (string.IsNullOrEmpty(name) || name == "Новый список") return;

        var existing = GroupListStorage.ForDomain(_domain)
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));

        if (existing != null)
        {
            LoadPreset(existing);
        }
        else
        {
            var newPreset = new GroupPreset { Name = name, Domain = _domain };
            GroupListStorage.AddOrUpdate(newPreset);
            GroupListStorage.Save();
            _selectedPreset        = newPreset;
            _listGroupDNs          = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            SetDeleteListActive(true);
            RefreshPresetsCombo();
            _cbLists.Text = name;
            Logger.Write($"Список «{name}» создан.", LogType.OK);
        }
    }

    private void LoadPresetByName(string name)
    {
        var preset = GroupListStorage.ForDomain(_domain)
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (preset != null) LoadPreset(preset);
    }

    private void LoadPreset(GroupPreset preset)
    {
        _selectedPreset        = preset;
        _listGroupDNs          = preset.Groups.Select(g => g.DN).ToHashSet(StringComparer.OrdinalIgnoreCase);
        SetDeleteListActive(true);
        _cbLists.Text          = preset.Name;

        _workingGroups.Clear();
        _workingGroups.AddRange(preset.Groups.Select(g => new GroupItem(g.CN, g.DN)));
        RefreshCurrentList();
        Logger.Write($"Список «{preset.Name}» загружен ({preset.Groups.Count} групп).", LogType.Info);
    }

    private void OnDeleteList(object? sender, EventArgs e)
    {
        if (!_deleteListActive || _selectedPreset == null) return;
        string name = _selectedPreset.Name;
        if (MessageBox.Show($"Удалить список «{name}»?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        GroupListStorage.Remove(name, _domain);
        GroupListStorage.Save();
        _selectedPreset        = null;
        _listGroupDNs          = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SetDeleteListActive(false);
        _cbLists.Text          = "";
        RefreshPresetsCombo();

        _workingGroups.Clear();
        _workingGroups.AddRange(_originalGroups);
        RefreshCurrentList();
        Logger.Write($"Список «{name}» удалён.", LogType.OK);
    }

    private void OnAddToPreset(object? sender, EventArgs e)
    {
        if (_selectedPreset == null)
        {
            Logger.Write("Выберите или создайте список групп.", LogType.Warning);
            return;
        }
        if (_workingGroups.Count == 0) return;

        int added = 0;
        foreach (var group in _workingGroups)
        {
            if (_listGroupDNs.Contains(group.DN)) continue;
            _listGroupDNs.Add(group.DN);
            _selectedPreset.Groups.Add(new GroupPresetEntry { CN = group.CN, DN = group.DN });
            added++;
        }

        if (added > 0)
        {
            GroupListStorage.Save();
            _lstCurrent.Invalidate();
            Logger.Write($"В список «{_selectedPreset.Name}» добавлено групп: {added}.", LogType.OK);
        }
    }

    // ── Working list ──────────────────────────────────────────────

    private void RefreshCurrentList()
    {
        _lstCurrent.BeginUpdate();
        _lstCurrent.Items.Clear();
        foreach (var g in _workingGroups)
            _lstCurrent.Items.Add(g.CN);
        _lstCurrent.EndUpdate();
        UpdateApplyButton();
    }

    private void UpdateApplyButton()
    {
        bool changed =
            _workingGroups.Count != _originalGroups.Count ||
            _workingGroups.Any(w => !_originalGroups.Any(o =>
                string.Equals(o.DN, w.DN, StringComparison.OrdinalIgnoreCase)));

        _btnApply.Enabled   = changed;
        _btnApply.BackColor = changed ? Color.FromArgb(50, 100, 200) : Color.FromArgb(160, 165, 175);
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _workingGroups.Count) return;
        var  group    = _workingGroups[e.Index];
        bool inPreset = _listGroupDNs.Contains(group.DN);
        bool selected = (e.State & DrawItemState.Selected) != 0;

        Color bg = selected ? SystemColors.Highlight
                 : inPreset ? Color.FromArgb(255, 255, 180)
                             : Color.White;
        Color fg = selected ? SystemColors.HighlightText : Color.Black;

        using var bgBrush = new SolidBrush(bg);
        using var fgBrush = new SolidBrush(fg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);
        e.Graphics.DrawString(group.CN, e.Font!, fgBrush, e.Bounds.X + 2, e.Bounds.Y + 1);
        e.DrawFocusRectangle();
    }

    // ── Search ────────────────────────────────────────────────────

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

            foreach (SearchResult r in searcher.FindAll())
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

    // ── Add / Remove / Load ───────────────────────────────────────

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
        _workingGroups.RemoveAt(_lstCurrent.SelectedIndex);
        RefreshCurrentList();
    }

    private void OnLoadUserGroups(object? sender, EventArgs e)
    {
        foreach (var g in _originalGroups)
        {
            if (!_workingGroups.Any(w => string.Equals(w.DN, g.DN, StringComparison.OrdinalIgnoreCase)))
                _workingGroups.Add(g);
        }
        _workingGroups.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.CN, b.CN));
        RefreshCurrentList();
        Logger.Write("Группы пользователя загружены.", LogType.Info);
    }

    // ── Apply ─────────────────────────────────────────────────────

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
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        int ok = 0, fail = 0;
        foreach (var g in toAdd)
        {
            if (ModifyGroupMembership(g.DN, add: true,  g.CN)) ok++;
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

            if (add) groupEntry.Properties["member"].Add(_userDN);
            else     groupEntry.Properties["member"].Remove(_userDN);

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
        0x80070005, 0x80072032, 0x8007052E, 0x80005000
    };

    private static bool IsAccessDenied(Exception ex)
    {
        if (ex is UnauthorizedAccessException) return true;
        if (ex is COMException c && Array.IndexOf(_accessDeniedCodes, (uint)c.ErrorCode) >= 0) return true;
        if (ex is System.Reflection.TargetInvocationException { InnerException: not null } tie)
            return tie.InnerException is UnauthorizedAccessException ||
                   (tie.InnerException is COMException comEx &&
                    Array.IndexOf(_accessDeniedCodes, (uint)comEx.ErrorCode) >= 0);
        return false;
    }
}
