using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab3_Passwords : UserControl
{
    private DataGridView?  _grid;
    private RadioButton?   _rdExpired;
    private RadioButton?   _rdExpireSoon;
    private NumericUpDown? _numDays;

    public Tab3_Passwords()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        _btnSearch.Click += (_, _) => DoSearch();
        _btnExport.Click += (_, _) => CsvExporter.ExportLast("expired_passwords.csv");
        _grid.KeyDown    += UiFactory.GridCopyHandler;
        _grid.CellFormatting += (_, e) =>
        {
            if (e.Value is DateTime dt)
            {
                e.Value             = dt.ToString("dd.MM.yyyy HH:mm");
                e.FormattingApplied = true;
            }
            else if (e.Value is TimeSpan ts)
            {
                e.Value = ts.TotalSeconds >= 0
                    ? $"Через {(int)ts.TotalDays} дн. {ts.Hours} ч."
                    : $"Истёк {(int)Math.Abs(ts.TotalDays)} дн. {Math.Abs(ts.Hours)} ч. назад";
                e.FormattingApplied = true;
            }
            else if (e.Value is DBNull || e.Value == null)
            {
                e.Value             = "";
                e.FormattingApplied = true;
            }
        };
    }

    private void DoSearch()
    {
        Logger.Write("Поиск пользователей по паролям...", LogType.Info);

        var    now    = DateTime.Now;
        var    props  = new[] { "sAMAccountName", "displayName", "msDS-UserPasswordExpiryTimeComputed", "pwdLastSet", "distinguishedName" };
        string filter = "(&(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(!(userAccountControl:1.2.840.113556.1.4.803:=65536)))";

        var results = new List<PwdRow>();
        int days    = (int)(_numDays?.Value ?? 5);

        foreach (var domain in MainForm.Domains)
        {
            try
            {
                var searcher = LdapHelper.CreateSearcher(domain, filter, props);
                if (searcher == null) continue;

                foreach (System.DirectoryServices.SearchResult r in searcher.FindAll())
                {
                    long expFT = LdapHelper.GetPropLong(r, "msDS-UserPasswordExpiryTimeComputed");
                    if (expFT == 0) continue;
                    var expDT = LdapHelper.FileTimeToDateTime(expFT);
                    if (expDT == null) continue;

                    bool include = false;
                    if (_rdExpired?.Checked == true && expDT < now) include = true;
                    if (_rdExpireSoon?.Checked == true)
                    {
                        var cutoff = now.AddDays(days);
                        if (expDT >= now && expDT <= cutoff) include = true;
                    }
                    if (!include) continue;

                    long pwdLastSetFT = LdapHelper.GetPropLong(r, "pwdLastSet");
                    var  pwdLastSetDT = LdapHelper.FileTimeToDateTime(pwdLastSetFT);

                    results.Add(new PwdRow
                    {
                        Домен           = domain,
                        SamAccountName  = LdapHelper.GetProp(r, "sAMAccountName"),
                        ОтображаемоеИмя = LdapHelper.GetProp(r, "displayName"),
                        РасположениеOU  = FormatOuPath(LdapHelper.GetProp(r, "distinguishedName")),
                        ДатаИстечения   = expDT.Value,
                        ОсталосьВремени = expDT.Value - now,
                        ПоследняяСмена  = pwdLastSetDT
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Ошибка в {domain}: {ex.Message}", LogType.Error);
            }
        }

        AppState.LastResults = results.Cast<object>().ToList();
        GridFiller.Fill(_grid!, results);
        Logger.Write($"Найдено: {results.Count} записей.", LogType.Summary);
    }

    private static string FormatOuPath(string dn) =>
        string.Join(", ",
            dn.Split(',')
              .Where(p => p.TrimStart().StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
              .Select(p => p.TrimStart()[3..]));

    private record PwdRow
    {
        public string    Домен           { get; init; } = "";
        public string    SamAccountName  { get; init; } = "";
        public string    ОтображаемоеИмя { get; init; } = "";
        public DateTime  ДатаИстечения   { get; init; }
        public TimeSpan  ОсталосьВремени { get; init; }
        public DateTime? ПоследняяСмена  { get; init; }
        public string    РасположениеOU  { get; init; } = "";
    }
}
