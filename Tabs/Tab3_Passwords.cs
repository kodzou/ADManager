using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public static class Tab3_Passwords
{
    private static DataGridView?  _grid;
    private static RadioButton?   _rdExpired;
    private static RadioButton?   _rdExpireSoon;
    private static NumericUpDown? _numDays;

    public static TabPage Create()
    {
        var tab = new TabPage("3. Пароли")
        {
            BackColor = Color.FromArgb(240, 242, 245)
        };

        var pan = new Panel
        {
            Location    = new Point(5, 5),
            Size        = new Size(980, 44),
            BackColor   = Color.FromArgb(230, 232, 238),
            BorderStyle = BorderStyle.FixedSingle
        };

        _rdExpired = new RadioButton
        {
            Text     = "Уже истёкшие пароли",
            Location = new Point(8, 12),
            Size     = new Size(200, 22),
            Checked  = true
        };

        _rdExpireSoon = new RadioButton
        {
            Text     = "Истекают в ближайшие",
            Location = new Point(215, 12),
            Size     = new Size(190, 22)
        };

        _numDays = new NumericUpDown
        {
            Location = new Point(410, 10),
            Size     = new Size(60, 22),
            Minimum  = 1,
            Maximum  = 30,
            Value    = 5
        };

        var lblDays = new Label { Text = "дней", Location = new Point(476, 13), AutoSize = true };

        var btnSearch = UiFactory.MakeActionButton("Найти", 100);
        btnSearch.Location = new Point(520, 9);
        btnSearch.Click   += (_, _) => DoSearch();

        var btnExport = UiFactory.MakeExportButton();
        btnExport.Location = new Point(626, 9);
        btnExport.Click   += (_, _) => CsvExporter.ExportLast("expired_passwords.csv");

        pan.Controls.AddRange(new Control[]
        {
            _rdExpired, _rdExpireSoon, _numDays, lblDays, btnSearch, btnExport
        });

        _grid = UiFactory.MakeGrid();
        _grid.Location = new Point(5, 58);
        _grid.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _grid.KeyDown += UiFactory.GridCopyHandler;

        // Форматирование ячеек: DateTime → "dd.MM.yyyy HH:mm", TimeSpan → читаемый текст
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

        tab.Controls.AddRange(new Control[] { pan, _grid });
        return tab;
    }

    private static void DoSearch()
    {
        Logger.Write("Поиск пользователей по паролям...", LogType.Info);

        var    now    = DateTime.Now;
        var    props  = new[] { "sAMAccountName", "displayName", "msDS-UserPasswordExpiryTimeComputed", "pwdLastSet" };
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

    private record PwdRow
    {
        public string    Домен           { get; init; } = "";
        public string    SamAccountName  { get; init; } = "";
        public string    ОтображаемоеИмя { get; init; } = "";
        public DateTime  ДатаИстечения   { get; init; }
        public TimeSpan  ОсталосьВремени { get; init; }
        public DateTime? ПоследняяСмена  { get; init; }
    }
}
