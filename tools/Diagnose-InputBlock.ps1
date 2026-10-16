<#
.SYNOPSIS
    Диагностика блокировки удалённого ввода на кассе (UIPI / уровень целостности / Secure desktop).

.DESCRIPTION
    Скрипт НИЧЕГО НЕ МЕНЯЕТ в системе: только читает и пишет отчёт.
    Отвечает на вопросы:
      1. Какие процессы сейчас запущены и с каким уровнем целостности (IL).
      2. Является ли кассир администратором (от этого зависит, можно ли поднять агент до High IL).
      3. Можно ли получить НЕотфильтрованный (elevated) токен для сессии кассира.
      4. Какая программа удалённого доступа стоит и от какого имени работает (для сравнения).
      5. Настройки UAC и политики авто-повышения диспетчера задач.
      6. Есть ли в логе fc-remote признаки блокировки SendInput.

    Запускать НА КАССЕ. Лучше от администратора (тогда видны все процессы),
    но запуск от кассира тоже даёт полезный результат.

.PARAMETER ProcessNames
    Имена процессов для замера уровня целостности.

.PARAMETER OutFile
    Путь к файлу отчёта. По умолчанию — на рабочем столе.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Diagnose-InputBlock.ps1

.EXAMPLE
    # Если диспетчер задач запускается не так, как ожидалось — сравнить с блокнотом:
    .\Diagnose-InputBlock.ps1 -ProcessNames fc-remote,taskmgr,notepad
#>

[CmdletBinding()]
param(
    [string[]] $ProcessNames = @('fc-remote', 'winlogon', 'taskmgr', 'mmc', 'explorer'),
    [string]   $OutFile
)

$ErrorActionPreference = 'Continue'

# ---------------------------------------------------------------------------
#  Нативный доступ: уровень целостности токена процесса
# ---------------------------------------------------------------------------
if (-not ('Win32Integrity' -as [type])) {
    Add-Type -Namespace Win32 -Name Integrity -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)]
public struct SID_AND_ATTRIBUTES { public IntPtr Sid; public uint Attributes; }

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

[DllImport("advapi32.dll", SetLastError = true)]
static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

[DllImport("advapi32.dll", SetLastError = true)]
static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, uint infoLength, out uint returnLength);

[DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr stringSid);

[DllImport("kernel32.dll")]
static extern bool CloseHandle(IntPtr handle);

[DllImport("kernel32.dll")]
static extern IntPtr LocalFree(IntPtr handle);

const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
const uint TOKEN_QUERY = 0x0008;
const int  TokenIntegrityLevel = 25;

// Возвращает RID уровня целостности из SID, например 0x2000 (Medium).
public static int GetIntegrityRid(int pid)
{
    IntPtr process = IntPtr.Zero, token = IntPtr.Zero, buffer = IntPtr.Zero, stringSid = IntPtr.Zero;
    try
    {
        process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return -1;
        if (!OpenProcessToken(process, TOKEN_QUERY, out token)) return -1;

        uint needed;
        GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out needed);
        if (needed == 0) return -1;

        buffer = Marshal.AllocHGlobal((int)needed);
        if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, needed, out needed)) return -1;

        SID_AND_ATTRIBUTES sa = (SID_AND_ATTRIBUTES)Marshal.PtrToStructure(buffer, typeof(SID_AND_ATTRIBUTES));
        if (!ConvertSidToStringSid(sa.Sid, out stringSid)) return -1;

        string s = Marshal.PtrToStringUni(stringSid);
        if (string.IsNullOrEmpty(s)) return -1;

        int dash = s.LastIndexOf('-');
        int rid;
        return (dash > 0 && int.TryParse(s.Substring(dash + 1), out rid)) ? rid : -1;
    }
    catch { return -1; }
    finally
    {
        if (stringSid != IntPtr.Zero) LocalFree(stringSid);
        if (buffer    != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        if (token     != IntPtr.Zero) CloseHandle(token);
        if (process   != IntPtr.Zero) CloseHandle(process);
    }
}
'@
}

function ConvertFrom-IntegrityRid {
    param([int] $Rid)
    switch ($Rid) {
        -1    { 'нет доступа (запусти от администратора)' }
        0x0000 { 'Untrusted' }
        0x1000 { 'Low' }
        0x2000 { 'Medium' }
        0x2100 { 'Medium Plus' }
        0x3000 { 'High' }
        0x4000 { 'System' }
        0x5000 { 'Protected' }
        default { "неизвестный (0x{0:X4})" -f $Rid }
    }
}

function Get-ProcessIntegrity {
    param([string] $Name)
    $lines = @()
    $procs = @(Get-Process -Name $Name -ErrorAction SilentlyContinue)
    if ($procs.Count -eq 0) { return ,@("$Name : не запущен") }
    foreach ($p in $procs) {
        $rid  = [Win32.Integrity]::GetIntegrityRid($p.Id)
        $name = ConvertFrom-IntegrityRid $rid
        $lines += ("{0} (PID {1}) : {2}" -f $p.ProcessName, $p.Id, $name)
    }
    return ,$lines
}

# ---------------------------------------------------------------------------
#  Сбор данных
# ---------------------------------------------------------------------------
$pairs = [ordered]@{}

$pairs['Когда']            = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
$pairs['Компьютер']        = "$env:COMPUTERNAME"
$pairs['Пользователь']     = "$env:USERDOMAIN\$env:USERNAME"
$osName = $null
try { $osName = (Get-CimInstance Win32_OperatingSystem -ErrorAction Stop).Caption } catch { }
if (-not $osName) { $osName = "$([Environment]::OSVersion.VersionString) (WMI недоступен)" }
$pairs['ОС']               = $osName

# 1. Уровни целостности интересующих процессов
$integrityLines = @()
foreach ($n in $ProcessNames) { $integrityLines += (Get-ProcessIntegrity $n) }
$pairs['Уровень целостности процессов'] = ($integrityLines -join "`n")

# 2. Сессия и учётка
$sessionId = (Get-Process -Id $PID).SessionId
$pairs['SessionId этого процесса'] = "$sessionId"

$isAdminGroup = $false
try {
    $me = [Security.Principal.WindowsIdentity]::GetCurrent()
    $isAdminGroup = (New-Object Security.Principal.WindowsPrincipal($me)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
} catch { }
$inAdminsGroup = $false
try {
    $grp = Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction Stop |
           Where-Object { $_.Name -like "*\$env:USERNAME" }
    $inAdminsGroup = [bool] $grp
} catch { }

$pairs['Учётка входит в группу Администраторы'] = if ($inAdminsGroup) { 'ДА' } else { 'НЕТ (или не удалось определить)' }
$pairs['Этот PowerShell запущен с повышенными правами'] = if ($isAdminGroup) { 'ДА' } else { 'НЕТ' }

# 3. Можно ли получить неотфильтрованный токен сессии кассира
$wtsOk = $false
if ($sessionId -ne 0) {
    try {
        if (-not ('Win32Wts' -as [type])) {
            Add-Type -Namespace Win32 -Name Wts -MemberDefinition @'
[DllImport("wtsapi32.dll", SetLastError = true)]
public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);
[DllImport("kernel32.dll")]
public static extern bool CloseHandle(IntPtr handle);
'@
        }
        $t = [IntPtr]::Zero
        $wtsOk = [Win32.Wts]::WTSQueryUserToken([uint32] $sessionId, [ref] $t)
        if ($t -ne [IntPtr]::Zero) { [void] [Win32.Wts]::CloseHandle($t) }
    } catch { $wtsOk = $false }
}
$pairs['WTSQueryUserToken для сессии кассира'] =
    if ($sessionId -eq 0) { 'н/д (Session 0)' }
    elseif ($wtsOk)       { 'успешно — токен сессии доступен' }
    else                  { "не удалось (код $([Runtime.InteropServices.Marshal]::GetLastWin32Error()))" }

# 4. Программы удалённого доступа — для сравнения
$remoteLines = @()
try {
    $remoteLines += Get-CimInstance Win32_Service -ErrorAction Stop |
        Where-Object { $_.PathName -match 'radmin|anydesk|teamviewer|rustdesk|vnc|dwagent|aeroadmin|litemanager|supremo|parsec|screenconnect' } |
        ForEach-Object { "служба: {0}  →  {1}   [StartName={2}]" -f $_.Name, $_.PathName, $_.StartName }
    $remoteLines += Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -match 'radmin|anydesk|teamviewer|rustdesk|vnc|dwagent|aeroadmin|litemanager|supremo|parsec|screenconnect' } |
        ForEach-Object {
            $rid = [Win32.Integrity]::GetIntegrityRid($_.Id)
            "процесс: {0} (PID {1}, сессия {2}) : {3}" -f $_.ProcessName, $_.Id, $_.SessionId, (ConvertFrom-IntegrityRid $rid)
        }
} catch { }
$pairs['Программы удалённого доступа'] = if ($remoteLines) { $remoteLines -join "`n" } else { 'не найдены по типовым именам' }

# 5. UAC и политики
$uac = @()
try {
    $k = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
    $p = Get-ItemProperty -Path $k -ErrorAction Stop
    $uac += "EnableLUA                  = $($p.EnableLUA)      (1 = UAC включён)"
    $uac += "ConsentPromptBehaviorAdmin = $($p.ConsentPromptBehaviorAdmin)      (0 = повышать без запроса, 5 = запрос согласия)"
    $uac += "PromptOnSecureDesktop      = $($p.PromptOnSecureDesktop)      (1 = запрос на защищённом столе)"
    $uac += "EnableUIADesktopToggle     = $($p.EnableUIADesktopToggle)"
    $uac += "FilterAdministratorToken   = $($p.FilterAdministratorToken)"
} catch { $uac += "не удалось прочитать $k" }
try {
    $tm = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name EnableLUA -ErrorAction SilentlyContinue
    $auto = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers' -ErrorAction SilentlyContinue
    $runAs = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System' -Name DisableTaskMgr -ErrorAction SilentlyContinue
    $uac += "DisableTaskMgr             = $($runAs.DisableTaskMgr)"
} catch { }
$pairs['Настройки UAC'] = $uac -join "`n"

# 6. Логи fc-remote
$logDir = Join-Path $env:ProgramData 'Automation\fc-remote\log'
$logInfo = @()
if (Test-Path $logDir) {
    $logInfo += "Каталог: $logDir"
    $latest = Get-ChildItem $logDir -Filter 'fc-remote-*.log' -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        $logInfo += "Последний файл: $($latest.Name) (изменён $($latest.LastWriteTime))"
        $hits = Select-String -Path $latest.FullName -Pattern 'SendInput' -ErrorAction SilentlyContinue
        if ($hits) {
            $logInfo += "Найдено упоминаний SendInput: $($hits.Count). Последние 5:"
            $logInfo += ($hits | Select-Object -Last 5 | ForEach-Object { "  " + $_.Line.Trim() })
        } else {
            $logInfo += 'Упоминаний SendInput в логе нет (ожидаемо при блокировке UIPI — она не логируется).'
        }
    } else {
        $logInfo += 'Файлов fc-remote-*.log не найдено.'
    }
} else {
    $logInfo += "Каталог логов не найден: $logDir"
}
$pairs['Логи fc-remote'] = $logInfo -join "`n"

# ---------------------------------------------------------------------------
#  Отчёт
# ---------------------------------------------------------------------------
$bar = '=' * 78
$sb = New-Object System.Text.StringBuilder
[void] $sb.AppendLine($bar)
[void] $sb.AppendLine(' ДИАГНОСТИКА БЛОКИРОВКИ УДАЛЁННОГО ВВОДА (fc-remote)')
[void] $sb.AppendLine($bar)
foreach ($key in $pairs.Keys) {
    [void] $sb.AppendLine()
    [void] $sb.AppendLine("--- $key ---")
    [void] $sb.AppendLine($pairs[$key])
}
[void] $sb.AppendLine()
[void] $sb.AppendLine($bar)
[void] $sb.AppendLine(' КАК ЧИТАТЬ РЕЗУЛЬТАТ')
[void] $sb.AppendLine($bar)
[void] $sb.AppendLine(' taskmgr : High,  fc-remote : Medium')
[void] $sb.AppendLine('     → UIPI. Ввод из Medium в High Windows молча отбрасывает.')
[void] $sb.AppendLine('       Лечится переходом на токен winlogon (см. ниже).')
[void] $sb.AppendLine(' fc-remote : System,  winlogon : System')
[void] $sb.AppendLine('     → агент уже запущен через токен winlogon: UIPI ввод не блокирует.')
[void] $sb.AppendLine('       Если повышенные окна всё равно не слушаются — дело не в целостности,')
[void] $sb.AppendLine('       а в привязке потока к input desktop (OpenInputDesktop + SetThreadDesktop).')
[void] $sb.AppendLine(' fc-remote : System, но терпит неудачу запуск на столе')
[void] $sb.AppendLine('     → смотрите лог fc-remote: "OpenProcess winlogon.exe ... не удался".')
[void] $sb.AppendLine('       Значит служба host запущена НЕ как LocalSystem и нет SeDebugPrivilege.')
[void] $sb.AppendLine(' taskmgr : Medium, fc-remote : Medium, но ввод не доходит')
[void] $sb.AppendLine('     → это НЕ UIPI. Смотрим гонки стримера, фокус окна, DPI/координаты.')
[void] $sb.AppendLine(' fc-remote : High')
[void] $sb.AppendLine('     → агент уже повышен; если ввод всё равно не идёт, дело не в UIPI.')
[void] $sb.AppendLine(' ConsentPromptBehaviorAdmin = 5 и PromptOnSecureDesktop = 1')
[void] $sb.AppendLine('     → при повышении taskmgr будет запрос UAC на защищённом столе:')
[void] $sb.AppendLine('       его не видно и не нажать ничем, кроме SYSTEM-процесса.')
[void] $sb.AppendLine(' Программа удалённого доступа работает от SYSTEM (StartName=LocalSystem)')
[void] $sb.AppendLine('     → именно поэтому в ней такого блока нет: SYSTEM выше любого UAC.')
[void] $sb.AppendLine($bar)

$text = $sb.ToString()
Write-Host $text

if (-not $OutFile) {
    $OutFile = Join-Path ([Environment]::GetFolderPath('Desktop')) ("fc-remote-diag-{0:yyyyMMdd-HHmmss}.txt" -f (Get-Date))
}
try {
    $text | Out-File -FilePath $OutFile -Encoding UTF8
    Write-Host "Отчёт сохранён: $OutFile" -ForegroundColor Green
} catch {
    Write-Host "Не удалось сохранить отчёт: $($_.Exception.Message)" -ForegroundColor Yellow
}
