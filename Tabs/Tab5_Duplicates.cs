using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab5_Duplicates : UserControl
{
    private DataGridView? _grid;
    private CheckBox?     _chkExact;

    public Tab5_Duplicates()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        _btnFind.Click   += (_, _) => DoFind();
        _btnExport.Click += (_, _) => CsvExporter.ExportLast("duplicates.csv");
        _grid.KeyDown    += UiFactory.GridCopyHandler;
    }

    private void DoFind()
    {
        Logger.Write("Загрузка всех активных УЗ для поиска дубликатов...", LogType.Info);

        string[] props  = { "sAMAccountName", "displayName", "sn", "userAccountControl" };
        string   filter = "(&(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";

        var allUsers = new List<(string Domain, string Sam, string DisplayName, string SN)>();

        foreach (var domain in MainForm.Domains)
        {
            try
            {
                var searcher = LdapHelper.CreateSearcher(domain, filter, props);
                if (searcher == null) continue;

                foreach (System.DirectoryServices.SearchResult r in searcher.FindAll())
                {
                    allUsers.Add((
                        domain,
                        LdapHelper.GetProp(r, "sAMAccountName"),
                        LdapHelper.GetProp(r, "displayName"),
                        LdapHelper.GetProp(r, "sn")
                    ));
                }
            }
            catch (Exception ex) { Logger.Write($"Ошибка в {domain}: {ex.Message}", LogType.Error); }
        }

        Logger.Write($"Загружено {allUsers.Count} пользователей. Анализ дубликатов...", LogType.Info);

        var results   = new List<DupRow>();
        var exactKeys = new HashSet<string>();
        int groupNum  = 0;

        var exactGroups = allUsers
            .Where(u => !string.IsNullOrEmpty(u.DisplayName))
            .GroupBy(u => u.DisplayName.ToLower().Trim())
            .Where(g => g.Count() >= 2);

        foreach (var grp in exactGroups)
        {
            groupNum++;
            foreach (var u in grp)
            {
                string key = $"{u.Domain}|{u.Sam}";
                exactKeys.Add(key);
                results.Add(new DupRow { Группа = $"Точный-{groupNum}", Тип = "Точный", Домен = u.Domain, SamAccountName = u.Sam, DisplayName = u.DisplayName });
            }
        }

        if (_chkExact?.Checked != true)
        {
            var snGroups = allUsers
                .Where(u => !string.IsNullOrEmpty(u.SN))
                .GroupBy(u => u.SN.ToLower().Trim())
                .Where(g => g.Select(x => x.Domain).Distinct().Count() >= 2);

            foreach (var grp in snGroups)
            {
                groupNum++;
                foreach (var u in grp)
                {
                    string key = $"{u.Domain}|{u.Sam}";
                    if (exactKeys.Contains(key)) continue;
                    results.Add(new DupRow { Группа = $"Похожий-{groupNum}", Тип = "Похожий", Домен = u.Domain, SamAccountName = u.Sam, DisplayName = u.DisplayName });
                }
            }
        }

        AppState.LastResults = results.Cast<object>().ToList();
        GridFiller.Fill(_grid!, results);
        Logger.Write($"Найдено {results.Count} записей дубликатов.", LogType.Summary);
    }

    private record DupRow
    {
        public string Группа         { get; init; } = "";
        public string Тип            { get; init; } = "";
        public string Домен          { get; init; } = "";
        public string SamAccountName { get; init; } = "";
        public string DisplayName    { get; init; } = "";
    }
}
