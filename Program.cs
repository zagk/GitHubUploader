namespace GitHubUploader;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string sourcePath = args.Length > 0 ? args[0].Trim('"') : "";
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(sourcePath));
    }
}
