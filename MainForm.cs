using Microsoft.Win32;

namespace GitHubUploader;

public sealed class MainForm : Form
{
    private readonly TextBox txtSourceInput = new() { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right }; // v1.9: 원본 입력칸
    private readonly Button btnPickFile = new() { Text = "파일", AutoSize = true }; // v1.9: 파일 선택
    private readonly RichTextBox rtSourcePath = new() { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ReadOnly = true, Multiline = false, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Horizontal }; // v1.9: 경로 표시 전용
    private bool suppressSrcInput;
    private readonly Button btnPickFolder = new() { Text = "폴더", AutoSize = true };
    private readonly TextBox txtToken = new() { UseSystemPasswordChar = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "ghp_... 또는 브라우저 로그인 사용" };
    private readonly Button btnLoadRepos = new() { Text = "레포 불러오기", AutoSize = true };
    private readonly Button btnBrowserLogin = new() { Text = "브라우저로 로그인", AutoSize = true };
    private readonly Label lblAccount = new() { Text = "미로그인", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private readonly TextBox txtSearch = new() { PlaceholderText = "레포 검색...", Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
    private readonly ListBox lstRepos = new() { Dock = DockStyle.Fill };
    private readonly TreeView treeTarget = new() { Dock = DockStyle.Fill, PathSeparator = "/", HideSelection = false }; // v1.7: 포커스 이동해도 선택 유지
    private readonly TextBox txtCommit = new() { Text = "upload via right-click", Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button btnUpload = new() { Text = "선택한 폴더에 업로드", AutoSize = true };
    private readonly Button btnRefreshFolders = new() { Text = "폴더 새로고침", AutoSize = true };
    private readonly Label lblBranch = new() { AutoSize = true }; // v1.6: 실제 default branch 표시
    private readonly Label lblTargetPath = new() { AutoSize = true }; // v1.7: 선택 중인 대상 경로
    private readonly CheckBox chkFilesOnly = new() { Text = "폴더 속 파일만", AutoSize = true, Enabled = false }; // v1.8: 체크 시 폴더 안 내용물만 업로드
    private readonly CheckBox chkContextMenu = new() { Text = "탐색기 우클릭 메뉴 등록 (GitHub에 업로드)", AutoSize = true };
    private bool suppressCtxCheck;
    private readonly TextBox txtLog = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.Lime }; // v1.5: 매트릭스 스타일

    private List<RepoInfo> allRepos = new();
    private readonly FlowLayoutPanel recentsPanel = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly SplitContainer split = new() { Dock = DockStyle.Fill }; // v1.5: 필드로 승격 (50:50 시작위치용)
    private List<string> recents = new();
    private static string RecentFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitHubUploader", "recent.txt");

    public MainForm(string sourcePath)
    {
        Text = "GitHub 업로더 v1.9 (git.exe 의존, 브라우저 로그인)";
        Width = 940;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        var baseFont = Font;
        Font = new Font(baseFont.FontFamily, baseFont.Size + 3); // v1.3: 전체 폰트 +3
        chkContextMenu.Font = baseFont; // v1.3: 우클릭 체크박스는 기존 크기 유지
        lblTargetPath.Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold); // v1.7: 대상 경로 굵게

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // source
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // source path display
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // token
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // search
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // recents
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38)); // repos+folders
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // branch label
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // target path
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // commit
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30)); // log
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // bottom checkbox
        Controls.Add(layout);

        // --- 원본 행 ---
        var p1 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        p1.Controls.Add(new Label { Text = "원본:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        txtSourceInput.Width = 500;
        txtSourceInput.TextChanged += (_, _) => { if (!suppressSrcInput) RefreshSourceDisplay(); };
        p1.Controls.Add(txtSourceInput);
        btnPickFolder.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog();
            if (d.ShowDialog() == DialogResult.OK) SetSourceText(d.SelectedPath);
        };
        p1.Controls.Add(btnPickFolder);
        btnPickFile.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "모든 파일|*.*", CheckFileExists = true };
            if (d.ShowDialog() == DialogResult.OK) SetSourceText(d.FileName);
        };
        p1.Controls.Add(btnPickFile);
        layout.Controls.Add(p1, 0, 0);

        // v1.9: 원본 밑에 전체 경로 표시 (마지막만 굵게)
        var pSrc = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        pSrc.Controls.Add(new Label { Text = "경로:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        rtSourcePath.Width = 700;
        rtSourcePath.Height = TextRenderer.MeasureText("Ag", rtSourcePath.Font).Height + 8;
        pSrc.Controls.Add(rtSourcePath);
        layout.Controls.Add(pSrc, 0, 1);
        SetSourceText(sourcePath);

        // --- 토큰 행 ---
        var p2 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        p2.Controls.Add(new Label { Text = "PAT:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        txtToken.Width = 620;
        txtToken.TextChanged += (_, _) => { txtToken.ForeColor = SystemColors.WindowText; }; // v1.3: 새 입력은 검정
        p2.Controls.Add(txtToken);
        btnLoadRepos.Click += async (_, _) => await LoadReposAsync();
        btnBrowserLogin.Click += async (_, _) => await BrowserLoginAsync();
        p2.Controls.Add(btnBrowserLogin);
        p2.Controls.Add(lblAccount);
        p2.Controls.Add(btnLoadRepos);
        layout.Controls.Add(p2, 0, 2);

        txtSearch.TextChanged += (_, _) => ApplyFilter();
        layout.Controls.Add(txtSearch, 0, 3);

        // --- 최근 레포 (v1.3: 검색 밑, 최대 5개) ---
        recentsPanel.Controls.Add(new Label { Text = "최근:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(recentsPanel, 0, 4);

        // --- 레포 + 폴더 (v1.3: 전체 트리 1회 조회, 폴더 아이콘) ---
        var folderIcons = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        folderIcons.Images.Add("folder", CreateFolderIcon());
        treeTarget.ImageList = folderIcons;
        split.Panel1.Controls.Add(lstRepos);
        split.Panel2.Controls.Add(treeTarget);
        lstRepos.SelectedIndexChanged += async (_, _) => await OnRepoSelectedAsync();
        treeTarget.AfterSelect += (_, _) => RefreshTargetPath(); // v1.7: 폴더 클릭 시 대상 경로 갱신
        layout.Controls.Add(split, 0, 5);

        lblBranch.Text = "브랜치: main  |  왼쪽에서 레포 선택 → 오른쪽에서 대상 폴더 선택(루트=/) → 커밋 메시지 입력 → 업로드";
        layout.Controls.Add(lblBranch, 0, 6);

        // v1.7: 선택 중인 대상 경로 (commit 바로 위, 굵게)
        lblTargetPath.Text = "대상: (레포 미선택)";
        layout.Controls.Add(lblTargetPath, 0, 7);

        var p6 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        p6.Controls.Add(new Label { Text = "Commit:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        txtCommit.Width = 700;
        p6.Controls.Add(txtCommit);
        layout.Controls.Add(p6, 0, 8);

        var p7 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        btnUpload.Click += async (_, _) => await UploadAsync();
        btnRefreshFolders.Click += async (_, _) => await LoadTreeForSelectedRepoAsync();
        // v1.3: 주요 버튼 크게
        btnUpload.Padding = new Padding(20, 10, 20, 10);
        btnRefreshFolders.Padding = new Padding(20, 10, 20, 10);
        btnUpload.Font = new Font(btnUpload.Font.FontFamily, btnUpload.Font.Size + 2, FontStyle.Bold);
        p7.Controls.Add(chkFilesOnly); // v1.8: 업로드 버튼 왼쪽
        p7.Controls.Add(btnUpload);
        p7.Controls.Add(btnRefreshFolders);
        layout.Controls.Add(p7, 0, 9);

        layout.Controls.Add(txtLog, 0, 10);

        // v1.3: 우클릭 체크박스는 맨아래, 폰트 그대로
        layout.Controls.Add(chkContextMenu, 0, 11);
        chkContextMenu.CheckedChanged += (_, _) => OnContextMenuToggled();

        Load += async (_, _) =>
        {
            RestoreWindowBounds(); // v1.5: 마지막 창 크기/위치
            txtToken.Text = TokenStore.Load(); // v1.4: 암호화 저장본, 구 평문은 자동 이관
            Log("v1.9 준비. '브라우저로 로그인' 또는 PAT 입력 후 '레포 불러오기'를 누르세요.");
            if (!string.IsNullOrWhiteSpace(txtSourceInput.Text))
                Log("원본: " + txtSourceInput.Text);
            RefreshCtxCheck();
            LoadRecents();
            RefreshRecentRow();
            if (!string.IsNullOrWhiteSpace(txtToken.Text))
                await ShowAccountAsync();
        };

        Shown += (_, _) =>
        {
            try { split.SplitterDistance = split.Width / 2; } catch { } // v1.5: 리스트/폴더 50:50 시작
        };
        FormClosing += (_, _) =>
        {
            try // v1.5: 창 크기/위치 저장
            {
                var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                Directory.CreateDirectory(Path.GetDirectoryName(UiFile)!);
                File.WriteAllText(UiFile, $"{b.X},{b.Y},{b.Width},{b.Height},{(WindowState == FormWindowState.Maximized ? 1 : 0)}");
            }
            catch { }
        };
    }

    private void Log(string s)
    {
        if (InvokeRequired) { Invoke(() => Log(s)); return; }
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}\r\n");
    }

    private string Token => txtToken.Text.Trim();

    // v1.9: 입력칸 설정 + 밑줄 경로 표시(마지막만 굵게) 갱신
    private void SetSourceText(string path)
    {
        suppressSrcInput = true;
        try { txtSourceInput.Text = (path ?? "").Trim().Trim('"'); }
        finally { suppressSrcInput = false; }
        RefreshSourceDisplay();
    }

    private void RefreshSourceDisplay()
    {
        string clean = txtSourceInput.Text.Trim().Trim('"');
        rtSourcePath.Text = clean;
        bool isDir = clean != "" && Directory.Exists(clean);
        chkFilesOnly.Enabled = isDir;
        if (!isDir) chkFilesOnly.Checked = false;
        string t = clean.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (t == "") return;
        int i = Math.Max(t.LastIndexOf(Path.DirectorySeparatorChar), t.LastIndexOf(Path.AltDirectorySeparatorChar));
        rtSourcePath.Select(i + 1, t.Length - (i + 1));
        rtSourcePath.SelectionFont = new Font(rtSourcePath.Font, FontStyle.Bold);
        rtSourcePath.Select(rtSourcePath.TextLength, 0);
        rtSourcePath.ScrollToCaret();
    }

    private static string UiFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitHubUploader", "window.txt");

    // v1.5: 마지막 창 크기/위치 복원 (없으면 기본값 유지)
    private void RestoreWindowBounds()
    {
        try
        {
            if (!File.Exists(UiFile)) return;
            var p = File.ReadAllText(UiFile).Split(',');
            if (p.Length < 5) return;
            int x = int.Parse(p[0]), y = int.Parse(p[1]);
            int w = Math.Max(int.Parse(p[2]), 700), h = Math.Max(int.Parse(p[3]), 520);
            var wa = Screen.PrimaryScreen!.WorkingArea;
            w = Math.Min(w, wa.Width); h = Math.Min(h, wa.Height);
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - 120));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - 120));
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(x, y, w, h);
            if (p[4] == "1") WindowState = FormWindowState.Maximized;
        }
        catch { }
    }

    // v1.2: 탐색기 우클릭 메뉴 체크박스. 체크=등록, 해제=삭제. exe 현재 위치 기준.
    private static bool IsContextMenuRegistered()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\GitHubUploader\command");
        return k?.GetValue("") is string s && s.Length > 0;
    }

    private void RefreshCtxCheck()
    {
        suppressCtxCheck = true;
        try { chkContextMenu.Checked = IsContextMenuRegistered(); }
        finally { suppressCtxCheck = false; }
    }

    private void OnContextMenuToggled()
    {
        if (suppressCtxCheck) return;
        try
        {
            if (chkContextMenu.Checked) RegisterContextMenu();
            else UnregisterContextMenu();
            Log(chkContextMenu.Checked ? "우클릭 메뉴 등록됨." : "우클릭 메뉴 삭제됨.");
        }
        catch (Exception ex)
        {
            Log("우클릭 메뉴 변경 실패: " + ex.Message);
            MessageBox.Show(ex.Message, "우클릭 메뉴");
            RefreshCtxCheck();
        }
    }

    private static void RegisterContextMenu()
    {
        string exe = Application.ExecutablePath;
        foreach (var baseKey in new[]
        {
            @"Software\Classes\Directory\shell\GitHubUploader",
            @"Software\Classes\*\shell\GitHubUploader",
        })
        {
            using var k = Registry.CurrentUser.CreateSubKey(baseKey);
            k.SetValue("", "GitHub에 업로드");
            k.SetValue("Icon", exe);
            using var c = Registry.CurrentUser.CreateSubKey(baseKey + @"\command");
            c.SetValue("", $"\"{exe}\" \"%1\"");
        }
    }

    private static void UnregisterContextMenu()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\GitHubUploader", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\GitHubUploader", throwOnMissingSubKey: false);
    }

    // v1.1: 브라우저 로그인 (gh CLI 필요)
    private async Task BrowserLoginAsync()
    {
        if (!GhAuth.IsGhInstalled())
        {
            var r = MessageBox.Show(
                "브라우저 로그인에는 GitHub CLI(gh)가 필요합니다.\n\n" +
                "지금 다운로드 페이지를 열까요?\n(설치 후 이 창을 다시 실행하세요)",
                "gh 필요", MessageBoxButtons.YesNo);
            if (r == DialogResult.Yes) GhAuth.OpenGhInstallPage();
            else Log("대안: PAT를 직접 붙여넣고 '레포 불러오기'를 누르세요.");
            return;
        }
        btnBrowserLogin.Enabled = false;
        try
        {
            Log("브라우저 로그인 시작... (브라우저에서 승인하세요)");
            await GhAuth.BrowserLoginAsync(Log);
            string token = await GhAuth.GetTokenAsync();
            txtToken.Text = token;
            TokenStore.Save(token);
            await ShowAccountAsync();
            await LoadReposAsync();
        }
        catch (Exception ex) { Log("로그인 오류: " + ex.Message); MessageBox.Show(ex.Message); }
        finally { btnBrowserLogin.Enabled = true; }
    }

    private async Task ShowAccountAsync()
    {
        try
        {
            string login = await GitHubApi.GetMyLoginAsync(Token);
            lblAccount.Text = "@" + login;
            txtToken.ForeColor = Color.LightGray; // v1.3: 로그인됨 = 연한 회색
            Log("로그인 계정: @" + login);
        }
        catch { lblAccount.Text = "미확인"; txtToken.ForeColor = SystemColors.WindowText; }
    }

    // v1.3: 최근 레포 (최대 5개)
    private void LoadRecents()
    {
        try
        {
            if (File.Exists(RecentFile))
                recents = File.ReadAllLines(RecentFile).Where(s => !string.IsNullOrWhiteSpace(s)).Take(5).ToList();
        }
        catch { recents = new List<string>(); }
    }

    private void SaveRecents()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecentFile)!);
            File.WriteAllLines(RecentFile, recents.Take(5));
        }
        catch { }
    }

    private void PushRecent(string fullName)
    {
        recents.Remove(fullName);
        recents.Insert(0, fullName);
        if (recents.Count > 5) recents.RemoveRange(5, recents.Count - 5);
        SaveRecents();
        RefreshRecentRow();
    }

    private void RefreshRecentRow()
    {
        for (int i = recentsPanel.Controls.Count - 1; i >= 0; i--)
            if (recentsPanel.Controls[i] is Button) recentsPanel.Controls[i].Dispose();
        foreach (var r in recents.Take(5))
        {
            var b = new Button { Text = r, AutoSize = true, Tag = r };
            b.Click += async (s, _) => await SelectRecentAsync((string)((Button)s).Tag);
            recentsPanel.Controls.Add(b);
        }
    }

    private async Task SelectRecentAsync(string fullName)
    {
        for (int i = 0; i < lstRepos.Items.Count; i++)
        {
            if (lstRepos.Items[i] is RepoInfo ri && ri.FullName == fullName)
            {
                lstRepos.SelectedIndex = i;
                return;
            }
        }
        if (allRepos.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(Token)) { MessageBox.Show("먼저 로그인하세요."); return; }
            if (!await LoadReposAsync()) { Log("레포 목록 로드 실패. 최근 선택 중단."); return; } // v1.6: 1회만 재시도
            if (allRepos.Count == 0) { Log("레포 목록이 비어 있습니다."); return; }
            await SelectRecentAsync(fullName);
        }
        else Log("목록에 없는 레포: " + fullName);
    }

    private async Task OnRepoSelectedAsync()
    {
        if (SelectedRepo is RepoInfo repo) PushRecent(repo.FullName);
        lblBranch.Text = $"브랜치: {CurrentBranch()}  |  왼쪽에서 레포 선택 → 오른쪽에서 대상 폴더 선택(루트=/) → 커밋 메시지 입력 → 업로드";
        RefreshTargetPath();
        await LoadTreeForSelectedRepoAsync();
    }

    // v1.7: commit 위에 선택 중인 대상 경로 표시 (owner/repo/폴더)
    private void RefreshTargetPath()
    {
        var repo = SelectedRepo;
        if (repo == null) { lblTargetPath.Text = "대상: (레포 미선택)"; return; }
        string t = SelectedTargetPath();
        lblTargetPath.Text = "대상: " + repo.Owner + "/" + repo.Name + (string.IsNullOrEmpty(t) ? "/ (루트)" : "/" + t);
    }

    // v1.6: 레포의 실제 default branch 사용 (main 고정 해제)
    private string CurrentBranch()
    {
        string b = SelectedRepo?.DefaultBranch?.Trim() ?? "";
        return string.IsNullOrEmpty(b) ? "main" : b;
    }

    // v1.3: 재귀 트리 1회 조회로 전체 폴더 표시 (정확한 +/- , 추가 API 호출 없음)
    private async Task LoadTreeForSelectedRepoAsync()
    {
        var repo = SelectedRepo;
        treeTarget.Nodes.Clear();
        if (repo == null) return;
        var root = new TreeNode("/") { Tag = "", ImageKey = "folder", SelectedImageKey = "folder" };
        treeTarget.Nodes.Add(root);
        try
        {
            var folders = await GitHubApi.GetFolderTreeAsync(Token, repo.Owner, repo.Name, CurrentBranch());
            foreach (var f in folders) root.Nodes.Add(BuildTreeNode(f));
            root.Expand();
            Log($"{repo.FullName}: 폴더 {CountFolders(folders)}개 표시");
        }
        catch (Exception ex)
        {
            Log("폴더 트리: " + ex.Message + " → 루트(/)에 업로드 가능");
            root.Expand();
        }
        RefreshTargetPath(); // v1.7: 트리 로드 후 대상 경로 갱신
    }

    private static TreeNode BuildTreeNode(GitHubApi.FolderNode f)
    {
        var n = new TreeNode(f.Name) { Tag = f.Path, ImageKey = "folder", SelectedImageKey = "folder" };
        foreach (var c in f.Children) n.Nodes.Add(BuildTreeNode(c));
        return n;
    }

    private static int CountFolders(List<GitHubApi.FolderNode> nodes)
    {
        int n = nodes.Count;
        foreach (var c in nodes) n += CountFolders(c.Children);
        return n;
    }

    private static Bitmap CreateFolderIcon()
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var back = new SolidBrush(Color.FromArgb(218, 178, 80));
            using var front = new SolidBrush(Color.FromArgb(247, 210, 110));
            using var line = new Pen(Color.FromArgb(160, 125, 50));
            g.FillRectangle(back, 1, 2, 7, 4);
            g.FillRectangle(front, 1, 5, 14, 9);
            g.DrawRectangle(line, 1, 5, 14, 9);
        }
        return bmp;
    }

    private async Task<bool> LoadReposAsync() // v1.6: 성공 여부 반환 (최근선택 1회 재시도용)
    {
        if (string.IsNullOrWhiteSpace(Token)) { MessageBox.Show("PAT를 입력하세요."); return false; }
        btnLoadRepos.Enabled = false;
        try
        {
            TokenStore.Save(Token); // v1.4: DPAPI 암호화 저장
            Log("레포 목록 불러오는 중...");
            allRepos = await GitHubApi.GetMyReposAsync(Token);
            ApplyFilter();
            await ShowAccountAsync();
            Log($"{allRepos.Count}개 레포 로드됨.");
            return true;
        }
        catch (Exception ex) { Log("오류: " + ex.Message); MessageBox.Show(ex.Message); return false; }
        finally { btnLoadRepos.Enabled = true; }
    }

    private void ApplyFilter()
    {
        lstRepos.Items.Clear();
        string q = txtSearch.Text.Trim();
        foreach (var r in allRepos)
        {
            if (q == "" || r.FullName.Contains(q, StringComparison.OrdinalIgnoreCase))
                lstRepos.Items.Add(r);
        }
    }

    private RepoInfo SelectedRepo => lstRepos.SelectedItem as RepoInfo;

    private string SelectedTargetPath()
    {
        var n = treeTarget.SelectedNode;
        if (n == null) return "";
        return (n.Tag as string) ?? "";
    }

    private async Task UploadAsync()
    {
        string source = txtSourceInput.Text.Trim().Trim('"');
        var repo = SelectedRepo;
        string targetPath = SelectedTargetPath();
        string commitMsg = txtCommit.Text.Trim();
        if (string.IsNullOrWhiteSpace(source) || (!File.Exists(source) && !Directory.Exists(source)))
        { MessageBox.Show("원본 파일/폴더 경로가 올바르지 않습니다."); return; }
        if (repo == null) { MessageBox.Show("레포를 선택하세요."); return; }
        if (string.IsNullOrWhiteSpace(commitMsg)) { MessageBox.Show("Commit 메시지를 입력하세요."); return; }
        if (string.IsNullOrWhiteSpace(Token)) { MessageBox.Show("PAT를 입력하세요."); return; }

        btnUpload.Enabled = false;
        string token = Token;
        string branch = string.IsNullOrEmpty(repo.DefaultBranch?.Trim()) ? "main" : repo.DefaultBranch.Trim();
        try
        {
            await GitHelper.EnsureGitExistsAsync();
            string workRoot = Path.Combine(Path.GetTempPath(), "GitHubUploader", repo.Owner, repo.Name);
            Directory.CreateDirectory(workRoot);
            var authEnv = GitHelper.AuthEnv(token); // v1.6: 토큰은 환경변수로만 (CLI·config에 기록 안 됨)
            string plainUrl = GitHelper.PlainUrl(repo.Owner, repo.Name);
            void log(string s) => Log(s);

            if (!Directory.Exists(Path.Combine(workRoot, ".git")))
            {
                Log($"clone 중: {repo.FullName} ({branch}) ...");
                Directory.CreateDirectory(Path.GetDirectoryName(workRoot)!);
                if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true);
                await GitHelper.RunGitAsync(new[] { "clone", "--branch", branch, "--depth", "1", plainUrl, workRoot }, Path.GetTempPath(), log, authEnv, token);
            }
            else
            {
                Log("기존 clone 업데이트 중...");
                await GitHelper.RunGitAsync(new[] { "remote", "set-url", "origin", plainUrl }, workRoot, log, null, token);
                await GitHelper.RunGitAsync(new[] { "fetch", "origin" }, workRoot, log, authEnv, token);
                try { await GitHelper.RunGitAsync(new[] { "checkout", branch }, workRoot, log, null, token); }
                catch { await GitHelper.RunGitAsync(new[] { "checkout", "-B", branch, "origin/" + branch }, workRoot, log, null, token); }
                await GitHelper.RunGitAsync(new[] { "pull", "--ff-only", "origin", branch }, workRoot, log, authEnv, token);
            }

            string destBase = string.IsNullOrEmpty(targetPath)
                ? workRoot
                : Path.Combine(workRoot, targetPath.Replace('/', Path.DirectorySeparatorChar));
            GitHelper.CopySourceInto(source, destBase, log, chkFilesOnly.Checked); // v1.8: 폴더 속 파일만 옵션

            await GitHelper.RunGitAsync(new[] { "add", "-A" }, workRoot, log);
            string status = await GitHelper.RunGitAsync(new[] { "status", "--porcelain" }, workRoot, log);
            if (string.IsNullOrWhiteSpace(status))
            {
                Log("변경 사항 없음. 업로드할 게 없습니다.");
                return;
            }
            await GitHelper.RunGitAsync(new[] { "commit", "-m", commitMsg }, workRoot, log, null, token);
            await GitHelper.RunGitAsync(new[] { "push", "origin", branch }, workRoot, log, authEnv, token);
            Log($"완료: {repo.FullName}/{(string.IsNullOrEmpty(targetPath) ? "(루트)" : targetPath)} 에 업로드됨.");
            MessageBox.Show("업로드 완료");
        }
        catch (Exception ex)
        {
            Log("업로드 실패: " + ex.Message);
            MessageBox.Show(ex.Message, "실패");
        }
        finally { btnUpload.Enabled = true; }
    }
}
