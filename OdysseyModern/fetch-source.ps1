$ErrorActionPreference = "Stop"
$repo = "https://github.com/deadwood2/OdysseyWebBrowser/archive/refs/heads/odyssey.zip"
$root = Join-Path (Get-Location) "odyssey-upstream"
$zip = Join-Path $env:TEMP "OdysseyWebBrowser-odyssey.zip"
$extract = Join-Path $env:TEMP "OdysseyWebBrowser-odyssey-extract"

Write-Host "Downloading upstream Odyssey source branch..."
Invoke-WebRequest -Uri $repo -OutFile $zip
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $extract -Force
$folder = Get-ChildItem $extract -Directory | Select-Object -First 1
if (-not $folder) { throw "Archive extraction did not contain a source directory." }
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
Move-Item $folder.FullName $root
Remove-Item $extract -Recurse -Force
Remove-Item $zip -Force
Write-Host "Source extracted to: $root"
Write-Host "This is source code, not a compiled browser. See OdysseyModern/README.md for target/build caveats."
