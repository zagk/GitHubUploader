using System.Net.Http.Headers;
using System.Text.Json;

namespace GitHubUploader;

public sealed class RepoInfo
{
    public string FullName { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Private { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public override string ToString() => FullName + (Private ? " (private)" : "");
}

public static class GitHubApi
{
    // v1.6: HttpClient 1개 재사용. 토큰은 요청별 헤더로만 전달.
    private static readonly HttpClient Shared = CreateShared();

    private static HttpClient CreateShared()
    {
        var c = new HttpClient();
        c.BaseAddress = new Uri("https://api.github.com/");
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubUploader", "v1"));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        c.Timeout = TimeSpan.FromSeconds(30);
        return c;
    }

    private static async Task<string> GetStringAsync(string url, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await Shared.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"GitHub API 실패 ({(int)res.StatusCode}): {body}");
        return body;
    }

    // 내 레포 목록 (최대 10페이지 x 100개)
    public static async Task<List<RepoInfo>> GetMyReposAsync(string token)
    {
        var list = new List<RepoInfo>();
        for (int page = 1; page <= 10; page++)
        {
            string body = await GetStringAsync($"user/repos?per_page=100&page={page}&sort=updated", token);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.GetArrayLength() == 0) break;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                list.Add(new RepoInfo
                {
                    FullName = el.GetProperty("full_name").GetString() ?? "",
                    Owner = el.GetProperty("owner").GetProperty("login").GetString() ?? "",
                    Name = el.GetProperty("name").GetString() ?? "",
                    Private = el.GetProperty("private").GetBoolean(),
                    DefaultBranch = el.TryGetProperty("default_branch", out var db) ? (db.GetString() ?? "main") : "main",
                });
            }
            if (doc.RootElement.GetArrayLength() < 100) break;
        }
        return list;
    }

    // v1.1: 로그인 계정 표시용
    public static async Task<string> GetMyLoginAsync(string token)
    {
        string body = await GetStringAsync("user", token);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("login", out var l) ? (l.GetString() ?? "?") : "?";
    }

    // v1.3: 재귀 트리 1회 조회용 폴더 노드
    public sealed class FolderNode
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public List<FolderNode> Children { get; } = new();
    }

    // v1.6: branch 파라미터 (default_branch 사용)
    public static async Task<List<FolderNode>> GetFolderTreeAsync(string token, string owner, string repo, string branch)
    {
        string body;
        try
        {
            body = await GetStringAsync($"repos/{owner}/{repo}/git/trees/{branch}?recursive=1", token);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("(404)")) throw new Exception($"빈 레포 또는 '{branch}' 브랜치 없음");
            throw;
        }
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("truncated", out var t) && t.GetBoolean())
            throw new Exception("레포가 너무 커서 트리 생략");
        var root = new List<FolderNode>();
        var map = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in doc.RootElement.GetProperty("tree").EnumerateArray())
        {
            if (el.GetProperty("type").GetString() != "tree") continue;
            string path = el.GetProperty("path").GetString() ?? "";
            if (path == "") continue;
            var node = new FolderNode { Path = path, Name = path.Split('/').Last() };
            map[path] = node;
            int slash = path.LastIndexOf('/');
            if (slash < 0) root.Add(node);
            else if (map.TryGetValue(path.Substring(0, slash), out var parent)) parent.Children.Add(node);
            else root.Add(node);
        }
        SortNodes(root);
        return root;
    }

    private static void SortNodes(List<FolderNode> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        foreach (var n in nodes) SortNodes(n.Children);
    }
}
