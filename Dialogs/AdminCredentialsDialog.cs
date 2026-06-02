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
        string netbios = domain.Split('.')[0];
        Text = $"Требуются права администратора — {domain}";
        _txtUser.PlaceholderText = $@"{netbios}\username";

        _btnOk.Click += (_, _) =>
        {
            string user = _txtUser.Text.Trim();
            // Добавляем домен если пользователь ввёл просто логин без префикса
            string fullUser = (user.Contains('\\') || user.Contains('@'))
                ? user
                : $@"{netbios}\{user}";
            Credential = new NetworkCredential(fullUser, _txtPwd.Text);
        };
    }
}