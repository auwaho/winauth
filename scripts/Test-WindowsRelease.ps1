#requires -Version 7.0
<# Run on a clean Windows profile. No config is supplied: WinAuth loads an empty
in-memory config. An external XML would use and modify the user's registry.
Only this invocation's process and temporary directory are cleaned up. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ArchivePath,
    [ValidateRange(5, 120)][int] $TimeoutSeconds = 30
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This smoke test requires Windows and PowerShell 7.' }
$archivePath = (Get-Item -LiteralPath $ArchivePath).FullName
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    if ($archive.Entries.Count -ne 1 -or $archive.Entries[0].FullName -cne 'WinAuth.exe') {
        throw 'Release ZIP must contain exactly one root entry named WinAuth.exe.'
    }
    if ($archive.Entries[0].Length -eq 0) { throw 'WinAuth.exe in the release ZIP is empty.' }
} finally { $archive.Dispose() }

if (Get-Process -Name WinAuth -ErrorAction SilentlyContinue) { throw 'A WinAuth process is already running.' }
$appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
if (-not $appData -or (Test-Path -LiteralPath (Join-Path $appData 'WinAuth')) -or
    (Test-Path -LiteralPath 'HKCU:\Software\WinAuth') -or (Test-Path -LiteralPath 'HKCU:\Software\WinAuth3')) {
    throw 'Use a clean Windows profile: existing WinAuth application data/registry settings will not be modified.'
}

# A matching title alone could be an error dialog. Require an enabled,
# responding WinForms main window and reject other visible titled windows.
# Native window checks also work without UI Automation or an input desktop.
if ($null -eq ('WinAuthReleaseSmoke.WindowProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
namespace WinAuthReleaseSmoke {
    public static class WindowProbe {
        private delegate bool Callback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int maximum);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        public static string Check(int processId) {
            bool ready = false;
            string error = null;
            EnumWindows(delegate(IntPtr window, IntPtr parameter) {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner != (uint)processId || !IsWindowVisible(window)) return true;
                var title = new StringBuilder(512); var name = new StringBuilder(256);
                GetWindowText(window, title, title.Capacity); GetClassName(window, name, name.Capacity);
                bool main = title.ToString() == "WinAuth" && name.ToString().StartsWith("WindowsForms10.Window.", StringComparison.Ordinal);
                if (!main && (title.Length > 0 || name.ToString() == "#32770")) error = "Unexpected dialog: " + title;
                IntPtr result;
                if (main && IsWindowEnabled(window) && SendMessageTimeout(window, 0, IntPtr.Zero, IntPtr.Zero, 3, 100, out result) != IntPtr.Zero) ready = true;
                return true;
            }, IntPtr.Zero);
            return error ?? (ready ? "READY" : "WAIT");
        }
    }
}
'@
}
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$smokeRoot = Join-Path $tempParent ('WinAuth-release-smoke-' + [Guid]::NewGuid().ToString('N'))
$process = $null
try {
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $smokeRoot)
    $startInfo = [Diagnostics.ProcessStartInfo]::new((Join-Path $smokeRoot 'WinAuth.exe'))
    $startInfo.UseShellExecute = $false
    $startInfo.WorkingDirectory = $smokeRoot
    $process = [Diagnostics.Process]::Start($startInfo)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $readySince = $null
    do {
        if ($process.HasExited) { throw "WinAuth.exe exited during startup (code $($process.ExitCode))." }
        $state = [WinAuthReleaseSmoke.WindowProbe]::Check($process.Id)
        if ($state -notin @('READY', 'WAIT')) { throw $state }
        if ($state -eq 'READY') {
            if ($null -eq $readySince) { $readySince = $timer.Elapsed.TotalSeconds }
            if (($timer.Elapsed.TotalSeconds - $readySince) -ge 1) { break }
        } else { $readySince = $null }
        if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw "WinAuth main window did not become ready within $TimeoutSeconds seconds." }
        Start-Sleep -Milliseconds 250
    } while ($true)
} finally {
    try {
        # FormClosing saves a config to AppData. Terminate only our empty test
        # instance to avoid that write, including when an error dialog is open.
        if ($null -ne $process) {
            if (-not $process.HasExited) { $process.Kill(); $null = $process.WaitForExit(5000) }
            $process.Dispose()
        }
    } finally {
        $cleanupPath = [IO.Path]::GetFullPath($smokeRoot)
        if (-not $cleanupPath.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($cleanupPath) -notmatch '^WinAuth-release-smoke-[0-9a-f]{32}$') { throw 'Invalid smoke cleanup path.' }
        if (Test-Path -LiteralPath $cleanupPath) { Remove-Item -LiteralPath $cleanupPath -Recurse -Force }
    }
}
Write-Host 'PASS: ZIP contains only root WinAuth.exe; its main window started from a clean directory/profile.'
