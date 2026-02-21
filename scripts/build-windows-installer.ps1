param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $root "EnvioSafTApp.csproj"
$publishDir = Join-Path $root "dist\windows\publish"
$installerScript = Join-Path $root "EnviaSaftInstaller.iss"

if (-not $SkipPublish) {
    Write-Host "Publishing Windows build ($Configuration, $RuntimeIdentifier, self-contained=true)..."
    dotnet publish $project -c $Configuration -r $RuntimeIdentifier --self-contained true -o $publishDir
}

$isccCandidates = @(
    "$Env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$Env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($cmd) {
    $isccCandidates = @($cmd.Source) + $isccCandidates
}

$iscc = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
    throw "ISCC.exe não encontrado. Instale o Inno Setup 6 e volte a correr este script."
}

Write-Host "Compiling installer with ISCC: $iscc"
Push-Location $root
try {
    & $iscc $installerScript
}
finally {
    Pop-Location
}

Write-Host "Installer ready at dist\\windows\\installer"
