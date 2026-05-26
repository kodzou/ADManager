using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public static class Tab6_SearchByList
{
    private static DataGridView?      _grid;
    private static CheckedListBox?    _clbDomains;
    private static TextBox?           _txtFio;
    private static CheckBox?          _chkActiveOnly;
    private static CheckBox?          _chkNoPrefix;
    private static CheckBox?          _chkExtPrefix;
    private static Label?             _lblCount;
    private static List<FieldDef>     _fields = new();
    private static Dictionary<string, CheckBox> _fieldChecks = new();

    private record FieldDef(string Name, string Label, bool Default, int Row);

    public static TabPage Create()
    {
        var tab = new TabPage("6. Поиск по списку")
        {
            BackColor = Color.FromArgb(240, 242, 245)
        };

        // ── Домены ──
        var lblDomains = UiFactory.MakeLabel("Домены:");
        lblDomains.Location = new Point(8, 6);

        _clbDomains = new CheckedListBox
        {
            Location     = new Point(8, 26),
            Size         = new Size(200, 100),
            CheckOnClick = true
        };
        foreach (var d in MainForm.Domains)
        {
            int idx     = _clbDomains.Items.Add(d);
            bool check  = d is "webbankir.local" or "wb-digital.local" or "intwave.local";
            _clbDomains.SetItemChecked(idx, check);
        }

        // ── Список ФИО ──
        var lblFio = UiFactory.MakeLabel("Список ФИО (каждое с новой строки):");
        lblFio.Location = new Point(220, 6);

        _txtFio = new TextBox
        {
            Location    = new Point(220, 26),
            Size        = new Size(380, 100),
            Multiline   = true,
            ScrollBars  = ScrollBars.Vertical,
            Font        = new Font("Segoe UI", 9f)
        };

        var btnLoadFile = new Button
        {
            Text      = "Загрузить из файла",
            Location  = new Point(610, 26),
            Size      = new Size(150, 28),
            BackColor = Color.FromArgb(100, 100, 200),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnLoadFile.FlatAppearance.BorderSize = 0;
        btnLoadFile.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Текстовые файлы (*.txt;*.csv)|*.txt;*.csv|Все файлы|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var lines = File.ReadAllLines(ofd.FileName, System.Text.Encoding.UTF8);
                _txtFio!.Text = string.Join("\r\n", lines);
                Logger.Write($"Загружено {lines.Length} строк из файла.", LogType.Info);
            }
        };

        var btnClearList = new Button
        {
            Text      = "Очистить список",
            Location  = new Point(610, 62),
            Size      = new Size(150, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnClearList.Click += (_, _) => _txtFio!.Text = "";

        // ── Фильтры ──
        var panFilters = new Panel
        {
            Location    = new Point(5, 135),
            Size        = new Size(980, 30),
            BackColor   = Color.FromArgb(230, 232, 238),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblFHead = new Label { Text = "Фильтры поиска:", Location = new Point(5, 7), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };

        _chkActiveOnly = new CheckBox
        {
            Text      = "Только активные УЗ",
            Location  = new Point(120, 6),
            Size      = new Size(160, 22),
            Checked   = true,
            ForeColor = Color.FromArgb(30, 120, 50)
        };

        _chkNoPrefix = new CheckBox { Text = "Без префиксов",     Location = new Point(290, 6), Size = new Size(130, 22) };
        _chkExtPrefix = new CheckBox { Text = "С префиксом ext_", Location = new Point(430, 6), Size = new Size(150, 22) };

        panFilters.Controls.AddRange(new Control[] { lblFHead, _chkActiveOnly, _chkNoPrefix, _chkExtPrefix });

        // ── Поля для выгрузки ──
        _fields = new List<FieldDef>
        {
            new("sAMAccountName", "Логин",             true,  0),
            new("displayName",    "Отображаемое имя",  true,  0),
            new("givenName",      "Имя",               false, 0),
            new("sn",             "Фамилия",           false, 0),
            new("mail",           "Email",             true,  0),
            new("department",     "Отдел",             false, 0),
            new("title",          "Должность",         false, 1),
            new("l",              "Город",             false, 1),
            new("mobile",         "Мобильный",         false, 1),
            new("manager",        "Руководитель",      false, 1),
            new("CanonicalName",  "Каноническое имя",  false, 1)
        };

        var panFields = new Panel
        {
            Location    = new Point(5, 170),
            Size        = new Size(980, 56),
            BackColor   = Color.FromArgb(230, 232, 238),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblFlds = new Label { Text = "Поля для выгрузки:", Location = new Point(5, 9), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
        panFields.Controls.Add(lblFlds);

        _fieldChecks = new Dictionary<string, CheckBox>();
        int[] colIdx = { 0, 0 };
        const int startX = 120, stepX = 148;

        foreach (var f in _fields)
        {
            var chk = new CheckBox { Text = f.Label, Checked = f.Default, AutoSize = true };
            chk.Location = new Point(startX + colIdx[f.Row] * stepX, f.Row == 0 ? 6 : 30);
            _fieldChecks[f.Name] = chk;
            panFields.Controls.Add(chk);
            colIdx[f.Row]++;
        }

        // ── Кнопки ──
        var btnFind = UiFactory.MakeActionButton("Найти", 100);
        btnFind.Location = new Point(5, 254);
        btnFind.Click   += (_, _) => DoFind();

        var btnExport = new Button
        {
            Text      = "Выгрузить в CSV",
            Location  = new Point(115, 254),
            Size      = new Size(140, 28),
            BackColor = Color.FromArgb(34, 139, 75),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnExport.FlatAppearance.BorderSize = 0;
        btnExport.Click += (_, _) => CsvExporter.ExportLast("ad_export.csv");

        var btnClear = new Button
        {
            Text      = "Очистить таблицу",
            Location  = new Point(265, 254),
            Size      = new Size(140, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnClear.Click += (_, _) =>
        {
            _grid!.Rows.Clear();
            _grid.Columns.Clear();
            _lblCount!.Text     = "";
            AppState.LastResults = new List<object>();
        };

        _lblCount = new Label
        {
            Location  = new Point(420, 258),
            AutoSize  = true,
            ForeColor = Color.FromArgb(50, 100, 200),
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _grid = UiFactory.MakeGrid();
        _grid.Location = new Point(5, 290);
        _grid.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _grid.KeyDown += UiFactory.GridCopyHandler;

        tab.SizeChanged += (_, _) =>
        {
            int h = tab.ClientSize.Height - _grid.Top - 5;
            int w = tab.ClientSize.Width  - _grid.Left - 5;
            if (h > 50) _grid.Size = new Size(w, h);
        };

        tab.Controls.AddRange(new Control[]
        {
            lblDomains, _clbDomains, lblFio, _txtFio,
            btnLoadFile, btnClearList,
            panFilters, panFields,
            btnFind, btnExport, btnClear, _lblCount, _grid
        });

        return tab;
    }

    private static void DoFind()
    {
        var selectedDomains = new List<string>();
        for (int i = 0; i < _clbDomains!.Items.Count; i++)
            if (_clbDomains.GetItemChecked(i))
                selectedDomains.Add(_clbDomains.Items[i].ToString()!);

        if (selectedDomains.Count == 0)
        {
            Logger.Write("Выберите хотя бы один домен.", LogType.Warning);
            return;
        }

        var fioLines = (_txtFio?.Text ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (fioLines.Count == 0)
        {
            Logger.Write("Список ФИО пуст.", LogType.Warning);
            return;
        }

        string[] ldapProps = { "displayName", "sAMAccountName", "sn", "givenName", "userAccountControl", "mail", "department", "title", "l", "mobile", "manager", "canonicalName" };

        var results = new List<Dictionary<string, string>>();
        var cols    = BuildColumnList();

        Logger.Write($"Поиск {fioLines.Count} ФИО в {selectedDomains.Count} домен(ах)...", LogType.Info);

        foreach (var fio in fioLines)
        {
            string norm  = fio.Replace('ё', 'е').Replace('Ё', 'Е');
            bool   found = false;

            foreach (var domain in selectedDomains)
            {
                try
                {
                    string filter = _chkActiveOnly?.Checked == true
                        ? $"(&(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(displayName=*{norm}*))"
                        : $"(&(objectClass=user)(displayName=*{norm}*))";

                    var searcher = LdapHelper.CreateSearcher(domain, filter, ldapProps);
                    if (searcher == null) continue;

                    foreach (System.DirectoryServices.SearchResult r in searcher.FindAll())
                    {
                        string sam = LdapHelper.GetProp(r, "sAMAccountName");
                        if (_chkNoPrefix?.Checked  == true && !System.Text.RegularExpressions.Regex.IsMatch(sam, @"^[a-zA-Z.]+$")) continue;
                        if (_chkExtPrefix?.Checked == true && !sam.StartsWith("ext_")) continue;

                        var row = new Dictionary<string, string>();
                        row["Domain"] = domain.Replace(".local", "");

                        foreach (var col in cols.Where(c => c != "Domain"))
                        {
                            row[col] = col == "manager"
                                ? LdapHelper.GetCNFromDN(LdapHelper.GetProp(r, "manager"))
                                : LdapHelper.GetProp(r, col == "CanonicalName" ? "canonicalName" : col.ToLower());
                        }
                        results.Add(row);
                        found = true;
                    }
                }
                catch (Exception ex) { Logger.Write($"Ошибка поиска в {domain}: {ex.Message}", LogType.Error); }
            }

            if (!found)
            {
                Logger.Write($"[WARN] Не найдено: {fio}", LogType.Warning);
                var row = new Dictionary<string, string>();
                row["Domain"] = "(не найдено)";
                foreach (var col in cols.Where(c => c != "Domain"))
                    row[col] = col == "displayName" ? fio : "";
                results.Add(row);
            }
        }

        AppState.LastResults = results.Cast<object>().ToList();
        GridFiller.FillDynamic(_grid!, cols, results);
        _lblCount!.Text = $"Готово: {results.Count} записей";
        Logger.Write($"Найдено: {results.Count} записей.", LogType.Summary);
    }

    private static List<string> BuildColumnList()
    {
        var cols = new List<string> { "Domain" };
        foreach (var f in _fields)
            if (_fieldChecks[f.Name].Checked)
                cols.Add(f.Name);
        return cols;
    }
}