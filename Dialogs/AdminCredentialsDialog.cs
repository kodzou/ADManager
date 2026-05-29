using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace ADManager.Dialogs;

public partial class AdminCredentialsDialog : Form
{
    public NetworkCredential? Credential { get; private set; }

    private TextBox _txtUser = null!;
    private TextBox _txtPwd  = null!;

    public AdminCredentialsDialog() { InitializeComponent(); }

    public AdminCredentialsDialog(string domain) : this()
    {
        Text = $"Требуются права администратора — {domain}";
        _btnOk.Click += (_, _) => Credential = new NetworkCredential(_txtUser.Text, _txtPwd.Text);
    }
}