# Third-party software and content — private candidate

This is a provenance and compliance register, not a declaration that every
component has been cleared for public redistribution. The current 1.0.0 installer
is self-contained x64 and includes .NET Desktop Runtime. Older references to a
framework-dependent package do not describe this candidate.

## Software

| Component | Version | Locally observed terms | Required follow-up |
|---|---|---|---|
| Microsoft.Data.Sqlite / Core | 10.0.12 | MIT in local package metadata/register | Preserve complete license and copyright |
| SQLitePCLRaw bundle/core/provider/native package | 2.1.12 | Apache-2.0 in NuGet metadata | Include license and applicable notices; verify native components |
| Microsoft.Web.WebView2 SDK | 1.0.4258.31 | Redistribution conditions in LICENSE.txt | Preserve copyright, conditions and disclaimer; no endorsement |
| .NET / WPF Desktop Runtime | 10.0.12 | MIT in local runtime license | Include applicable runtime licenses and third-party notices |
| Windows SDK runtime components | 10.0.17763.57 runtime pack | Package references Windows SDK terms | Verify applicable redistribution conditions |
| WebView2 Evergreen Runtime | installed separately | Not bundled | User obtains it from Microsoft; SDK is not the Runtime |

`LICENSES/WebView2-SDK.txt` is included. This file alone is not the full set of
applicable dependency notices. Microsoft copyrights and SQLitePCLRaw/SourceGear
copyrights must be retained where applicable. No software license is extended to
unrelated content merely because it is stored in a SQLite database.

## Skyrim Wiki corpus

The candidate includes `Data/knowledge.db` with 5579 entities. Source article URLs
point to the Russian Elder Scrolls Wiki on Fandom. Database `Sources.License`
contains a general CC BY-SA label requiring further confirmation, not a specific
verified license version for every article.

Fandom's general licensing policy describes text under CC BY-SA 3.0 except where
otherwise stated. This does not settle exceptions, embedded game/book text, images
or rights not covered by that license. Verify the actual applicable terms and
provide appropriate attribution, license references and change notices, including
ShareAlike obligations for adaptations, before public distribution.

- https://www.fandom.com/licensing
- https://community.fandom.com/wiki/Help:Licensing
- https://creativecommons.org/licenses/by-sa/3.0/

Preserved source URLs are useful provenance but do not alone establish compliance.
Do not describe all extracted statements or inferred relations as verified canon.

## Community RP rules

The separate `Data/rp_rules.db` contains 33 articles selected and approved by the
application author for this candidate. They are community materials, **not Skyrim
Wiki canon**. Original article IDs, texts and necessary metadata were retained;
one internal link was preserved and a local official-database link excluded.

## Authorship and Acknowledgments

- **Author of the Skyrim RP Encyclopedia:** [Alise-Hatori](https://github.com/Alise-Hatori) — responsible for application and tool development, as well as the organization of project materials.
- **SkyRP Map:** [Open Map](https://skyrpmap.com/) · [Creator Community on Discord](https://discord.gg/DQ6YqDTb4).
- **RP Rules and Community Materials:** [NirnTales](https://nirntales.com/) — the source for the roleplay ruleset, presented here as a collection of 33 articles.

The Skyrim RP Encyclopedia is an independent fan project and is not affiliated with the official developers or rights holders of *The Elder Scrolls*. This project is neither endorsed nor sponsored by Bethesda, ZeniMax, Microsoft, Fandom, or any providers of mapping services.

## License and Rights

The original code for the Skyrim RP Encyclopedia is distributed under the Alise-Hatori Non-Commercial License (version 1.0). This license permits the free use, study, modification, and distribution of the code, subject to the terms therein. Commercial use requires prior written consent from the rights holder.

RP materials, maps, images, encyclopedic texts, and third-party libraries are governed by their own respective licenses, permissions, and terms set by their respective rights holders. The license covering the original code does not automatically extend to these materials.

For more details, please refer to:
- [LICENSE](LICENSE)
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
- [CONTENT_ATTRIBUTION.md](CONTENT_ATTRIBUTION.md)


Approval to include the selected articles is not proof of ownership of any
third-party passages. One article, "Положение Талморского Посольства", includes
Discord message/channel references. These links do not themselves prove borrowing,
authorship or permission. Resolve provenance and author consent before public use.
No entire personal database, notes, history or favourites are included.

## Visual assets and original work

The project includes branding images/icons, NordicArchive illustrations and a
DarkSkyrim texture. The theme README describes the stone texture as generated;
that description is not a complete rights record for all assets. Confirm origins,
generation/source terms, ownership and permitted redistribution for each group.
Replace, obtain permission for or omit disputed resources before public release.

The original code, scenario texts and event templates have no agreed public license
in this repository. No LICENSE is supplied because none has been authorized.

Skyrim and The Elder Scrolls and related marks belong to their respective owners.
This is an independent fan project, not an officially endorsed product. Online map
providers, Discord, Fandom and Microsoft do not endorse this application merely
because their services or components are used.
