using Capture;
using Xunit;

namespace Domain.Tests;

public class DxgiDuplicationPickerTests
{
    [Fact]
    public void пропускает_программный_адаптер()
    {
        var picked = DxgiDuplicationPicker.Pick(
        [
            new DxgiOutputCandidate(0, 0, IsSoftware: true, AttachedToDesktop: true, 0, 0, "WARP", "\\\\.\\DISPLAY1")
        ]);

        Assert.Null(picked);
    }

    [Fact]
    public void пропускает_выход_не_на_рабочем_столе()
    {
        var picked = DxgiDuplicationPicker.Pick(
        [
            new DxgiOutputCandidate(0, 0, IsSoftware: false, AttachedToDesktop: false, 0, 0, "Intel", "\\\\.\\DISPLAY1")
        ]);

        Assert.Null(picked);
    }

    [Fact]
    public void предпочитает_основной_монитор_00()
    {
        var secondary = new DxgiOutputCandidate(0, 0, false, true, 1920, 0, "Intel", "\\\\.\\DISPLAY1");
        var primary = new DxgiOutputCandidate(0, 1, false, true, 0, 0, "Intel", "\\\\.\\DISPLAY2");

        var picked = DxgiDuplicationPicker.Pick([secondary, primary]);

        Assert.Equal(1u, picked!.Value.OutputIndex);
    }

    [Fact]
    public void берёт_первый_привязанный_если_нет_00()
    {
        var first = new DxgiOutputCandidate(0, 0, false, true, 1920, 0, "Intel", "\\\\.\\DISPLAY1");
        var second = new DxgiOutputCandidate(0, 1, false, true, 3840, 0, "Intel", "\\\\.\\DISPLAY2");

        var picked = DxgiDuplicationPicker.Pick([first, second]);

        Assert.Equal(0u, picked!.Value.OutputIndex);
    }
}
