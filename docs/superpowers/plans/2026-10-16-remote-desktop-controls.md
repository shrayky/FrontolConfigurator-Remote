# Удалённый доступ: качество, колесо, буфер — план fc-remote

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Агент применяет пресеты качества и скорости потока, прокручивает колесо мыши и синхронизирует текстовый буфер кассы с окном оператора.

**Architecture:** Настройки живут в одном singleton и читаются перед каждым кадром, стрим не перезапускается. Колесо — ветка `Win32MouseInput` для `ev="wheel"`. Буфер опрашивается по `GetClipboardSequenceNumber`, пока сессия активна: процесс — Worker без окна, `AddClipboardFormatListener` сообщения не доставит.

**Tech Stack:** .NET Worker, SignalR Client, GDI JPEG, Win32 `SendInput`, xUnit в `Domain.Tests`.

## Global Constraints

- Контракт совпадает с планом `fc` `docs/superpowers/plans/2026-10-16-remote-desktop-controls.md`.
- `RemoteStreamSettings`: `SessionId`, `Quality`, `MaxSide`, `IntervalMs`. Допустимо только `25/960`, `40/1280`, `60/1600` и интервалы `40`, `67`, `200`. Иное не применять.
- Пока настройка не пришла, оставить текущее поведение: качество JPEG 40, длинная сторона 1280, интервал 67 мс.
- Кадр длиннее 1 000 000 байт на хаб не отправлять. Один повтор кодирования с качеством 25. В лог писать только размер, не пиксели.
- `RemotePointer.Delta` — браузерный `deltaY` в пикселях. Положительный `delta` крутит вниз. В Win32 положительный `mouseData` крутит вверх, знак меняется. Один щелчок — `mouseData` кратный 120.
- `RemoteClipboard.Text` не длиннее 32 000 символов, пустую строку не принимать и не слать. Содержимое в лог не писать.
- Ввод применять только если `ShouldApplyInput(sessionId)` истинен, как у мыши и клавиатуры.
- Текст, который агент сам записал в буфер, обратно на хаб не отправлять.
- Комментарий на русском только у метода, где порядок шагов неочевиден.
- Сразу после старта и после возобновления сессии агент один раз вызывает `ReportCapabilities` с `streamSettings: true` и `clipboard: true`. Без этого вызова Командер считает возможности выключенными и прячет списки качества и блок буфера. Колесо отдельно не объявлять: у него нет контрола, старый агент событие `wheel` и так отбрасывает.

## Контракт

Методы, которые сервер вызывает на клиенте:

- `RemoteStreamSettings(RemoteStreamSettings settings)`
- `RemotePointer` — уже есть; для колеса `Ev == "wheel"`, координаты и `Delta`
- `RemoteClipboard(RemoteClipboard clipboard)` — записать текст на кассу

Метод, который агент вызывает на хабе:

- `UploadClipboard(RemoteClipboard clipboard)` — текст с кассы
- `ReportCapabilities(RemoteCapabilities capabilities)` — агент сообщает, что умеет пресеты потока и текстовый буфер

```csharp
namespace Domain.RemoteDesktop.Dto;

public class RemoteCapabilities
{
    public Guid SessionId { get; set; }
    public bool StreamSettings { get; set; }
    public bool Clipboard { get; set; }
}
```

## Файлы

- Создать: `src/core/Domain/RemoteDesktop/StreamSettingsGuard.cs`
- Создать: `src/core/Domain/RemoteDesktop/ClipboardTextGuard.cs`
- Создать: `src/core/Domain/RemoteDesktop/WheelMapping.cs`
- Создать: `src/core/Domain/RemoteDesktop/Dto/RemoteStreamSettings.cs`
- Создать: `src/core/Domain/RemoteDesktop/Dto/RemoteClipboard.cs`
- Создать: `src/core/Domain/RemoteDesktop/Dto/RemoteCapabilities.cs`
- Создать: `src/infrastructure/Capture/StreamCaptureSettings.cs`
- Создать: `src/infrastructure/Capture/Win32Clipboard.cs`
- Создать: `src/tests/Domain.Tests/StreamSettingsGuardTests.cs`
- Создать: `src/tests/Domain.Tests/ClipboardTextGuardTests.cs`
- Создать: `src/tests/Domain.Tests/WheelMappingTests.cs`
- Изменить: `src/core/Domain/RemoteDesktop/Dto/RemotePointer.cs`
- Изменить: `src/core/Domain/RemoteDesktop/FrameSendPolicy.cs`
- Изменить: `src/tests/Domain.Tests/FrameSendPolicyTests.cs`
- Изменить: `src/infrastructure/Capture/GdiScreenCapture.cs`
- Изменить: `src/infrastructure/Capture/JpegSessionStreamer.cs`
- Изменить: `src/infrastructure/Capture/Win32MouseInput.cs`
- Изменить: `src/infrastructure/Capture/NativeMethods.cs`
- Изменить: `src/infrastructure/Capture/RegistrationExtension.cs`
- Изменить: `src/infrastructure/CommanderExchange/SignalRRemoteClient.cs`

---

### Task 1: Общие проверки и колесо

**Files:**
- Create: `src/core/Domain/RemoteDesktop/StreamSettingsGuard.cs`
- Create: `src/core/Domain/RemoteDesktop/ClipboardTextGuard.cs`
- Create: `src/core/Domain/RemoteDesktop/WheelMapping.cs`
- Create: `src/core/Domain/RemoteDesktop/Dto/RemoteStreamSettings.cs`
- Create: `src/core/Domain/RemoteDesktop/Dto/RemoteClipboard.cs`
- Modify: `src/core/Domain/RemoteDesktop/Dto/RemotePointer.cs`
- Test: `src/tests/Domain.Tests/StreamSettingsGuardTests.cs`
- Test: `src/tests/Domain.Tests/ClipboardTextGuardTests.cs`
- Test: `src/tests/Domain.Tests/WheelMappingTests.cs`

**Interfaces:**
- Consumes: ничего
- Produces: `StreamSettingsGuard.IsAllowed(int quality, int maxSide, int intervalMs)`, `ClipboardTextGuard.MaxChars = 32000`, `ClipboardTextGuard.IsAllowed(string? text)`, `WheelMapping.Consume(int remainder, int deltaY)` → `(int MouseData, int Remainder)`, DTO, `RemotePointer.Delta`

- [ ] **Step 1: Написать падающие тесты**

`StreamSettingsGuardTests` и `ClipboardTextGuardTests` — те же утверждения, что в плане fc Task 1 (пространство имён `Domain.Tests`).

`src/tests/Domain.Tests/WheelMappingTests.cs`:

```csharp
using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class WheelMappingTests
{
    [Fact]
    public void щелчок_вниз_даёт_минус_120()
    {
        var (mouseData, remainder) = WheelMapping.Consume(0, 100);

        Assert.Equal(-120, mouseData);
        Assert.Equal(-20, remainder);
    }

    [Fact]
    public void щелчок_вверх_даёт_плюс_120()
    {
        var (mouseData, remainder) = WheelMapping.Consume(0, -100);

        Assert.Equal(120, mouseData);
        Assert.Equal(20, remainder);
    }

    [Fact]
    public void мелкие_шаги_копятся_до_щелчка()
    {
        var (first, remainder) = WheelMapping.Consume(0, 40);
        var (second, next) = WheelMapping.Consume(remainder, 40);
        var (third, last) = WheelMapping.Consume(next, 40);

        Assert.Equal(0, first);
        Assert.Equal(0, second);
        Assert.Equal(-120, third);
        Assert.Equal(0, last);
    }

    [Fact]
    public void большой_шаг_даёт_несколько_щелчков()
    {
        var (mouseData, remainder) = WheelMapping.Consume(0, 250);

        Assert.Equal(-240, mouseData);
        Assert.Equal(10, remainder);
    }

    [Fact]
    public void ноль_не_тратит_остаток()
    {
        var (mouseData, remainder) = WheelMapping.Consume(30, 0);

        Assert.Equal(0, mouseData);
        Assert.Equal(30, remainder);
    }
}
```

- [ ] **Step 2: Убедиться, что тесты не собираются**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter "FullyQualifiedName~StreamSettingsGuardTests|FullyQualifiedName~ClipboardTextGuardTests|FullyQualifiedName~WheelMappingTests"`

Expected: FAIL, типы не найдены.

- [ ] **Step 3: Реализовать**

`StreamSettingsGuard` и `ClipboardTextGuard` — копия из плана fc Task 1, пространство имён `Domain.RemoteDesktop`.

DTO:

```csharp
namespace Domain.RemoteDesktop.Dto;

public class RemoteStreamSettings
{
    public Guid SessionId { get; set; }
    public int Quality { get; set; }
    public int MaxSide { get; set; }
    public int IntervalMs { get; set; }
}
```

```csharp
namespace Domain.RemoteDesktop.Dto;

public class RemoteClipboard
{
    public Guid SessionId { get; set; }
    public string Text { get; set; } = string.Empty;
}
```

В `RemotePointer` добавить `public int Delta { get; set; }`.

`src/core/Domain/RemoteDesktop/WheelMapping.cs`:

```csharp
namespace Domain.RemoteDesktop;

public static class WheelMapping
{
    public const int Notch = 120;
    public const int NotchThreshold = 100;

    /// <summary>
    /// Копит deltaY браузера. Положительный delta крутит вниз, а положительный mouseData Win32 — вверх.
    /// Шаг меньше 100 остаётся в remainder, иначе уходит один или несколько щелчков по 120.
    /// </summary>
    public static (int MouseData, int Remainder) Consume(int remainder, int deltaY)
    {
        var accumulated = remainder + deltaY;
        if (Math.Abs(accumulated) < NotchThreshold)
            return (0, accumulated);

        var sign = accumulated > 0 ? 1 : -1;
        var notches = Math.Max(1, Math.Abs(accumulated) / Notch);
        var leftover = accumulated - sign * notches * Notch;
        return (-sign * notches * Notch, leftover);
    }
}
```

- [ ] **Step 4: Прогнать тесты**

Run: команда из шага 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/core/Domain/RemoteDesktop/StreamSettingsGuard.cs src/core/Domain/RemoteDesktop/ClipboardTextGuard.cs src/core/Domain/RemoteDesktop/WheelMapping.cs src/core/Domain/RemoteDesktop/Dto/RemoteStreamSettings.cs src/core/Domain/RemoteDesktop/Dto/RemoteClipboard.cs src/core/Domain/RemoteDesktop/Dto/RemotePointer.cs src/tests/Domain.Tests/StreamSettingsGuardTests.cs src/tests/Domain.Tests/ClipboardTextGuardTests.cs src/tests/Domain.Tests/WheelMappingTests.cs
git commit -m "feat: контракт потока, буфера и щелчка колеса"
```

---

### Task 2: Интервал кадров

**Files:**
- Modify: `src/core/Domain/RemoteDesktop/FrameSendPolicy.cs`
- Modify: `src/tests/Domain.Tests/FrameSendPolicyTests.cs`
- Create: `src/infrastructure/Capture/StreamCaptureSettings.cs`

**Interfaces:**
- Consumes: `StreamSettingsGuard.IsAllowed`
- Produces: `FrameSendPolicy.SetIntervalMs(int intervalMs)`, `StreamCaptureSettings.Apply(RemoteStreamSettings)`, `StreamCaptureSettings.Quality`, `MaxSide`, `IntervalMs`. Старые тесты политики на 67 мс остаются зелёными.

- [ ] **Step 1: Дописать тесты интервала в `FrameSendPolicyTests`**

```csharp
[Fact]
public void интервал_200мс_режет_кадр_на_67мс()
{
    var policy = new FrameSendPolicy();
    policy.SetIntervalMs(200);
    var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    Assert.True(policy.ShouldSend(JpegA, now));
    Assert.False(policy.ShouldSend(JpegB, now.AddMilliseconds(67)));
    Assert.True(policy.ShouldSend(JpegB, now.AddMilliseconds(200)));
}

[Fact]
public void интервал_40мс_пропускает_следующий_кадр()
{
    var policy = new FrameSendPolicy();
    policy.SetIntervalMs(40);
    var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    Assert.True(policy.ShouldSend(JpegA, now));
    Assert.True(policy.ShouldSend(JpegB, now.AddMilliseconds(40)));
}

[Fact]
public void чужой_интервал_не_меняется()
{
    var policy = new FrameSendPolicy();
    policy.SetIntervalMs(16);
    var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    Assert.True(policy.ShouldSend(JpegA, now));
    Assert.False(policy.ShouldSend(JpegB, now.AddMilliseconds(30)));
    Assert.True(policy.ShouldSend(JpegB, now.AddMilliseconds(67)));
}
```

- [ ] **Step 2: Убедиться, что новый тест не собирается**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter "FullyQualifiedName~FrameSendPolicyTests"`

Expected: FAIL, нет `SetIntervalMs`.

- [ ] **Step 3: Интервал в политике и хранилище настроек**

В `FrameSendPolicy` заменить использование константы в ветке «другой хеш» на поле:

```csharp
private int _intervalMs = (int)MinInterval.TotalMilliseconds;

public void SetIntervalMs(int intervalMs)
{
    if (intervalMs is 40 or 67 or 200)
        _intervalMs = intervalMs;
}
```

В `ShouldSend` условие `utcNow - _lastSend < MinInterval` заменить на `utcNow - _lastSend < TimeSpan.FromMilliseconds(_intervalMs)`. `MinInterval` и `KeepaliveInterval` не удалять: пауза цикла и keepalive на них опираются, пока Task 4 не переведёт цикл на настройку.

`src/infrastructure/Capture/StreamCaptureSettings.cs`:

```csharp
using Domain.RemoteDesktop;
using Domain.RemoteDesktop.Dto;

namespace Capture;

public sealed class StreamCaptureSettings
{
    public const int DefaultQuality = 40;
    public const int DefaultMaxSide = 1280;
    public const int DefaultIntervalMs = 67;
    public const int FallbackQuality = 25;
    public const int MaxJpegBytes = 1_000_000;

    private int _quality = DefaultQuality;
    private int _maxSide = DefaultMaxSide;
    private int _intervalMs = DefaultIntervalMs;

    public int Quality => Volatile.Read(ref _quality);
    public int MaxSide => Volatile.Read(ref _maxSide);
    public int IntervalMs => Volatile.Read(ref _intervalMs);

    public void Apply(RemoteStreamSettings settings)
    {
        if (settings == null
            || !StreamSettingsGuard.IsAllowed(settings.Quality, settings.MaxSide, settings.IntervalMs))
            return;

        Volatile.Write(ref _quality, settings.Quality);
        Volatile.Write(ref _maxSide, settings.MaxSide);
        Volatile.Write(ref _intervalMs, settings.IntervalMs);
    }
}
```

- [ ] **Step 4: Прогнать тесты политики**

Run: команда из шага 2.

Expected: PASS, старые 6 тестов и 3 новых.

- [ ] **Step 5: Commit**

```bash
git add src/core/Domain/RemoteDesktop/FrameSendPolicy.cs src/tests/Domain.Tests/FrameSendPolicyTests.cs src/infrastructure/Capture/StreamCaptureSettings.cs
git commit -m "feat: интервал кадров удалённого доступа задаётся пресетом"
```

---

### Task 3: Качество JPEG

**Files:**
- Modify: `src/infrastructure/Capture/GdiScreenCapture.cs`
- Modify: `src/infrastructure/Capture/RegistrationExtension.cs`

**Interfaces:**
- Consumes: `StreamCaptureSettings.Quality`, `MaxSide`, `FallbackQuality`, `MaxJpegBytes`
- Produces: кодирование кадра читает настройки, а не константы `JpegQuality` и `MaxDimension`

DXGI уже вызывает `EncodeDesktopBitmap`, отдельный путь качества не нужен.

- [ ] **Step 1: Внедрить настройки в `GdiScreenCapture`**

Конструктор принимает `StreamCaptureSettings`. Удалить `private const long JpegQuality = 40` и `private const int MaxDimension = 1280`.

В `EncodeDesktopBitmap` масштаб брать из `_settings.MaxSide`, кодировать `_settings.Quality`.

```csharp
[SupportedOSPlatform("windows")]
private byte[] EncodeJpeg(Bitmap bitmap, long quality)
{
    var codec = ImageCodecInfo.GetImageEncoders()
        .First(item => item.FormatID == ImageFormat.Jpeg.Guid);

    using var encoderParameters = new EncoderParameters(1);
    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

    using var stream = new MemoryStream();
    bitmap.Save(stream, codec, encoderParameters);
    return stream.ToArray();
}

/// <summary>
/// Кодирует кадр текущим качеством. Если JPEG длиннее лимита хаба, один раз повторяет с качеством 25.
/// </summary>
[SupportedOSPlatform("windows")]
private byte[] EncodeWithinLimit(Bitmap bitmap)
{
    var jpeg = EncodeJpeg(bitmap, _settings.Quality);
    if (jpeg.Length <= StreamCaptureSettings.MaxJpegBytes)
        return jpeg;

    _logger.LogWarning(
        "JPEG {Bytes} байт длиннее {MaxBytes}, повтор с качеством {Quality}",
        jpeg.Length,
        StreamCaptureSettings.MaxJpegBytes,
        StreamCaptureSettings.FallbackQuality);

    jpeg = EncodeJpeg(bitmap, StreamCaptureSettings.FallbackQuality);
    if (jpeg.Length <= StreamCaptureSettings.MaxJpegBytes)
        return jpeg;

    _logger.LogWarning(
        "JPEG {Bytes} байт всё ещё длиннее {MaxBytes}, кадр не отправляем",
        jpeg.Length,
        StreamCaptureSettings.MaxJpegBytes);
    return [];
}
```

`EncodeDesktopBitmap` возвращает `EncodeWithinLimit` и для исходного, и для уменьшенного bitmap.

- [ ] **Step 2: Зарегистрировать singleton**

В `AddScreenCapture` первой строкой: `services.AddSingleton<StreamCaptureSettings>();`

`GdiScreenCapture` получит его из DI. `DxgiScreenCapture` создаёт `GdiScreenCapture` через конструктор — контейнер подставит зависимость сам, ручную передачу не добавлять.

- [ ] **Step 3: Собрать захват**

Run: `dotnet build src/infrastructure/Capture/Capture.csproj`

Expected: сборка без ошибок. Поведение по умолчанию не меняется, пока `Apply` не вызван.

- [ ] **Step 4: Commit**

```bash
git add src/infrastructure/Capture/GdiScreenCapture.cs src/infrastructure/Capture/RegistrationExtension.cs
git commit -m "feat: качество JPEG удалённого доступа берётся из пресета"
```

---

### Task 4: Стрим читает интервал

**Files:**
- Modify: `src/infrastructure/Capture/JpegSessionStreamer.cs`

**Interfaces:**
- Consumes: `StreamCaptureSettings.IntervalMs`, `FrameSendPolicy.SetIntervalMs`
- Produces: пауза цикла равна текущему интервалу, не короче времени самого захвата

- [ ] **Step 1: Передать настройки в стример**

Конструктор `JpegSessionStreamer` получает `StreamCaptureSettings`. В `RunAsync` перед циклом `policy.SetIntervalMs(_settings.IntervalMs)` недостаточно: пресет может смениться на лету. В начале каждой итерации:

```csharp
var intervalMs = _settings.IntervalMs;
policy.SetIntervalMs(intervalMs);
```

Обе задержки `Task.Delay(FrameSendPolicy.MinInterval, token)` заменить на `Task.Delay(intervalMs, token)`.

- [ ] **Step 2: Собрать RemoteApp**

Run: `dotnet build src/presentation/RemoteApp/RemoteApp.csproj`

Expected: сборка без ошибок.

- [ ] **Step 3: Commit**

```bash
git add src/infrastructure/Capture/JpegSessionStreamer.cs
git commit -m "feat: частота кадров удалённого доступа следует пресету"
```

---

### Task 5: Колесо в SendInput

**Files:**
- Modify: `src/infrastructure/Capture/NativeMethods.cs`
- Modify: `src/infrastructure/Capture/Win32MouseInput.cs`
- Modify: `src/infrastructure/CommanderExchange/SignalRRemoteClient.cs` — только фильтр `OnRemotePointer`

**Interfaces:**
- Consumes: `WheelMapping.Consume`, `RemotePointer.Ev`, `RemotePointer.Delta`, `MouseInput.mouseData`
- Produces: флаг `MOUSEEVENTF_WHEEL = 0x0800`, остаток колеса в `Win32MouseInput`

- [ ] **Step 1: Флаг и ветка wheel**

В `NativeMethods` рядом с остальными флагами мыши:

```csharp
public const uint MOUSEEVENTF_WHEEL = 0x0800;
```

`Win32MouseInput` хранит `private int _wheelRemainder`.

В `Apply` после сборки `flags` для down/up:

```csharp
var mouseData = 0u;
if (ev is "wheel")
{
    var (data, remainder) = WheelMapping.Consume(_wheelRemainder, pointer.Delta);
    _wheelRemainder = remainder;
    if (data == 0)
        return;

    flags = NativeMethods.MOUSEEVENTF_WHEEL;
    mouseData = unchecked((uint)data);
}
```

В структуру `MouseInput` записать `mouseData = mouseData`. Для move/down/up поле остаётся 0, флаги как сейчас. Абсолютные `dx`/`dy` для колеса тоже заполнять: курсор должен стоять над тем местом, куда смотрит оператор.

- [ ] **Step 2: Принять событие в клиенте**

В `OnRemotePointer` условие `pointer.Ev is not ("down" or "up" or "move")` заменить на отказ всего, кроме `"down"`, `"up"`, `"move"`, `"wheel"`.

- [ ] **Step 3: Собрать RemoteApp**

Run: `dotnet build src/presentation/RemoteApp/RemoteApp.csproj`

Expected: сборка без ошибок.

- [ ] **Step 4: Commit**

```bash
git add src/infrastructure/Capture/NativeMethods.cs src/infrastructure/Capture/Win32MouseInput.cs src/infrastructure/CommanderExchange/SignalRRemoteClient.cs
git commit -m "feat: прокрутка колеса на кассе из удалённой сессии"
```

---

### Task 6: Буфер кассы

**Files:**
- Create: `src/infrastructure/Capture/Win32Clipboard.cs`
- Modify: `src/infrastructure/Capture/NativeMethods.cs`
- Modify: `src/infrastructure/Capture/RegistrationExtension.cs`
- Modify: `src/infrastructure/CommanderExchange/SignalRRemoteClient.cs`

**Interfaces:**
- Consumes: `ClipboardTextGuard.IsAllowed`, `ShouldApplyInput`, `RemoteClipboard`
- Produces: `Win32Clipboard.WriteText(string text)`, `Win32Clipboard.ReadIfChanged()`, `Win32Clipboard.Arm()`, вызовы хаба `UploadClipboard` и обработчик `RemoteClipboard`

Опрос раз в 500 мс, только пока сессия запущена. `Arm` запоминает текущий номер последовательности и не шлёт текст, который уже лежал в буфере до старта.

- [ ] **Step 1: Объявления Win32**

В `NativeMethods`:

```csharp
public const uint CF_UNICODETEXT = 13;
public const uint GMEM_MOVEABLE = 0x0002;

[SupportedOSPlatform("windows")]
[DllImport("user32.dll")]
public static extern uint GetClipboardSequenceNumber();

[SupportedOSPlatform("windows")]
[DllImport("user32.dll", SetLastError = true)]
public static extern bool OpenClipboard(IntPtr hWndNewOwner);

[SupportedOSPlatform("windows")]
[DllImport("user32.dll")]
public static extern bool CloseClipboard();

[SupportedOSPlatform("windows")]
[DllImport("user32.dll")]
public static extern bool EmptyClipboard();

[SupportedOSPlatform("windows")]
[DllImport("user32.dll")]
public static extern IntPtr GetClipboardData(uint uFormat);

[SupportedOSPlatform("windows")]
[DllImport("user32.dll")]
public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

[SupportedOSPlatform("windows")]
[DllImport("kernel32.dll", SetLastError = true)]
public static extern IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

[SupportedOSPlatform("windows")]
[DllImport("kernel32.dll")]
public static extern IntPtr GlobalLock(IntPtr hMem);

[SupportedOSPlatform("windows")]
[DllImport("kernel32.dll")]
public static extern bool GlobalUnlock(IntPtr hMem);

[SupportedOSPlatform("windows")]
[DllImport("kernel32.dll")]
public static extern IntPtr GlobalFree(IntPtr hMem);
```

- [ ] **Step 2: `Win32Clipboard`**

```csharp
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Capture;

public sealed class Win32Clipboard(ILogger<Win32Clipboard> logger)
{
    private readonly object _sync = new();
    private uint _lastSequence;
    private string _suppressEcho = string.Empty;

    /// <summary>
    /// Запоминает номер буфера на старте сессии, чтобы не отправлять уже лежащий текст.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void Arm()
    {
        if (!OperatingSystem.IsWindows())
            return;

        lock (_sync)
            _lastSequence = NativeMethods.GetClipboardSequenceNumber();
    }

    [SupportedOSPlatform("windows")]
    public void WriteText(string text)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(text))
            return;

        var bytes = Encoding.Unicode.GetBytes(text + "\0");
        var memory = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (nuint)bytes.Length);
        if (memory == IntPtr.Zero)
        {
            logger.LogWarning("GlobalAlloc для буфера не выделил память, размер {Bytes}", bytes.Length);
            return;
        }

        var locked = NativeMethods.GlobalLock(memory);
        if (locked == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(memory);
            logger.LogWarning("GlobalLock для буфера не удался");
            return;
        }

        Marshal.Copy(bytes, 0, locked, bytes.Length);
        NativeMethods.GlobalUnlock(memory);

        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            NativeMethods.GlobalFree(memory);
            logger.LogWarning("OpenClipboard не открыл буфер для записи, код {Error}", Marshal.GetLastWin32Error());
            return;
        }

        try
        {
            NativeMethods.EmptyClipboard();
            if (NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, memory) == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(memory);
                logger.LogWarning("SetClipboardData не записал текст, код {Error}", Marshal.GetLastWin32Error());
                return;
            }

            memory = IntPtr.Zero;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }

        lock (_sync)
        {
            _suppressEcho = text;
            _lastSequence = NativeMethods.GetClipboardSequenceNumber();
        }
    }

    /// <summary>
    /// Возвращает новый текст буфера. Собственная запись и неизменный номер дают null.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public string? ReadIfChanged()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var sequence = NativeMethods.GetClipboardSequenceNumber();
        lock (_sync)
        {
            if (sequence == _lastSequence)
                return null;

            _lastSequence = sequence;
        }

        var text = ReadText();
        lock (_sync)
        {
            if (text == null || text == _suppressEcho)
                return null;
        }

        return text;
    }

    [SupportedOSPlatform("windows")]
    private string? ReadText()
    {
        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
            return null;

        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
                return null;

            var locked = NativeMethods.GlobalLock(handle);
            if (locked == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUni(locked);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }
}
```

`SetClipboardData` при успехе забирает память себе, поэтому `GlobalFree` после успеха не вызывать. `memory = IntPtr.Zero` это фиксирует.

- [ ] **Step 3: Подписка и опрос в `SignalRRemoteClient`**

Зарегистрировать `services.AddSingleton<Win32Clipboard>()`. Добавить параметр конструктора `Win32Clipboard clipboard`.

Рядом с подпиской `RemotePointer`:

```csharp
_connection.On<RemoteStreamSettings>("RemoteStreamSettings", OnRemoteStreamSettings);
_connection.On<RemoteClipboard>("RemoteClipboard", OnRemoteClipboard);
```

```csharp
private void OnRemoteStreamSettings(RemoteStreamSettings settings)
{
    try
    {
        if (settings == null || !ShouldApplyInput(settings.SessionId))
            return;

        _captureSettings.Apply(settings);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка обработки RemoteStreamSettings");
    }
}

private void OnRemoteClipboard(RemoteClipboard clipboard)
{
    try
    {
        if (clipboard == null
            || !ShouldApplyInput(clipboard.SessionId)
            || !ClipboardTextGuard.IsAllowed(clipboard.Text)
            || !OperatingSystem.IsWindows())
            return;

        _clipboard.WriteText(clipboard.Text);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка обработки RemoteClipboard");
    }
}
```

Для этого клиенту нужен `StreamCaptureSettings _captureSettings` в конструкторе. Тот же singleton, что у захвата.

Опрос:

```csharp
private CancellationTokenSource? _clipboardCts;

private void StartClipboardWatch(Guid sessionId)
{
    StopClipboardWatch();
    if (OperatingSystem.IsWindows())
        _clipboard.Arm();

    var cts = new CancellationTokenSource();
    _clipboardCts = cts;
    _ = WatchClipboardAsync(sessionId, cts.Token);
}

private void StopClipboardWatch()
{
    _clipboardCts?.Cancel();
    _clipboardCts = null;
}

private async Task WatchClipboardAsync(Guid sessionId, CancellationToken token)
{
    try
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(500, token);
            if (!ShouldApplyInput(sessionId) || !OperatingSystem.IsWindows())
                continue;

            var text = _clipboard.ReadIfChanged();
            if (!ClipboardTextGuard.IsAllowed(text))
                continue;

            await UploadClipboardAsync(sessionId, text!);
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка опроса буфера сессии {SessionId}", sessionId);
    }
}

private async Task UploadClipboardAsync(Guid sessionId, string text)
{
    try
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
            return;

        await _connection.SendAsync("UploadClipboard", new RemoteClipboard
        {
            SessionId = sessionId,
            Text = text
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка отправки буфера сессии {SessionId}", sessionId);
    }
}
```

`StartClipboardWatch(sessionId)` вызывать сразу после `_streamer.Start` в `OnRemoteSessionStart` и в `TryResumeSessionAsync`. `StopClipboardWatch()` вызывать рядом с каждым `_streamer.Stop`: `OnRemoteSessionStop`, `OnConnectionClosed`, `Reconnecting`, `StopAsync`.

Сразу после `StartClipboardWatch` отправить возможности. Это единственный сигнал Командеру показать качество и буфер. Старый агент метод не вызывает, и контролы остаются скрытыми.

```csharp
private async Task ReportCapabilitiesAsync(Guid sessionId)
{
    try
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
            return;

        await _connection.SendAsync("ReportCapabilities", new RemoteCapabilities
        {
            SessionId = sessionId,
            StreamSettings = true,
            Clipboard = true
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка отправки возможностей сессии {SessionId}", sessionId);
    }
}
```

Файл DTO — `src/core/Domain/RemoteDesktop/Dto/RemoteCapabilities.cs`, содержимое из раздела «Контракт». Добавить его в commit этого шага.

- [ ] **Step 4: Собрать и прогнать тесты домена**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj`

Expected: PASS, включая прежние тесты политики и координат.

- [ ] **Step 5: Проверить на кассе**

Нужны обновлённые `fc` и `fc-remote`.

- Среднее/обычно совпадает с нынешней картинкой. Высокое/быстро делает кадр резче и чаще, низкое/медленно — мягче и реже. В логе нет текста буфера.
- Щелчок колеса над списком Frontol прокручивает его на один шаг. Страница Командера при этом не едет.
- `Ctrl+V` в поле на кассе вставляет текст, скопированный у оператора.
- Копирование текста на кассе даёт в окне одно сообщение «Буфер с кассы получен» и заполняет поле. Повторное копирование той же строки молчит. Текст, пришедший с оператора, назад сообщением не уходит.
- После остановки сессии опрос буфера прекращается: смена буфера на кассе больше не шлёт `UploadClipboard`.

- [ ] **Step 6: Commit**

```bash
git add src/core/Domain/RemoteDesktop/Dto/RemoteCapabilities.cs src/infrastructure/Capture/Win32Clipboard.cs src/infrastructure/Capture/NativeMethods.cs src/infrastructure/Capture/RegistrationExtension.cs src/infrastructure/CommanderExchange/SignalRRemoteClient.cs
git commit -m "feat: текстовый буфер кассы в сессии удалённого доступа"
```
