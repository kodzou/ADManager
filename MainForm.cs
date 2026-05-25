using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;
using ADManager.Tabs;

namespace ADManager;

public partial class MainForm : Form
{
    private readonly TabControl _tabControl;
    private readonly Panel      _logPanel;
    private readonly RichTextBox _logRtb;
    private readonly Label      _hintBar;
    private readonly Button     _btnClearLog;

    private const int LogPanelHeight = 192;
    private const int HintBarHeight  = 20;

    public static readonly string[] Domains =
    {
        "webbankir.local",
        "wb-digital.local",
        "intwave.local",
        "prodavay.local",
        "freedom.local"
    };

    public void SelectTab(int index)
    {
        if (_tabControl.TabPages.Count > index)
            _tabControl.SelectedIndex = index;
    }

    public MainForm()
    {
        // Базовые настройки формы
        Text            = "AD Manager";
        Size            = new Size(1260, 830);
        MinimumSize     = new Size(900, 680);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = Color.FromArgb(240, 242, 245);
        Font            = new Font("Segoe UI", 9.5f);

        // === TabControl ===
        _tabControl = new TabControl
        {
            ItemSize = new Size(140, 24)
        };

        // === Log Panel ===
        _logPanel = new Panel
        {
            BackColor = Color.FromArgb(22, 24, 32)
        };

        var logHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 26,
            BackColor = Color.FromArgb(40, 44, 58)
        };

        var lblLogTitle = new Label
        {
            Text      = "Журнал операций",
            ForeColor = Color.FromArgb(180, 190, 210),
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location  = new Point(8, 4),
            AutoSize  = true
        };

        _btnClearLog = new Button
        {
            Text      = "Очистить",
            Size      = new Size(70, 20),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(180, 190, 210),
            BackColor = Color.FromArgb(60, 65, 80),
            Font      = new Font("Segoe UI", 8f)
        };
        _btnClearLog.FlatAppearance.BorderSize = 0;
        _btnClearLog.Click += (_, _) => _logRtb.Clear();

        logHeader.Controls.AddRange(new Control[] { lblLogTitle, _btnClearLog });

        _logRtb = new RichTextBox
        {
            ReadOnly    = true,
            BackColor   = Color.FromArgb(22, 24, 32),
            ForeColor   = Color.FromArgb(200, 210, 230),
            Font        = new Font("Consolas", 9f),
            Dock        = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            WordWrap    = false,
            ScrollBars  = RichTextBoxScrollBars.Vertical
        };

        _logPanel.Controls.Add(_logRtb);
        _logPanel.Controls.Add(logHeader);

        // === Hint Bar ===
        _hintBar = new Label
        {
            Text      = "Ctrl+C — скопировать выделенные ячейки    Ctrl+A — выделить всё",
            ForeColor = Color.FromArgb(110, 120, 140),
            Font      = new Font("Segoe UI", 8f),
            BackColor = Color.FromArgb(230, 232, 238),
            Height    = HintBarHeight,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(8, 0, 0, 0)
        };

        Controls.AddRange(new Control[] { _tabControl, _logPanel, _hintBar });

        // === Инициализация логгера ===
        Logger.Init(_logRtb);

        // === Добавляем вкладки ===
        _tabControl.TabPages.Add(Tab1_UserSearch.Create());
        _tabControl.TabPages.Add(Tab2_Groups.Create());
        _tabControl.TabPages.Add(Tab3_Passwords.Create());
        _tabControl.TabPages.Add(Tab4_Activity.Create());
        _tabControl.TabPages.Add(Tab5_Duplicates.Create());
        _tabControl.TabPages.Add(Tab6_SearchByList.Create());
        _tabControl.TabPages.Add(Tab7_SearchByOU.Create());

        // === Events ===
        Resize  += (_, _) => UpdateLayout();
        Shown   += OnShown;
    }

    private void UpdateLayout()
    {
        int w     = ClientSize.Width;
        int h     = ClientSize.Height;
        int logH  = LogPanelHeight;
        int hintH = HintBarHeight;

        _hintBar.SetBounds(0, h - hintH, w, hintH);
        _logPanel.SetBounds(0, h - hintH - logH, w, logH);
        _btnClearLog.Location = new Point(_logPanel.Width - 76, 3);
        _tabControl.SetBounds(5, 5, w - 10, h - hintH - logH - 10);
    }

    private void OnShown(object? sender, EventArgs e)
    {
        UpdateLayout();
        Logger.Write($"AD Manager запущен. Домены: {string.Join(", ", Domains)}", LogType.Info);
        Logger.Write("[INFO] Ctrl+C — копировать ячейки, Ctrl+A — выделить всё", LogType.Info);
    }
}