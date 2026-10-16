param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $PackagePath).Path
$problems = [Collections.Generic.List[string]]::new()
function Require-Asset([string]$relative) {
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (!$path.StartsWith($root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Asset path escapes package directory' }
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { $problems.Add("Missing asset: $relative") }
    return $path
}
$null = Require-Asset 'SkyrimRpEncyclopedia.exe'
$null = Require-Asset 'SkyrimRpEncyclopedia.dll'
$depsPath = Require-Asset 'SkyrimRpEncyclopedia.deps.json'
$runtimePath = Require-Asset 'SkyrimRpEncyclopedia.runtimeconfig.json'
if ($problems.Count) { throw ($problems -join [Environment]::NewLine) }
$deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json
$runtime = Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json
$frameworks = @($runtime.runtimeOptions.frameworks | ForEach-Object { $_.name })
$includedFrameworks = @($runtime.runtimeOptions.includedFrameworks | ForEach-Object { $_.name })
$hasDesktopFramework = 'Microsoft.WindowsDesktop.App' -in $frameworks
$isSelfContainedDesktop = 'Microsoft.WindowsDesktop.App' -in $includedFrameworks
if (!$hasDesktopFramework -and !$isSelfContainedDesktop) { $problems.Add('Missing Windows Desktop runtime declaration or self-contained desktop runtime') }
if ($isSelfContainedDesktop) {
    foreach ($asset in @('hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll', 'PresentationFramework.dll', 'WindowsBase.dll')) {
        $null = Require-Asset $asset
    }
}
$target = $deps.targets.PSObject.Properties[$deps.runtimeTarget.name].Value
if ($null -eq $target) { throw 'Dependency runtime target not found' }
$sqliteDeclared = $false
foreach ($library in $target.PSObject.Properties) {
    foreach ($kind in @('runtime','native','runtimeTargets','resources')) {
        $assets = $library.Value.PSObject.Properties[$kind]
        if ($null -eq $assets) { continue }
        foreach ($asset in $assets.Value.PSObject.Properties) {
            $leaf = [IO.Path]::GetFileName($asset.Name)
            if ($leaf -eq '_._') { continue }
            if ($kind -eq 'runtimeTargets') { $relative = $asset.Name }
            elseif ($kind -eq 'resources') { $relative = Join-Path $asset.Value.locale $leaf }
            else { $relative = $leaf }
            $path = Require-Asset $relative
            if ($leaf -eq 'Microsoft.Data.Sqlite.dll') {
                $sqliteDeclared = $true
                if (Test-Path -LiteralPath $path -PathType Leaf) {
                    $version = [Reflection.AssemblyName]::GetAssemblyName($path).Version.ToString()
                    if ($version -ne $asset.Value.assemblyVersion) { $problems.Add("SQLite assembly version mismatch: $version / $($asset.Value.assemblyVersion)") }
                }
            }
        }
    }
}
if (!$sqliteDeclared) { $problems.Add('Microsoft.Data.Sqlite.dll is not declared in deps.json') }
if ($problems.Count) { throw ("Incomplete release package. Keep the entire published directory, including DLLs and runtimes. No databases were changed.`n" + ($problems -join [Environment]::NewLine)) }
Write-Output ("Release package OK: dependency assets, SQLite assembly version and " + $(if ($isSelfContainedDesktop) { 'self-contained Windows Desktop runtime.' } else { 'Windows Desktop runtime declaration.' }))
