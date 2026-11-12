"""Local review of V1.6 behavior suggestions and explicit catalog application."""
from __future__ import annotations

import argparse
from contextlib import contextmanager, nullcontext
from datetime import date, datetime, timezone
import os
from pathlib import Path
import re
import sys

from behavior_classifier import NAMES, atomic_write, decode_json, digest, encoded, load_json, safe_path
from behavior_v16 import VERSION as CLASSIFIER_VERSION, classify
from behavior_apply_v16 import apply_changes, recover, validate_catalog

VERSION = 'behavior-approval-1.0'
RESEARCH = '_pesquisa/comportamento/v16'
POLICY_FILENAME = 'behavior-operators.local.json'
CLUB_PATH = re.compile(r'([a-z][a-z0-9-]{0,79})/([1-9][0-9]{0,3})/clubes/[A-Za-z0-9][A-Za-z0-9_-]{0,100}[.]json')
IDENTIFIER = re.compile(r'[A-Za-z0-9][A-Za-z0-9_-]{0,79}')


def sha(value):
    return digest(encoded(value))


def now():
    return datetime.now(timezone.utc).isoformat(timespec='seconds')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def text(value):
    return isinstance(value, str) and bool(value.strip())


def is_behavior(value):
    return type(value) is int and value in range(len(NAMES))


def behavior_name(value):
    return NAMES[value] if is_behavior(value) else None


def date_or_none(value):
    if value is not None:
        require(isinstance(value, str) and date.fromisoformat(value).isoformat() == value,
                'Data inválida. Use AAAA-MM-DD ou null.')
    return value


def os_account():
    if os.name != 'nt':
        import pwd
        return pwd.getpwuid(os.geteuid()).pw_name
    import ctypes
    buffer = ctypes.create_unicode_buffer(32768)
    length = ctypes.c_ulong(len(buffer))
    if not ctypes.windll.advapi32.GetUserNameW(buffer, ctypes.byref(length)):
        raise OSError('Não foi possível identificar a conta do Windows.')
    return buffer.value


def authorize(policy_path, alias):
    require(policy_path.is_file(), f'Política local de operadores ausente: {POLICY_FILENAME}.')
    policy = load_json(policy_path)
    account = policy.get('operators', {}).get(alias)
    require(isinstance(alias, str) and IDENTIFIER.fullmatch(alias) and text(account)
            and account.casefold() == os_account().casefold(),
            'Operador não autorizado pela política local para esta conta do sistema.')
    return alias


@contextmanager
def write_lock(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('a+b') as stream:
        stream.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        yield


class Ledger:
    """A local sequence of hashed events. Current status is derived from the history."""

    def __init__(self, path):
        self.path = path
        self.raw = path.read_bytes() if path.exists() else None
        data = decode_json(self.raw) if self.raw is not None else {'version': VERSION, 'events': []}
        require(data.get('version') == VERSION, 'Versão do histórico incompatível.')
        self.events = data['events']
        self.suggestions = {}
        self.decisions = {}
        self.latest = {}
        self.active = {}
        self.superseded = set()
        self.applications = {}
        previous = None
        for event in self.events:
            payload = {key: value for key, value in event.items() if key != 'eventId'}
            require(payload['previousEventId'] == previous and sha(payload) == event['eventId'],
                    'Histórico alterado ou fora de sequência.')
            previous = event['eventId']
            self.index(event)
        require(data.get('head') == previous, 'HEAD do histórico inconsistente.')

    def index(self, event):
        kind = event['kind']
        if kind == 'EVIDENCE':
            for item in event['data']['suggestions']:
                suggestion_id = item['suggestionId']
                require(suggestion_id == sha(item['evidence']) and suggestion_id not in self.suggestions,
                        'Identificador de sugestão inválido ou duplicado.')
                self.suggestions[suggestion_id] = item['evidence']
        elif kind == 'REVIEW':
            for decision in event['data']['decisions']:
                approval_id = decision['decisionId']
                content = {key: value for key, value in decision.items() if key != 'decisionId'}
                require(approval_id == sha(content) and approval_id not in self.decisions,
                        'Identificador de decisão inválido ou duplicado.')
                suggestion_id = decision['suggestionId']
                key = player_key(self.suggestions[suggestion_id])
                if decision['status'] == 'APPROVED':
                    prior = self.active.get(key)
                    require(prior == decision['supersedes'], 'Substituição inconsistente no histórico.')
                    if prior:
                        self.superseded.add(prior)
                    self.active[key] = approval_id
                self.latest[suggestion_id] = decision
                self.decisions[approval_id] = decision
        elif kind == 'APPLY':
            for approval_id in event['data']['approvalIds']:
                require(approval_id in self.decisions and approval_id not in self.applications,
                        'Aprovação ausente ou aplicação duplicada no histórico.')
                self.applications[approval_id] = event['data']
        else:
            raise ValueError('Evento de auditoria desconhecido.')

    def append(self, kind, data, operator):
        event = {
            'kind': kind, 'data': data, 'operator': operator, 'recordedAt': now(),
            'previousEventId': self.events[-1]['eventId'] if self.events else None,
        }
        event['eventId'] = sha(event)
        content = encoded({'version': VERSION, 'head': event['eventId'], 'events': [*self.events, event]})
        atomic_write(self.path, self.raw, content)
        self.index(event)
        self.raw = content
        self.events.append(event)
        return event['eventId']

    def has_committed(self, transaction_id, approval_ids):
        persisted = Ledger(self.path)
        return all(persisted.applications.get(item, {}).get('transactionId') == transaction_id
                   for item in approval_ids)


def player_key(evidence):
    scope = evidence['scope']
    return scope['year'], scope['playerId'].casefold()


def snapshot(root, relative):
    match = CLUB_PATH.fullmatch(relative)
    require(match, 'Alvo deve ser um JSON anual em país/ano/clubes.')
    slug, year = match.groups()
    manifest_path = safe_path(root, f'{slug}/{year}/manifest.json')
    manifest_raw = manifest_path.read_bytes()
    manifest = decode_json(manifest_raw)
    club_raw = safe_path(root, relative).read_bytes()
    club = decode_json(club_raw)
    country = manifest['country']
    require(manifest['schemaVersion'] == club['schemaVersion'] == '1.1'
            and manifest['year'] == club['year'] == int(year)
            and country['slug'] == slug and country['code'] == club['country']
            and relative.split('/', 2)[2] in manifest['clubs'],
            'Clube, país, ano, versão ou referência no manifest incompatível.')
    scope = {'catalogId': manifest['catalogId'], 'clubId': club['catalogClubId'],
             'country': club['country'], 'year': club['year']}
    return club_raw, club, digest(manifest_raw), scope


def find_player(club, player_id, position):
    require(isinstance(player_id, str) and IDENTIFIER.fullmatch(player_id)
            and position in ('G', 'D', 'M', 'A'), 'ID ou posição inválida.')
    players = [player for player in club['players'] if player['id'].casefold() == player_id.casefold()]
    require(len(players) == 1 and players[0]['id'] == player_id and players[0]['position'] == position,
            'Jogador inexistente, duplicado ou com posição incompatível.')
    behavior = players[0].get('behavior')
    require('strength' not in players[0] and (behavior is None or is_behavior(behavior)),
            'Força individual ou comportamento inválido no catálogo 1.1.')
    return players[0]


def verified_proposal(root, reference):
    if reference is None:
        return None, {}
    path = reference['path']
    require(path.startswith('_pesquisa/comportamento/') and path.endswith('.json'),
            'Relatório de sugestões deve estar em _pesquisa/comportamento/.')
    raw = safe_path(root, path).read_bytes()
    require(digest(raw) == reference['sha256'], 'Relatório de sugestões alterado.')
    report = decode_json(raw)
    require(report['version'] == CLASSIFIER_VERSION, 'Versão experimental incompatível.')
    rows = []
    by_id = {}
    normalized_ids = set()
    for result in report['results']:
        player_id = result['playerCatalogId']
        require(player_id.casefold() not in normalized_ids, 'Jogador duplicado no relatório experimental.')
        normalized_ids.add(player_id.casefold())
        by_id[player_id] = result
        rows.append({
            **result['historicalStatistics'],
            **{key: result[key] for key in ('playerCatalogId', 'position', 'initialDivision',
                                           'comparisonGroup', 'baseBehavior')},
        })
    require(sha(classify(rows, report['config'])) == sha(report),
            'Sugestões não reproduzíveis com a versão, configuração e estatísticas declaradas.')
    return report, by_id


def source_metadata(entries):
    require(isinstance(entries, list), 'Informe uma lista de fontes, vazia quando ausentes.')
    sources = []
    for entry in entries:
        require(text(entry.get('name')), 'Fonte informada exige nome.')
        reference = entry.get('reference')
        require(reference is None or text(reference), 'Referência de fonte inválida.')
        sources.append({
            'name': entry['name'], 'reference': reference,
            'referenceDate': date_or_none(entry.get('referenceDate')),
            'collectedAt': date_or_none(entry.get('collectedAt')),
        })
    require(len({sha(source) for source in sources}) == len(sources), 'Fontes duplicadas.')
    return sources


def register(root, ledger, request, operator):
    require(request.get('version') == VERSION, 'Versão de entrada incompatível.')
    proposal = request.get('proposal')
    report, by_id = verified_proposal(root, proposal)
    snapshots = {}
    entries = []
    seen = set()
    for item in request['players']:
        path = item['target']
        if path not in snapshots:
            snapshots[path] = snapshot(root, path)
        raw, club, manifest_hash, scope = snapshots[path]
        require(all(type(item[key]) is type(value) and item[key] == value for key, value in scope.items()),
                'Identidade de catálogo, clube, país ou ano diverge da evidência.')
        player = find_player(club, item['playerId'], item['position'])
        sources = source_metadata(item['sources'])
        conflict = item.get('sourceConflict')
        require(conflict is None or text(conflict), 'Descreva o conflito entre as fontes ou use null.')
        result = by_id.get(item['playerId'])
        if report is not None:
            require(result is not None and result['position'] == item['position']
                    and result['baseBehavior'] == player.get('behavior'),
                    'Sugestão não corresponde ao jogador, posição ou comportamento atual.')
        alerts = []
        if not sources:
            alerts.append('SOURCE_MISSING')
        elif not any(source['referenceDate'] for source in sources):
            alerts.append('REFERENCE_DATE_MISSING')
        if conflict:
            alerts.append('SOURCE_CONFLICT')
        suggested = result['effectiveBehaviorProposal'] if result else None
        if suggested is None:
            alerts.append('INSUFFICIENT_STATISTICS' if report else 'MANUAL_RESEARCH')
        evidence = {
            'version': VERSION, 'scope': {**scope, 'playerId': item['playerId'], 'position': item['position']},
            'target': path, 'targetSha256': digest(raw), 'manifestSha256': manifest_hash,
            'name': player.get('name'), 'currentBehavior': player.get('behavior'),
            'sourceStatus': 'DECLARED' if sources else 'MISSING', 'sources': sources,
            'sourceConflict': conflict, 'alerts': alerts,
            'quality': 'REVIEW_REQUIRED' if alerts else 'EXPERIMENTAL_SAMPLE',
            'initialStatus': 'REVIEW_REQUIRED' if alerts else 'PENDING',
            'suggestedBehavior': suggested, 'suggestedName': behavior_name(suggested), 'confidence': None,
            'proposal': proposal, 'classifierVersion': CLASSIFIER_VERSION if report else None,
            'classifierConfig': report['config'] if report else None, 'classification': result,
        }
        suggestion_id = sha(evidence)
        key = player_key(evidence)
        require(key not in seen and suggestion_id not in ledger.suggestions, 'Sugestão ou jogador duplicado.')
        seen.add(key)
        entries.append({'suggestionId': suggestion_id, 'evidence': evidence})
    require(entries, 'Selecione pelo menos um jogador.')
    event_id = ledger.append('EVIDENCE', {'suggestions': entries}, operator)
    return {'eventId': event_id, 'suggestionIds': [entry['suggestionId'] for entry in entries]}


def assert_report_current(root, evidence, verified_reports):
    proposal = evidence['proposal']
    if proposal and (proposal['path'], proposal['sha256']) not in verified_reports:
        require(digest(safe_path(root, proposal['path']).read_bytes()) == proposal['sha256'],
                'Evidência alterada após registro. Registre e revise novamente.')
        verified_reports.add((proposal['path'], proposal['sha256']))


def assert_current(root, evidence, snapshots, verified_reports):
    assert_report_current(root, evidence, verified_reports)
    path = evidence['target']
    if path not in snapshots:
        snapshots[path] = snapshot(root, path)
    raw, club, manifest_hash, scope = snapshots[path]
    require(digest(raw) == evidence['targetSha256'] and manifest_hash == evidence['manifestSha256']
            and all(evidence['scope'][key] == value for key, value in scope.items()),
            'JSON do clube ou manifest alterado. Registre e revise novamente.')
    player = find_player(club, evidence['scope']['playerId'], evidence['scope']['position'])
    return player


def plan_review(root, ledger, selected, operator):
    seen = set()
    snapshots, verified_reports = {}, set()
    decisions = []
    for selection in selected:
        suggestion_id = selection['suggestionId']
        evidence = ledger.suggestions[suggestion_id]
        key = player_key(evidence)
        require(key not in seen, 'Jogador duplicado na seleção.')
        seen.add(key)
        prior_decision = ledger.latest.get(suggestion_id)
        require(not prior_decision or prior_decision['action'] == 'DEFER',
                'Sugestão já decidida. Registre nova evidência para uma revisão.')
        action = selection['action']
        reason = selection['reason']
        require(action in ('ACCEPT', 'MANUAL', 'REJECT', 'DEFER') and text(reason),
                'Informe ação explícita e justificativa para cada jogador selecionado.')
        supplied = selection.get('behavior')
        require(action == 'MANUAL' or supplied is None, 'Informe behavior somente na decisão manual.')
        supersedes = selection.get('supersedes')
        if action in ('ACCEPT', 'MANUAL'):
            assert_current(root, evidence, snapshots, verified_reports)
            if action == 'ACCEPT':
                require(evidence['initialStatus'] == 'PENDING' and is_behavior(evidence['suggestedBehavior']),
                        'Sugestão exige revisão. Use decisão manual fundamentada ou rejeite/adie.')
                value = evidence['suggestedBehavior']
            else:
                require(is_behavior(supplied), 'Decisão manual exige behavior inteiro de 0 a 5.')
                value = supplied
            require(supersedes == ledger.active.get(key),
                    'Já existe aprovação ativa ou a referência foi substituída. Confirme o ID atual em supersedes.')
            current = evidence['currentBehavior']
            if current is not None and value != current:
                require(selection.get('replaceCatalogBehavior') is True,
                        'Confirme explicitamente a troca do behavior já presente no catálogo.')
            status = 'APPROVED'
        else:
            require(supersedes is None, 'Rejeitar ou adiar não substitui aprovação existente.')
            value = None
            status = 'REJECTED' if action == 'REJECT' else evidence['initialStatus']
        decision = {
            'suggestionId': suggestion_id, 'evidenceSha256': sha(evidence), 'action': action,
            'status': status, 'suggestedBehavior': evidence['suggestedBehavior'], 'approvedBehavior': value,
            'approvedName': behavior_name(value),
            'supersedes': supersedes, 'operator': operator, 'decidedAt': now(), 'reason': reason.strip(),
        }
        decisions.append({**decision, 'decisionId': sha(decision)})
    require(decisions, 'Selecione explicitamente pelo menos um jogador.')
    return decisions


def list_suggestions(root, ledger):
    snapshots, verified_reports = {}, set()
    rows = []
    for suggestion_id, evidence in ledger.suggestions.items():
        latest = ledger.latest.get(suggestion_id)
        status = latest['status'] if latest else evidence['initialStatus']
        decision_id = latest['decisionId'] if latest else None
        if decision_id in ledger.superseded:
            status = 'SUPERSEDED'
        alert = None
        if status in ('APPROVED', 'PENDING', 'REVIEW_REQUIRED') and decision_id not in ledger.applications:
            try:
                assert_current(root, evidence, snapshots, verified_reports)
            except (ValueError, OSError, KeyError) as error:
                alert = str(error)
        rows.append({
            'suggestionId': suggestion_id, **evidence, 'status': status,
            'decision': latest, 'activeApprovalId': ledger.active.get(player_key(evidence)),
            'applied': decision_id in ledger.applications, 'staleAlert': alert,
        })
    return rows


def plan_application(root, ledger, approval_ids):
    require(isinstance(approval_ids, list) and approval_ids
            and all(isinstance(value, str) for value in approval_ids)
            and len(set(approval_ids)) == len(approval_ids), 'Selecione IDs de aprovação distintos.')
    snapshots, verified_reports, changes = {}, set(), []
    replay = all(approval_id in ledger.applications for approval_id in approval_ids)
    if not replay:
        require(not any(approval_id in ledger.applications for approval_id in approval_ids),
                'Separe as aprovações já aplicadas das novas.')
    for approval_id in approval_ids:
        decision = ledger.decisions[approval_id]
        evidence = ledger.suggestions[decision['suggestionId']]
        require(decision['status'] == 'APPROVED' and decision['evidenceSha256'] == sha(evidence)
                and ledger.active.get(player_key(evidence)) == approval_id,
                'Aprovação inválida, rejeitada ou substituída.')
        if replay:
            assert_report_current(root, evidence, verified_reports)
            receipt = ledger.applications[approval_id]
            path = evidence['target']
            current, _, manifest_hash, _ = snapshot(root, path)
            require(digest(current) == receipt['files'][path]['afterSha256']
                    and manifest_hash == evidence['manifestSha256'],
                    'Arquivo alterado desde a aplicação. Reaplicação bloqueada.')
            continue
        player = assert_current(root, evidence, snapshots, verified_reports)
        value = decision['approvedBehavior']
        require(is_behavior(value), 'Comportamento aprovado inválido.')
        changes.append({'approvalId': approval_id, 'target': evidence['target'],
                        'playerId': player['id'], 'before': player.get('behavior'), 'after': value})
        player['behavior'] = value
    files = {}
    for path, (before, club, manifest_hash, _) in snapshots.items():
        # Retain exact bytes when a decision confirms an existing value.
        after = before if decode_json(before) == club else encoded(club)
        files[path] = (before, after, manifest_hash)
    return {'status': 'ALREADY_APPLIED' if replay else 'PLANNED', 'changes': changes}, files


def check_application_evidence(root, ledger, approval_ids):
    verified = set()
    for item in approval_ids:
        assert_report_current(root, ledger.suggestions[ledger.decisions[item]['suggestionId']], verified)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True, help='Raiz local dos catálogos')
    parser.add_argument('--operator', help='Alias autorizado da conta do sistema')
    commands = parser.add_subparsers(dest='command', required=True)
    commands.add_parser('list')
    commands.add_parser('history')
    commands.add_parser('register').add_argument('--input', required=True)
    review = commands.add_parser('review')
    review.add_argument('--input', required=True)
    review.add_argument('--confirm', action='store_true')
    apply = commands.add_parser('apply')
    apply.add_argument('--approval-id', action='append', required=True)
    apply.add_argument('--validator-dll', help='Elifoot.Api.dll do build local Release')
    mode = apply.add_mutually_exclusive_group()
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--confirm', action='store_true')
    commands.add_parser('recover').add_argument('--confirm', required=True, action='store_true')
    args = parser.parse_args(argv)
    root = Path(args.root).absolute()
    require(root.is_dir(), 'Raiz dos catálogos não encontrada.')
    ledger_path = safe_path(root, f'{RESEARCH}/history.json')
    journal = safe_path(root, f'{RESEARCH}/transaction.json')
    lock = safe_path(root, f'{RESEARCH}/workflow.lock')
    command = args.command
    needs_operator = command not in ('list', 'history')
    if needs_operator:
        require(args.operator, 'Informe um alias de operador autorizado.')
        policy = safe_path(root.parent, POLICY_FILENAME)
        authorize(policy, args.operator)
    writes = command == 'register' or getattr(args, 'confirm', False)
    with write_lock(lock) if writes else nullcontext():
        ledger = Ledger(ledger_path)
        if command == 'recover':
            return recover(root, journal, ledger)
        require(not journal.exists() or command in ('list', 'history'),
                'Transação interrompida. Execute recover --confirm antes de prosseguir.')
        if command == 'list':
            return list_suggestions(root, ledger)
        if command == 'history':
            return {'version': VERSION, 'events': ledger.events}
        if command == 'register':
            return register(root, ledger, load_json(args.input), args.operator)
        if command == 'review':
            request = load_json(args.input)
            require(request.get('version') == VERSION, 'Versão de revisão incompatível.')
            decisions = plan_review(root, ledger, request['selected'], args.operator)
            if not args.confirm:
                return {'status': 'AWAITING_CONFIRMATION', 'decisions': decisions}
            event_id = ledger.append('REVIEW', {'decisions': decisions}, args.operator)
            return {'status': 'RECORDED', 'eventId': event_id, 'decisions': decisions}
        plan, files = plan_application(root, ledger, args.approval_id)
        if not args.confirm or plan['status'] == 'ALREADY_APPLIED':
            return plan
        require(args.validator_dll, 'Aplicação exige o validador local Elifoot.Api.dll.')
        return apply_changes(root, journal, ledger, args.approval_id, files, plan['changes'],
                             args.operator, lambda path: validate_catalog(path, args.validator_dll),
                             lambda: check_application_evidence(root, ledger, args.approval_id))


if __name__ == '__main__':
    try:
        sys.stdout.buffer.write(encoded(main()))
    except (ValueError, OSError, KeyError, TypeError) as error:
        raise SystemExit(str(error)) from error
