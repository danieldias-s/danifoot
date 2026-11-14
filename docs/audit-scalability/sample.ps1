param([int]$ServerPid, [string]$OutputPath)

$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class AuditIo {
    [StructLayout(LayoutKind.Sequential)]
    public struct Counters {
        public ulong Reads, Writes, Other, ReadBytes, WriteBytes, OtherBytes;
    }
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool GetProcessIoCounters(IntPtr process, out Counters counters);
}
'@
while ($true) {
    $process = Get-Process -Id $ServerPid -ErrorAction SilentlyContinue
    if (!$process) { break }
    $io = [AuditIo+Counters]::new()
    if (![AuditIo]::GetProcessIoCounters($process.Handle, [ref]$io)) { throw 'GetProcessIoCounters falhou.' }
    [ordered]@{
        ms = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        cpuSec = $process.TotalProcessorTime.TotalSeconds
        rss = $process.WorkingSet64
        privateBytes = $process.PrivateMemorySize64
        reads = $io.Reads
        writes = $io.Writes
        readBytes = $io.ReadBytes
        writeBytes = $io.WriteBytes
    } | ConvertTo-Json -Compress | Add-Content -LiteralPath $OutputPath
    Start-Sleep -Milliseconds 200
}
