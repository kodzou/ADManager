using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab2_Groups : UserControl
{
    private DataGridView? _grid;
    private TextBox?      _txtSam;
    private ComboBox?     _cbDomain;

    public Tab2_Groups()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        _btnSearch.Click += (_, _) => DoSearch();
        _txtSam.KeyDown  += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            DoSearch();
        };
        _grid.KeyDown += UiFactory.GridCopyHandler;
    }

    public void SetSearch(string sam, string domain)
    {
        if (_txtSam   != null) _txtSam.Text = sam;
        if (_cbDomain != null) _cbDomain.SelectedItem = domain;
    }

    public void TriggerSearch() => DoSearch();

    private void DoSearch()
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

        var rows     = new List<GroupRow>();
        var memberOf = result.Properties["memberOf"];

        if (memberOf != null)
        {
            foreach (string dn in memberOf)
            {
                string cn  = System.Text.RegularExpressions.Regex.Match(dn, @"CN=([^,]+)") is { Success: true } m  ? m.Groups[1].Value  : dn;
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
