import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import behavior_approval_v16 as workflow
import behavior_apply_v16 as transaction
from behavior_classifier import digest, encoded, load_json
from behavior_v16 import classify


class BehaviorApprovalTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory(prefix='elifoot-approval-test-')
        self.addCleanup(folder.cleanup)
        self.workspace = Path(folder.name)
        self.root = self.workspace / 'catalogos'
        self.history = self.root / workflow.RESEARCH / 'history.json'
        self.journal = self.root / workflow.RESEARCH / 'transaction.json'
        self.target = 'brasil/1998/clubes/synthetic-a.json'
        self.other = 'brasil/1998/clubes/synthetic-b.json'
        self.manifest = 'brasil/1998/manifest.json'
        self.proposal = '_pesquisa/comportamento/test-proposal.json'
        self.operator = 'operador-sintetico'
        self.write(self.manifest, {
            'schemaVersion': '1.1', 'catalogId': 'br-synthetic', 'country': {
                'code': 'BR', 'name': 'Brasil', 'slug': 'brasil'},
            'year': 1998, 'name': 'Catálogo sintético', 'status': 'DRAFT', 'gender': 'Male',
            'clubs': ['clubes/synthetic-a.json', 'clubes/synthetic-b.json'],
        })
        self.make_club(self.target, 'synthetic-a', 'p1', 'p2')
        self.make_club(self.other, 'synthetic-b', 'p3')
        self.write_proposal()

    def write(self, relative, value):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(encoded(value))

    def make_club(self, relative, club_id, *players):
        self.write(f'identidades/{club_id}/identidade.json', {
            'schemaVersion': '1.1', 'catalogClubId': club_id, 'name': 'Clube sintético',
            'shortName': 'Sintético', 'abbreviation': 'SYN', 'colors': ['#112233', '#FFFFFF'],
            'crestFile': None,
        })
        self.write(relative, {
            'schemaVersion': '1.1', 'catalogClubId': club_id, 'country': 'BR', 'year': 1998,
            'status': 'DRAFT', 'teamStrength': 10, 'historicalRanking': None, 'tieBreakPriority': 1,
            'stadium': None, 'coach': None,
            'players': [{'id': player_id, 'name': f'Atleta sintético {player_id}', 'position': 'D',
                         'nationality': 'BR', 'isStar': False, 'behavior': None} for player_id in players],
        })

    def write_proposal(self, minutes=900):
        rows = [{
            'playerCatalogId': player_id, 'position': 'D', 'initialDivision': 1,
            'comparisonGroup': 'temporada-sintetica', 'baseBehavior': None, 'minutes': minutes,
            'fouls': i * 10, 'yellowCards': 0, 'redCards': 0,
        } for i, player_id in enumerate(('p1', 'p2', 'p3'))]
        self.write(self.proposal, classify(rows))

    def metadata(self, player_id='p1', **fields):
        other = player_id == 'p3'
        item = {
            'catalogId': 'br-synthetic', 'clubId': 'synthetic-b' if other else 'synthetic-a',
            'country': 'BR', 'year': 1998, 'playerId': player_id, 'position': 'D',
            'target': self.other if other else self.target,
            'sources': [{'name': 'Arquivo sintético', 'reference': 'acervo-teste/p.1',
                         'referenceDate': '1998-06-30', 'collectedAt': None}],
        }
        return {**item, **fields}

    def ledger(self):
        return workflow.Ledger(self.history)

    def register(self, *players, manual=False):
        proposal = None if manual else {
            'path': self.proposal, 'sha256': digest((self.root / self.proposal).read_bytes()),
        }
        return workflow.register(self.root, self.ledger(), {
            'version': workflow.VERSION, 'proposal': proposal, 'players': list(players),
        }, self.operator)['suggestionIds']

    def selection(self, suggestion_id, action='ACCEPT', **fields):
        return {'suggestionId': suggestion_id, 'action': action,
                'reason': 'Decisão sintética documentada.', **fields}

    def review(self, *selected):
        ledger = self.ledger()
        decisions = workflow.plan_review(self.root, ledger, list(selected), self.operator)
        ledger.append('REVIEW', {'decisions': decisions}, self.operator)
        return decisions

    def approved(self, *players):
        ids = self.register(*(self.metadata(player_id) for player_id in players))
        return [entry['decisionId'] for entry in self.review(*(self.selection(item) for item in ids))]

    def apply(self, approval_ids, validate=None):
        ledger = self.ledger()
        plan, files = workflow.plan_application(self.root, ledger, approval_ids)
        if plan['status'] == 'ALREADY_APPLIED':
            return plan
        return transaction.apply_changes(
            self.root, self.journal, ledger, approval_ids, files, plan['changes'], self.operator,
            validate or (lambda _: {'valid': True}),
            lambda: workflow.check_application_evidence(self.root, ledger, approval_ids),
        )

    def test_new_suggestion_pending_with_reproducible_evidence(self):
        suggestion_id, = self.register(self.metadata())
        row, = workflow.list_suggestions(self.root, self.ledger())
        self.assertEqual('PENDING', row['status'])
        self.assertEqual(suggestion_id, workflow.sha(self.ledger().suggestions[suggestion_id]))
        self.assertEqual(900, row['classification']['historicalStatistics']['minutes'])
        self.assertEqual(5, row['classifierConfig']['weights']['redCards'])
        self.assertEqual('acervo-teste/p.1', row['sources'][0]['reference'])
        self.assertIsNone(row['confidence'])
        self.assertIsNone(row['decision'])

    def test_individual_accept_preserves_suggestion_and_does_not_apply(self):
        suggestion_id, = self.register(self.metadata())
        before = (self.root / self.target).read_bytes()
        decision, = self.review(self.selection(suggestion_id))
        self.assertEqual('APPROVED', decision['status'])
        self.assertEqual(decision['suggestedBehavior'], decision['approvedBehavior'])
        self.assertEqual(self.operator, decision['operator'])
        self.assertEqual(before, (self.root / self.target).read_bytes())

    def test_individual_reject_cannot_be_applied(self):
        suggestion_id, = self.register(self.metadata())
        decision, = self.review(self.selection(suggestion_id, 'REJECT'))
        self.assertEqual('REJECTED', decision['status'])
        with self.assertRaisesRegex(ValueError, 'rejeitada'):
            self.apply([decision['decisionId']])

    def test_manual_correction_preserves_original_suggestion(self):
        suggestion_id, = self.register(self.metadata())
        decision, = self.review(self.selection(suggestion_id, 'MANUAL', behavior=5))
        self.assertNotEqual(decision['suggestedBehavior'], decision['approvedBehavior'])
        self.assertEqual('SARRAFEIRO', decision['approvedName'])

    def test_partial_batch_and_explicit_deferral(self):
        ids = self.register(*(self.metadata(player_id) for player_id in ('p1', 'p2', 'p3')))
        self.review(self.selection(ids[0]), self.selection(ids[2], 'DEFER'))
        self.assertEqual(['APPROVED', 'PENDING', 'PENDING'],
                         [row['status'] for row in workflow.list_suggestions(self.root, self.ledger())])
        self.review(self.selection(ids[2], 'REJECT'))
        self.assertEqual('REJECTED', self.ledger().latest[ids[2]]['status'])

    def test_batch_can_reject_all_explicitly_selected(self):
        ids = self.register(self.metadata(), self.metadata('p3'))
        decisions = self.review(*(self.selection(item, 'REJECT') for item in ids))
        self.assertEqual(['REJECTED', 'REJECTED'], [decision['status'] for decision in decisions])

    def test_review_confirmation_and_dry_run_leave_files_untouched(self):
        suggestion_id, = self.register(self.metadata())
        policy = self.workspace / workflow.POLICY_FILENAME
        policy.write_bytes(encoded({'operators': {self.operator: workflow.os_account()}}))
        request = self.workspace / 'review.json'
        request.write_bytes(encoded({'version': workflow.VERSION, 'selected': [self.selection(suggestion_id)]}))
        base = ['--root', str(self.root), '--operator', self.operator]
        original = self.history.read_bytes()
        args = [*base, 'review', '--input', str(request)]
        self.assertEqual('AWAITING_CONFIRMATION', workflow.main(args)['status'])
        self.assertEqual(original, self.history.read_bytes())
        result = workflow.main([*args, '--confirm'])
        approval = result['decisions'][0]['decisionId']
        before = {path.relative_to(self.root): path.read_bytes()
                  for path in self.root.rglob('*') if path.is_file()}
        for flags in ([], ['--dry-run']):
            plan = workflow.main([*base, 'apply', '--approval-id', approval, *flags])
            self.assertEqual('PLANNED', plan['status'])
        self.assertEqual(before, {path.relative_to(self.root): path.read_bytes()
                                  for path in self.root.rglob('*') if path.is_file()})

    def test_missing_source_is_explicit_and_cannot_use_accept(self):
        suggestion_id, = self.register(self.metadata(sources=[]))
        row, = workflow.list_suggestions(self.root, self.ledger())
        self.assertEqual('MISSING', row['sourceStatus'])
        self.assertEqual('REVIEW_REQUIRED', row['status'])
        with self.assertRaisesRegex(ValueError, 'exige revisão'):
            self.review(self.selection(suggestion_id))

    def test_insufficient_minutes_never_default_to_fair_play(self):
        self.write_proposal(minutes=899)
        suggestion_id, = self.register(self.metadata())
        evidence = self.ledger().suggestions[suggestion_id]
        self.assertIn('INSUFFICIENT_STATISTICS', evidence['alerts'])
        self.assertIsNone(evidence['suggestedBehavior'])

    def test_conflicting_sources_are_highlighted_and_need_manual_decision(self):
        suggestion_id, = self.register(self.metadata(sourceConflict='Os registros sintéticos divergem.'))
        row, = workflow.list_suggestions(self.root, self.ledger())
        self.assertIn('SOURCE_CONFLICT', row['alerts'])
        self.assertEqual('REVIEW_REQUIRED', row['status'])
        with self.assertRaisesRegex(ValueError, 'exige revisão'):
            self.review(self.selection(suggestion_id))

    def test_manual_without_statistics_supports_all_six_codes(self):
        for code in range(6):
            with self.subTest(code=code):
                metadata = self.metadata(sources=[{'name': f'Pesquisa sintética {code}'}])
                suggestion_id, = self.register(metadata, manual=True)
                ledger = self.ledger()
                previous = ledger.active.get((1998, 'p1'))
                decision, = self.review(self.selection(suggestion_id, 'MANUAL', behavior=code,
                                                       supersedes=previous, replaceCatalogBehavior=True))
                self.assertIsNone(decision['suggestedBehavior'])
                self.apply([decision['decisionId']])
                self.assertEqual(code, load_json(self.root / self.target)['players'][0]['behavior'])

    def test_invalid_behavior_rejected_including_boolean(self):
        suggestion_id, = self.register(self.metadata(), manual=True)
        for value in (-1, 6, True, 3.0, None):
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, 'inteiro de 0 a 5'):
                self.review(self.selection(suggestion_id, 'MANUAL', behavior=value))

    def test_unknown_player_club_and_incompatible_scope(self):
        with self.assertRaisesRegex(ValueError, 'Jogador inexistente'):
            self.register(self.metadata('absent'), manual=True)
        with self.assertRaises(FileNotFoundError):
            self.register(self.metadata(target='brasil/1998/clubes/absent.json'), manual=True)
        for field, value in [('clubId', 'wrong'), ('catalogId', 'wrong'), ('year', 2026), ('country', 'ES')]:
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'diverge'):
                self.register(self.metadata(**{field: value}), manual=True)

    def test_evidence_tampering_before_registration_review_or_application(self):
        proposal_path = self.root / self.proposal
        original = proposal_path.read_bytes()
        tampered = load_json(proposal_path)
        tampered['results'][0]['effectiveBehaviorProposal'] = 5
        self.write(self.proposal, tampered)
        with self.assertRaisesRegex(ValueError, 'não reproduzíveis'):
            self.register(self.metadata())
        proposal_path.write_bytes(original)
        suggestion_id, = self.register(self.metadata())
        proposal_path.write_bytes(original + b' ')
        with self.assertRaisesRegex(ValueError, 'Evidência alterada'):
            self.review(self.selection(suggestion_id))
        proposal_path.write_bytes(original)
        decision, = self.review(self.selection(suggestion_id))
        proposal_path.write_bytes(original + b' ')
        with self.assertRaisesRegex(ValueError, 'Evidência alterada'):
            self.apply([decision['decisionId']])

    def test_json_and_manifest_changes_after_approval_are_conflicts(self):
        approvals = self.approved('p1')
        for relative in (self.target, self.manifest):
            path = self.root / relative
            before = path.read_bytes()
            path.write_bytes(before + b' ')
            self.assertIsNotNone(workflow.list_suggestions(self.root, self.ledger())[0]['staleAlert'])
            with self.assertRaisesRegex(ValueError, 'alterado'):
                self.apply(approvals)
            path.write_bytes(before)

    def test_duplicate_suggestions_selections_and_approvals_blocked(self):
        with self.assertRaisesRegex(ValueError, 'duplicado'):
            self.register(self.metadata(), self.metadata())
        suggestion_id, = self.register(self.metadata())
        with self.assertRaisesRegex(ValueError, 'duplicado'):
            self.register(self.metadata())
        with self.assertRaisesRegex(ValueError, 'duplicado'):
            self.review(self.selection(suggestion_id), self.selection(suggestion_id))
        decision, = self.review(self.selection(suggestion_id))
        approval = decision['decisionId']
        with self.assertRaisesRegex(ValueError, 'distintos'):
            self.apply([approval, approval])

    def test_batch_application_preserves_fields_and_is_idempotent(self):
        approvals = self.approved('p1', 'p2', 'p3')
        previous = {path: load_json(self.root / path) for path in (self.target, self.other)}
        receipt = self.apply(approvals)
        self.assertEqual('APPLIED', receipt['status'])
        self.assertEqual(3, len(receipt['changes']))
        self.assertEqual(2, len(receipt['files']))
        for change in receipt['changes']:
            club = previous[change['target']]
            player = next(p for p in club['players'] if p['id'] == change['playerId'])
            player['behavior'] = change['after']
        for path, expected in previous.items():
            self.assertEqual(expected, load_json(self.root / path))
        history = self.history.read_bytes()
        self.assertEqual('ALREADY_APPLIED', self.apply(approvals)['status'])
        self.assertEqual(history, self.history.read_bytes())

    def test_no_silent_replacement_of_approved_or_catalog_behavior(self):
        first, = self.approved('p1')
        self.apply([first])
        next_id, = self.register(self.metadata(sources=[{'name': 'Outra pesquisa'}]), manual=True)
        with self.assertRaisesRegex(ValueError, 'aprovação ativa'):
            self.review(self.selection(next_id, 'MANUAL', behavior=5))
        with self.assertRaisesRegex(ValueError, 'troca'):
            self.review(self.selection(next_id, 'MANUAL', behavior=5, supersedes=first))
        second, = self.review(self.selection(next_id, 'MANUAL', behavior=5, supersedes=first,
                                            replaceCatalogBehavior=True))
        self.assertEqual('SUPERSEDED', workflow.list_suggestions(self.root, self.ledger())[0]['status'])
        self.assertIn(first, self.ledger().decisions)
        with self.assertRaisesRegex(ValueError, 'substituída'):
            self.apply([first])
        self.apply([second['decisionId']])
        self.assertEqual(5, load_json(self.root / self.target)['players'][0]['behavior'])

    def test_history_hash_detects_alteration(self):
        self.approved('p1')
        history = load_json(self.history)
        history['events'][0]['operator'] = 'other'
        self.history.write_bytes(encoded(history))
        with self.assertRaisesRegex(ValueError, 'Histórico alterado'):
            self.ledger()

    def test_unauthorized_alias_does_not_create_history_or_lock(self):
        policy = self.workspace / workflow.POLICY_FILENAME
        policy.write_bytes(encoded({'operators': {self.operator: 'another-system-account'}}))
        with self.assertRaisesRegex(ValueError, 'não autorizado'):
            workflow.main(['--root', str(self.root), '--operator', self.operator,
                           'register', '--input', str(policy)])
        self.assertFalse(self.history.parent.exists())

    def test_second_writer_cannot_acquire_catalog_lock(self):
        lock = self.history.parent / 'workflow.lock'
        with workflow.write_lock(lock):
            with self.assertRaises(OSError), workflow.write_lock(lock):
                self.fail('A segunda instância adquiriu o bloqueio.')
        with workflow.write_lock(lock):
            self.assertTrue(lock.exists())

    def test_catalog_v10_cannot_be_a_write_target(self):
        club = load_json(self.root / self.target)
        club['schemaVersion'] = '1.0'
        self.write(self.target, club)
        with self.assertRaisesRegex(ValueError, 'versão'):
            self.register(self.metadata(), manual=True)

    def test_failed_preflight_writes_no_catalog_or_journal(self):
        approvals = self.approved('p1', 'p3')
        original = (self.root / self.target).read_bytes()

        def invalid(_):
            raise ValueError('Catálogo inválido na cópia.')

        with self.assertRaisesRegex(ValueError, 'cópia'):
            self.apply(approvals, invalid)
        self.assertEqual(original, (self.root / self.target).read_bytes())
        self.assertFalse(self.journal.exists())

    def test_failure_second_replace_rolls_back_entire_batch(self):
        approvals = self.approved('p1', 'p3')
        original = {path: (self.root / path).read_bytes() for path in (self.target, self.other)}
        real_write = transaction.atomic_write

        def fail_second(path, before, after):
            if path == self.root / self.other:
                raise OSError('Falha sintética no segundo clube.')
            real_write(path, before, after)

        with patch.object(transaction, 'atomic_write', fail_second), self.assertRaisesRegex(OSError, 'segundo'):
            self.apply(approvals)
        for path, value in original.items():
            self.assertEqual(value, (self.root / path).read_bytes())
        self.assertFalse(self.journal.exists())
        self.assertFalse(self.ledger().applications)

    def test_external_change_during_batch_is_preserved(self):
        approvals = self.approved('p1', 'p3')
        first = self.root / self.target
        second = self.root / self.other
        original = first.read_bytes()
        external = second.read_bytes() + b' '
        real_write = transaction.atomic_write

        def concurrent_write(path, before, after):
            real_write(path, before, after)
            if path == first and before == original:
                second.write_bytes(external)

        with patch.object(transaction, 'atomic_write', concurrent_write), self.assertRaisesRegex(ValueError, 'externa'):
            self.apply(approvals)
        self.assertEqual(original, first.read_bytes())
        self.assertEqual(external, second.read_bytes())
        self.assertTrue(self.journal.exists())
        with self.assertRaisesRegex(ValueError, 'externa'):
            transaction.recover(self.root, self.journal, self.ledger())

    def test_post_validation_failure_rolls_back(self):
        approvals = self.approved('p1', 'p3')
        original = (self.root / self.target).read_bytes()
        calls = []

        def post_failure(root):
            calls.append(root)
            if root == self.root:
                raise ValueError('Falha pós-aplicação.')
            return {'valid': True}

        with self.assertRaisesRegex(ValueError, 'pós-aplicação'):
            self.apply(approvals, post_failure)
        self.assertEqual(2, len(calls))
        self.assertEqual(original, (self.root / self.target).read_bytes())
        self.assertFalse(self.journal.exists())

    def test_evidence_changed_during_validation_blocks_whole_batch(self):
        approvals = self.approved('p1', 'p3')
        proposal = self.root / self.proposal
        before = (self.root / self.target).read_bytes()

        def change_evidence(root):
            if root == self.root:
                proposal.write_bytes(proposal.read_bytes() + b' ')
            return {'valid': True}

        with self.assertRaisesRegex(ValueError, 'Evidência alterada'):
            self.apply(approvals, change_evidence)
        self.assertEqual(before, (self.root / self.target).read_bytes())
        self.assertFalse(self.ledger().applications)
        self.assertFalse(self.journal.exists())

    def test_recovery_after_process_interruption(self):
        approvals = self.approved('p1', 'p3')
        _, files = workflow.plan_application(self.root, self.ledger(), approvals)
        self.journal.write_bytes(encoded(transaction.journal_data(approvals, files)))
        target = self.root / self.target
        before, after, _ = files[self.target]
        target.write_bytes(after)
        receipt = transaction.recover(self.root, self.journal, self.ledger())
        self.assertEqual('ROLLED_BACK', receipt['status'])
        self.assertEqual(before, target.read_bytes())
        self.assertFalse(self.journal.exists())

    def test_recovery_after_receipt_preserves_applied_catalog(self):
        approvals = self.approved('p1')
        _, files = workflow.plan_application(self.root, self.ledger(), approvals)
        self.apply(approvals)
        self.journal.write_bytes(encoded(transaction.journal_data(approvals, files)))
        self.assertEqual('ALREADY_COMMITTED',
                         transaction.recover(self.root, self.journal, self.ledger())['status'])
        self.assertEqual(files[self.target][1], (self.root / self.target).read_bytes())
        self.assertEqual('ALREADY_APPLIED', self.apply(approvals)['status'])

    def test_source_url_is_stored_without_network(self):
        source = {'name': 'Acervo sintético', 'reference': 'https://example.invalid/test',
                  'referenceDate': '1998-06-30'}
        with patch('socket.socket', side_effect=AssertionError('O fluxo tentou abrir uma conexão.')):
            suggestion, = self.register(self.metadata(sources=[source]))
            decision, = self.review(self.selection(suggestion))
            self.apply([decision['decisionId']])
        evidence = self.ledger().suggestions[suggestion]
        self.assertEqual(source['reference'], evidence['sources'][0]['reference'])

    @unittest.skipUnless(os.environ.get('ELIFOOT_TEST_VALIDATOR_DLL'), 'Configure Elifoot.Api.dll local para integração.')
    def test_real_validator_accepts_draft_then_ready_and_preserves_v10(self):
        dll = os.environ['ELIFOOT_TEST_VALIDATOR_DLL']
        validate = lambda root: transaction.validate_catalog(root, dll)
        model = Path(__file__).resolve().parents[3] / 'catalogos' / 'modelos' / 'exemplo' / 'brasil' / '1998'
        # Use a separate country/year path for a preexisting V1.0 fixture, leaving its bytes intact.
        legacy = self.root / 'portugal' / '1998'
        shutil.copytree(model, legacy)
        manifest = load_json(legacy / 'manifest.json')
        manifest['country'] = {'code': 'PT', 'name': 'Portugal', 'slug': 'portugal'}
        manifest['catalogId'] = 'pt-legacy'
        (legacy / 'manifest.json').write_bytes(encoded(manifest))
        for ref in manifest['clubs']:
            club = load_json(legacy / ref)
            club['country'] = 'PT'
            for player in club['players']:
                player['id'] = 'legacy-' + player['id']
            (legacy / ref).write_bytes(encoded(club))
        previous = {path: path.read_bytes() for path in legacy.rglob('*.json')}
        self.assertEqual(2, validate(self.root)['catalogCount'])
        receipt = self.apply(self.approved('p1'), validate)
        self.assertEqual('DRAFT', load_json(self.root / self.target)['status'])
        self.assertEqual(2, receipt['validation']['catalogCount'])

        club = load_json(self.root / self.target)
        club['status'] = 'READY'
        club['coach'] = {'id': 'synthetic-ready-coach', 'name': 'Treinador sintético',
                         'nationality': 'BR', 'preferredTactics': {'home': ['4-4-2'], 'away': []}}
        club['players'][1]['behavior'] = 2
        for index, position in enumerate('GDDDMMMMA'):
            club['players'].append({'id': f'synthetic-ready-{index}', 'name': 'Atleta sintético',
                                    'position': position, 'nationality': 'BR', 'behavior': 2, 'isStar': False})
        self.write(self.target, club)
        updated_manifest = load_json(self.root / self.manifest)
        updated_manifest.update(status='READY', clubs=['clubes/synthetic-a.json'])
        self.write(self.manifest, updated_manifest)
        suggestion_id, = self.register(self.metadata(), manual=True)
        previous_approval = self.ledger().active[(1998, 'p1')]
        decision, = self.review(self.selection(suggestion_id, 'MANUAL', behavior=3,
                                               supersedes=previous_approval, replaceCatalogBehavior=True))
        ready = self.apply([decision['decisionId']], validate)
        self.assertGreaterEqual(ready['validation']['readyCatalogCount'], 1)
        self.assertEqual(previous, {path: path.read_bytes() for path in previous})


if __name__ == '__main__':
    unittest.main()
