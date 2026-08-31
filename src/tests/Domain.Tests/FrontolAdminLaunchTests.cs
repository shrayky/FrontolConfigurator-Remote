using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class FrontolAdminLaunchTests
{
    /// <summary>
    /// На x64 сначала Program Files (x86), затем Program Files.
    /// </summary>
    [Fact]
    public void Candidates_x86_затем_program_files()
    {
        var candidates = FrontolAdminLaunch.Candidates(
            @"C:\Program Files (x86)",
            @"C:\Program Files");

        Assert.Equal(
            [
                @"C:\Program Files (x86)\ATOL\Frontol6\BIN\FrontolAdmin.exe",
                @"C:\Program Files\ATOL\Frontol6\BIN\FrontolAdmin.exe"
            ],
            candidates);
    }

    /// <summary>
    /// Берём первый существующий файл из кандидатов.
    /// </summary>
    [Fact]
    public void Resolve_первый_существующий()
    {
        var path = FrontolAdminLaunch.Resolve(
            [
                @"C:\missing\FrontolAdmin.exe",
                @"D:\ATOL\FrontolAdmin.exe"
            ],
            exists: p => p.EndsWith(@"D:\ATOL\FrontolAdmin.exe", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(@"D:\ATOL\FrontolAdmin.exe", path);
    }

    /// <summary>
    /// Нет файла — null, чтобы UI показал ошибку.
    /// </summary>
    [Fact]
    public void Resolve_нет_файла_null()
    {
        var path = FrontolAdminLaunch.Resolve(
            [@"C:\missing\FrontolAdmin.exe"],
            exists: _ => false);

        Assert.Null(path);
    }
}
