namespace Capture;

internal readonly record struct DxgiOutputCandidate(
    uint AdapterIndex,
    uint OutputIndex,
    bool IsSoftware,
    bool AttachedToDesktop,
    int DesktopLeft,
    int DesktopTop,
    string AdapterName,
    string OutputName);

internal static class DxgiDuplicationPicker
{
    /// <summary>
    /// Выбирает аппаратный выход на рабочем столе; предпочитает координаты (0,0).
    /// </summary>
    public static DxgiOutputCandidate? Pick(IEnumerable<DxgiOutputCandidate> candidates)
    {
        DxgiOutputCandidate? attached = null;
        foreach (var candidate in candidates)
        {
            if (candidate.IsSoftware || !candidate.AttachedToDesktop)
                continue;

            if (candidate.DesktopLeft == 0 && candidate.DesktopTop == 0)
                return candidate;

            attached ??= candidate;
        }

        return attached;
    }
}
