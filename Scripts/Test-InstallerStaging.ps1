param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$StagingPath,
    [string]$RulesSHA256 = ''
)

$ErrorActionPreference = 'Stop'
$expectedKnowledgeHash = '785B72ECAFC9E2103213AD1E66F7D2B928B332CEC53C2EAB006F7BF20139B899'
$root = (Resolve-Path -LiteralPath $StagingPath).Path
$app = Join-Path $root 'App'
$knowledge = Join-Path $root 'Data\knowledge.db'
$problems = [Collections.Generic.List[string]]::new()
$rules = Join-Path $root 'Data\rp_rules.db'
if ($RulesSHA256) {
    if (!(Test-Path -LiteralPath $rules -PathType Leaf)) { $problems.Add('Missing Data\rp_rules.db') }
    elseif ((Get-FileHash -LiteralPath $rules -Algorithm SHA256).Hash -ne $RulesSHA256) { $problems.Add('RP rules SHA-256 mismatch') }
}

function Require-File([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        $problems.Add("Missing required file: $RelativePath")
    }
}

foreach ($file in @(
    'App\SkyrimRpEncyclopedia.exe',
    'App\SkyrimRpEncyclopedia.dll',
    'App\SkyrimRpEncyclopedia.deps.json',
    'App\SkyrimRpEncyclopedia.runtimeconfig.json',
    'App\Microsoft.Data.Sqlite.dll',
    'App\e_sqlite3.dll',
    'App\runtimes\win-x64\native\WebView2Loader.dll',
    'Data\knowledge.db'
)) { Require-File $file }

if (Test-Path -LiteralPath $knowledge -PathType Leaf) {
    $hash = (Get-FileHash -LiteralPath $knowledge -Algorithm SHA256).Hash
    if ($hash -ne $expectedKnowledgeHash) {
        $problems.Add("Knowledge DB SHA-256 mismatch: $hash")
    }
}

$forbiddenNames = @('user.db', 'official.db', 'knowledge.db-wal', 'knowledge.db-shm')
$forbiddenPatterns = @('*.db-wal', '*.db-shm', '*.log', '*.pdb')
foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($root, $file.FullName)
    $isApprovedKnowledge = $relative -eq 'Data\knowledge.db' -or ($RulesSHA256 -and $relative -eq 'Data\rp_rules.db')
    if (($file.Extension -eq '.db' -and !$isApprovedKnowledge) -or
        $forbiddenNames -contains $file.Name -or
        ($forbiddenPatterns | Where-Object { $file.Name -like $_ })) {
        $problems.Add("Forbidden staging asset: $relative")
    }
}

foreach ($directory in Get-ChildItem -LiteralPath $root -Recurse -Directory) {
    $relative = [IO.Path]::GetRelativePath($root, $directory.FullName)
    if ($relative -match '(?i)(WebView2|EBWebView|QA|Tests|obj|bin)') {
        $problems.Add("Forbidden staging directory: $relative")
    }
}

if ($problems.Count) {
    throw ("Installer staging rejected:`n" + ($problems -join [Environment]::NewLine))
}

Write-Output "Installer staging OK: self-contained x64 application, SQLite/runtime assets and verified Data\knowledge.db."
if ($RulesSHA256) { Write-Output 'Approved read-only Data\rp_rules.db included; no personal database.' }
