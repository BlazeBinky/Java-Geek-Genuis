param(
    [ValidateSet("balanced", "fast")]
    [string]$Profile = "balanced",
    [ValidateSet("off", "on")]
    [string]$Media = "off",
    [ValidateSet("off", "on")]
    [string]$Xslt = "off",
    [ValidateRange(0, 64)]
    [int]$Jobs = 0,
    [string]$Image = "walkero/amigagccondocker:os4-gcc11-3.5.2"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker was not found. Install Docker Desktop, enable its Linux container engine, and retry."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$cacheHostDir = Join-Path $env:LOCALAPPDATA "OdysseyBrowser\ccache"
if ($Ccache -ne "off") {
    New-Item -ItemType Directory -Force -Path $cacheHostDir | Out-Null
}
Write-Host "Pulling AmigaOS 4 PPC build environment: $Image"
& docker pull $Image
if ($LASTEXITCODE -ne 0) { throw "Docker image pull failed." }

$dockerArgs = @(
    "run", "--rm", "--init",
    "--volume", "$($repoRoot):/opt/code",
    "--workdir", "/opt/code",
    "--env", "ODYSSEY_BUILD_PROFILE=$Profile",
    "--env", "ODYSSEY_OS4_MEDIA=$Media",
    "--env", "ODYSSEY_OS4_XSLT=$Xslt",
    "--env", "ODYSSEY_CCACHE=$Ccache"
)

if ($Ccache -ne "off") {
    $dockerArgs += @(
        "--volume", "$($cacheHostDir):/opt/odyssey-ccache",
        "--env", "CCACHE_DIR=/opt/odyssey-ccache"
    )
}

if ($Jobs -gt 0) {
    $dockerArgs += @("--env", "BUILD_JOBS=$Jobs")
}

$dockerArgs += @($Image, "bash", "./OdysseyModern/fetch-and-build-os4.sh")
Write-Host "Starting experimental AmigaOS 4 PPC cross-build."
Write-Host "Profile: $Profile | Media: $Media | XSLT: $Xslt | Ccache: $Ccache | Jobs: $(if ($Jobs -gt 0) { $Jobs } else { 'auto' })"
Write-Host "Compiler logs will be under cross-build-amigaos4/."
Write-Host "This does not promise the newer Odyssey engine is already ported to OS4."
& docker @dockerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Cross-build failed. Inspect cross-build-amigaos4/odyssey-configure.log and odyssey-build.log."
}
