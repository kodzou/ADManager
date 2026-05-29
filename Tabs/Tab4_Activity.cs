using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab4_Activity : UserControl
{
    private DataGridView?  _grid;
    private NumericUpDown? _numDays;
    private CheckBox?      _chkMustChange;
    private RadioButton?   _rdAll;
    private RadioButton?   _rdUsers;
    private RadioButton?   _rdComputers;

    public Tab4_Activity()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        _btnSearch.Click += (_, _) => DoSearch();
        _btnExport.Click += (_, _) => CsvExporter.ExportLast("user_activity.csv");
        _grid.KeyDown    += UiFactory.GridCopyHandler;
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
    }

    private void DoSearch()
    {
        int    days     = (int)(_numDays?.Value ?? 90);
        var    cutoff   = DateTime.Now.AddDays(-days);
        long   cutoffFT = cutoff.ToFileTime();

        string mode = _rdUsers?.Checked == true     ? "users"
                    : _rdComputers?.Checked == true ? "computers"
                    : "all";

        Logger.Write($"Поиск неактивных УЗ (более {days} дней, режим: {mode})...", LogType.Info);

        var results = new List<ActivityRow>();

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
                        long lltFT   = LdapHelper.GetPropLong(r, "lastLogonTimestamp");
                        var  lltDT   = LdapHelper.FileTimeToDateTime(lltFT);
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
                            ПоследнийВход   = lltDT,
                            ДнейНазад       = daysAgo,
                            СменитьПароль   = mustChg ? "Да" : "Нет"
                        });
                    }
                }
                catch (Exception ex) { Logger.Write($"Ошибка в {domain}: {ex.Message}", LogType.Error); }
            }
        }

        if (mode is "all" or "computers")
        {
            string[] propsC  = { "sAMAccountName", "displayName", "lastLogonTimestamp", "userAccountControl", "operatingSystem" };
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
                            ПоследнийВход   = lltDT,
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
        public DateTime? ПоследнийВход   { get; init; }
        public int       ДнейНазад       { get; init; }
        public string    СменитьПароль   { get; init; } = "";
    }
}
