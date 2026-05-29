using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab6_SearchByList : UserControl
{
    private DataGridView?      _grid;
    private CheckedListBox?    _clbDomains;
    private TextBox?           _txtFio;
    private CheckBox?          _chkActiveOnly;
    private CheckBox?          _chkNoPrefix;
    private CheckBox?          _chkExtPrefix;
    private Label?             _lblCount;

    private List<FieldDef>               _fields      = new();
    private Dictionary<string, CheckBox> _fieldChecks = new();

    private record FieldDef(string Name, string Label, bool Default, int Row);

    public Tab6_SearchByList()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        // Заполняем домены
        foreach (var d in MainForm.Domains)
        {
            int  idx   = _clbDomains!.Items.Add(d);
            bool check = d is "webbankir.local" or "wb-digital.local" or "intwave.local";
            _clbDomains.SetItemChecked(idx, check);
        }

        // Строим чекбоксы полей выгрузки
        _fields = new List<FieldDef>
        {
            new("sAMAccountName", "Логин",            true,  0),
            new("displayName",    "Отображаемое имя", true,  0),
            new("givenName",      "Имя",              false, 0),
            new("sn",             "Фамилия",          false, 0),
            new("mail",           "Email",            true,  0),
            new("department",     "Отдел",            false, 0),
            new("title",          "Должность",        false, 1),
            new("l",              "Город",            false, 1),
            new("mobile",         "Мобильный",        false, 1),
            new("manager",        "Руководитель",     false, 1),
            new("CanonicalName",  "Каноническое имя", false, 1)
        };
        _fieldChecks = new Dictionary<string, CheckBox>();
        int[] colIdx = { 0, 0 };
        const int startX = 120, stepX = 148;
        foreach (var f in _fields)
        {
            var chk = new CheckBox { Text = f.Label, Checked = f.Default, AutoSize = true };
            chk.Location = new Point(startX + colIdx[f.Row] * stepX, f.Row == 0 ? 6 : 30);
            _fieldChecks[f.Name] = chk;
            _panFields.Controls.Add(chk);
            colIdx[f.Row]++;
        }

        // Events
        _btnFind.Click   += (_, _) => DoFind();
        _btnExport.Click += (_, _) => CsvExporter.ExportLast("ad_export.csv");
        _btnClear.Click  += (_, _) =>
        {
            _grid!.Rows.Clear();
            _grid.Columns.Clear();
            _lblCount!.Text      = "";
            AppState.LastResults = new List<object>();
        };
        _btnLoadFile.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Текстовые файлы (*.txt;*.csv)|*.txt;*.csv|Все файлы|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var lines = File.ReadAllLines(ofd.FileName, System.Text.Encoding.UTF8);
                _txtFio!.Text = string.Join("\r\n", lines);
                Logger.Write($"Загружено {lines.Length} строк из файла.", LogType.Info);
            }
        };
        _btnClearList.Click += (_, _) => _txtFio!.Text = "";
        _grid.KeyDown += UiFactory.GridCopyHandler;
    }

    private void DoFind()
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
            string norm  = ExtractSearchName(fio);
            bool   found = false;

            foreach (var domain in selectedDomains)
            {
                try
                {
                    string filter = _chkActiveOnly?.Checked == true
                        ? $"(&(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(displayName={norm}*))"
                        : $"(&(objectClass=user)(displayName={norm}*))";

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

        int foundCount    = results.Count(r => r.TryGetValue("Domain", out var d) && d != "(не найдено)");
        int notFoundCount = results.Count - foundCount;
        _lblCount!.Text = notFoundCount > 0
            ? $"Найдено: {foundCount} | Не найдено: {notFoundCount}"
            : $"Готово: {foundCount} записей";
        Logger.Write(notFoundCount > 0
            ? $"Найдено: {foundCount} пользователей, не найдено: {notFoundCount}."
            : $"Найдено: {foundCount} записей.", LogType.Summary);
    }

    private static string ExtractSearchName(string fio)
    {
        string clean = System.Text.RegularExpressions.Regex.Replace(fio, @"\s*\(.*?\)", "").Trim();
        clean = clean.Replace('ё', 'е').Replace('Ё', 'Е');
        var parts = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]} {parts[1]}" : clean;
    }

    private List<string> BuildColumnList()
    {
        var cols = new List<string> { "Domain" };
        foreach (var f in _fields)
            if (_fieldChecks[f.Name].Checked)
                cols.Add(f.Name);
        return cols;
    }
}
