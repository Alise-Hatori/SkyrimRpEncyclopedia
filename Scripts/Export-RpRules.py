"""Export an explicitly approved RP subset. Source is always opened read-only; never copy user.db."""
import argparse
import hashlib
import json
import pathlib
import sqlite3
from contextlib import closing


def export(source, destination):
    source, destination = pathlib.Path(source).resolve(), pathlib.Path(destination).resolve()
    if destination.exists() or destination.with_suffix('.partial').exists():
        raise ValueError('Refusing to overwrite an existing output')
    before = hashlib.sha256(source.read_bytes()).hexdigest()
    with closing(sqlite3.connect(source.as_uri() + '?mode=ro', uri=True)) as src:
        src.execute('PRAGMA query_only=ON')
        src.execute('BEGIN')
        src.row_factory = sqlite3.Row
        articles = list(src.execute('SELECT * FROM Articles WHERE Category=? ORDER BY Id', ('Правила RP',)))
        if len(articles) != 33 or any(a['Id'] <= 0 or not (a['Content'] or '').strip() for a in articles):
            raise ValueError('Expected exactly 33 positive-ID nonempty RP articles')
        ids = [a['Id'] for a in articles]
        marks = ','.join('?' for _ in ids)
        data = {'Articles': [tuple(0 if k == 'IsFavorite' else a[k] for k in a.keys()) for a in articles]}
        data['ArticleTags'] = list(map(tuple, src.execute(f'SELECT * FROM ArticleTags WHERE ArticleId IN ({marks}) ORDER BY ArticleId,TagId', ids)))
        data['ArticleFlags'] = list(map(tuple, src.execute(f'SELECT * FROM ArticleFlags WHERE ArticleId IN ({marks}) ORDER BY ArticleId,FlagId', ids)))
        data['Tags'] = list(map(tuple, src.execute(f'SELECT * FROM Tags WHERE Id IN(SELECT TagId FROM ArticleTags WHERE ArticleId IN ({marks})) ORDER BY Id', ids)))
        data['Flags'] = list(map(tuple, src.execute(f'SELECT * FROM Flags WHERE Id IN(SELECT FlagId FROM ArticleFlags WHERE ArticleId IN ({marks})) ORDER BY Id', ids)))
        data['Categories'] = list(map(tuple, src.execute('SELECT * FROM Categories WHERE Name=? ORDER BY Id', ('Правила RP',))))
        links = list(src.execute(f'SELECT * FROM Links WHERE SourceIsOfficial=0 AND SourceArticleId IN ({marks}) ORDER BY Id', ids))
        internal = [l for l in links if l['TargetIsOfficial'] == 0 and l['TargetArticleId'] in ids]
        excluded = [l for l in links if l not in internal]
        if len(internal) != 1 or internal[0]['Id'] != 2 or len(excluded) != 1 or excluded[0]['Id'] != 3:
            raise ValueError('Relationships differ from the explicitly approved internal #2 / excluded external #3')
        data['Links'] = list(map(tuple, internal))
        destination.parent.mkdir(parents=True, exist_ok=True)
        partial = destination.with_suffix('.partial')
        # New output only; schemas are copied, but rows are selected explicitly from the approved tables.
        with closing(sqlite3.connect(partial)) as dst, dst:
            for table, rows in data.items():
                schema = src.execute('SELECT sql FROM sqlite_master WHERE type=? AND name=?', ('table', table)).fetchone()[0]
                dst.execute(schema)
                if rows:
                    dst.executemany('INSERT INTO ' + table + ' VALUES(' + ','.join('?' for _ in rows[0]) + ')', rows)
            dst.execute('CREATE TABLE PackageInfo(Key TEXT PRIMARY KEY, Value TEXT NOT NULL)')
            dst.executemany('INSERT INTO PackageInfo VALUES(?,?)', [('Kind', 'CommunityRpRules'), ('Version', '1.0.0'),
                ('ArticleCount', '33'), ('ExcludedLinkId', '3'), ('ExcludedLinkReason', 'Local official.db reference; excluded by owner approval')])
        with closing(sqlite3.connect(partial.as_uri() + '?mode=ro', uri=True)) as check:
            if check.execute('PRAGMA integrity_check').fetchall() != [('ok',)]:
                raise ValueError('Exported database integrity failed')
            for table, rows in data.items():
                actual = check.execute('SELECT * FROM ' + table).fetchall()
                if sorted(actual) != sorted(rows):
                    raise ValueError('Exact row comparison failed: ' + table)
            # Explicitly compare the full original article fields, except private favorite state.
            for original, copied in zip(articles, check.execute('SELECT * FROM Articles ORDER BY Id')):
                expected = tuple(0 if k == 'IsFavorite' else original[k] for k in original.keys())
                if expected != copied:
                    raise ValueError('Article content or metadata changed')
    if hashlib.sha256(source.read_bytes()).hexdigest() != before:
        raise ValueError('SOURCE HASH CHANGED: stop before accepting export')
    partial.rename(destination)
    return {'Articles': 33, 'ContentCharacters': sum(len(a['Content']) for a in articles),
        'VerifiedTables': {k: len(v) for k,v in data.items()}, 'ExactComparison': True,
        'ExcludedLinkIds': [3], 'PrivateTablesExported': False,
        'RulesSHA256': hashlib.sha256(destination.read_bytes()).hexdigest(), 'SourceUnchanged': True}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    print(json.dumps(export(args.source, args.output), ensure_ascii=True))
