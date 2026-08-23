using Microsoft.Extensions.DependencyInjection;

namespace Capture;

public static class RegistrationExtension
{
    /// <summary>
    /// Регистрирует захват экрана, стример JPEG и эмуляцию мыши.
    /// </summary>
    public static IServiceCollection AddScreenCapture(this IServiceCollection services)
    {
        services.AddSingleton<GdiScreenCapture>();
        services.AddSingleton<DxgiScreenCapture>();
        services.AddSingleton<IScreenCapture, ScreenCaptureService>();
        services.AddSingleton<JpegSessionStreamer>();
        services.AddSingleton<Win32MouseInput>();
        return services;
    }
}
