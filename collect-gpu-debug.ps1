# collect-gpu-debug.ps1 — read-only GPU detection collector for TimeNotice beta.
# Answers: does Windows itself report per-process GPU share for the game,
# and does the fullscreen check hold steady while you play?
#
# Run ON THE GAMING PC while Dota 2 is focused (fullscreen or borderless):
#   powershell -ExecutionPolicy Bypass -File collect-gpu-debug.ps1 -ExeName dota2 -Seconds 40
# Then paste collect-gpu-debug-output.txt back here. Changes nothing on the PC.
param([string]$ExeName = "dota2", [int]$Seconds = 40)

$ErrorActionPreference = "SilentlyContinue"
$out = Join-Path $PSScriptRoot "collect-gpu-debug-output.txt"
"" | Out-File $out  # fresh file each run

function Emit([string]$s) { $s | Tee-Object -FilePath $out -Append | Out-String | Write-Host -NoNewline }

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class FG {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@
Add-Type -AssemblyName System.Windows.Forms

Emit "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') exe=$ExeName seconds=$Seconds"
try {
  $os = Get-CimInstance Win32_OperatingSystem
  Emit "os=$($os.Caption) build=$($os.BuildNumber)"
  foreach ($v in (Get-CimInstance Win32_VideoController)) { Emit "gpu=$($v.Name) driver=$($v.DriverVersion)" }
} catch { Emit "os/gpu query failed: $_" }

$procs = Get-Process -Name $ExeName
if (-not $procs) { Emit "GAME NOT RUNNING: no process named '$ExeName' (start Dota first, then rerun)"; return }
$tags = $procs | ForEach-Object { "pid_$($_.Id)_" }
Emit "game pids=$($procs.Id -join ',')"

try {
  $cat = New-Object System.Diagnostics.PerformanceCounterCategory("GPU Engine")
  $names = $cat.GetInstanceNames()
} catch { Emit "NO GPU Engine category on this PC (VM or driver without per-process counters)"; return }
Emit "engine instances=$($names.Count)"
$match = @($names | Where-Object { $n = $_; ($tags | Where-Object { $n -like "*$_*" }).Count -gt 0 })
Emit "matching this game=$($match.Count)"
$match | Select-Object -First 10 | ForEach-Object { Emit "  inst=$_" }

# One counter per instance; first NextValue() only primes (always 0), real readings come after.
$all = @{}
foreach ($n in $names) {
  try { $c = New-Object System.Diagnostics.PerformanceCounter("GPU Engine", "Utilization Percentage", $n, $true); $c.NextValue() | Out-Null; $all[$n] = $c } catch { }
}
Start-Sleep -Seconds 1

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$ticks = [math]::Max(1, [math]::Floor($Seconds / 5))
for ($i = 1; $i -le $ticks; $i++) {
  Start-Sleep -Seconds 5
  $fg = "?"; $fs = "?"; $mine = 0.0; $total = 0.0
  try {
    $h = [FG]::GetForegroundWindow()
    $fgPid = 0; [void][FG]::GetWindowThreadProcessId($h, [ref]$fgPid)
    $p = Get-Process -Id $fgPid; $fg = "$($p.ProcessName).exe"
    $r = New-Object FG+RECT; [void][FG]::GetWindowRect($h, [ref]$r)
    $tol = 2
    $fs = [math]::Abs($r.Left - $b.Left) -le $tol -and [math]::Abs($r.Top - $b.Top) -le $tol -and `
          [math]::Abs($r.Right - $b.Right) -le $tol -and [math]::Abs($r.Bottom - $b.Bottom) -le $tol
  } catch { }
  foreach ($kv in $all.GetEnumerator()) {
    try { $v = $kv.Value.NextValue() } catch { continue }
    $total += $v
    foreach ($t in $tags) { if ($kv.Key -like "*$t*") { $mine += $v; break } }
  }
  Emit ("t={0}s fg={1} fs={2} game-gpu={3:N0}% total-gpu={4:N0}%" -f ($i * 5), $fg, $fs, $mine, $total)
}
Emit "done -> $out"
