namespace Domain.RemoteDesktop;

/// <summary>
/// Результат поиска каталога базы Frontol.
/// </summary>
public readonly record struct FrontolDbFolderResolve(string? Folder, string Error, string? Tried = null)
{
    public bool Success => !string.IsNullOrEmpty(Folder);
}

/// <summary>
/// Каталог базы Frontol из frontol.ini (ключ Path=).
/// </summary>
public static class FrontolDbFolderLaunch
{
    public const string NotFoundError = "Каталог базы Frontol не найден";
    public const string IniNotFoundError = "Не найден frontol.ini";
    public const string PathMissingError = "В frontol.ini нет пути к базе";

    /// <summary>
    /// Путь к frontol.ini в ProgramData этой машины.
    /// </summary>
    public static string IniPathOnThisMachine()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "atol", "frontol6", "settings", "frontol.ini");
    }

    /// <summary>
    /// Значение Path= из строк ini или null.
    /// </summary>
    public static string? FromIni(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!line.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = LocalPath(line["Path=".Length..].Trim());
            if (value.Length == 0)
                return null;

            return value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return null;
    }

    /// <summary>
    /// Firebird host:C:\... → локальный путь, как DriveMetricsReader в агенте.
    /// </summary>
    private static string LocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        path = path.Trim().Trim('"');
        var parts = path.Split(':');
        if (parts.Length >= 3)
            return $"{parts[1]}:{string.Join(":", parts.Skip(2))}";

        return path;
    }

    /// <summary>
    /// Ищет каталог: ini есть, Path= задан; если Path= — файл gdb, берём родительскую папку.
    /// </summary>
    public static FrontolDbFolderResolve Resolve(
        string iniPath,
        Func<string, bool> fileExists,
        Func<string, IEnumerable<string>> readLines,
        Func<string, bool> directoryExists)
    {
        if (!fileExists(iniPath))
            return new FrontolDbFolderResolve(null, IniNotFoundError);

        var folder = FromIni(readLines(iniPath));
        if (folder == null)
            return new FrontolDbFolderResolve(null, PathMissingError);

        if (directoryExists(folder))
            return new FrontolDbFolderResolve(folder, string.Empty);

        if (fileExists(folder))
        {
            var parent = Path.GetDirectoryName(folder);
            if (!string.IsNullOrEmpty(parent) && directoryExists(parent))
                return new FrontolDbFolderResolve(parent, string.Empty);
        }

        return new FrontolDbFolderResolve(null, NotFoundError, folder);
    }

    /// <summary>
    /// Ищет каталог базы на этой машине.
    /// </summary>
    public static FrontolDbFolderResolve ResolveOnThisMachine()
    {
        return Resolve(IniPathOnThisMachine(), File.Exists, File.ReadAllLines, Directory.Exists);
    }
}
