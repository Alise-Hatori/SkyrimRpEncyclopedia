# Skyrim RP Encyclopedia 1.0.0 — private release candidate

This candidate brings together local article search, the Knowledge Atlas, personal
articles and metadata, offline scenario/event generators, an overlay and mini-map.
Both NordCore and SkyRP Map are available; web authentication is handled by the
websites and the local WebView2 profile.

## Data

- 5579 Knowledge entities in the separate read-only Wiki corpus.
- 33 community RP articles in a separate read-only package, not Wiki canon.
- One internal RP link retained; one local external reference deliberately excluded.
- No personal database, viewing history, notes, cookies or browser profile bundled.

## Verification and limitations

Release builds and legacy/full smoke fixtures are checked separately. Installation
and update were previously exercised in an isolated directory on the development
PC. The user reported working maps and RP rules in the installed candidate.
Clean Windows acceptance and explicit restart-persistence confirmation remain open.
The latest observed map session logged connection-aborted events and two timeouts;
their causes are not established. A prior OAuth HTTP 401 is not declared fixed.

This is not public redistribution clearance. Wiki attribution/licensing exceptions,
graphics provenance, RP authorship and complete dependency notices remain open.
The application code has no agreed public license. Keep this candidate private.

## Approved installer

`SkyrimRP-Setup-1.0.0.exe`, SHA-256:
`56053E26DB2629C29813FF78D259CA82C087918F1D7B18AC76AB57F9B3F40DF4`

See `Data/README.md` for database hashes. Assets must be verified independently
before use. No public tag or published release is implied by this document.
