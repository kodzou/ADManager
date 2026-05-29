using System.Drawing;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager;

public partial class MainForm : Form
{
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
        InitializeComponent();
        _tab1.Tab2 = _tab2;
        _btnClearLog.Click += (_, _) => _logRtb.Clear();
        Logger.Init(_logRtb);
        Shown += OnShown;
    }

    private void OnShown(object? sender, EventArgs e)
    {
        Logger.Write($"AD Manager запущен. Домены: {string.Join(", ", Domains)}", LogType.Info);
        Logger.Write("[INFO] Ctrl+C — копировать ячейки, Ctrl+A — выделить всё", LogType.Info);
    }
}