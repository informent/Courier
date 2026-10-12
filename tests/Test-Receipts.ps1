param([Parameter(Mandatory=$true)][string]$Exe)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('courier-packaged-receipts-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
function Invoke-Receipt([string]$Arguments, [int]$Expected) {
    $process = Start-Process -FilePath $Exe -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'Receipt command timed out' }
    if ($process.ExitCode -ne $Expected) { throw ('Receipt command exit: ' + $process.ExitCode) }
}
try {
    $file = Join-Path $root 'renamed.bin'
    [IO.File]::WriteAllText($file, 'confirmed content')
    $history = Join-Path $root 'history.json'
    $receipt = @{Direction='Received';CompletedAt=[DateTimeOffset]::UtcNow.ToString('o');Name='original.bin';Length=(Get-Item $file).Length;Sha256=(Get-FileHash $file).Hash}
    [IO.File]::WriteAllText($history, (ConvertTo-Json -InputObject @($receipt)))
    $export = Join-Path $root 'export.json'
    Invoke-Receipt ('--export-history "' + $history + '" --report "' + $export + '"') 0
    $report = Join-Path $root 'match.json'
    $arguments = '--verify-receipt "' + $export + '" 1 "' + $file + '" --report "' + $report + '"'
    Invoke-Receipt $arguments 0
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if (!$result.Matches -or $result.ActualSha256 -ne $receipt.Sha256) { throw 'Receipt verification mismatch' }
    $digest = (Get-FileHash $report).Hash
    Invoke-Receipt $arguments 2
    if ((Get-FileHash $report).Hash -ne $digest) { throw 'Report overwritten' }
    [IO.File]::WriteAllText($file, 'changed content')
    $changed = Join-Path $root 'changed.json'
    Invoke-Receipt ('--verify-receipt "' + $export + '" 1 "' + $file + '" --report "' + $changed + '"') 3
    if ((Get-Content -LiteralPath $changed -Raw | ConvertFrom-Json).Matches) { throw 'Changed file matched' }
    Write-Output 'PASS: packaged receipt export, renamed-file verification, overwrite protection and mismatch exit'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or !([IO.Path]::GetFileName($resolved)).StartsWith('courier-packaged-receipts-')) { throw 'Unsafe cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
