using System.DirectoryServices;
using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public static class Tab7_SearchByOU
{
    private static DataGridView? _grid;
    private static ComboBox?     _cbDomain;
    private static TextBox?      _txtSearch;
    private static ListBox?      _lbResults;
    private static CheckBox?     _chkActiveOnly;
    private static CheckBox?     _chkSubtree;
    private static Label?        _lblCount;

    private static List<OuItem>          _ouItems  = new();
    private static Dictionary<string, CheckBox> _fieldChecks = new();
    private static List<FieldDef>               _fields      = new();

    private record FieldDef(string Name, string Label, bool Default, int Row);
    private record OuItem(string Domain, string Name, string DN);

    public static TabPage Create()
    {
        var tab = new TabPage("7. Поиск по OU")
        {
            BackColor = Color.FromArgb(240, 242, 245)
        };

        // ── Строка 1: домен + поиск OU ──
        var lblDomain = UiFactory.MakeLabel("Домен:");
        lblDomain.Location = new Point(8, 6);

        _cbDomain = UiFactory.MakeComboBox(
            new[] { "— Все домены —" }.Concat(MainForm.Domains).ToArray(), 0, 220);
        _cbDomain.Location = new Point(8, 26);

        var lblOuSearch = UiFactory.MakeLabel("Поиск OU по названию:");
        lblOuSearch.Location = new Point(240, 6);

        _txtSearch = UiFactory.MakeTextBox("Введите название OU...", 300);
        _txtSearch.Location = new Point(240, 26);

        var btnFindOU = UiFactory.MakeActionButton("Найти OU", 110);
        btnFindOU.Location = new Point(550, 24);
        btnFindOU.Click   += (_, _) => DoFindOU();

        _txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            DoFindOU();
        };

        // ── ListBox найденных OU ──
        var lblOuList = UiFactory.MakeLabel("Найденные OU (выберите одну):");
        lblOuList.Location = new Point(8, 54);

        _lbResults = new ListBox
        {
            Location          = new Point(8, 74),
            Size              = new Size(980, 110),
            HorizontalScrollbar = true,
            Font              = new Font("Consolas", 8.5f)
        };
        _lbResults.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // ── Фильтры ──
        var panFilters = new Panel
        {
            Location    = new Point(5, 192),
            Size        = new Size(980, 28),
            BackColor   = Color.FromArgb(230, 232, 238),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblFHead = new Label { Text = "Фильтры поиска:", Location = new Point(5, 6), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };

        _chkActiveOnly = new CheckBox
        {
            Text      = "Только активные УЗ",
            Location  = new Point(120, 5),
            Size      = new Size(160, 20),
            Checked   = true,
            ForeColor = Color.FromArgb(30, 120, 50)
        };

        _chkSubtree = new CheckBox
        {
            Text     = "Включая вложенные OU",
            Location = new Point(290, 5),
            Size     = new Size(180, 20),
            Checked  = true
        };

        panFilters.Controls.AddRange(new Control[] { lblFHead, _chkActiveOnly, _chkSubtree });

        // ── Поля для выгрузки ──
        _fields = new List<FieldDef>
        {
            new("sAMAccountName", "Логин",             true,  0),
            new("displayName",    "Отображаемое имя",  true,  0),
            new("mail",           "Email",             true,  0),
            new("department",     "Отдел",             false, 0),
            new("title",          "Должность",         false, 0),
            new("mobile",         "Мобильный",         false, 1),
            new("l",              "Город",             false, 1),
            new("manager",        "Руководитель",      false, 1),
            new("CanonicalName",  "Каноническое имя",  false, 1)
        };

        var panFields = new Panel
        {
            Location    = new Point(5, 224),
            Size        = new Size(980, 56),
            BackColor   = Color.FromArgb(230, 232, 238),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblFlds = new Label { Text = "Поля для выгрузки:", Location = new Point(5, 9), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
        panFields.Controls.Add(lblFlds);

        _fieldChecks = new Dictionary<string, CheckBox>();
        int[] colIdx = { 0, 0 };
        const int startX = 120, stepX = 155;

        foreach (var f in _fields)
        {
            var chk = new CheckBox { Text = f.Label, Checked = f.Default, AutoSize = true };
            chk.Location = new Point(startX + colIdx[f.Row] * stepX, f.Row == 0 ? 6 : 30);
            _fieldChecks[f.Name] = chk;
            panFields.Controls.Add(chk);
            colIdx[f.Row]++;
        }

        // ── Кнопки / счётчик ──
        var btnShow = UiFactory.MakeActionButton("Показать пользователей", 180);
        btnShow.Location = new Point(5, 292);
        btnShow.Click   += (_, _) => DoShowUsers();

        var btnExport = UiFactory.MakeExportButton();
        btnExport.Location = new Point(195, 292);
        btnExport.Click   += (_, _) =>
        {
            string ouName = _ouItems.Count > 0 && _lbResults!.SelectedIndex >= 0
                ? _ouItems[_lbResults.SelectedIndex].Name
                : "ou";
            CsvExporter.ExportLast($"ou_{ouName}_export.csv");
        };

        _lblCount = new Label
        {
            Location  = new Point(330, 296),
            AutoSize  = true,
            ForeColor = Color.FromArgb(50, 100, 200),
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _grid = UiFactory.MakeGrid();
        _grid.Location = new Point(5, 327);
        _grid.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _grid.KeyDown += UiFactory.GridCopyHandler;

        // Масштабируем высоту вручную при каждом изменении размера вкладки.
        tab.SizeChanged += (_, _) =>
        {
            int h = tab.ClientSize.Height - _grid.Top - 5;
            int w = tab.ClientSize.Width  - _grid.Left - 5;
            if (h > 50) _grid.Size = new Size(w, h);
        };

        tab.Controls.AddRange(new Control[]
        {
            lblDomain, _cbDomain, lblOuSearch, _txtSearch, btnFindOU,
            lblOuList, _lbResults, panFilters, panFields,
            btnShow, btnExport, _lblCount, _grid
        });

        return tab;
    }

    // ── Найти OU ─────────────────────────────────────────────
    private static void DoFindOU()
    {
        string searchText = _txtSearch?.Text.Trim() ?? "";
        if (searchText.Length < 2)
        {
            Logger.Write("Введите минимум 2 символа для поиска OU.", LogType.Warning);
            return;
        }

        _lbResults!.Items.Clear();
        _ouItems.Clear();

        string ouFilter = $"(&(objectClass=organizationalUnit)(name=*{searchText}*))";
        string selected = _cbDomain?.SelectedItem?.ToString() ?? "— Все домены —";
        var domainsToSearch = selected == "— Все домены —" ? MainForm.Domains : new[] { selected };

        Logger.Write($"Поиск OU: '{searchText}'...", LogType.Info);

        foreach (var domain in domainsToSearch)
        {
            try
            {
                var searcher = LdapHelper.CreateSearcher(domain, ouFilter, new[] { "name", "distinguishedName" });
                if (searcher == null) continue;

                foreach (SearchResult r in searcher.FindAll())
                {
                    string ouName = LdapHelper.GetProp(r, "name");
                    string ouDN   = LdapHelper.GetProp(r, "distinguishedName");

                    // Подсчёт активных пользователей
                    int userCount = CountUsers(domain, ouDN);

                    string display = $"{domain}  |  {ouName}  |  👤 {userCount}  |  {FormatOuPath(ouDN)}";
                    _lbResults.Items.Add(display);
                    _ouItems.Add(new OuItem(domain, ouName, ouDN));
                }
            }
            catch (Exception ex) { Logger.Write($"Ошибка поиска OU в {domain}: {ex.Message}", LogType.Error); }
        }

        Logger.Write($"Найдено OU: {_lbResults.Items.Count}", LogType.Summary);
    }

    private static int CountUsers(string domain, string ouDN)
    {
        try
        {
            const string countFilter = "(&(objectClass=user)(!(objectClass=computer))(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";
            var entry    = new DirectoryEntry($"LDAP://{domain}/{ouDN}");
            var searcher = new DirectorySearcher(entry)
            {
                Filter      = countFilter,
                SearchScope = SearchScope.Subtree,
                PageSize    = 1000,
                SizeLimit   = 0
            };
            searcher.PropertiesToLoad.Add("sAMAccountName");
            return searcher.FindAll().Count;
        }
        catch { return -1; }
    }

    // ── Показать пользователей ────────────────────────────────
    private static void DoShowUsers()
    {
        int selIdx = _lbResults?.SelectedIndex ?? -1;
        if (selIdx < 0 || _ouItems.Count == 0)
        {
            Logger.Write("Выберите OU из списка.", LogType.Warning);
            return;
        }

        var ou        = _ouItems[selIdx];
        string domain = ou.Domain;
        string ouDN   = ou.DN;

        string[] ldapProps = { "sAMAccountName", "displayName", "mail", "department", "title", "mobile", "l", "manager", "canonicalName", "userAccountControl" };

        var scope  = _chkSubtree?.Checked == true ? SearchScope.Subtree : SearchScope.OneLevel;
        string filter = _chkActiveOnly?.Checked == true
            ? "(&(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))"
            : "(objectClass=user)";

        Logger.Write($"Поиск пользователей в OU: {ouDN}", LogType.Info);

        try
        {
            var entry    = new DirectoryEntry($"LDAP://{domain}/{ouDN}");
            var searcher = new DirectorySearcher(entry)
            {
                Filter      = filter,
                SearchScope = scope,
                PageSize    = 1000
            };
            foreach (var p in ldapProps) searcher.PropertiesToLoad.Add(p);

            var cols    = BuildColumnList(ou.Name);
            var results = new List<Dictionary<string, string>>();

            foreach (SearchResult r in searcher.FindAll())
            {
                var row = new Dictionary<string, string>();
                row["Domain"] = domain.Replace(".local", "");
                row["OU"]     = ou.Name;

                foreach (var col in cols.Where(c => c is not "Domain" and not "OU"))
                {
                    row[col] = col switch
                    {
                        "manager"       => LdapHelper.GetCNFromDN(LdapHelper.GetProp(r, "manager")),
                        "CanonicalName" => LdapHelper.GetProp(r, "canonicalName"),
                        _               => LdapHelper.GetProp(r, col.ToLower())
                    };
                }
                results.Add(row);
            }

            AppState.LastResults = results.Cast<object>().ToList();
            GridFiller.FillDynamic(_grid!, cols, results);
            _lblCount!.Text = $"Найдено: {results.Count} пользователей";
            Logger.Write($"Найдено {results.Count} пользователей в OU '{ou.Name}'.", LogType.Summary);
        }
        catch (Exception ex)
        {
            Logger.Write($"Ошибка поиска в OU: {ex.Message}", LogType.Error);
        }
    }

    // Преобразует DN вида "OU=Finance,OU=Corporate,DC=example,DC=local" → "Finance, Corporate"
    private static string FormatOuPath(string dn) =>
        string.Join(", ",
            dn.Split(',')
              .Where(p => p.TrimStart().StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
              .Select(p => p.TrimStart()[3..]));

    private static List<string> BuildColumnList(string ouName)
    {
        var cols = new List<string> { "Domain", "OU" };
        foreach (var f in _fields)
            if (_fieldChecks[f.Name].Checked)
                cols.Add(f.Name);
        return cols;
    }
}