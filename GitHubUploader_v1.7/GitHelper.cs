using System.Diagnostics;
using System.Text;

namespace GitHubUploader;

public static class GitHelper
{
    // v1.6: ArgumentList 기반(수동 인용 불필요) + 토큰 마스킹.
    // 토큰은 args에 절대 넣지 말고 env(AuthEnv)로 전달할 것.
    public static async Task<string> RunGitAsync(string[] args, string workDir, Action<string> log, IDictionary<string, string> env = null, string mask = null)
    {
        string Safe(string s) => string.IsNullOrEmpty(mask) ? s : s.Replace(mask, "***");
        var psi = new ProcessStartInfo("git", "")
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (env != null)
            foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
        var sb = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) { sb.AppendLine(e.Data); log?.Invoke(Safe(e.Data)); } };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) { sb.AppendLine(e.Data); log?.Invoke(Safe(e.Data)); } };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
            throw new Exception($"git {Safe(string.Join(" ", args))} 실패 (exit {p.ExitCode})\n{Safe(sb.ToString())}");
        return sb.ToString();
    }

    public static async Task EnsureGitExistsAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "--version")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) throw new Exception("git 실행 불가");
            await p.WaitForExitAsync();
            if (p.ExitCode != 0) throw new Exception("git --version 실패");
        }
        catch (Exception ex)
        {
            throw new Exception("git.exe를 찾지 못했습니다. Git for Windows를 설치하고 PATH에 등록하세요. " + ex.Message);
        }
    }

    // v1.6: 토큰 없는 일반 URL. 인증은 AuthEnv()의 http.extraHeader로만 수행.
    public static string PlainUrl(string owner, string repo)
        => $"https://github.com/{owner}/{repo}.git";

    // v1.6: 토큰을 커맨드라인·.git/config에 남기지 않는 인증용 환경변수.
    public static IDictionary<string, string> AuthEnv(string token)
    {
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + token));
        return new Dictionary<string, string>
        {
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraHeader",
            ["GIT_CONFIG_VALUE_0"] = "AUTHORIZATION: basic " + b64,
        };
    }

    public static void CopySourceInto(string sourcePath, string destDir, Action<string> log)
    {
        if (File.Exists(sourcePath))
        {
            Directory.CreateDirectory(destDir);
            string dest = Path.Combine(destDir, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, dest, overwrite: true);
            log?.Invoke($"파일 복사: {sourcePath} -> {dest}");
        }
        else if (Directory.Exists(sourcePath))
        {
            string folderName = new DirectoryInfo(sourcePath.TrimEnd(Path.DirectorySeparatorChar)).Name;
            string dest = Path.Combine(destDir, folderName);
            int skipped = CopyDir(sourcePath, dest);
            log?.Invoke($"폴더 복사: {sourcePath} -> {dest}" + (skipped > 0 ? $" (.git {skipped}개 제외)" : ""));
        }
        else
        {
            throw new Exception("원본 경로가 없습니다: " + sourcePath);
        }
    }

    // v1.6: .git 제어 폴더는 복사 제외 (클론 손상 방지). 제외 개수를 반환.
    private static int CopyDir(string src, string dst)
    {
        int skipped = 0;
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
        foreach (var d in Directory.GetDirectories(src))
        {
            if (string.Equals(Path.GetFileName(d), ".git", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }
            skipped += CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }
        return skipped;
    }
}
