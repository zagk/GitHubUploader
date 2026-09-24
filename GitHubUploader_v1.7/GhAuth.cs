using System.Diagnostics;

namespace GitHubUploader;

// v1.1: 브라우저 로그인 = gh CLI(official GitHub CLI)의 웹 로그인 재사용.
// 별도 OAuth 앱 등록 없이 브라우저가 열리고, 토큰은 gh가 안전하게 보관.
// 앱은 `gh auth token`으로 토큰만 읽어 API/클론에 사용.
public static class GhAuth
{
    public static bool IsGhInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo("gh", "--version")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            if (!p.WaitForExit(5000)) return false; // v1.6: 타임아웃 명시
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    // 브라우저를 열어 로그인. 사용자가 브라우저에서 완료할 때까지 대기.
    public static async Task BrowserLoginAsync(Action<string> log)
    {
        var psi = new ProcessStartInfo("gh", "auth login -w -h github.com")
        {
            UseShellExecute = true, // 콘솔창 표시 (코드 입력/안내 확인용)
        };
        using var p = Process.Start(psi);
        if (p == null) throw new Exception("gh 실행 실패");
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
            throw new Exception("브라우저 로그인이 취소되었거나 실패했습니다.");
        log?.Invoke("브라우저 로그인 완료.");
    }

    public static async Task<string> GetTokenAsync()
    {
        var psi = new ProcessStartInfo("gh", "auth token")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = new Process { StartInfo = psi };
        p.Start();
        string token = (await p.StandardOutput.ReadToEndAsync()).Trim();
        string err = (await p.StandardError.ReadToEndAsync()).Trim();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(token))
            throw new Exception("gh 토큰 읽기 실패. 먼저 브라우저 로그인을 완료하세요. " + err);
        return token;
    }

    public static void OpenGhInstallPage()
    {
        Process.Start(new ProcessStartInfo("https://cli.github.com/") { UseShellExecute = true });
    }
}
