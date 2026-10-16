# Skyrim RP Encyclopedia 1.0 installer

1. Build a verified self-contained x64 staging directory:

   `pwsh -File Scripts\New-InstallerStaging.ps1 -KnowledgeDatabase "<approved full Knowledge DB path>" -RulesDatabase "<verified rp_rules.db path>" -StagingRoot "<new staging root>"`

2. Build the installer on a machine with Inno Setup 6 or newer:

   `ISCC.exe /DStagingDir="<verified staging path>" Installer\SkyrimRP-Setup-1.0.0.iss`

The installer embeds the self-contained .NET 10 application, verified `Data\knowledge.db`, and separate read-only `Data\rp_rules.db`.
Personal user.db is never an installer input. The rules exporter selects only the approved 33 articles and metadata.
The local official.db reference (link #3) is excluded; internal RP link #2 is preserved.
For a separate current-user validation installation use /CURRENTUSER /DIR="<new empty folder>" /NOICONS /NOCLOSEAPPLICATIONS /NORESTART.
First verify that no same-AppId current-user installation exists. Do not overwrite an existing installation or register test shortcuts.
It does not embed a WebView2 installer. It checks for the official WebView2 Evergreen Runtime and exits with an explanation if it is absent.

Uninstall removes only the application directory. Personal databases, overlay settings, logs and the WebView2 user profile remain in `%LOCALAPPDATA%\SkyrimRpEncyclopedia`.
