$repoRoot = Resolve-Path "$PSScriptRoot\.."
$pythonScript = Join-Path $repoRoot "scripts\GenerateFacadeLibrary.py"
$rhinoExe = "C:\Program Files\Rhino 8\System\Rhino.exe"

if (!(Test-Path $rhinoExe)) {
    Write-Host "Could not find Rhino: $rhinoExe"
    exit 1
}

if (!(Test-Path $pythonScript)) {
    Write-Host "Could not find Python script: $pythonScript"
    exit 1
}

Write-Host ""
Write-Host "Generate facade library"
Write-Host ""
Write-Host "[1] Skip existing images"
Write-Host "[2] Regenerate everything"
Write-Host ""

$choice = Read-Host "Choice"

if ($choice -eq "2") {
    $env:LICHEN_FACADE_MODE = "RedoAll"
} else {
    $env:LICHEN_FACADE_MODE = "SkipExisting"
}

$env:LICHEN_CLOSE_RHINO = "1"

$command = "_-RunPythonScript `"$pythonScript`""

Write-Host ""
Write-Host "Mode: $env:LICHEN_FACADE_MODE"
Write-Host "Launching Rhino..."

$process = Start-Process -FilePath $rhinoExe -ArgumentList "/nosplash" -PassThru

Start-Sleep -Seconds 8

Add-Type -AssemblyName System.Windows.Forms
$wshell = New-Object -ComObject WScript.Shell

$activated = $false
for ($i = 0; $i -lt 30; $i++) {
    if ($wshell.AppActivate($process.Id)) {
        $activated = $true
        break
    }

    Start-Sleep -Seconds 1
}

if (-not $activated) {
    Write-Host "Could not activate Rhino window."
    exit 1
}

Start-Sleep -Milliseconds 500

Set-Clipboard -Value $command
[System.Windows.Forms.SendKeys]::SendWait("^v")
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")

Write-Host "Sent command to Rhino."