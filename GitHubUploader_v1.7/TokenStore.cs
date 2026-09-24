using System.Security.Cryptography;
using System.Text;

namespace GitHubUploader;

// v1.4: PAT를 DPAPI(현재 윈도우 사용자 귀속)로 암호화 저장.
// 파일이 유출되어도 본인 계정에서만 복호화됨. 구 평문 token.txt는 자동 이관 후 삭제.
public static class TokenStore
{
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitHubUploader");

    private static string DatFile => Path.Combine(Dir, "token.dat");
    private static string LegacyFile => Path.Combine(Dir, "token.txt");

    public static void Save(string token)
    {
        Directory.CreateDirectory(Dir);
        byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(DatFile, enc);
        try { if (File.Exists(LegacyFile)) File.Delete(LegacyFile); } catch { }
    }

    public static string Load()
    {
        try
        {
            if (File.Exists(DatFile))
                return Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(File.ReadAllBytes(DatFile), null, DataProtectionScope.CurrentUser)).Trim();
        }
        catch { }
        // 구버전 평문 이관
        try
        {
            if (File.Exists(LegacyFile))
            {
                string t = File.ReadAllText(LegacyFile, Encoding.UTF8).Trim();
                if (t != "")
                {
                    Save(t);
                    return t;
                }
            }
        }
        catch { }
        return "";
    }
}
