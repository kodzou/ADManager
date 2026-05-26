using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public static class Tab4_Activity
{
    private static DataGridView?  _grid;
    private static NumericUpDown? _numDays;
    private static CheckBox?      _chkMustChange;
    private static RadioButton?   _rdAll;
    private static RadioButton?   _rdUsers;
    private static RadioButton?   _rdComputers;

    public static TabPage Create()
    {
        var tab = new TabPage("4. Активность")
        {
            BackColor = Color.FromArgb(240, 242, 245)
        };

        var pan = new Panel
        {
            Location    = new Point(5, 5),
            Size        = new Size(980, 70),
            BackColor   = Color.FromArgb(230, 232, 238),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Строка 1
        var lblPre = new Label { Text = "Последний вход более", Location = new Point(8, 13), AutoSize = true };

        _numDays = new NumericUpDown
        {
            Location = new Point(160, 10),
            Size     = new Size(70, 22),
            Minimum  = 1,
            Maximum  = 3650,
            Value    = 90
        };

        var lblPost = new Label { Text = "дней назад", Location = new Point(235, 13), AutoSize = true };

        _chkMustChange = new CheckBox
        {
            Text     = "Только с флагом \"Сменить пароль при входе\"",
            Location = new Point(320, 12),
            Size     = new Size(320, 22)
        };

        var btnSearch = UiFactory.MakeActionButton("Найти", 100);
        btnSearch.Location = new Point(648, 9);
        btnSearch.Click   += (_, _) => DoSearch();

        var btnExport = UiFactory.MakeExportButton();
        btnExport.Location = new Point(754, 9);
        btnExport.Click   += (_, _) => CsvExporter.ExportLast("user_activity.csv");

        // Строка 2
        var lblType = new Label { Text = "Тип объекта:", Location = new Point(8, 44), AutoSize = true };

        _rdAll       = new RadioButton { Text = "Все УЗ",               Location = new Point(100, 42), Size = new Size(90, 22),  Checked = true };
        _rdUsers     = new RadioButton { Text = "Только пользователи", Location = new Point(196, 42), Size = new Size(160, 22) };
        _rdComputers = new RadioButton { Text = "Только устройства",   Location = new Point(362, 42), Size = new Size(150, 22) };

        pan.Controls.AddRange(new Control[]
        {
            lblPre, _numDays, lblPost, _chkMustChange, btnSearch, btnExport,
            lblType, _rdAll, _rdUsers, _rdComputers
        });

        _grid = UiFactory.MakeGrid();
        _grid.Location = new Point(5, 84);
        _grid.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _grid.KeyDown += UiFactory.GridCopyHandler;

        // Форматирование: DateTime? → "dd.MM.yyyy HH:mm" или "Никогда"
        _grid.CellFormatting += (_, e) =>
        {
            if (e.Value is DateTime dt)
            {
                e.Value             = dt.ToString("dd.MM.yyyy HH:mm");
                e.FormattingApplied = true;
            }
            else if (e.Value is DBNull || e.Value == null)
            {
                e.Value             = "Никогда";
                e.FormattingApplied = true;
            }
        };

        tab.Controls.AddRange(new Control[] { pan, _grid });
        return tab;
    }

    private static void DoSearch()
    {
        int    days     = (int)(_numDays?.Value ?? 90);
        var    cutoff   = DateTime.Now.AddDays(-days);
        long   cutoffFT = cutoff.ToFileTime();

        string mode = _rdUsers?.Checked == true     ? "users"
                    : _rdComputers?.Checked == true ? "computers"
                    : "all";

        Logger.Write($"Поиск неактивных УЗ (более {days} дней, режим: {mode})...", LogType.Info);

        var results = new List<ActivityRow>();

        // ── Пользователи ──
        if (mode is "all" or "users")
        {
            string[] propsU  = { "sAMAccountName", "displayName", "lastLogonTimestamp", "pwdLastSet", "userAccountControl" };
            string   filterU = _chkMustChange?.Checked == true
                ? $"(&(objectClass=user)(!(objectClass=computer))(!(userAccountControl:1.2.840.113556.1.4.803:=2))(lastLogonTimestamp<={cutoffFT})(pwdLastSet=0))"
                : $"(&(objectClass=user)(!(objectClass=computer))(!(userAccountControl:1.2.840.113556.1.4.803:=2))(lastLogonTimestamp<={cutoffFT}))";

            foreach (var domain in MainForm.Domains)
            {
                try
                {
                    var searcher = LdapHelper.CreateSearcher(domain, filterU, propsU);
                    if (searcher == null) continue;

                    foreach (System.DirectoryServices.SearchResult r in searcher.FindAll())
                    {
                        long lltFT  = LdapHelper.GetPropLong(r, "lastLogonTimestamp");
                        var  lltDT  = LdapHelper.FileTimeToDateTime(lltFT);
                        int  daysAgo = lltDT.HasValue ? (int)(DateTime.Now - lltDT.Value).TotalDays : 9999;

                        long pwdLS   = LdapHelper.GetPropLong(r, "pwdLastSet");
                        int  uac     = (int)LdapHelper.GetPropLong(r, "userAccountControl");
                        bool mustChg = pwdLS == 0 && (uac & 65536) == 0;

                        results.Add(new ActivityRow
                        {
                            Тип             = "Пользователь",
                            Домен           = domain,
                            SamAccountName  = LdapHelper.GetProp(r, "sAMAccountName"),
                            ОтображаемоеИмя = LdapHelper.GetProp(r, "displayName"),
                            ПоследнийВход   = lltDT,   // DateTime? — null означает "Никогда"
                            ДнейНазад       = daysAgo,
                            СменитьПароль   = mustChg ? "Да" : "Нет"
                        });
                    }
                }
                catch (Exception ex) { Logger.Write($"Ошибка в {domain}: {ex.Message}", LogType.Error); }
            }
        }

        // ── Устройства ──
        if (mode is "all" or "computers")
        {
            string[] propsC = { "sAMAccountName", "displayName", "lastLogonTimestamp", "userAccountControl", "operatingSystem" };
            string   filterC = $"(&(objectClass=computer)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(lastLogonTimestamp<={cutoffFT}))";

            foreach (var domain in MainForm.Domains)
            {
                try
                {
                    var searcher = LdapHelper.CreateSearcher(domain, filterC, propsC);
                    if (searcher == null) continue;

                    foreach (System.DirectoryServices.SearchResult r in searcher.FindAll())
                    {
                        long lltFT   = LdapHelper.GetPropLong(r, "lastLogonTimestamp");
                        var  lltDT   = LdapHelper.FileTimeToDateTime(lltFT);
                        int  daysAgo = lltDT.HasValue ? (int)(DateTime.Now - lltDT.Value).TotalDays : 9999;

                        results.Add(new ActivityRow
                        {
                            Тип             = "Устройство",
                            Домен           = domain,
                            SamAccountName  = LdapHelper.GetProp(r, "sAMAccountName"),
                            ОтображаемоеИмя = LdapHelper.GetProp(r, "displayName"),
                            ПоследнийВход   = lltDT,   // DateTime? — null означает "Никогда"
                            ДнейНазад       = daysAgo,
                            СменитьПароль   = "—"
                        });
                    }
                }
                catch (Exception ex) { Logger.Write($"Ошибка в {domain}: {ex.Message}", LogType.Error); }
            }
        }

        AppState.LastResults = results.Cast<object>().ToList();
        GridFiller.Fill(_grid!, results);
        Logger.Write($"Найдено: {results.Count} записей.", LogType.Summary);
    }

    private record ActivityRow
    {
        public string    Тип             { get; init; } = "";
        public string    Домен           { get; init; } = "";
        public string    SamAccountName  { get; init; } = "";
        public string    ОтображаемоеИмя { get; init; } = "";
        public DateTime? ПоследнийВход   { get; init; }   // null → "Никогда"
        public int       ДнейНазад       { get; init; }
        public string    СменитьПароль   { get; init; } = "";
    }
}
