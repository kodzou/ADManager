using System.DirectoryServices;
using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab7_SearchByOU : UserControl
{
    private DataGridView? _grid;
    private ComboBox?     _cbDomain;
    private TextBox?      _txtSearch;
    private ListBox?      _lbResults;
    private CheckBox?     _chkActiveOnly;
    private CheckBox?     _chkSubtree;
    private Label?        _lblCount;

    private List<OuItem>                 _ouItems     = new();
    private Dictionary<string, CheckBox> _fieldChecks = new();
    private List<FieldDef>               _fields      = new();

    private record FieldDef(string Name, string Label, bool Default, int Row);
    private record OuItem(string Domain, string Name, string DN);

    public Tab7_SearchByOU()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        // Заполняем домены
        _cbDomain!.Items.Add("— Все домены —");
        foreach (var d in MainForm.Domains) _cbDomain.Items.Add(d);
        _cbDomain.SelectedIndex = 0;

        // Строим чекбоксы полей выгрузки
        _fields = new List<FieldDef>
        {
            new("sAMAccountName", "Логин",            true,  0),
            new("displayName",    "Отображаемое имя", true,  0),
            new("mail",           "Email",            true,  0),
            new("department",     "Отдел",            false, 0),
            new("title",          "Должность",        false, 0),
            new("mobile",         "Мобильный",        false, 1),
            new("l",              "Город",            false, 1),
            new("manager",        "Руководитель",     false, 1),
            new("CanonicalName",  "Каноническое имя", false, 1)
        };
        _fieldChecks = new Dictionary<string, CheckBox>();
        int[] colIdx = { 0, 0 };
        const int startX = 120, stepX = 155;
        foreach (var f in _fields)
        {
            var chk = new CheckBox { Text = f.Label, Checked = f.Default, AutoSize = true };
            chk.Location = new Point(startX + colIdx[f.Row] * stepX, f.Row == 0 ? 6 : 30);
            _fieldChecks[f.Name] = chk;
            _panFields.Controls.Add(chk);
            colIdx[f.Row]++;
        }

        // Events
        _btnFindOU.Click += (_, _) => DoFindOU();
        _btnShow.Click   += (_, _) => DoShowUsers();
        _btnExport.Click += (_, _) =>
        {
            string ouName = _ouItems.Count > 0 && _lbResults!.SelectedIndex >= 0
                ? _ouItems[_lbResults.SelectedIndex].Name
                : "ou";
            CsvExporter.ExportLast($"ou_{ouName}_export.csv");
        };
        _txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            DoFindOU();
        };
        _grid.KeyDown += UiFactory.GridCopyHandler;
    }

    private void DoFindOU()
    {
        string searchText = _txtSearch?.Text.Trim() ?? "";
        if (searchText.Length < 2)
        {
            Logger.Write("Введите минимум 2 символа для поиска OU.", LogType.Warning);
            return;
        }

        _lbResults!.Items.Clear();
        _ouItems.Clear();

        string ouFilter  = $"(&(objectClass=organizationalUnit)(name=*{searchText}*))";
        string selected  = _cbDomain?.SelectedItem?.ToString() ?? "— Все домены —";
        var    domainsToSearch = selected == "— Все домены —" ? MainForm.Domains : new[] { selected };

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

                    int    userCount = CountUsers(domain, ouDN);
                    string display   = $"{domain}  |  {ouName}  |  👤 {userCount}  |  {FormatOuPath(ouDN)}";
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

    private void DoShowUsers()
    {
        int selIdx = _lbResults?.SelectedIndex ?? -1;
        if (selIdx < 0 || _ouItems.Count == 0)
        {
            Logger.Write("Выберите OU из списка.", LogType.Warning);
            return;
        }

        var    ou     = _ouItems[selIdx];
        string domain = ou.Domain;
        string ouDN   = ou.DN;

        string[] ldapProps = { "sAMAccountName", "displayName", "mail", "department", "title", "mobile", "l", "manager", "canonicalName", "userAccountControl" };

        var    scope  = _chkSubtree?.Checked == true ? SearchScope.Subtree : SearchScope.OneLevel;
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

    private static string FormatOuPath(string dn) =>
        string.Join(", ",
            dn.Split(',')
              .Where(p => p.TrimStart().StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
              .Select(p => p.TrimStart()[3..]));

    private List<string> BuildColumnList(string ouName)
    {
        var cols = new List<string> { "Domain", "OU" };
        foreach (var f in _fields)
            if (_fieldChecks[f.Name].Checked)
                cols.Add(f.Name);
        return cols;
    }
}
