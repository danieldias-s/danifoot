param([Parameter(Mandatory)][string]$OutputPath)

$counters = @(
    '\PhysicalDisk(0 D:)\Transferências de disco/s'
    '\PhysicalDisk(0 D:)\Bytes de disco/s'
    '\PhysicalDisk(0 D:)\% tempo de disco'
    '\PhysicalDisk(0 D:)\Comprimento da fila de disco atual'
)
Get-Counter -Counter $counters -SampleInterval 1 -Continuous | ForEach-Object {
    $sample = $_.CounterSamples
    [ordered]@{
        ms = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        transfersSec = $sample[0].CookedValue
        bytesSec = $sample[1].CookedValue
        diskTimePct = $sample[2].CookedValue
        queue = $sample[3].CookedValue
    } | ConvertTo-Json -Compress | Add-Content -LiteralPath $OutputPath
}
