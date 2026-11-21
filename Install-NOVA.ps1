$ErrorActionPreference = "Stop"

$source = $PSScriptRoot
$application = Join-Path $source "NOVA.exe"
if (-not (Test-Path -LiteralPath $application -PathType Leaf)) {
    throw "NOVA.exe wurde nicht gefunden. Starte Install-NOVA.cmd im NOVA-Installationspaket."
}

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\NOVA"
New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
& robocopy $source $installDirectory /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XF "Install-NOVA.ps1" "Install-NOVA.cmd"
$copyExitCode = $LASTEXITCODE
if ($copyExitCode -ge 8) {
    throw "NOVA-Dateien konnten nicht vollständig installiert werden (robocopy Exitcode $copyExitCode). Schließe NOVA und versuche es erneut."
}
Copy-Item -LiteralPath $application -Destination (Join-Path $installDirectory "NOVA.exe") -Force

$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$shortcutPath = Join-Path $desktop "NOVA.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDirectory "NOVA.exe"
$shortcut.WorkingDirectory = $installDirectory
$shortcut.IconLocation = "$(Join-Path $installDirectory 'NOVA.exe'),0"
$shortcut.Description = "NOVA Sprachassistent"
$shortcut.Save()

Write-Host "NOVA wurde installiert: $installDirectory"
Write-Host "Desktop-Verknüpfung erstellt: $shortcutPath"
