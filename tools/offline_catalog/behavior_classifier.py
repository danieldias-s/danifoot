"""Offline preparation tool. Standard library only; never imported by the game."""
from __future__ import annotations

import argparse
from bisect import bisect_left, bisect_right
from collections import Counter, defaultdict
import csv
import hashlib
import io
import json
import math
import os
from pathlib import Path
import re
import tempfile
from datetime import date

VERSION = 'discipline-percentile-1.0'
NAMES = ['FAIR PLAY', 'CORDEIRINHO', 'CAVALHEIRO', 'CANELEIRO', 'CACETEIRO', 'SARRAFEIRO']
METRICS = ['minutes', 'matches', 'yellowCards', 'redCards', 'fouls', 'duels', 'duelsWon']
RATE_FIELDS = ['yellowCardsPer90', 'redCardsPer90', 'foulsPer90', 'duelsPer90', 'duelsWonPer90']
FIELDS = ['playerCatalogId', 'club', 'country', 'year', 'position', 'competition',
          'source', 'collectedAt'] + METRICS + RATE_FIELDS
DEFAULT_CONFIG = {
    'version': VERSION, 'minimumMinutes': 900, 'minimumMatches': 10, 'minimumCohort': 10,
    'cardWeight': .65, 'foulWeight': .35, 'yellowPoints': 1, 'redPoints': 3,
    'cleanMaximum': .25, 'lowPhysicalMaximum': .25, 'highMinimum': .65,
    'veryHighMinimum': .85, 'extremeMinimum': .95,
    'extremeMinimumMinutes': 1800, 'extremeMinimumMatches': 20,
    'extremeMinimumCards': 6, 'extremeMinimumFouls': 20,
}


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + '\n').encode('utf-8')


def digest(value):
    return hashlib.sha256(value).hexdigest()


def load_json(path):
    return decode_json(Path(path).read_bytes())


def decode_json(raw):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError('Propriedade repetida: ' + key)
            result[key] = value
        return result
    return json.loads(raw.decode('utf-8-sig'), object_pairs_hook=pairs,
                      parse_constant=lambda x: (_ for _ in ()).throw(ValueError('Número inválido: ' + x)))


def validate_config(config):
    if set(config) != set(DEFAULT_CONFIG) or config['version'] != VERSION:
        raise ValueError('Configuração/versão desconhecida.')
    if any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) or v < 0
           for k, v in config.items() if k != 'version'):
        raise ValueError('Parâmetros devem ser números finitos não negativos.')
    if not math.isclose(config['cardWeight'] + config['foulWeight'], 1):
        raise ValueError('Pesos devem somar 1.')
    if not (0 <= config['cleanMaximum'] < config['highMinimum'] < config['veryHighMinimum'] < config['extremeMinimum'] <= 1):
        raise ValueError('Limiares percentis fora de ordem.')
    if not 0 <= config['lowPhysicalMaximum'] <= 1 or config['minimumCohort'] < 2:
        raise ValueError('Configuração física/amostra inválida.')
    for field in ['minimumMinutes', 'minimumMatches', 'minimumCohort', 'extremeMinimumMinutes',
                  'extremeMinimumMatches', 'extremeMinimumCards', 'extremeMinimumFouls']:
        if type(config[field]) is not int:
            raise ValueError('Limite deve ser inteiro: ' + field)
    if config['extremeMinimumMinutes'] < config['minimumMinutes'] or config['extremeMinimumMatches'] < config['minimumMatches']:
        raise ValueError('Amostra extrema não pode ser menor que a amostra comum.')


def validate_rows(rows):
    if not isinstance(rows, list) or len(rows) > 100000:
        raise ValueError('Informe uma lista de até 100000 registros.')
    for row in rows:
        if not isinstance(row, dict) or set(row) - set(FIELDS):
            raise ValueError('Registro contém campos desconhecidos.')
        for field in FIELDS[:8]:
            value = row.get(field)
            if field == 'year':
                if type(value) is not int or not 1 <= value <= 9999:
                    raise ValueError('Ano inválido.')
            elif not isinstance(value, str) or not value.strip():
                raise ValueError('Campo obrigatório: ' + field)
        if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,79}', row['playerCatalogId']) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,79}', row['club']):
            raise ValueError('Identidade de jogador/clube inválida.')
        if row['position'] not in ('G', 'D', 'M', 'A') or not re.fullmatch('[A-Z]{2}', row['country']):
            raise ValueError('Posição ou país inválido.')
        date.fromisoformat(row['collectedAt'])
        for field in METRICS:
            value = row.get(field)
            if value is not None and (type(value) is not int or not 0 <= value <= 2147483647):
                raise ValueError('Estatística deve ser inteiro não negativo ou null: ' + field)
        if row.get('duelsWon') is not None and row.get('duels') is not None and row['duelsWon'] > row['duels']:
            raise ValueError('Duelos vencidos excedem disputados.')
        for field in RATE_FIELDS:
            value = row.get(field)
            if value is not None and (type(value) not in (int, float) or not math.isfinite(value) or not 0 <= value <= 2147483647):
                raise ValueError('Taxa deve ser número finito não negativo ou null: ' + field)
            count = row.get(field.removesuffix('Per90'))
            if value is not None and count is not None and row.get('minutes') and not math.isclose(value, count * 90 / row['minutes'], rel_tol=1e-9, abs_tol=1e-9):
                raise ValueError('Taxa diverge da contagem/minutos; revise a entrada: ' + field)
    ids = [digest(encoded(r)) for r in rows]
    if len(set(ids)) != len(ids):
        raise ValueError('Registro estatístico duplicado; não somar duas vezes.')


def read_rows(path):
    path = Path(path)
    if path.stat().st_size > 50000000:
        raise ValueError('Entrada excede 50 MB.')
    if path.suffix.lower() == '.json':
        rows = load_json(path)
    elif path.suffix.lower() == '.csv':
        reader = csv.DictReader(io.StringIO(path.read_text(encoding='utf-8-sig')))
        if reader.fieldnames is None or len(set(reader.fieldnames)) != len(reader.fieldnames):
            raise ValueError('Cabeçalho CSV repetido/vazio.')
        rows = []
        for raw in reader:
            if None in raw:
                raise ValueError('Quantidade de colunas inválida.')
            row = dict(raw)
            for key in ['year'] + METRICS:
                value = row.get(key)
                if value is not None:
                    row[key] = None if value.strip() == '' else int(value)
            for key in RATE_FIELDS:
                value = row.get(key)
                if value is not None:
                    if value.strip() == '':
                        row.pop(key)
                    else:
                        row[key] = float(value)
            rows.append(row)
    else:
        raise ValueError('Use CSV ou JSON local.')
    validate_rows(rows)
    return rows


def rates(row, config):
    minutes = row.get('minutes')
    def per90(field):
        value = row.get(field)
        return row.get(field + 'Per90') if not minutes or value is None else value * 90 / minutes
    yellow, red = per90('yellowCards'), per90('redCards')
    return {'yellow90': yellow, 'red90': red, 'fouls90': per90('fouls'),
            'duels90': per90('duels'), 'duelsWon90': per90('duelsWon'),
            'cards90': None if yellow is None or red is None else yellow * config['yellowPoints'] + red * config['redPoints']}


def cohort_key(row):
    return tuple(row[k] for k in ['country', 'year', 'position', 'competition', 'source'])


def percentile(value, sample):
    if value is None or len(sample) < 2:
        return None
    if value == 0:  # a known zero is the floor, including a cohort with all zero events
        return 0.0
    left, right = bisect_left(sample, value), bisect_right(sample, value)
    return max(0.0, min(1.0, (left + (right - left - 1) / 2) / (len(sample) - 1)))


def classify(rows, config=None):
    config = dict(DEFAULT_CONFIG if config is None else config)
    validate_config(config)
    validate_rows(rows)
    rows = sorted(rows, key=lambda r: digest(encoded(r)))
    normalized = [rates(r, config) for r in rows]
    cohorts = defaultdict(list)
    counts = Counter((r['country'], r['year'], r['playerCatalogId']) for r in rows)
    for i, row in enumerate(rows):
        if (row.get('minutes') or 0) >= config['minimumMinutes'] and (row.get('matches') or 0) >= config['minimumMatches']:
            cohorts[cohort_key(row)].append(i)
    samples = {key: {metric: sorted(normalized[j][metric] for j in indices if normalized[j][metric] is not None)
                     for metric in ['cards90', 'fouls90', 'duels90']} for key, indices in cohorts.items()}
    results = []
    for i, row in enumerate(rows):
        reasons = []
        percentiles = {}
        sizes = {}
        for metric in ['cards90', 'fouls90', 'duels90']:
            sample = samples.get(cohort_key(row), {}).get(metric, [])
            sizes[metric] = len(sample)
            percentiles[metric] = percentile(normalized[i][metric], sample)
        cards, fouls, physical = (percentiles[m] for m in ['cards90', 'fouls90', 'duels90'])
        indiscipline = None if cards is None or fouls is None else config['cardWeight'] * cards + config['foulWeight'] * fouls
        no_data = not row.get('minutes') or all(row.get(m) is None for m in METRICS[2:] + RATE_FIELDS)
        if (row.get('minutes') or 0) < config['minimumMinutes'] or (row.get('matches') or 0) < config['minimumMatches']:
            reasons.append('Minutos/partidas insuficientes.')
        if any(s < config['minimumCohort'] for s in sizes.values()):
            reasons.append('Amostra comparável insuficiente para uma ou mais métricas.')
        if any(normalized[i][m] is None for m in ['cards90', 'fouls90', 'duels90']):
            reasons.append('Métrica desconhecida: não substituída por zero.')
        if counts[(row['country'], row['year'], row['playerCatalogId'])] > 1:
            reasons.append('Fontes/recortes múltiplos do mesmo atleta: revisão obrigatória; não foram somados.')
        behavior = None
        if not no_data and indiscipline is not None and physical is not None:
            behavior = 2
            if indiscipline <= config['cleanMaximum']:
                behavior = 1 if physical <= config['lowPhysicalMaximum'] else 0
            elif indiscipline >= config['highMinimum'] and fouls >= config['highMinimum'] and physical >= config['highMinimum']:
                behavior = 3
                if cards >= config['veryHighMinimum'] and fouls >= config['veryHighMinimum']:
                    behavior = 4
                if (cards >= config['extremeMinimum'] and fouls >= config['extremeMinimum']
                    and row.get('minutes', 0) >= config['extremeMinimumMinutes']
                    and row.get('matches', 0) >= config['extremeMinimumMatches']
                    and (row.get('yellowCards') or 0) + (row.get('redCards') or 0) >= config['extremeMinimumCards']
                    and (row.get('fouls') or 0) >= config['extremeMinimumFouls']):
                    behavior = 5
        status = 'SEM_DADOS' if no_data else 'REVISAO' if reasons else 'SUGERIDO'
        results.append({'rowId': digest(encoded(row)), 'statistics': row, 'rates': normalized[i],
                        'cohort': dict(zip(['country', 'year', 'position', 'competition', 'source'], cohort_key(row))),
                        'sampleSizes': sizes, 'percentiles': percentiles, 'indiscipline': indiscipline,
                        'physicalIntensity': physical, 'suggestedBehavior': behavior,
                        'suggestedName': None if behavior is None else NAMES[behavior], 'status': status,
                        'confidence': 'SEM_DADOS' if no_data else 'BAIXA' if reasons else 'MODERADA',
                        'explanation': reasons or ['Critérios configurados atendidos; sugestão auxiliar exige aprovação humana.'],
                        'manualDecision': None})
    return {'classifierVersion': VERSION, 'config': config, 'configHash': digest(encoded(config)),
            'inputHash': digest(encoded(rows)), 'results': results}


def safe_path(root, relative):
    root = Path(root).absolute()
    relative = str(relative)
    if not relative or '\\' in relative or ':' in relative or relative.startswith('/') or any(x in ('', '.', '..') for x in relative.split('/')):
        raise ValueError('Caminho relativo inseguro.')
    target = root.joinpath(*relative.split('/'))
    for part in [target, *target.parents]:
        if part.is_symlink() or part.is_junction():
            raise ValueError('Links/junctions não permitidos.')
    if root not in target.parents:
        raise ValueError('Caminho fora da raiz.')
    return target


def write_new(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as output:
        output.write(encoded(value))


def atomic_write(path, expected, replacement):
    """Stage a write beside its target and check the original bytes before replacing."""
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix='.behavior-', suffix='.tmp', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'wb') as output:
            output.write(replacement)
            output.flush()
            os.fsync(output.fileno())
        current = path.read_bytes() if path.exists() else None
        if current != expected:
            raise ValueError(f'Arquivo alterado durante a operação: {path.name}.')
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def scope(root, slug, year, country):
    if not re.fullmatch('[a-z][a-z0-9-]{0,79}', slug):
        raise ValueError('Slug inválido.')
    manifest = load_json(safe_path(root, f'{slug}/{year}/manifest.json'))
    if manifest.get('year') != year or manifest.get('country', {}).get('code') != country or manifest['country'].get('slug') != slug:
        raise ValueError('País/ano não corresponde ao manifest local.')
    return manifest


def report_path(root, slug, year, filename):
    return safe_path(root, f'_pesquisa/comportamento/{slug}/{year}/{filename}')


def validate_report(report):
    if report.get('classifierVersion') != VERSION:
        raise ValueError('Versão de evidências incompatível.')
    rows = [r['statistics'] for r in report['results']]
    original = classify(rows, report['config'])
    # Recompute every suggestion/index from original input; only the manual decisions may differ.
    checked = json.loads(json.dumps(report))
    original_status = {r['rowId']: r['status'] for r in original['results']}
    for result in checked['results']:
        if result.get('manualDecision') is not None:
            result['status'] = original_status.get(result['rowId'])
        result['manualDecision'] = None
    checked.pop('scope', None)
    if checked != original:
        raise ValueError('Evidências/configuração alteradas; gere novo relatório.')


def review(report, row_id, behavior, justification, root, relative):
    validate_report(report)
    if type(behavior) is not int or behavior not in range(6) or not justification.strip():
        raise ValueError('Decisão manual exige comportamento 0..5 e justificativa.')
    matches = [r for r in report['results'] if r['rowId'] == row_id]
    if len(matches) != 1:
        raise ValueError('Selecione um rowId único.')
    target = safe_path(root, relative)
    club = load_json(target)
    row = matches[0]['statistics']
    if club.get('schemaVersion') != '1.1' or (club.get('catalogClubId'), club.get('country'), club.get('year')) != (row['club'], row['country'], row['year']):
        raise ValueError('Clube/versão/país/ano diverge da evidência.')
    players = [p for p in club['players'] if p['id'] == row['playerCatalogId']]
    if len(players) != 1 or players[0]['position'] != row['position']:
        raise ValueError('Jogador/posição não pertence ao elenco alvo.')
    matches[0]['manualDecision'] = {'status': 'MANUAL', 'approved': True, 'behavior': behavior,
        'justification': justification.strip(), 'target': relative, 'targetSha256': digest(target.read_bytes())}
    matches[0]['status'] = 'MANUAL'
    return report


def apply_approved(report, row_id, root):
    validate_report(report)
    rows = [r for r in report['results'] if r['rowId'] == row_id]
    if len(rows) != 1:
        raise ValueError('rowId ausente/duplicado.')
    row = rows[0]; decision = row.get('manualDecision') or {}
    behavior = decision.get('behavior')
    if decision.get('status') != 'MANUAL' or decision.get('approved') is not True or type(behavior) is not int or behavior not in range(6) or not decision.get('justification', '').strip():
        raise ValueError('Aplique somente decisão manual explicitamente aprovada com justificativa.')
    target = safe_path(root, decision['target'])
    raw = target.read_bytes()
    club = load_json(target)
    stat = row['statistics']
    if club.get('schemaVersion') != '1.1' or (club.get('catalogClubId'), club.get('country'), club.get('year')) != (stat['club'], stat['country'], stat['year']):
        raise ValueError('Alvo diverge da aprovação.')
    parts = decision['target'].split('/')
    if len(parts) != 4 or parts[1] != str(stat['year']) or parts[2] != 'clubes':
        raise ValueError('Aplicação deve apontar para clube anual.')
    manifest = scope(root, parts[0], stat['year'], stat['country'])
    if 'clubes/' + parts[3] not in manifest.get('clubs', []):
        raise ValueError('Clube não referenciado pelo manifest.')
    players = [p for p in club.get('players', []) if p.get('id') == stat['playerCatalogId']]
    if len(players) != 1 or players[0].get('position') != stat['position'] or any('strength' in p for p in club['players']):
        raise ValueError('Elenco/posição/força individual incompatível.')
    if digest(raw) != decision['targetSha256']:
        if players[0].get('behavior') == behavior:
            # Exact replay is permitted only if every other field matches the approved original backup.
            backup = report_path(root, parts[0], stat['year'], decision['targetSha256'] + '.backup.json')
            original = load_json(backup)
            if digest(backup.read_bytes()) != decision['targetSha256']:
                raise ValueError('Backup modificado.')
            for p in original['players']:
                if p['id'] == stat['playerCatalogId']:
                    p['behavior'] = behavior
            if original == club:
                return {'status': 'JA_APLICADO', 'target': decision['target']}
        raise ValueError('Arquivo mudou após a aprovação; revise novamente.')
    players[0]['behavior'] = behavior  # the only catalog field this tool changes
    backup = report_path(root, parts[0], stat['year'], digest(raw) + '.backup.json')
    backup.parent.mkdir(parents=True, exist_ok=True)
    if not backup.exists():
        with backup.open('xb') as output:
            output.write(raw)
    elif backup.read_bytes() != raw:
        raise ValueError('Backup conflitante.')
    replacement = encoded(club)
    atomic_write(target, raw, replacement)
    return {'status': 'APLICADO', 'target': decision['target'], 'previousSha256': digest(raw),
            'sha256': digest(replacement), 'rowId': row_id, 'decision': decision}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    classify_cmd = sub.add_parser('classify')
    classify_cmd.add_argument('--input', required=True)
    classify_cmd.add_argument('--config')
    for command in [classify_cmd]:
        command.add_argument('--country', required=True)
        command.add_argument('--slug', required=True)
        command.add_argument('--year', type=int, required=True)
    review_cmd = sub.add_parser('review')
    review_cmd.add_argument('--behavior', type=int, required=True)
    review_cmd.add_argument('--justification', required=True)
    review_cmd.add_argument('--target', required=True, help='Caminho relativo à raiz de catálogos')
    apply_cmd = sub.add_parser('apply')
    for command in [review_cmd, apply_cmd]:
        command.add_argument('--report', required=True, help='Caminho relativo à raiz de catálogos')
        command.add_argument('--row-id', required=True)
    for command in [classify_cmd, review_cmd, apply_cmd]:
        command.add_argument('--root', required=True)
    args = parser.parse_args(argv)
    if args.command == 'classify':
        scope(args.root, args.slug, args.year, args.country)
        rows = read_rows(args.input)
        if any(r['country'] != args.country or r['year'] != args.year for r in rows):
            raise ValueError('Entrada contém país/ano fora do escopo solicitado.')
        report = classify(rows, load_json(args.config) if args.config else None)
        report['scope'] = {'country': args.country, 'year': args.year, 'slug': args.slug}
        output = report_path(args.root, args.slug, args.year, digest(encoded(report)) + '.json')
        if not output.exists():
            write_new(output, report)
        elif output.read_bytes() != encoded(report):
            raise ValueError('Relatório conflitante.')
        print(output)
    else:
        if not args.report.startswith('_pesquisa/comportamento/') or not args.report.endswith('.json'):
            raise ValueError('Relatório deve estar na pasta de evidências.')
        report = load_json(safe_path(args.root, args.report))
        if args.command == 'review':
            report = review(report, args.row_id, args.behavior, args.justification, args.root, args.target)
            s = report['scope']
            output = report_path(args.root, s['slug'], s['year'], digest(encoded(report)) + '.review.json')
            write_new(output, report)
            print(output)
        else:
            receipt = apply_approved(report, args.row_id, args.root)
            stat = next(r['statistics'] for r in report['results'] if r['rowId'] == args.row_id)
            slug = report['scope']['slug']
            output = report_path(args.root, slug, stat['year'], digest(encoded(receipt)) + '.receipt.json')
            if not output.exists():
                write_new(output, receipt)
            print(encoded(receipt).decode('utf-8'))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, TypeError) as error:
        raise SystemExit(str(error)) from error
