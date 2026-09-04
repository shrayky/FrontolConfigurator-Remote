using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class FrontolDbFolderLaunchTests
{
    /// <summary>
    /// Path= из frontol.ini — каталог базы.
    /// </summary>
    [Fact]
    public void FromIni_берёт_Path()
    {
        var folder = FrontolDbFolderLaunch.FromIni(
        [
            "[DataBase]",
            @"Path=D:\Frontol\DB"
        ]);

        Assert.Equal(@"D:\Frontol\DB", folder);
    }

    /// <summary>
    /// Path= с префиксом Firebird localhost: — локальный каталог.
    /// </summary>
    [Fact]
    public void FromIni_убирает_префикс_сервера()
    {
        var folder = FrontolDbFolderLaunch.FromIni(
        [
            "[DataBase]",
            @"Path=localhost:D:\Frontol\DB"
        ]);

        Assert.Equal(@"D:\Frontol\DB", folder);
    }

    /// <summary>
    /// Path= в кавычках — путь без кавычек.
    /// </summary>
    [Fact]
    public void FromIni_убирает_кавычки()
    {
        var folder = FrontolDbFolderLaunch.FromIni(
        [
            "[DataBase]",
            @"Path=""D:\Frontol\DB"""
        ]);

        Assert.Equal(@"D:\Frontol\DB", folder);
    }

    /// <summary>
    /// Нет Path= — каталог неизвестен.
    /// </summary>
    [Fact]
    public void FromIni_нет_ключа_null()
    {
        var folder = FrontolDbFolderLaunch.FromIni(
        [
            "[DataBase]",
            "DB=Main.gdb"
        ]);

        Assert.Null(folder);
    }

    /// <summary>
    /// Нет frontol.ini — ошибка для окна оператора.
    /// </summary>
    [Fact]
    public void Resolve_нет_ini_ошибка()
    {
        var result = FrontolDbFolderLaunch.Resolve(
            @"C:\ProgramData\atol\frontol6\settings\frontol.ini",
            fileExists: _ => false,
            readLines: _ => [],
            directoryExists: _ => true);

        Assert.False(result.Success);
        Assert.Equal(FrontolDbFolderLaunch.IniNotFoundError, result.Error);
    }

    /// <summary>
    /// Ini есть, Path= нет — ошибка для окна оператора.
    /// </summary>
    [Fact]
    public void Resolve_нет_Path_ошибка()
    {
        var result = FrontolDbFolderLaunch.Resolve(
            @"C:\ProgramData\atol\frontol6\settings\frontol.ini",
            fileExists: _ => true,
            readLines: _ => ["[DataBase]", "DB=Main.gdb"],
            directoryExists: _ => true);

        Assert.False(result.Success);
        Assert.Equal(FrontolDbFolderLaunch.PathMissingError, result.Error);
    }

    /// <summary>
    /// Path= есть, папки нет — ошибка.
    /// </summary>
    [Fact]
    public void Resolve_папки_нет_ошибка()
    {
        var result = FrontolDbFolderLaunch.Resolve(
            @"C:\ProgramData\atol\frontol6\settings\frontol.ini",
            fileExists: _ => true,
            readLines: _ => [@"Path=D:\missing"],
            directoryExists: _ => false);

        Assert.False(result.Success);
        Assert.Equal(FrontolDbFolderLaunch.NotFoundError, result.Error);
    }

    /// <summary>
    /// Ini и папка есть — путь для проводника.
    /// </summary>
    [Fact]
    public void Resolve_существующая_папка()
    {
        var result = FrontolDbFolderLaunch.Resolve(
            @"C:\ProgramData\atol\frontol6\settings\frontol.ini",
            fileExists: _ => true,
            readLines: _ => [@"Path=D:\Frontol\DB"],
            directoryExists: path => path == @"D:\Frontol\DB");

        Assert.True(result.Success);
        Assert.Equal(@"D:\Frontol\DB", result.Folder);
    }

    /// <summary>
    /// Path= указывает на Main.gdb — открываем родительский каталог.
    /// </summary>
    [Fact]
    public void Resolve_файл_gdb_берёт_папку()
    {
        var gdb = @"D:\Frontol\DB\Main.gdb";
        var result = FrontolDbFolderLaunch.Resolve(
            @"C:\ProgramData\atol\frontol6\settings\frontol.ini",
            fileExists: path => path.EndsWith("frontol.ini", StringComparison.OrdinalIgnoreCase)
                || path == gdb,
            readLines: _ => [$"Path={gdb}"],
            directoryExists: path => path == @"D:\Frontol\DB");

        Assert.True(result.Success);
        Assert.Equal(@"D:\Frontol\DB", result.Folder);
    }
}
