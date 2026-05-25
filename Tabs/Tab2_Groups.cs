using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public static class Tab2_Groups
{
    private static DataGridView? _grid;
    private static TextBox?      _txtSam;
    private static ComboBox?     _cbDomain;

    public static TabPage Create()
    {
        var tab = new TabPage("2. Группы пользователя")
        {
            BackColor = Color.FromArgb(240, 242, 245)
        };

        var lblSam = UiFactory.MakeLabel("Логин:");
        lblSam.Location = new Point(8, 6);

        _txtSam = UiFactory.MakeTextBox("Напр.: i.ivanov", 200);
        _txtSam.Location = new Point(8, 26);

        var lblDomain = UiFactory.MakeLabel("Домен:");
        lblDomain.Location = new Point(218, 6);

        _cbDomain = UiFactory.MakeComboBox(MainForm.Domains, 0, 200);
        _cbDomain.Location = new Point(218, 26);

        var btnSearch = UiFactory.MakeActionButton("Показать группы", 150);
        btnSearch.Location = new Point(424, 24);
        btnSearch.Click   += (_, _) => DoSearch();

        _grid = UiFactory.MakeGrid();
        _grid.Location = new Point(5, 58);
        _grid.Anchor   =
            AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right | AnchorStyles.Bottom;
        _grid.KeyDown += UiFactory.GridCopyHandler;

        tab.Controls.AddRange(new Control[]
        {
            lblSam, _txtSam, lblDomain, _cbDomain, btnSearch, _grid
        });

        return tab;
    }

    // Публичный метод: Tab1 вызывает его при переходе «Группы пользователя»
    public static void SetSearch(string sam, string domain)
    {
        if (_txtSam  != null) _txtSam.Text = sam;
        if (_cbDomain != null) _cbDomain.SelectedItem = domain;
    }

    public static void TriggerSearch() => DoSearch();

    private static void DoSearch()
    {
        string sam    = _txtSam?.Text.Trim() ?? "";
        string domain = _cbDomain?.SelectedItem?.ToString() ?? "";

        if (string.IsNullOrEmpty(sam))
        {
            Logger.Write("Введите логин пользователя.", LogType.Warning);
            return;
        }

        Logger.Write($"Поиск групп: {sam} @ {domain}", LogType.Info);

        var searcher = LdapHelper.CreateSearcher(
            domain,
            $"(&(objectClass=user)(sAMAccountName={sam}))",
            new[] { "memberOf", "displayName" });

        if (searcher == null) return;

        var result = searcher.FindOne();
        if (result == null)
        {
            Logger.Write($"Пользователь {sam} не найден в {domain}", LogType.Error);
            return;
        }

        var rows = new List<GroupRow>();
        var memberOf = result.Properties["memberOf"];

        if (memberOf != null)
        {
            foreach (string dn in memberOf)
            {
                string cn  = System.Text.RegularExpressions.Regex.Match(dn, @"CN=([^,]+)") is { Success: true } m ? m.Groups[1].Value : dn;
                string ou  = System.Text.RegularExpressions.Regex.Match(dn, @"OU=([^,]+)") is { Success: true } m2 ? m2.Groups[1].Value : "";
                rows.Add(new GroupRow { GroupCN = cn, OU = ou, Domain = domain, DistinguishedName = dn });
            }
        }

        GridFiller.Fill(_grid!, rows);
        Logger.Write($"Найдено {rows.Count} групп для {sam}.", LogType.Summary);
    }

    private record GroupRow
    {
        public string GroupCN           { get; init; } = "";
        public string OU                { get; init; } = "";
        public string Domain            { get; init; } = "";
        public string DistinguishedName { get; init; } = "";
    }
}