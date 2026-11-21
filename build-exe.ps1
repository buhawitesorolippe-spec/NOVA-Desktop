$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "desktop\Nova.Desktop\Nova.Desktop.csproj"
$output = Join-Path $PSScriptRoot "dist"
$assemblyName = "NOVA"
$runningInstances = Get-CimInstance Win32_Process -Filter "Name = 'NOVA.exe'" |
    Where-Object { $_.ExecutablePath -eq (Join-Path $output "NOVA.exe") }

if ($runningInstances) {
    throw "NOVA läuft noch. Schließe NOVA, bevor du die Standarddatei dist\NOVA.exe aktualisierst."
}
$publishOutput = Join-Path $output ".publish-$assemblyName"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK 8 oder neuer wurde nicht gefunden. Installiere zuerst das .NET 8 SDK für Windows."
}

$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or [version]$sdkVersion -lt [version]"8.0") {
    throw "NOVA benötigt .NET SDK 8 oder neuer. Gefundene Version: $sdkVersion"
}

& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishOutput `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    "-p:AssemblyName=$assemblyName"

if ($LASTEXITCODE -ne 0) {
    throw "Der EXE-Build ist fehlgeschlagen (Exitcode $LASTEXITCODE)."
}

$publishedExe = Join-Path $publishOutput "$assemblyName.exe"
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Der Build wurde beendet, aber die erwartete EXE fehlt: $publishedExe"
}
Copy-Item -LiteralPath $publishedExe -Destination (Join-Path $output "$assemblyName.exe") -Force
foreach ($noticeFile in @("WpfMath-LICENSE.txt", "WpfMath-FONT-LICENSES.md")) {
    $noticePath = Join-Path $publishOutput $noticeFile
    if (-not (Test-Path -LiteralPath $noticePath)) {
        throw "Der Build wurde beendet, aber der Drittanbieterhinweis fehlt: $noticePath"
    }
    Copy-Item -LiteralPath $noticePath -Destination (Join-Path $output $noticeFile) -Force
}
Remove-Item -LiteralPath $publishOutput -Recurse -Force
$legacyUpdate = Join-Path $output "NOVA-update.exe"
if (Test-Path -LiteralPath $legacyUpdate) {
    Remove-Item -LiteralPath $legacyUpdate -Force
}

$models = Join-Path $output "models"
New-Item -ItemType Directory -Force -Path $models | Out-Null
$runtimes = Join-Path $output "llama-runtimes"
foreach ($kind in @("CPU", "CUDA", "Vulkan")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $runtimes $kind) | Out-Null
}
Copy-Item -Force (Join-Path $PSScriptRoot ".env.example") (Join-Path $output ".env.example")
Copy-Item -Force (Join-Path $PSScriptRoot "Install-NOVA.ps1") (Join-Path $output "Install-NOVA.ps1")
Copy-Item -Force (Join-Path $PSScriptRoot "Install-NOVA.cmd") (Join-Path $output "Install-NOVA.cmd")
Write-Host "Fertig: $output\$assemblyName.exe"
Write-Host "Installer starten: $output\Install-NOVA.cmd"
Write-Host "Lege GGUF-Dateien in $models"
Write-Host "Lege llama.cpp-ZIP-Dateien in $runtimes\CPU, $runtimes\CUDA oder $runtimes\Vulkan"
