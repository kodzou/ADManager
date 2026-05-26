using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Dialogs;

public class ChangePasswordDialog : Form
{
    private readonly string _domain;
    private readonly string _samAccountName;
    private          string _userDN;

    private readonly TextBox  _txtNewPwd;
    private readonly TextBox  _txtConfirm;
    private readonly Label    _lblMatch;
    private readonly Label    _lblResult;
    private readonly CheckBox _chkMustChange;

    // Генератор — правая панель
    private readonly NumericUpDown _numLength;
    private readonly CheckBox      _chkMemo;
    private readonly CheckBox      _chkUpper;
    private readonly CheckBox      _chkLower;
    private readonly CheckBox      _chkDigits;
    private readonly CheckBox      _chkSymbols;
    private readonly CheckBox      _chkNoRepeat;
    private readonly Button[]      _genButtons = new Button[5];

    public ChangePasswordDialog(string domain, string samAccountName)
    {
        _domain         = domain;
        _samAccountName = samAccountName;
        _userDN         = GetUserDN(domain, samAccountName);

        Text            = $"Смена пароля — {samAccountName} @ {domain}";
        Size            = new Size(820, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = Color.FromArgb(240, 242, 245);

        // ── Левая панель ──────────────────────────────────────
        var leftPanel = new Panel
        {
            Location = new Point(10, 10),
            Size     = new Size(370, 370)
        };

        var lblNewPwd = new Label { Text = "Новый пароль:", Location = new Point(0, 5), Size = new Size(360, 18) };
        _txtNewPwd = new TextBox
        {
            Location             = new Point(0, 25),
            Size                 = new Size(360, 24),
            UseSystemPasswordChar = true,
            Font                 = new Font("Consolas", 10f)
        };

        var lblConfirm = new Label { Text = "Подтверждение пароля:", Location = new Point(0, 58), Size = new Size(360, 18) };
        _txtConfirm = new TextBox
        {
            Location             = new Point(0, 78),
            Size                 = new Size(360, 24),
            UseSystemPasswordChar = true,
            Font                 = new Font("Consolas", 10f)
        };

        var chkShow = new CheckBox { Text = "Показать пароль", Location = new Point(0, 108), Size = new Size(200, 22) };

        _lblMatch = new Label
        {
            Location = new Point(210, 108),
            Size     = new Size(150, 22),
            Font     = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _chkMustChange = new CheckBox
        {
            Text     = "Требовать смены пароля при следующем входе",
            Location = new Point(0, 138),
            Size     = new Size(360, 22)
        };

        var btnApply = new Button
        {
            Text      = "Применить",
            Location  = new Point(0, 170),
            Size      = new Size(120, 32),
            BackColor = Color.FromArgb(50, 55, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnApply.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button
        {
            Text         = "Отмена",
            Location     = new Point(130, 170),
            Size         = new Size(100, 32),
            DialogResult = DialogResult.Cancel
        };

        _lblResult = new Label { Location = new Point(0, 212), Size = new Size(360, 40), Font = new Font("Segoe UI", 9f) };

        leftPanel.Controls.AddRange(new Control[]
        {
            lblNewPwd, _txtNewPwd, lblConfirm, _txtConfirm,
            chkShow, _lblMatch, _chkMustChange, btnApply, btnCancel, _lblResult
        });

        // ── Правая панель ─────────────────────────────────────
        var rightPanel = new Panel
        {
            Location    = new Point(390, 10),
            Size        = new Size(400, 426),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor   = Color.White
        };

        var lblGenTitle = new Label
        {
            Text     = "Генератор паролей",
            Location = new Point(5, 8),
            Size     = new Size(380, 22),
            Font     = new Font("Segoe UI", 10f, FontStyle.Bold)
        };

        var lblLenLabel = new Label { Text = "Длина пароля:", Location = new Point(5, 38), Size = new Size(110, 22) };
        _numLength = new NumericUpDown
        {
            Location = new Point(120, 36),
            Size     = new Size(70, 22),
            Minimum  = 4,
            Maximum  = 256,
            Value    = 12
        };

        _chkMemo = new CheckBox { Text = "Запоминающийся пароль", Location = new Point(5, 64), Size = new Size(380, 22) };
        var lblMemoHint = new Label
        {
            Text      = "Слоги + цифры. Лёгкий для запоминания.",
            Location  = new Point(5, 86),
            Size      = new Size(380, 18),
            ForeColor = Color.FromArgb(100, 110, 130),
            Font      = new Font("Segoe UI", 8.5f),
            Visible   = false
        };

        var separator = new Label { Location = new Point(5, 108), Size = new Size(380, 2), BorderStyle = BorderStyle.Fixed3D };

        _chkUpper   = new CheckBox { Text = "Прописные буквы (A–Z)", Location = new Point(5, 116), Size = new Size(380, 20), Checked = true };
        _chkLower   = new CheckBox { Text = "Строчные буквы (a–z)",  Location = new Point(5, 138), Size = new Size(380, 20), Checked = true };
        _chkDigits  = new CheckBox { Text = "Цифры (0–9)",           Location = new Point(5, 160), Size = new Size(380, 20), Checked = true };
        _chkSymbols = new CheckBox { Text = "Символы !\"№;%:?*()_+", Location = new Point(5, 182), Size = new Size(380, 20) };
        _chkNoRepeat = new CheckBox { Text = "Избегать повторения символов", Location = new Point(5, 204), Size = new Size(380, 20) };

        var btnGenerate = new Button
        {
            Text      = "Сгенерировать 5 вариантов",
            Location  = new Point(5, 230),
            Size      = new Size(385, 30),
            BackColor = Color.FromArgb(34, 139, 75),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnGenerate.FlatAppearance.BorderSize = 0;

        var lblGenHint = new Label
        {
            Text      = "Нажмите — подставится в поля и скопируется",
            Location  = new Point(5, 265),
            Size      = new Size(385, 16),
            ForeColor = Color.FromArgb(100, 110, 130),
            Font      = new Font("Segoe UI", 8.5f)
        };

        // 5 кнопок-паролей
        for (int i = 0; i < 5; i++)
        {
            var gb = new Button
            {
                Location  = new Point(5, 284 + i * 28),
                Size      = new Size(385, 24),
                Font      = new Font("Consolas", 9.5f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(248, 249, 252),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible   = false
            };
            gb.FlatAppearance.BorderSize = 0;
            _genButtons[i] = gb;
            rightPanel.Controls.Add(gb);

            // Замыкание через локальную копию индекса
            var btn = gb;
            btn.Click += (_, _) =>
            {
                string pwd = btn.Text;
                if (string.IsNullOrEmpty(pwd) || pwd.StartsWith('(')) return;
                _txtNewPwd.Text  = pwd;
                _txtConfirm.Text = pwd;
                Clipboard.SetText(pwd);

                var origColor = btn.BackColor;
                btn.BackColor = Color.FromArgb(100, 220, 130);
                var timer = new System.Windows.Forms.Timer { Interval = 400 };
                timer.Tick += (_, _) =>
                {
                    btn.BackColor = origColor;
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            };
        }

        rightPanel.Controls.AddRange(new Control[]
        {
            lblGenTitle, lblLenLabel, _numLength, _chkMemo, lblMemoHint,
            separator, _chkUpper, _chkLower, _chkDigits, _chkSymbols, _chkNoRepeat,
            btnGenerate, lblGenHint
        });

        Controls.Add(leftPanel);
        Controls.Add(rightPanel);
        CancelButton = btnCancel;

        // ── Events ────────────────────────────────────────────
        _chkMemo.CheckedChanged += (_, _) =>
        {
            lblMemoHint.Visible  = _chkMemo.Checked;
            _chkUpper.Enabled    = !_chkMemo.Checked;
            _chkLower.Enabled    = !_chkMemo.Checked;
            _chkDigits.Enabled   = !_chkMemo.Checked;
            _chkSymbols.Enabled  = !_chkMemo.Checked;
            _chkNoRepeat.Enabled = !_chkMemo.Checked;
        };

        chkShow.CheckedChanged += (_, _) =>
        {
            _txtNewPwd.UseSystemPasswordChar  = !chkShow.Checked;
            _txtConfirm.UseSystemPasswordChar = !chkShow.Checked;
        };

        EventHandler updateMatch = (_, _) => UpdateMatchLabel();
        _txtNewPwd.TextChanged  += updateMatch;
        _txtConfirm.TextChanged += updateMatch;

        btnGenerate.Click += OnGenerate;
        btnApply.Click    += OnApply;
    }

    // ── Вспомогательные ──────────────────────────────────────

    private void UpdateMatchLabel()
    {
        if (_txtNewPwd.Text.Length == 0)
        {
            _lblMatch.Text = "";
            return;
        }
        if (_txtNewPwd.Text == _txtConfirm.Text)
        {
            _lblMatch.Text      = "✔ Совпадают";
            _lblMatch.ForeColor = Color.FromArgb(50, 180, 80);
        }
        else
        {
            _lblMatch.Text      = "✖ Не совпадают";
            _lblMatch.ForeColor = Color.FromArgb(220, 60, 60);
        }
    }

    private void OnGenerate(object? sender, EventArgs e)
    {
        int len = (int)_numLength.Value;
        for (int i = 0; i < 5; i++)
        {
            string pwd = _chkMemo.Checked
                ? PasswordGenerator.GenerateMemorable(len)
                : PasswordGenerator.GenerateFromPool(
                    len,
                    _chkUpper.Checked, _chkLower.Checked,
                    _chkDigits.Checked, _chkSymbols.Checked,
                    _chkNoRepeat.Checked);

            _genButtons[i].Text    = pwd;
            _genButtons[i].Visible = true;
        }
    }

    private void OnApply(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_txtNewPwd.Text))
        {
            SetResult("Пароль не может быть пустым.", false);
            return;
        }
        if (_txtNewPwd.Text != _txtConfirm.Text)
        {
            SetResult("Пароли не совпадают.", false);
            return;
        }

        // Повторная попытка: GetUserDN мог вернуть "" в конструкторе из-за временной
        // недоступности LDAP. Без DN путь получается "LDAP://domain/" → E_ADS_BAD_PATHNAME.
        if (string.IsNullOrEmpty(_userDN))
            _userDN = GetUserDN(_domain, _samAccountName);

        if (string.IsNullOrEmpty(_userDN))
        {
            SetResult("✖ Не удалось найти объект пользователя в AD.", false);
            Logger.Write($"DN не найден для {_samAccountName} @ {_domain}. Проверьте подключение к домену.", LogType.Error);
            return;
        }

        string pwd        = _txtNewPwd.Text;
        bool   mustChange = _chkMustChange.Checked;

        bool ok = LdapHelper.InvokeADOperation(_domain, _userDN, entry =>
        {
            entry.Invoke("SetPassword", new object[] { pwd });
            entry.Properties["pwdLastSet"].Value = mustChange ? 0 : -1;
            entry.CommitChanges();
        }, this);

        if (ok)
        {
            SetResult("✔ Пароль успешно изменён.", true);
            Logger.Write($"Пароль изменён для {_samAccountName} @ {_domain}", LogType.OK);
        }
        else
        {
            SetResult("✖ Ошибка смены пароля.", false);
        }
    }

    private void SetResult(string text, bool success)
    {
        _lblResult.Text      = text;
        _lblResult.ForeColor = success
            ? Color.FromArgb(50, 180, 80)
            : Color.FromArgb(220, 60, 60);
    }

    private static string GetUserDN(string domain, string sam)
    {
        var searcher = LdapHelper.CreateSearcher(
            domain,
            $"(&(objectClass=user)(sAMAccountName={sam}))",
            new[] { "distinguishedName" });
        var res = searcher?.FindOne();
        return res == null ? "" : LdapHelper.GetProp(res, "distinguishedName");
    }
}