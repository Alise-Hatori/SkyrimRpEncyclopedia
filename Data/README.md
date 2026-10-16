# Release data

Databases are not tracked in Git. The approved candidate uses:

The standalone Knowledge DB is supplied as `knowledge.db.zip` to reduce transfer
size. Extract its single `knowledge.db` entry without changing the database. Verify
the extracted file against the table below. `SHA256SUMS-ARCHIVE.txt` also identifies
the archive itself. The installer already includes the original uncompressed DB.

| File | Bytes | Records | SHA-256 |
|---|---:|---|---|
| `knowledge.db` | 216580096 | 5579 entities | `785B72ECAFC9E2103213AD1E66F7D2B928B332CEC53C2EAB006F7BF20139B899` |
| `rp_rules.db` | 274432 | 33 articles | `16C85B195F20024527D04767E02CE75BC4C497758A8F233AF7FBA55B5D541B71` |

The installed layout is `<application>/Data/<file>`. For source-based launches,
use the documented environment variables or copy approved data into the output's
`Data` directory. Do not substitute a personal database.

Wiki content and community RP rules are separate corpora. Their inclusion in a
private candidate does not establish clearance for public redistribution. Obtain
data only from an authorized distribution and preserve its notices and provenance.
