# Build, isolated launch and packaging

Run commands from the project root on Windows with the .NET 10 SDK. Restore is
an explicit network operation; `--no-restore` builds require an existing restore.

## Isolated manual launch

After a Release x64 build, use a fresh temporary folder for writable test data.
These environment changes affect the child process only:

```powershell
$test = Join-Path ([IO.Path]::GetTempPath()) ('SkyrimRP-QA-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $test | Out-Null
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = (Resolve-Path .\bin\Release\net10.0-windows10.0.17763.0\win-x64\SkyrimRpEncyclopedia.exe).Path
$start.WorkingDirectory = $test
$start.UseShellExecute = $false
$start.Environment['SKYRIM_RP_USER_DB'] = Join-Path $test 'user.db'
$start.Environment['SKYRIM_RP_OFFICIAL_DB'] = Join-Path $test 'official.db'
$start.Environment['SKYRIM_RP_MVP_DB'] = (Resolve-Path .\Data\knowledge.db).Path
[Diagnostics.Process]::Start($start)
```

This isolates writable databases, **not** the shared LocalAppData WebView2 profile
or overlay settings. Do not clear cookies for testing. OAuth interaction must be
performed by the user. Close all application windows normally when finished.

## Packaging

The existing candidate installer must not be overwritten. New builds use a new
staging root outside the source tree:

```powershell
.\Scripts\New-InstallerStaging.ps1 `
  -KnowledgeDatabase (Resolve-Path .\Data\knowledge.db).Path `
  -RulesDatabase (Resolve-Path .\Data\rp_rules.db).Path `
  -StagingRoot (Join-Path ([IO.Path]::GetTempPath()) ('SkyrimRP-stage-' + [guid]::NewGuid().ToString('N')))
```

Inspect the generated manifest/layout and run the included validation scripts.
Compile `Installer/SkyrimRP-Setup-1.0.0.iss` with Inno Setup and an explicit
`StagingDir` pointing at that new staging. Follow `Installer/README.md`.

Never copy `bin/Debug`, LocalAppData, browser profiles, personal databases or QA logs
into a distribution. Verify hashes before and after every operation involving data.

## Acceptance boundaries

- A passing build is not a visual test or a clean-machine installation test.
- Test clean installation in a non-default directory on a separate Windows/VM.
- Test update with disposable personal data and verify it is preserved.
- Test both maps, overlay modes, mini-map, restart persistence and actual OAuth.
- Confirm 5579 Knowledge entities, all 33 RP articles, search and the internal link.
- Clear content rights and complete third-party notices before public release.
