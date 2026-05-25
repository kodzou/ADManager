using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public static class Tab5_Duplicates
{
    private static DataGridView? _grid;
    private static CheckBox?     _chkExact;

    public static TabPage Create()
    {
        var tab = new TabPage("5. Дубликаты")
        {
            BackColor = Color.FromArgb(240, 242, 245)
        };

        var pan = new Panel
        {
            Location    = new Point(5, 5),
            Size        = new Size(980, 50),
            BackColor   = Color.FromArgb(230, 232, 238),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblDesc = new Label
        {
            Text      = "Поиск дублирующихся и похожих УЗ среди активных УЗ по всем доменам (по DisplayName и фамилии)",
            Location  = new Point(8, 8),
            Size      = new Size(680, 18),
            ForeColor = Color.FromArgb(60, 70, 100)
        };

        _chkExact = new CheckBox
        {
            Text     = "Только точные совпадения",
            Location = new Point(8, 27),
            Size     = new Size(220, 20)
        };

        var btnFind = UiFactory.MakeActionButton("Найти дубликаты", 150);
        btnFind.Location = new Point(700, 11);
        btnFind.Click   += (_, _) => DoFind();

        var btnExport = UiFactory.MakeExportButton();
        btnExport.Location = new Point(856, 11);
        btnExport.Click   += (_, _) => CsvExporter.ExportLast("duplicates.csv");

        pan.Controls.AddRange(new Control[] { lblDesc, _chkExact, btnFind, btnExport });

        _grid = UiFactory.MakeGrid();
        _grid.Location = new Point(5, 60);
        _grid.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _grid.KeyDown += UiFactory.GridCopyHandler;

        tab.Controls.AddRange(new Control[] { pan, _grid });
        return tab;
    }

    private static void DoFind()
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

        // Точные совпадения по DisplayName
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

        // Похожие — по фамилии (SN) в разных доменах
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
        public string Группа        { get; init; } = "";
        public string Тип           { get; init; } = "";
        public string Домен         { get; init; } = "";
        public string SamAccountName { get; init; } = "";
        public string DisplayName   { get; init; } = "";
    }
}