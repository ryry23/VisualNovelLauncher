param(
    [string]$OutputDirectory = "$PSScriptRoot\publish",
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'VisualNovelLauncher.csproj'

$arguments = @(
    'publish', $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $OutputDirectory,
    "--self-contained=$($SelfContained.IsPresent.ToString().ToLowerInvariant())"
)

if ($SelfContained) {
    $arguments += '-p:PublishSingleFile=true'
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $OutputDirectory -Force

Write-Host "Published to: $OutputDirectory"

