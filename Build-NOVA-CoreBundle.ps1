$ErrorActionPreference = "Stop"

$dist = Join-Path $PSScriptRoot "dist"
$output = Join-Path $PSScriptRoot "core-bundle"
$modelNames = @(
    "Qwen3.8-2B-Q8_0.gguf",
    "Qwen3-0.6B-Q4_K_M.gguf",
    "SmolVLM2-500M-Video-Instruct-Q4_K_M.gguf",
    "SmolVLM2-500M-Video-Instruct-mmproj-f16.gguf",
    "SmolVLM2-256M-Video-Instruct-Q4_K_M.gguf",
    "mmproj-SmolVLM2-256M-Video-Instruct-Q8_0.gguf"
)

if (Test-Path -LiteralPath $output) {
    throw "Der Ausgabeordner existiert bereits: $output. Verschiebe oder entferne ihn vor einem neuen Paketlauf."
}

$requiredFiles = @(
    (Join-Path $dist "NOVA.exe"),
    (Join-Path $PSScriptRoot "Install-NOVA.ps1"),
    (Join-Path $PSScriptRoot "Install-NOVA.cmd"),
    (Join-Path $PSScriptRoot ".env.example"),
    (Join-Path $PSScriptRoot "README.md"),
    (Join-Path $dist "WpfMath-LICENSE.txt"),
    (Join-Path $dist "WpfMath-FONT-LICENSES.md")
) + ($modelNames | ForEach-Object { Join-Path (Join-Path $dist "models") $_ })

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Erforderliche Paketdatei fehlt: $file"
    }
}

$runtimeExecutable = Get-ChildItem -LiteralPath (Join-Path $dist "llama-runtimes\CPU") `
    -Filter "llama-server.exe" -File -Recurse |
    Sort-Object FullName |
    Select-Object -First 1
if ($null -eq $runtimeExecutable) {
    throw "Keine CPU-Runtime unter dist\llama-runtimes\CPU gefunden."
}

New-Item -ItemType Directory -Path $output | Out-Null
New-Item -ItemType Directory -Path (Join-Path $output "models") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $output "llama-runtimes\CPU") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $output "LICENSES") | Out-Null

Copy-Item -LiteralPath (Join-Path $dist "NOVA.exe") -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Install-NOVA.ps1") -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Install-NOVA.cmd") -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot ".env.example") -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "README.md") -Destination $output
Copy-Item -LiteralPath (Join-Path $dist "WpfMath-LICENSE.txt") `
    -Destination (Join-Path $output "LICENSES")
Copy-Item -LiteralPath (Join-Path $dist "WpfMath-FONT-LICENSES.md") `
    -Destination (Join-Path $output "LICENSES")
foreach ($modelName in $modelNames) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $dist "models") $modelName) `
        -Destination (Join-Path $output "models")
}

$runtimeTarget = Join-Path (Join-Path $output "llama-runtimes\CPU") $runtimeExecutable.Directory.Name
New-Item -ItemType Directory -Path $runtimeTarget -Force | Out-Null
Get-ChildItem -LiteralPath $runtimeExecutable.Directory.FullName -Force |
    Copy-Item -Destination $runtimeTarget -Recurse -Force

$licenseDirectory = Join-Path $output "LICENSES"
Invoke-WebRequest -Uri "https://www.apache.org/licenses/LICENSE-2.0.txt" `
    -OutFile (Join-Path $licenseDirectory "Apache-2.0.txt")
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/ggml-org/llama.cpp/master/LICENSE" `
    -OutFile (Join-Path $licenseDirectory "llama.cpp-MIT.txt")

$runtimeLicenses = Get-ChildItem -LiteralPath $runtimeExecutable.Directory.FullName `
    -File -Recurse | Where-Object { $_.Name -match "^(LICENSE|NOTICE)(\.|$)" }
foreach ($license in $runtimeLicenses) {
    $runtimeLicenseName = "runtime-$($license.Directory.Name)-$($license.Name)"
    Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $licenseDirectory $runtimeLicenseName)
}

@"
NOVA Core Bundle - Modellquellen

Qwen3.8-2B-Q8_0.gguf
Quelle: https://huggingface.co/empero-ai/Qwen3.8-2B-GGUF
Lizenz: Apache-2.0 (siehe LICENSES/Apache-2.0.txt)

Qwen3-0.6B-Q4_K_M.gguf
Quelle: https://huggingface.co/unsloth/Qwen3-0.6B-GGUF

SmolVLM2-500M-Video-Instruct-Q4_K_M.gguf und SmolVLM2-500M-Video-Instruct-mmproj-f16.gguf
Quelle: https://huggingface.co/second-state/SmolVLM2-500M-Video-Instruct-GGUF

SmolVLM2-256M-Video-Instruct-Q4_K_M.gguf und mmproj-SmolVLM2-256M-Video-Instruct-Q8_0.gguf
Quelle: https://huggingface.co/ggml-org/SmolVLM2-256M-Video-Instruct-GGUF
Lizenz: Apache-2.0 (siehe LICENSES/Apache-2.0.txt)

llama.cpp CPU-Runtime
Lizenz und Hinweise: LICENSES/llama.cpp-MIT.txt sowie alle mitgelieferten Runtime-Lizenzdateien.
"@ | Set-Content -LiteralPath (Join-Path $output "MODEL-SOURCES.txt") -Encoding UTF8

@"
NOVA Core Bundle

Installiere NOVA durch Doppelklick auf Install-NOVA.cmd. Das Paket enthält
ein lokales Qwen3.8-2B-Textmodell sowie Pocket Performance und
Ultra-Performance mit ihren Bildmodellen, passenden Projektoren und einer
CPU-Runtime. Research-Bilder werden über das separate SmolVLM2-Vision-Fallback
analysiert. Für den ersten Start ist kein Modell- oder GPU-Download
erforderlich. NOVA erkennt die CPU-Runtime automatisch, wenn keine andere
llama.cpp-Runtime konfiguriert ist. Die CPU kann langsamer als eine passende
GPU sein.

Modellprofile für Research und Coder lassen sich direkt in den jeweiligen
Arbeitsbereichen auswählen. Nur die im Paket enthaltenen Modelle stehen
ohne zusätzliche Downloads zur Verfügung.

Lizenz- und Quellenangaben stehen in MODEL-SOURCES.txt und LICENSES.
"@ | Set-Content -LiteralPath (Join-Path $output "README-Core-Bundle.txt") -Encoding UTF8

$totalBytes = (Get-ChildItem -LiteralPath $output -File -Recurse |
    Measure-Object -Property Length -Sum).Sum
if ($totalBytes -gt 4.1GB) {
    throw "Das Kernpaket ist mit $([Math]::Round($totalBytes / 1GB, 2)) GiB größer als das Limit von 4,1 GiB: $output"
}

Write-Host "Kernpaket erstellt: $output"
Write-Host ("Gesamtgröße: {0:N2} GiB" -f ($totalBytes / 1GB))
Write-Host "Installieren: $output\Install-NOVA.cmd"
