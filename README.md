# Skyrim RP Encyclopedia

A Windows desktop encyclopedia and role-playing toolkit for Skyrim communities.
Built with C#/.NET 10, WPF, SQLite and Microsoft Edge WebView2.

> **Private 1.0.0 release candidate — not cleared for public redistribution.**
> Wiki content, visual assets and community rules have separate provenance and
> licensing requirements. See [third-party notices](THIRD_PARTY_NOTICES.md).
> No open-source license for the original application code has been selected.

## Features

- Search and read a local Skyrim Knowledge DB; browse categories and relationships
  in the Knowledge Atlas.
- Read a separate **RP Rules** collection, clearly marked as community rules,
  **not Skyrim Wiki canon**.
- Create personal articles, notes and metadata without changing bundled corpora.
- Generate structured RP scenarios and simple events offline, with reproducible
  seeds, validation and separate public/GM text where supported.
- Use an overlay and mini-map alongside the main window.
- Switch between [NordCore](https://map.nordcore.dev/) and
  [SkyRP Map](https://skyrpmap.com/). The selected map is persisted locally.
- An optional assistant integrates with local Ollama; neither Ollama nor a model
  is bundled. The application does not require it for offline generators.

No screenshots are included: current private QA captures have not been approved
for distribution.

## Requirements

- Windows x64. The project targets Windows 10 build 17763; this target is not a
  guarantee of support or successful testing on every Windows edition.
- Microsoft Edge WebView2 Evergreen Runtime, installed separately. The installer
  checks for it and does not silently download it.
- The candidate installer includes .NET 10 Desktop Runtime (self-contained x64).
- Internet access is needed for online maps, OAuth and external source links,
  not for reading bundled articles or using offline generators.

## Install and update

1. Obtain the approved installer and checksum from the private release candidate.
2. Verify SHA-256 before running it. Do not copy only the program EXE.
3. Run the installer and choose a destination directory.
4. Launch **Skyrim RP Encyclopedia**. Bundled data is discovered automatically:
   `Data/knowledge.db` and `Data/rp_rules.db` under the installed application.

The candidate contains **5,579 Knowledge entities** and **33 RP articles**. The
databases remain separate and are read-only. The RP package preserves one
internal article link; an external link to a developer's official DB was excluded.

Updating replaces application files and bundled data, not the personal database.
Back up your data first. Installation/update has been tested in an isolated folder
on the development PC, **not on a clean Windows VM**. Detailed map/OAuth acceptance
and restart persistence must be recorded separately; successful builds do not
prove these scenarios.

## Personal data and backup

Personal databases, settings and the shared WebView2 profile are stored beneath
`%LOCALAPPDATA%\SkyrimRpEncyclopedia`, outside the installation directory.
Close the application normally before making a private backup. Keep the backup
outside this repository. A full profile backup can contain authenticated web
sessions: do not upload or share it.

The installer does not bundle a personal `user.db` or `official.db`, and uninstall
does not intentionally clear the LocalAppData browser profile. External browser
sign-in is not automatically transferred to WebView2.

## Build and test

Install the .NET 10 SDK on Windows, restore dependencies from their official
sources, then run from the repository root:

```powershell
dotnet restore .\SkyrimRpEncyclopedia.csproj
dotnet restore .\Tests\SkyrimRpEncyclopedia.MvpSmokeTests.csproj
dotnet build .\SkyrimRpEncyclopedia.csproj -c Release -r win-x64 --no-restore
dotnet build .\Tests\SkyrimRpEncyclopedia.MvpSmokeTests.csproj -c Release --no-restore
```

Smoke tests require an existing fixture and an independently known entity count:

```powershell
$env:SKYRIM_RP_MVP_DB = (Resolve-Path .\Data\knowledge.db).Path
$env:SKYRIM_RP_TEST_MVP_EXPECTED_ENTITY_COUNT = '5579'
dotnet run --project .\Tests\SkyrimRpEncyclopedia.MvpSmokeTests.csproj -c Release --no-build
```

For the legacy fixture, select its actual file and set the expected count to `125`.
The legacy fixture is not bundled. Never infer the expected count from the tested
database. Tests use temporary writable databases; source fixtures stay read-only.

See [build and packaging instructions](docs/BUILD_AND_PACKAGE.md) for isolated
launches and installer preparation. Large databases and the installer are not
tracked in Git; see [data checksums](Data/README.md).

## Release status and licensing

This is an independent fan project. It is not an official Bethesda, ZeniMax,
Microsoft, Fandom, Discord, NordCore or SkyRP product and does not claim endorsement.

Application code, dependencies, Wiki text, graphics and community RP rules are
distinct materials. Private access does not settle rights for public redistribution.
Do not make the repository or its candidate assets public until the listed rights,
attribution and acceptance blockers are resolved.
