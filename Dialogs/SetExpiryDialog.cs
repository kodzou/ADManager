using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Dialogs;

public partial class SetExpiryDialog : Form
{
    private string        _domain     = "";
    private string        _dn         = "";
    private RadioButton   _rdoNever   = null!;
    private RadioButton   _rdoExpires = null!;
    private DateTimePicker _dtp       = null!;
    private IWin32Window? _ownerWin;

    public SetExpiryDialog() { InitializeComponent(); }

    public SetExpiryDialog(string domain, string sam, string dn, string displayName, IWin32Window ownerWin)
    {
        _domain   = domain;
        _dn       = dn;
        _ownerWin = ownerWin;

        var searcher   = LdapHelper.CreateSearcher(
            domain,
            $"(&(objectClass=user)(sAMAccountName={sam}))",
            new[] { "accountExpires" });
        var result     = searcher?.FindOne();
        long accExpRaw = result != null ? LdapHelper.GetPropLong(result, "accountExpires") : 0L;
        bool neverExp  = accExpRaw <= 0L || accExpRaw == long.MaxValue;

        var expDT = neverExp ? null : LdapHelper.FileTimeToDateTime(accExpRaw);

        Text            = "Срок действия учётной записи";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = Color.FromArgb(240, 242, 245);
        ClientSize      = new Size(460, 212);

        var lblName = new Label
        {
            Text      = displayName,
            Location  = new Point(12, 10),
            AutoSize  = true,
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 60, 130)
        };

        var lblLogin = new Label
        {
            Text      = $"{domain.Replace(".local", "")} / {sam}",
            Location  = new Point(12, 34),
            AutoSize  = true,
            ForeColor = Color.FromArgb(80, 90, 110),
            Font      = new Font("Segoe UI", 9f)
        };

        var sep = new Panel
        {
            Location  = new Point(8, 58),
            Size      = new Size(444, 1),
            BackColor = Color.FromArgb(190, 195, 210)
        };

        _rdoNever = new RadioButton
        {
            Text     = "Никогда",
            Location = new Point(12, 70),
            AutoSize = true,
            Checked  = neverExp
        };

        _rdoExpires = new RadioButton
        {
            Text     = "Истекает:",
            Location = new Point(12, 100),
            AutoSize = true,
            Checked  = !neverExp
        };

        // Один пикер с датой и временем: "5 июня 2026 в 18:00"
        _dtp = new DateTimePicker
        {
            Location     = new Point(112, 97),
            Size         = new Size(330, 24),
            Format       = DateTimePickerFormat.Custom,
            CustomFormat = "d MMMM yyyy 'в' HH:mm",
            Value        = expDT ?? DateTime.Today,
            Enabled      = !neverExp
        };

        _rdoNever.CheckedChanged   += (_, _) => _dtp.Enabled = !_rdoNever.Checked;
        _rdoExpires.CheckedChanged += (_, _) => _dtp.Enabled =  _rdoExpires.Checked;

        var btnApply = new Button
        {
            Text      = "Применить",
            Location  = new Point(238, 162),
            Size      = new Size(110, 32),
            BackColor = Color.FromArgb(50, 100, 200),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnApply.FlatAppearance.BorderSize = 0;
        btnApply.Click += (_, _) => OnApply();

        var btnCancel = new Button
        {
            Text         = "Отмена",
            Location     = new Point(358, 162),
            Size         = new Size(90, 32),
            DialogResult = DialogResult.Cancel,
            FlatStyle    = FlatStyle.Flat
        };
        btnCancel.FlatAppearance.BorderSize = 0;

        AcceptButton = btnApply;
        CancelButton = btnCancel;

        Controls.AddRange(new Control[]
        {
            lblName, lblLogin, sep,
            _rdoNever, _rdoExpires, _dtp,
            btnApply, btnCancel
        });
    }

    private void OnApply()
    {
        long   newExpiry;
        string logMsg;

        if (_rdoNever.Checked)
        {
            newExpiry = 0L;
            logMsg    = "Срок действия снят (не ограничен).";
        }
        else
        {
            // Секунды обнуляем — AD хранит с точностью 100 нс, пользователю достаточно минут
            var dt    = new DateTime(_dtp.Value.Year, _dtp.Value.Month, _dtp.Value.Day,
                                     _dtp.Value.Hour, _dtp.Value.Minute, 0, DateTimeKind.Local);
            newExpiry = dt.ToFileTime();
            logMsg    = $"Срок действия установлен: {dt:dd.MM.yyyy HH:mm}.";
        }

        bool ok = LdapHelper.InvokeADOperation(_domain, _dn, entry =>
        {
            LdapHelper.SetLargeInteger(entry, "accountExpires", newExpiry);
            entry.CommitChanges();
        }, _ownerWin);

        if (ok)
        {
            Logger.Write(logMsg, LogType.OK);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
