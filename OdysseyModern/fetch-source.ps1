$ErrorActionPreference = "Stop"
$repo = "https://github.com/deadwood2/OdysseyWebBrowser/archive/refs/heads/odyssey.zip"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$root = Join-Path $repoRoot "odyssey-upstream"
$zip = Join-Path $env:TEMP "OdysseyWebBrowser-odyssey.zip"
$extract = Join-Path $env:TEMP "OdysseyWebBrowser-odyssey-extract"

Write-Host "Downloading upstream Odyssey source branch..."
Invoke-WebRequest -Uri $repo -OutFile $zip
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $extract -Force
$folder = Get-ChildItem $extract -Directory | Select-Object -First 1
if (-not $folder) { throw "Archive extraction did not contain a source directory." }
if (Test-Path $root) { throw "Destination already exists: $root. Move it aside or remove it yourself; this script will not overwrite source files." }
Move-Item $folder.FullName $root
Remove-Item $extract -Recurse -Force
Remove-Item $zip -Force
Write-Host "Source extracted to: $root"
$patcher = Join-Path $PSScriptRoot "prepare-upstream.py"
$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) { throw "Python 3 is required to apply the downstream modernization changes." }
& $python.Source $patcher $root
if ($LASTEXITCODE -ne 0) { throw "Downstream preparation failed. Review the error rather than continuing with an unpatched tree." }
Write-Host "Downstream build-system patches applied."
Write-Host "This is source code, not a compiled browser. See OdysseyModern/README.md for target/build caveats."
