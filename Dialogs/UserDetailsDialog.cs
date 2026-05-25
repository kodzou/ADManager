using System.DirectoryServices;
using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Dialogs;

public class UserDetailsDialog : Form
{
    public UserDetailsDialog(string domain, string samAccountName)
    {
        var props = new[]
        {
            "displayName", "sAMAccountName", "mail", "mobile", "l", "st",
            "title", "department", "company", "manager", "canonicalName",
            "userAccountControl", "whenCreated", "whenChanged"
        };

        var searcher = LdapHelper.CreateSearcher(
            domain,
            $"(&(objectClass=user)(sAMAccountName={samAccountName}))",
            props);

        SearchResult? result = searcher?.FindOne();

        if (result == null)
        {
            Logger.Write($"Пользователь {samAccountName} не найден в {domain}", LogType.Error);
            Load += (_, _) => Close();
            return;
        }

        int uac     = (int)LdapHelper.GetPropLong(result, "userAccountControl");
        bool enabled = (uac & 2) == 0;

        var fields = new[]
        {
            ("Отображаемое имя", "displayName"),
            ("Логин",            "sAMAccountName"),
            ("Домен",            "_domain"),
            ("Эл. почта",        "mail"),
            ("Мобильный",        "mobile"),
            ("Город",            "l"),
            ("Область / край",   "st"),
            ("Должность",        "title"),
            ("Отдел",            "department"),
            ("Организация",      "company"),
            ("Руководитель",     "manager"),
            ("Каноническое имя", "canonicalName"),
            ("УЗ активна",       "_enabled"),
            ("Создан",           "whenCreated"),
            ("Изменён",          "whenChanged")
        };

        string displayName = LdapHelper.GetProp(result, "displayName");

        const int rowH      = 28;
        const int padTop    = 10;
        const int padBottom = 50;
        int totalH = padTop + fields.Length * rowH + padBottom;

        Text            = $"Информация — {displayName} ({samAccountName})";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = Color.FromArgb(240, 242, 245);
        ClientSize      = new Size(580, totalH);

        const int lblW = 150;
        const int txtW = 400;
        const int x0   = 10;
        const int x1   = 165;
        int y = padTop;

        foreach (var (label, attr) in fields)
        {
            var lbl = new Label
            {
                Text      = label + ":",
                Location  = new Point(x0, y + 3),
                Size      = new Size(lblW, 18),
                ForeColor = Color.FromArgb(80, 90, 110),
                Font      = new Font("Segoe UI", 8.5f)
            };

            string val = attr switch
            {
                "_domain"  => domain,
                "_enabled" => enabled ? "Да" : "Нет",
                "manager"  => LdapHelper.GetCNFromDN(LdapHelper.GetProp(result, "manager")),
                "whenCreated" => TryFormatDate(LdapHelper.GetProp(result, "whenCreated")),
                "whenChanged" => TryFormatDate(LdapHelper.GetProp(result, "whenChanged")),
                _             => LdapHelper.GetProp(result, attr)
            };

            var txt = new TextBox
            {
                Text      = val,
                ReadOnly  = true,
                BackColor = Color.White,
                Location  = new Point(x1, y),
                Size      = new Size(txtW, 22)
            };

            Controls.Add(lbl);
            Controls.Add(txt);
            y += rowH;
        }

        var btnClose = new Button
        {
            Text         = "Закрыть",
            Size         = new Size(100, 30),
            Location     = new Point(240, y + 8),
            DialogResult = DialogResult.OK,
            BackColor    = Color.FromArgb(50, 55, 70),
            ForeColor    = Color.White,
            FlatStyle    = FlatStyle.Flat
        };
        btnClose.FlatAppearance.BorderSize = 0;
        AcceptButton = btnClose;
        Controls.Add(btnClose);

        Logger.Write($"Подробная информация загружена: {displayName}", LogType.OK);
    }

    private static string TryFormatDate(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return DateTime.TryParse(raw, out var dt)
            ? dt.ToString("dd.MM.yyyy HH:mm")
            : raw;
    }
}