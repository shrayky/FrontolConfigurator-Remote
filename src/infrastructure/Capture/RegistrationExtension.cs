using Microsoft.Extensions.DependencyInjection;

namespace Capture;

public static class RegistrationExtension
{
        /// <summary>
        /// Регистрирует захват экрана, стример JPEG и эмуляцию мыши/клавиатуры.
        /// </summary>
        public static IServiceCollection AddScreenCapture(this IServiceCollection services)
        {
            services.AddSingleton<GdiScreenCapture>();
            services.AddSingleton<DxgiScreenCapture>();
            services.AddSingleton<IScreenCapture, ScreenCaptureService>();
            services.AddSingleton<JpegSessionStreamer>();
            services.AddSingleton<Win32MouseInput>();
            services.AddSingleton<Win32KeyboardInput>();
            services.AddSingleton<Win32TaskManagerStart>();
            services.AddSingleton<Win32FrontolAdminStart>();
            services.AddSingleton<Win32FrontolDbFolderOpen>();
            return services;
        }
}
