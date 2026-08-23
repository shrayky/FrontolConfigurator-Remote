namespace Capture;

public interface IScreenCapture
{
    /// <summary>
    /// Захватывает основной монитор в JPEG (масштаб max сторона 1280).
    /// </summary>
    (int Width, int Height, byte[] Jpeg) Capture();
}
