namespace Domain.RemoteDesktop;

/// <summary>
/// Пути к Frontol Администратор на кассе (x64 и x86).
/// </summary>
public static class FrontolAdminLaunch
{
    public const string NotFoundError = "Frontol Администратор не найден";

    /// <summary>
    /// Кандидаты: сначала Program Files (x86), затем Program Files.
    /// </summary>
    public static IReadOnlyList<string> Candidates(string programFilesX86, string programFiles)
    {
        return
        [
            Path.Combine(programFilesX86, "ATOL", "Frontol6", "BIN", "FrontolAdmin.exe"),
            Path.Combine(programFiles, "ATOL", "Frontol6", "BIN", "FrontolAdmin.exe")
        ];
    }

    /// <summary>
    /// Первый существующий путь или null.
    /// </summary>
    public static string? Resolve(IEnumerable<string> candidates, Func<string, bool> exists)
    {
        foreach (var path in candidates)
        {
            if (exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Ищет FrontolAdmin.exe в стандартных каталогах этой машины.
    /// </summary>
    public static string? ResolveOnThisMachine()
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Resolve(Candidates(programFilesX86, programFiles), File.Exists);
    }
}
