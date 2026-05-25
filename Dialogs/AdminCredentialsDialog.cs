using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace ADManager.Dialogs;

public class AdminCredentialsDialog : Form
{
    public NetworkCredential? Credential { get; private set; }

    private readonly TextBox _txtUser;
    private readonly TextBox _txtPwd;

    public AdminCredentialsDialog(string domain)
    {
        Text            = $"Требуются права администратора — {domain}";
        Size            = new Size(360, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = Color.FromArgb(240, 242, 245);

        var lblUser = new Label
        {
            Text     = "Логин (domain\\user или UPN):",
            Location = new Point(10, 15),
            Size     = new Size(330, 18)
        };

        _txtUser = new TextBox
        {
            Location = new Point(10, 35),
            Size     = new Size(330, 22)
        };

        var lblPwd = new Label
        {
            Text     = "Пароль:",
            Location = new Point(10, 65),
            Size     = new Size(330, 18)
        };

        _txtPwd = new TextBox
        {
            Location             = new Point(10, 85),
            Size                 = new Size(330, 22),
            UseSystemPasswordChar = true
        };

        var btnOk = new Button
        {
            Text         = "OK",
            Location     = new Point(170, 120),
            Size         = new Size(80, 28),
            DialogResult = DialogResult.OK,
            BackColor    = Color.FromArgb(50, 55, 70),
            ForeColor    = Color.White,
            FlatStyle    = FlatStyle.Flat
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) =>
        {
            Credential = new NetworkCredential(_txtUser.Text, _txtPwd.Text);
        };

        var btnCancel = new Button
        {
            Text         = "Отмена",
            Location     = new Point(260, 120),
            Size         = new Size(80, 28),
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange(new Control[] { lblUser, _txtUser, lblPwd, _txtPwd, btnOk, btnCancel });
    }
}