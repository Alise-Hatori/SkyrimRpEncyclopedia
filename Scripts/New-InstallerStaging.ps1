param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$KnowledgeDatabase,

    [string]$StagingRoot = (Join-Path ([System.IO.Path]::GetTempPath()) 'SkyrimRP_ReleaseStaging'),

    [string]$Version = '1.0.0',
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$RulesDatabase
)

$ErrorActionPreference = 'Stop'

$expectedKnowledgeHash = '785B72ECAFC9E2103213AD1E66F7D2B928B332CEC53C2EAB006F7BF20139B899'
$projectRoot = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$staging = Join-Path $StagingRoot "SkyrimRP-$Version-$timestamp"
$appDirectory = Join-Path $staging 'App'
$dataDirectory = Join-Path $staging 'Data'

if (Test-Path -LiteralPath $staging) {
    throw "Refusing to reuse an existing staging directory: $staging"
}

$sourceHash = (Get-FileHash -LiteralPath $KnowledgeDatabase -Algorithm SHA256).Hash
$rulesHash = if ($RulesDatabase) { (Get-FileHash -LiteralPath $RulesDatabase -Algorithm SHA256).Hash } else { '' }
if ($sourceHash -ne $expectedKnowledgeHash) {
    throw "Knowledge DB hash mismatch. Expected $expectedKnowledgeHash, got $sourceHash."
}

New-Item -ItemType Directory -Path $appDirectory, $dataDirectory -Force | Out-Null

try {
    Push-Location $projectRoot
    & dotnet publish -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false `
        -o $appDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Self-contained publish failed with exit code $LASTEXITCODE."
    }

    & "$projectRoot\Scripts\Test-ReleasePackage.ps1" -PackagePath $appDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Release package validation failed with exit code $LASTEXITCODE."
    }
}
catch {
    throw
}
finally {
    Pop-Location -ErrorAction SilentlyContinue
}

$stagedKnowledgeDatabase = Join-Path $dataDirectory 'knowledge.db'
Copy-Item -LiteralPath $KnowledgeDatabase -Destination $stagedKnowledgeDatabase
$stagedHash = (Get-FileHash -LiteralPath $stagedKnowledgeDatabase -Algorithm SHA256).Hash
if ($stagedHash -ne $expectedKnowledgeHash) {
    throw "Staged Knowledge DB hash mismatch. Expected $expectedKnowledgeHash, got $stagedHash."
}

if ($RulesDatabase) {
    Copy-Item -LiteralPath $RulesDatabase -Destination (Join-Path $dataDirectory 'rp_rules.db')
    if ((Get-FileHash -LiteralPath $RulesDatabase -Algorithm SHA256).Hash -ne $rulesHash) { throw 'Source RP package changed' }
}
foreach ($name in @('README_RU.md','THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $appDirectory $name)
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSES') -Destination $appDirectory -Recurse
& "$projectRoot\Scripts\Test-InstallerStaging.ps1" -StagingPath $staging -RulesSHA256 $rulesHash
if ($LASTEXITCODE -ne 0) {
    throw "Installer staging validation failed with exit code $LASTEXITCODE."
}

Write-Output "Installer staging ready: $staging"
