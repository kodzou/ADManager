using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab2_Groups : UserControl
{
    private DataGridView? _grid;
    private TextBox?      _txtSam;
    private ComboBox?     _cbDomain;

    private string _currentSam     = "";
    private string _currentDomain  = "";
    private string _currentDN      = "";
    private string _currentDisplay = "";
    private List<(string CN, string DN)> _currentGroupList = new();

    public Tab2_Groups()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()
    {
        _btnSearch.Click     += (_, _) => DoSearch();
        _btnEditGroups.Click += OnEditGroups;
        _txtSam.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            DoSearch();
        };
        _grid.KeyDown += UiFactory.GridCopyHandler;
    }

    private void OnEditGroups(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentSam))
        {
            Logger.Write("Сначала выполните поиск групп пользователя.", LogType.Warning);
            return;
        }

        var cred = CredentialCache.Get(_currentDomain);
        if (cred == null)
        {
            Logger.Write("Для редактирования групп требуются права администратора.", LogType.Warning);
            using var credDlg = new Dialogs.AdminCredentialsDialog(_currentDomain);
            if (credDlg.ShowDialog(this.FindForm()) != DialogResult.OK)
            {
                Logger.Write("Операция отменена пользователем.", LogType.Inactive);
                return;
            }
            cred = credDlg.Credential!;
            CredentialCache.Store(_currentDomain, cred);
            Logger.Write("Учётные данные сохранены до конца текущего дня.", LogType.Info);
        }

        if (string.IsNullOrEmpty(_currentDN))
        {
            Logger.Write("Не удалось определить DN пользователя. Повторите поиск.", LogType.Error);
            return;
        }

        using var dlg = new Dialogs.EditGroupsDialog(
            _currentDomain, _currentSam, _currentDN, _currentDisplay, cred, _currentGroupList);

        if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            DoSearch();
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
            new[] { "memberOf", "displayName", "distinguishedName" });

        if (searcher == null) return;

        var result = searcher.FindOne();
        if (result == null)
        {
            Logger.Write($"Пользователь {sam} не найден в {domain}", LogType.Error);
            return;
        }

        _currentSam     = sam;
        _currentDomain  = domain;
        _currentDN      = LdapHelper.GetProp(result, "distinguishedName");
        _currentDisplay = LdapHelper.GetProp(result, "displayName", sam);

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

        _currentGroupList = rows.Select(r => (r.GroupCN, r.DistinguishedName)).ToList();

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
