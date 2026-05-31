using System.DirectoryServices;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using ADManager.Helpers;

namespace ADManager.Tabs;

public partial class Tab_CreateUser : UserControl
{
    // ── Left panel ────────────────────────────────────────────
    private ComboBox _cbOuDomain  = null!;
    private TextBox  _txtOuSearch = null!;
    private TreeView _ouTree      = null!;

    // ── Right panel — user data ───────────────────────────────
    private TextBox  _txtFullName    = null!;
    private TextBox  _txtLogin       = null!;
    private TextBox  _txtPassword    = null!;
    private TextBox  _txtEmail       = null!;
    private TextBox  _txtDescription = null!;
    private TextBox  _txtMobile      = null!;
    private TextBox  _txtCity        = null!;
    private TextBox  _txtRegion      = null!;
    private TextBox  _txtPosition    = null!;
    private TextBox  _txtDepartment  = null!;
    private TextBox  _txtOrg         = null!;
    private TextBox  _txtManager     = null!;
    private TextBox  _txtSelOU       = null!;
    private string   _managerDN      = "";

    // ── Template checkboxes ───────────────────────────────────
    private CheckBox _chkTplOU   = null!;
    private CheckBox _chkTplCity = null!;
    private CheckBox _chkTplReg  = null!;
    private CheckBox _chkTplPos  = null!;
    private CheckBox _chkTplDept = null!;
    private CheckBox _chkTplOrg  = null!;
    private CheckBox _chkTplMgr  = null!;

    // ── Template zone ─────────────────────────────────────────
    private ComboBox _cbTemplates     = null!;
    private TextBox  _txtTemplateName = null!;

    // ── State ─────────────────────────────────────────────────
    private string _selectedOUDN     = "";
    private string _selectedOUDomain = "";
    private List<UserTemplate> _templates = new();

    public Tab_BulkOperations? TabBulk { get; set; }

    private static readonly string TemplatesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ADManager", "create_user_templates.json");

    private static readonly Dictionary<string, string> _orgMap = new()
    {
        ["webbankir.local"]  = "ООО МФК \"ВЭББАНКИР\"",
        ["wb-digital.local"] = "ООО \"ВБ-ДИДЖИТАЛ\"",
        ["intwave.local"]    = "ООО \"ИНТЕЛЛЕКТ УЭЙВ\"",
        ["prodavay.local"]   = "ООО \"ПРОДАВАЙ\"",
        ["freedom.local"]    = "ООО МКК \"ВЭББАНКИР\""
    };

    private static readonly Dictionary<char, string> _translitMap = new()
    {
        ['а']="a",  ['б']="b",  ['в']="v",  ['г']="g",  ['д']="d",
        ['е']="e",  ['ё']="e",  ['ж']="zh", ['з']="z",  ['и']="i",
        ['й']="i",  ['к']="k",  ['л']="l",  ['м']="m",  ['н']="n",
        ['о']="o",  ['п']="p",  ['р']="r",  ['с']="s",  ['т']="t",
        ['у']="u",  ['ф']="f",  ['х']="kh", ['ц']="ts", ['ч']="ch",
        ['ш']="sh", ['щ']="shch",['ъ']="ie",['ы']="y",  ['ь']="",
        ['э']="e",  ['ю']="iu", ['я']="ia"
    };

    public Tab_CreateUser()
    {
        InitializeComponent();
        BuildUI();
        WireEvents();
        LoadTemplates();
    }

    // ═══════════════════════════════════════════════════════════
    // UI Building
    // ═══════════════════════════════════════════════════════════

    private void BuildUI()
    {
        BackColor = Color.FromArgb(240, 242, 245);

        var leftPanel = new Panel
        {
            Dock      = DockStyle.Left,
            Width     = 282,
            BackColor = Color.FromArgb(240, 242, 245),
            Padding   = new Padding(4)
        };

        var rightPanel = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(240, 242, 245)
        };

        var splitter = new Splitter
        {
            Dock  = DockStyle.Left,
            Width = 4,
            BackColor = Color.FromArgb(200, 205, 215)
        };

        BuildLeftPanel(leftPanel);
        BuildRightPanel(rightPanel);

        Controls.Add(rightPanel);
        Controls.Add(splitter);
        Controls.Add(leftPanel);
    }

    private void BuildLeftPanel(Panel p)
    {
        var lblDomain = new Label
        {
            Text      = "Домен:",
            Location  = new Point(4, 6),
            AutoSize  = true,
            ForeColor = Color.FromArgb(90, 100, 120),
            Font      = new Font("Segoe UI", 8.5f)
        };

        _cbOuDomain = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location      = new Point(4, 24),
            Width         = p.Width - 12,
            Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font          = new Font("Segoe UI", 9f)
        };
        foreach (var d in MainForm.Domains) _cbOuDomain.Items.Add(d);

        var lblSearch = new Label
        {
            Text      = "Поиск OU:",
            Location  = new Point(4, 56),
            AutoSize  = true,
            ForeColor = Color.FromArgb(90, 100, 120),
            Font      = new Font("Segoe UI", 8.5f)
        };

        _txtOuSearch = new TextBox
        {
            Location        = new Point(4, 74),
            Width           = p.Width - 12,
            PlaceholderText = "Название OU, Enter — найти",
            Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font            = new Font("Segoe UI", 9f)
        };

        _ouTree = new TreeView
        {
            Location      = new Point(4, 102),
            Size          = new Size(p.Width - 12, p.Height - 106),
            Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            HideSelection = false,
            Scrollable    = true,
            BorderStyle   = BorderStyle.FixedSingle,
            ShowLines     = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            Font          = new Font("Segoe UI", 9f)
        };

        p.Controls.AddRange(new Control[] { lblDomain, _cbOuDomain, lblSearch, _txtOuSearch, _ouTree });
    }

    private void BuildRightPanel(Panel p)
    {
        var tplPanel = BuildTemplatePanel();
        tplPanel.Dock = DockStyle.Top;

        var dataPanel = new Panel { Dock = DockStyle.Fill };
        BuildDataFields(dataPanel);

        // tplPanel added last → processed first by WinForms docking (docks to top)
        p.Controls.Add(dataPanel);
        p.Controls.Add(tplPanel);
    }

    private void BuildDataFields(Panel p)
    {
        // All fields are the same width — ~35 visible chars with Segoe UI 9f
        const int LX   = 8;    // label left x
        const int LW   = 130;  // label width
        const int FX   = 143;  // field start x
        const int FH   = 24;   // field height
        const int RH   = 30;   // row height
        const int FW   = 255;  // unified field width (~35 chars)
        // Layout rule: [field] → [checkbox @ CHKX] → [button @ BTNX]
        // Buttons always at BTNX regardless of whether a checkbox is present
        const int CHKX = FX + FW + 8;         // 406
        const int BTNX = CHKX + 22 + 6;       // 434

        // ── Кнопка "Создать пользователя" и подсказка — выровнены с полем поиска OU (y=74 слева) ──
        var btnCreate = new Button
        {
            Text      = "Создать пользователя",
            Location  = new Point(8, 0),
            Size      = new Size(200, 28),
            BackColor = Color.FromArgb(50, 100, 200),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        btnCreate.FlatAppearance.BorderSize = 0;
        btnCreate.Click += (_, _) => OnCreateUser();

        var lblChkHint = new Label
        {
            Text      = "☑  — добавить в шаблон",
            Location  = new Point(CHKX, 7),
            AutoSize  = true,
            ForeColor = Color.FromArgb(90, 100, 120),
            Font      = new Font("Segoe UI", 8.5f)
        };

        p.Controls.AddRange(new Control[] { btnCreate, lblChkHint });

        int y = 36;

        Label MkLabel(string text) => new Label
        {
            Text      = text,
            ForeColor = Color.FromArgb(90, 100, 120),
            Font      = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleRight,
            Size      = new Size(LW, FH),
            Location  = new Point(LX, y + 1)
        };

        TextBox MkField(string placeholder = "", bool readOnly = false) => new TextBox
        {
            Location        = new Point(FX, y),
            Size            = new Size(FW, FH),
            PlaceholderText = placeholder,
            ReadOnly        = readOnly,
            BackColor       = readOnly ? Color.FromArgb(235, 237, 242) : Color.White,
            Font            = new Font("Segoe UI", 9f)
        };

        CheckBox MkChk() => new CheckBox
        {
            Location = new Point(CHKX, y + 3),
            Size     = new Size(22, 22),
            Checked  = true
        };

        Button MkBtn(string text, int width)
        {
            var b = new Button
            {
                Text      = text,
                Location  = new Point(BTNX, y),
                Size      = new Size(width, FH),
                BackColor = Color.FromArgb(50, 55, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        // ── Выбранное OU  [field][chk] ───────────────────────
        p.Controls.Add(MkLabel("Выбранное OU:"));
        _txtSelOU = MkField("OU не выбрано", true);
        _chkTplOU = MkChk();
        p.Controls.AddRange(new Control[] { _txtSelOU, _chkTplOU });
        y += RH;

        // ── Фамилия и Имя  [field][   ][btn] ─────────────────
        p.Controls.Add(MkLabel("Фамилия и Имя:"));
        _txtFullName = MkField("Введите Фамилию и Имя");
        var btnGenAll = MkBtn("Создать логин и пароль", 180);
        p.Controls.AddRange(new Control[] { _txtFullName, btnGenAll });
        y += RH;

        // ── Логин  [field] ────────────────────────────────────
        p.Controls.Add(MkLabel("Логин:"));
        _txtLogin = MkField();
        p.Controls.Add(_txtLogin);
        y += RH;

        // ── Пароль  [field][   ][btn] ─────────────────────────
        p.Controls.Add(MkLabel("Пароль:"));
        _txtPassword = MkField();
        var btnRegen = MkBtn("Перегенерировать", 180);
        p.Controls.AddRange(new Control[] { _txtPassword, btnRegen });
        y += RH;

        // ── E-mail  [field] ───────────────────────────────────
        p.Controls.Add(MkLabel("E-mail:"));
        _txtEmail = MkField();
        p.Controls.Add(_txtEmail);
        y += RH;

        // ── Описание  [field] ─────────────────────────────────
        p.Controls.Add(MkLabel("Описание:"));
        _txtDescription = MkField();
        p.Controls.Add(_txtDescription);
        y += RH;

        // ── Мобильный  [field] ────────────────────────────────
        p.Controls.Add(MkLabel("Мобильный:"));
        _txtMobile = MkField();
        p.Controls.Add(_txtMobile);
        y += RH;

        // ── Город  [field][chk] ───────────────────────────────
        p.Controls.Add(MkLabel("Город:"));
        _txtCity    = MkField();
        _chkTplCity = MkChk();
        p.Controls.AddRange(new Control[] { _txtCity, _chkTplCity });
        y += RH;

        // ── Область/Край  [field][chk] ────────────────────────
        p.Controls.Add(MkLabel("Область/Край:"));
        _txtRegion = MkField();
        _chkTplReg = MkChk();
        p.Controls.AddRange(new Control[] { _txtRegion, _chkTplReg });
        y += RH;

        // ── Должность  [field][chk] ───────────────────────────
        p.Controls.Add(MkLabel("Должность:"));
        _txtPosition = MkField();
        _chkTplPos   = MkChk();
        p.Controls.AddRange(new Control[] { _txtPosition, _chkTplPos });
        y += RH;

        // ── Отдел  [field][chk] ───────────────────────────────
        p.Controls.Add(MkLabel("Отдел:"));
        _txtDepartment = MkField();
        _chkTplDept    = MkChk();
        p.Controls.AddRange(new Control[] { _txtDepartment, _chkTplDept });
        y += RH;

        // ── Организация  [field][chk] ─────────────────────────
        p.Controls.Add(MkLabel("Организация:"));
        _txtOrg    = MkField(readOnly: true);
        _chkTplOrg = MkChk();
        p.Controls.AddRange(new Control[] { _txtOrg, _chkTplOrg });
        y += RH;

        // ── Руководитель  [field][chk][btn] ──────────────────
        p.Controls.Add(MkLabel("Руководитель:"));
        _txtManager  = MkField(readOnly: true);
        _chkTplMgr   = MkChk();
        var btnFindMgr = MkBtn("Найти", 88);
        p.Controls.AddRange(new Control[] { _txtManager, _chkTplMgr, btnFindMgr });
        y += RH;

        _ = y;

        btnGenAll.Click  += (_, _) => GenerateLoginAndPassword();
        btnRegen.Click   += (_, _) => { _txtPassword.Text = PasswordGenerator.GenerateMemorable(12); };
        btnFindMgr.Click += (_, _) => FindManager();
    }

    private Panel BuildTemplatePanel()
    {
        // Зона выбора шаблона — выровнена по высоте с дропдауном домена слева (y=24)
        const int CW  = 200;
        const int BH  = 26;
        const int TH  = 24;
        const int GAP = 6;
        const int ROW_Y = 24;

        var p = new Panel
        {
            Height    = 74,
            BackColor = Color.FromArgb(228, 230, 236)
        };

        // ── [combo(CW)] [delete] [name field] [save]  (y=24) ──
        int x = 8;

        _cbTemplates = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location      = new Point(x, ROW_Y),
            Size          = new Size(CW, BH),
            Font          = new Font("Segoe UI", 9f)
        };
        x += CW + GAP;

        var btnDel = MakeDarkBtn("Удалить", new Point(x, ROW_Y), new Size(90, BH));
        x += 90 + GAP * 3;

        _txtTemplateName = new TextBox
        {
            Location        = new Point(x, ROW_Y + 1),
            Size            = new Size(280, TH),
            PlaceholderText = "Название шаблона",
            Font            = new Font("Segoe UI", 9f)
        };
        x += 280 + GAP;

        var btnSave = MakeDarkBtn("Создать", new Point(x, ROW_Y), new Size(90, BH));

        p.Controls.AddRange(new Control[]
            { _cbTemplates, btnDel, _txtTemplateName, btnSave });

        btnSave.Click += (_, _) => OnSaveTemplate();
        btnDel.Click  += (_, _) => OnDeleteTemplate();

        return p;
    }

    private static Button MakeDarkBtn(string text, Point loc, Size size)
    {
        var b = new Button { Text=text, Location=loc, Size=size,
            BackColor=Color.FromArgb(50,55,70), ForeColor=Color.White,
            FlatStyle=FlatStyle.Flat, Font=new Font("Segoe UI",8.5f,FontStyle.Bold) };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private static Button MakeGreenBtn(string text, Point loc, Size size)
    {
        var b = new Button { Text=text, Location=loc, Size=size,
            BackColor=Color.FromArgb(34,139,75), ForeColor=Color.White,
            FlatStyle=FlatStyle.Flat, Font=new Font("Segoe UI",8.5f,FontStyle.Bold) };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private static Button MakeRedBtn(string text, Point loc, Size size)
    {
        var b = new Button { Text=text, Location=loc, Size=size,
            BackColor=Color.FromArgb(180,50,50), ForeColor=Color.White,
            FlatStyle=FlatStyle.Flat, Font=new Font("Segoe UI",8.5f,FontStyle.Bold) };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    // ═══════════════════════════════════════════════════════════
    // Event wiring
    // ═══════════════════════════════════════════════════════════

    private void WireEvents()
    {
        _cbOuDomain.SelectedIndexChanged += (_, _) =>
        {
            string domain = _cbOuDomain.SelectedItem?.ToString() ?? "";
            LoadOuTree(domain);
            _txtOrg.Text = _orgMap.TryGetValue(domain, out string? org) ? org : "";
        };

        _txtOuSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            SearchOuInTree(_txtOuSearch.Text.Trim());
        };

        _ouTree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is string dn)
            {
                _selectedOUDN     = dn;
                _selectedOUDomain = _cbOuDomain.SelectedItem?.ToString() ?? "";
                _txtSelOU.Text    = FormatOuPath(dn);
            }
        };

        // Контекстное меню OU-дерева
        var ouCtxMenu    = new ContextMenuStrip();
        var menuOUToBulk = new ToolStripMenuItem("➕  Добавить в Массовые операции");
        ouCtxMenu.Items.Add(menuOUToBulk);
        _ouTree.ContextMenuStrip = ouCtxMenu;

        ouCtxMenu.Opening += (_, _) =>
            menuOUToBulk.Enabled = _ouTree.SelectedNode?.Tag is string s && s.Length > 0;

        _ouTree.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                var node = _ouTree.HitTest(e.X, e.Y).Node;
                if (node != null) _ouTree.SelectedNode = node;
            }
        };

        menuOUToBulk.Click += (_, _) =>
        {
            if (_ouTree.SelectedNode?.Tag is not string dn || string.IsNullOrEmpty(dn)) return;
            string domain      = _cbOuDomain.SelectedItem?.ToString() ?? "";
            string displayPath = FormatOuPath(dn);
            TabBulk?.SetTargetOU(dn, domain, displayPath);
            Logger.Write($"OU «{displayPath}» добавлено в Массовые операции.", LogType.Info);
        };

        _txtLogin.TextChanged += (_, _) => UpdateEmail();

        _cbTemplates.SelectedIndexChanged += (_, _) =>
        {
            if (_cbTemplates.SelectedIndex > 0)
                OnApplyTemplate();
        };

        // Trigger initial load
        _cbOuDomain.SelectedIndex = 0;
    }

    // ═══════════════════════════════════════════════════════════
    // OU Tree
    // ═══════════════════════════════════════════════════════════

    private void LoadOuTree(string domain)
    {
        _ouTree.Nodes.Clear();
        _ouTree.Nodes.Add(new TreeNode("Загрузка...") { ForeColor = Color.Gray });

        Task.Run(() =>
        {
            try
            {
                var searcher = LdapHelper.CreateSearcher(
                    domain,
                    "(objectClass=organizationalUnit)",
                    new[] { "name", "distinguishedName" });

                if (searcher == null) return;
                searcher.SearchScope = SearchScope.Subtree;

                var ous = new List<(string Name, string DN)>();
                foreach (SearchResult r in searcher.FindAll())
                {
                    string name = LdapHelper.GetProp(r, "name");
                    string dn   = LdapHelper.GetProp(r, "distinguishedName");
                    if (!string.IsNullOrEmpty(dn)) ous.Add((name, dn));
                }

                ous.Sort((a, b) => a.DN.Length.CompareTo(b.DN.Length));

                var nodeMap   = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
                var rootNodes = new List<TreeNode>();
                string domainDC = DomainToDC(domain);

                foreach (var (name, dn) in ous)
                {
                    var node = new TreeNode(name) { Tag = dn };
                    nodeMap[dn] = node;
                    string parentDN = GetParentDN(dn);

                    if (string.Equals(parentDN, domainDC, StringComparison.OrdinalIgnoreCase))
                        rootNodes.Add(node);
                    else if (nodeMap.TryGetValue(parentDN, out var parent))
                        parent.Nodes.Add(node);
                    else
                        rootNodes.Add(node);
                }

                _ouTree.Invoke(() =>
                {
                    _ouTree.BeginUpdate();
                    _ouTree.Nodes.Clear();
                    foreach (var n in rootNodes) _ouTree.Nodes.Add(n);
                    _ouTree.EndUpdate();
                    Logger.Write($"Загружено {ous.Count} OU из {domain}.", LogType.Info);
                });
            }
            catch (Exception ex)
            {
                _ouTree.Invoke(() =>
                {
                    _ouTree.Nodes.Clear();
                    _ouTree.Nodes.Add(new TreeNode("Ошибка загрузки") { ForeColor = Color.Red });
                });
                Logger.Write($"Ошибка загрузки OU {domain}: {ex.Message}", LogType.Error);
            }
        });
    }

    private void SearchOuInTree(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        query = query.ToLowerInvariant();

        _ouTree.BeginUpdate();
        _ouTree.CollapseAll();
        TreeNode? first = null;

        void Walk(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                Walk(node.Nodes);
                if (node.Text.ToLowerInvariant().Contains(query))
                {
                    ExpandToNode(node);
                    first ??= node;
                }
            }
        }
        Walk(_ouTree.Nodes);
        _ouTree.EndUpdate();

        if (first != null)
        {
            _ouTree.SelectedNode = first;
            first.EnsureVisible();
            Logger.Write($"OU найдено: {first.Text}", LogType.Info);
        }
        else
        {
            Logger.Write($"OU '{query}' не найдено в {_cbOuDomain.SelectedItem}.", LogType.Warning);
        }
    }

    private static void ExpandToNode(TreeNode node)
    {
        var p = node.Parent;
        while (p != null) { p.Expand(); p = p.Parent; }
    }

    // ═══════════════════════════════════════════════════════════
    // Login / Password / Email generation
    // ═══════════════════════════════════════════════════════════

    private void GenerateLoginAndPassword()
    {
        string fullName = _txtFullName.Text.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        { Logger.Write("Введите Фамилию и Имя.", LogType.Warning); return; }

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        { Logger.Write("Введите Фамилию и Имя через пробел.", LogType.Warning); return; }

        string surname   = parts[0];
        string firstName = parts[1];
        string domain    = !string.IsNullOrEmpty(_selectedOUDomain)
                           ? _selectedOUDomain
                           : _cbOuDomain.SelectedItem?.ToString() ?? "webbankir.local";

        string tSurname   = Transliterate(surname);
        string tFirstName = Transliterate(firstName);

        _txtLogin.Text    = FindUniqueLogin(domain, tFirstName, tSurname);
        _txtPassword.Text = PasswordGenerator.GenerateMemorable(12);
        UpdateEmail();
    }

    private string FindUniqueLogin(string domain, string firstName, string surname)
    {
        for (int len = 1; len <= firstName.Length; len++)
        {
            string candidate = (firstName[..len] + "." + surname).ToLowerInvariant();
            if (!CheckLoginExists(domain, candidate))
                return candidate;
            Logger.Write($"Логин '{candidate}' занят, пробуем следующий вариант...", LogType.Warning);
        }
        string fallback = (surname + DateTime.Now.ToString("MMdd")).ToLowerInvariant();
        Logger.Write($"Все варианты заняты, используется: {fallback}", LogType.Warning);
        return fallback;
    }

    private static bool CheckLoginExists(string domain, string login)
    {
        try
        {
            var s = LdapHelper.CreateSearcher(domain,
                $"(&(objectClass=user)(sAMAccountName={login}))",
                new[] { "sAMAccountName" });
            return s?.FindOne() != null;
        }
        catch { return false; }
    }

    private void UpdateEmail()
    {
        string login = _txtLogin.Text.Trim();
        if (string.IsNullOrEmpty(login)) { _txtEmail.Text = ""; return; }
        string domain      = !string.IsNullOrEmpty(_selectedOUDomain)
                             ? _selectedOUDomain
                             : _cbOuDomain.SelectedItem?.ToString() ?? "webbankir.local";
        string emailDomain = domain.Replace(".local", ".ru");
        _txtEmail.Text     = $"{login}@{emailDomain}";
    }

    // ═══════════════════════════════════════════════════════════
    // Manager search
    // ═══════════════════════════════════════════════════════════

    private void FindManager()
    {
        using var dlg = new Dialogs.FindManagerDialog();
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _managerDN       = dlg.SelectedDN;
            _txtManager.Text = dlg.SelectedDisplay;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Templates
    // ═══════════════════════════════════════════════════════════

    private void LoadTemplates()
    {
        try
        {
            if (File.Exists(TemplatesPath))
            {
                string json = File.ReadAllText(TemplatesPath);
                _templates  = JsonSerializer.Deserialize<List<UserTemplate>>(json) ?? new();
            }
        }
        catch { _templates = new(); }
        RefreshTemplatesCombo();
    }

    private void SaveTemplatesFile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TemplatesPath)!);
            File.WriteAllText(TemplatesPath,
                JsonSerializer.Serialize(_templates, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Logger.Write($"Ошибка сохранения шаблонов: {ex.Message}", LogType.Error); }
    }

    private void RefreshTemplatesCombo()
    {
        _cbTemplates.Items.Clear();
        _cbTemplates.Items.Add("— Выберите шаблон —");
        foreach (var t in _templates) _cbTemplates.Items.Add(t.Name);
        _cbTemplates.SelectedIndex = 0;
    }

    private void OnSaveTemplate()
    {
        string name = _txtTemplateName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        { Logger.Write("Введите название шаблона.", LogType.Warning); return; }

        var tpl = new UserTemplate
        {
            Name           = name,
            OUDomain       = _chkTplOU.Checked   ? _selectedOUDomain  : "",
            OUDN           = _chkTplOU.Checked   ? _selectedOUDN      : "",
            OUDisplay      = _chkTplOU.Checked   ? _txtSelOU.Text     : "",
            City           = _chkTplCity.Checked ? _txtCity.Text      : "",
            Region         = _chkTplReg.Checked  ? _txtRegion.Text    : "",
            Position       = _chkTplPos.Checked  ? _txtPosition.Text  : "",
            Department     = _chkTplDept.Checked ? _txtDepartment.Text: "",
            Organization   = _chkTplOrg.Checked  ? _txtOrg.Text       : "",
            ManagerDN      = _chkTplMgr.Checked  ? _managerDN         : "",
            ManagerDisplay = _chkTplMgr.Checked  ? _txtManager.Text   : ""
        };

        var existing = _templates.FirstOrDefault(t => t.Name == name);
        if (existing != null) _templates.Remove(existing);
        _templates.Add(tpl);

        SaveTemplatesFile();
        RefreshTemplatesCombo();
        Logger.Write($"Шаблон «{name}» сохранён.", LogType.OK);
    }

    private void OnDeleteTemplate()
    {
        int idx = _cbTemplates.SelectedIndex;
        if (idx <= 0) { Logger.Write("Выберите шаблон для удаления.", LogType.Warning); return; }

        string name = _templates[idx - 1].Name;
        if (MessageBox.Show($"Удалить шаблон «{name}»?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        _templates.RemoveAt(idx - 1);
        SaveTemplatesFile();
        RefreshTemplatesCombo();
        Logger.Write($"Шаблон «{name}» удалён.", LogType.OK);
    }

    private void OnApplyTemplate()
    {
        int idx = _cbTemplates.SelectedIndex;
        if (idx <= 0) return;
        var tpl = _templates[idx - 1];

        // Collect fields that would overwrite existing data
        var affected = new List<string>();
        if (!string.IsNullOrEmpty(tpl.OUDomain) && !string.IsNullOrEmpty(_selectedOUDN))
            affected.Add("Выбранное OU");
        if (!string.IsNullOrEmpty(tpl.City)      && !string.IsNullOrEmpty(_txtCity.Text))
            affected.Add("Город");
        if (!string.IsNullOrEmpty(tpl.Region)    && !string.IsNullOrEmpty(_txtRegion.Text))
            affected.Add("Область/Край");
        if (!string.IsNullOrEmpty(tpl.Position)  && !string.IsNullOrEmpty(_txtPosition.Text))
            affected.Add("Должность");
        if (!string.IsNullOrEmpty(tpl.Department)&& !string.IsNullOrEmpty(_txtDepartment.Text))
            affected.Add("Отдел");
        if (!string.IsNullOrEmpty(tpl.Organization) && !string.IsNullOrEmpty(_txtOrg.Text))
            affected.Add("Организация");
        if (!string.IsNullOrEmpty(tpl.ManagerDN) && !string.IsNullOrEmpty(_managerDN))
            affected.Add("Руководитель");

        if (affected.Count > 0)
        {
            string msg = $"Применение шаблона «{tpl.Name}» заменит данные в полях:\n" +
                         string.Join("\n", affected.Select(a => $"  • {a}")) +
                         "\n\nПродолжить?";
            if (MessageBox.Show(msg, "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                _cbTemplates.SelectedIndex = 0;
                return;
            }
        }

        if (!string.IsNullOrEmpty(tpl.OUDomain))
        {
            _selectedOUDomain = tpl.OUDomain;
            _selectedOUDN     = tpl.OUDN;
            _txtSelOU.Text    = tpl.OUDisplay;
            int di = _cbOuDomain.Items.IndexOf(tpl.OUDomain);
            if (di >= 0) _cbOuDomain.SelectedIndex = di;
        }
        if (!string.IsNullOrEmpty(tpl.City))         _txtCity.Text       = tpl.City;
        if (!string.IsNullOrEmpty(tpl.Region))       _txtRegion.Text     = tpl.Region;
        if (!string.IsNullOrEmpty(tpl.Position))     _txtPosition.Text   = tpl.Position;
        if (!string.IsNullOrEmpty(tpl.Department))   _txtDepartment.Text = tpl.Department;
        if (!string.IsNullOrEmpty(tpl.Organization)) _txtOrg.Text        = tpl.Organization;
        if (!string.IsNullOrEmpty(tpl.ManagerDN))
        {
            _managerDN       = tpl.ManagerDN;
            _txtManager.Text = tpl.ManagerDisplay;
        }

        Logger.Write($"Шаблон «{tpl.Name}» применён.", LogType.OK);
    }

    // ═══════════════════════════════════════════════════════════
    // Create user in AD
    // ═══════════════════════════════════════════════════════════

    private void OnCreateUser()
    {
        if (string.IsNullOrWhiteSpace(_txtFullName.Text))
        { Logger.Write("Введите Фамилию и Имя.", LogType.Warning); return; }
        if (string.IsNullOrWhiteSpace(_txtLogin.Text))
        { Logger.Write("Логин пуст. Нажмите «Создать логин и пароль».", LogType.Warning); return; }
        if (string.IsNullOrWhiteSpace(_txtPassword.Text))
        { Logger.Write("Пароль не может быть пустым.", LogType.Warning); return; }
        if (string.IsNullOrEmpty(_selectedOUDN))
        { Logger.Write("Выберите OU в дереве слева.", LogType.Warning); return; }

        string domain   = _selectedOUDomain;
        string ouDN     = _selectedOUDN;
        string login    = _txtLogin.Text.Trim();
        string password = _txtPassword.Text;

        var parts       = _txtFullName.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string surname   = parts.Length > 0 ? parts[0] : "";
        string givenName = parts.Length > 1 ? parts[1] : "";
        string displayName = $"{surname} {givenName}".Trim();

        Logger.Write($"Создание пользователя {login} в {domain} / {FormatOuPath(ouDN)}...", LogType.Info);

        bool ok = LdapHelper.InvokeADOperation(domain, ouDN, entry =>
        {
            var user = entry.Children.Add($"CN={displayName}", "user");
            user.Properties["sAMAccountName"].Value    = login;
            user.Properties["userPrincipalName"].Value = $"{login}@{domain}";
            user.Properties["displayName"].Value       = displayName;
            user.Properties["givenName"].Value         = givenName;
            user.Properties["sn"].Value                = surname;

            if (!string.IsNullOrEmpty(_txtEmail.Text))
                user.Properties["mail"].Value = _txtEmail.Text;
            if (!string.IsNullOrEmpty(_txtDescription.Text))
                user.Properties["description"].Value = _txtDescription.Text;
            if (!string.IsNullOrEmpty(_txtMobile.Text))
                user.Properties["mobile"].Value = _txtMobile.Text;
            if (!string.IsNullOrEmpty(_txtCity.Text))
                user.Properties["l"].Value = _txtCity.Text;
            if (!string.IsNullOrEmpty(_txtRegion.Text))
                user.Properties["st"].Value = _txtRegion.Text;
            if (!string.IsNullOrEmpty(_txtPosition.Text))
                user.Properties["title"].Value = _txtPosition.Text;
            if (!string.IsNullOrEmpty(_txtDepartment.Text))
                user.Properties["department"].Value = _txtDepartment.Text;
            if (!string.IsNullOrEmpty(_txtOrg.Text))
                user.Properties["company"].Value = _txtOrg.Text;
            if (!string.IsNullOrEmpty(_managerDN))
                user.Properties["manager"].Value = _managerDN;

            user.CommitChanges();
            user.Invoke("SetPassword", new object[] { password });
            user.Properties["userAccountControl"].Value = 0x200; // NORMAL_ACCOUNT
            user.Properties["pwdLastSet"].Value = -1;
            user.CommitChanges();
        }, this);

        if (ok)
            Logger.Write($"Пользователь {displayName} ({login}) успешно создан в {domain}.", LogType.OK);
    }

    // ═══════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════

    private static string Transliterate(string text)
    {
        var sb = new StringBuilder();
        foreach (char c in text.ToLowerInvariant())
        {
            if (_translitMap.TryGetValue(c, out string? m))
                sb.Append(m);
            else if (char.IsAsciiLetter(c) || c == '-')
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static string DomainToDC(string domain) =>
        string.Join(",", domain.Split('.').Select(p => $"DC={p}"));

    private static string GetParentDN(string dn)
    {
        int idx = dn.IndexOf(',');
        return idx < 0 ? "" : dn[(idx + 1)..];
    }

    private static string FormatOuPath(string dn) =>
        string.Join(" > ",
            dn.Split(',')
              .Where(p => p.TrimStart().StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
              .Reverse()
              .Select(p => p.TrimStart()[3..]));

    // ═══════════════════════════════════════════════════════════
    // Template model
    // ═══════════════════════════════════════════════════════════

    private class UserTemplate
    {
        public string Name           { get; set; } = "";
        public string OUDomain       { get; set; } = "";
        public string OUDN           { get; set; } = "";
        public string OUDisplay      { get; set; } = "";
        public string City           { get; set; } = "";
        public string Region         { get; set; } = "";
        public string Position       { get; set; } = "";
        public string Department     { get; set; } = "";
        public string Organization   { get; set; } = "";
        public string ManagerDN      { get; set; } = "";
        public string ManagerDisplay { get; set; } = "";
    }
}
