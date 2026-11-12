import copy
import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

MODULE = Path(__file__).parents[1] / 'behavior_classifier.py'
spec = importlib.util.spec_from_file_location('classifier', MODULE)
bc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bc)


def row(i=0, **changes):
    value = dict(playerCatalogId=f'p-{i}', club='synthetic', country='BR', year=1998,
                 position='D', competition='SYNTHETIC', source='synthetic-source', collectedAt='2026-11-12',
                 minutes=1800, matches=20, yellowCards=i, redCards=0, fouls=i*2, duels=i*5, duelsWon=i)
    value.update(changes)
    return value


def result(report, player):
    return next(r for r in report['results'] if r['statistics']['playerCatalogId'] == player)


class ClassifierTests(unittest.TestCase):
    def test_rates_are_per90_not_raw_counts(self):
        actual = bc.rates(row(minutes=900, yellowCards=10, redCards=2, fouls=30, duels=100), bc.DEFAULT_CONFIG)
        self.assertEqual((1, .2, 3, 10, 1.6), tuple(actual[k] for k in ['yellow90','red90','fouls90','duels90','cards90']))

    def test_weights_and_config_validation(self):
        report = bc.classify([row(i) for i in range(101)])
        r = result(report, 'p-70')
        self.assertAlmostEqual(.65*r['percentiles']['cards90']+.35*r['percentiles']['fouls90'], r['indiscipline'])
        for changes in [{'cardWeight': .7}, {'minimumMinutes': -1}, {'minimumCohort': 1}, {'extremeMinimum': .5}, {'minimumMatches': True}]:
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                bc.classify([], bc.DEFAULT_CONFIG | changes)

    def test_provided_rates_do_not_invent_original_counts(self):
        value = row(yellowCards=None, redCards=None, fouls=None, duels=None,
                    yellowCardsPer90=.2, redCardsPer90=0, foulsPer90=1.5, duelsPer90=10)
        r = bc.classify([value])['results'][0]
        self.assertEqual(.2, r['rates']['yellow90']); self.assertEqual(1.5, r['rates']['fouls90'])
        self.assertIsNone(r['statistics']['yellowCards']); self.assertIsNone(r['statistics']['fouls'])
        self.assertEqual('REVISAO', r['status'])

    def test_rates_must_be_finite_and_consistent_with_counts(self):
        for changes in [{'foulsPer90':-1}, {'duelsPer90':float('nan')}, {'redCardsPer90':True}, {'yellowCardsPer90':100}]:
            with self.subTest(changes=changes), self.assertRaises(ValueError):bc.classify([row(**changes)])

    def test_profiles_use_both_dimensions(self):
        rows = [row(i) for i in range(101)]
        rows.append(row(101, yellowCards=0, fouls=0, duels=1000))
        report = bc.classify(rows)
        expected = {'p-101':0, 'p-0':1, 'p-50':2, 'p-70':3, 'p-90':4, 'p-100':5}
        for player, behavior in expected.items():
            with self.subTest(player=player):
                r = result(report, player)
                self.assertEqual(behavior, r['suggestedBehavior'])
                self.assertEqual('SUGERIDO', r['status'])
                self.assertEqual('MODERADA', r['confidence'])

    def test_one_red_is_never_enough_for_extreme(self):
        rows = [row(i) for i in range(101)] + [row(101, yellowCards=0, redCards=1, fouls=1000, duels=1000)]
        self.assertNotEqual(5, result(bc.classify(rows), 'p-101')['suggestedBehavior'])

    def test_unknown_and_known_zero_are_different(self):
        known = row(yellowCards=0, redCards=0)
        unknown = row(yellowCards=None, redCards=None)
        self.assertEqual(0, bc.rates(known, bc.DEFAULT_CONFIG)['cards90'])
        self.assertIsNone(bc.rates(unknown, bc.DEFAULT_CONFIG)['cards90'])
        r = bc.classify([unknown])['results'][0]
        self.assertIsNone(r['suggestedBehavior']); self.assertEqual('REVISAO', r['status'])

    def test_no_data_never_defaults_to_zero_behavior(self):
        for changes in [dict.fromkeys(bc.METRICS), {'minutes':0}, {'minutes':None}]:
            with self.subTest(changes=changes):
                r = bc.classify([row(**changes)])['results'][0]
                self.assertEqual('SEM_DADOS', r['status']); self.assertIsNone(r['suggestedBehavior'])

    def test_low_minutes_matches_or_cohort_require_review(self):
        for changes in [{'minutes':89}, {'matches':1}, {}]:
            r = bc.classify([row(**changes)])['results'][0]
            self.assertEqual('REVISAO', r['status']); self.assertEqual('BAIXA', r['confidence'])

    def test_partial_data_cannot_be_suggested_with_certainty(self):
        rows = [row(i) for i in range(101)] + [row(101, duels=None)]
        r = result(bc.classify(rows), 'p-101')
        self.assertEqual('REVISAO', r['status']); self.assertIsNone(r['physicalIntensity'])

    def test_cohorts_separate_position_competition_year_country_and_source(self):
        base = [row(i) for i in range(20)]
        before = result(bc.classify(base), 'p-10')
        for field, value in [('position','G'), ('competition','OTHER'), ('year',2026), ('country','ES'), ('source','OTHER')]:
            other = [row(100+i, **{field:value}, fouls=10000) for i in range(20)]
            after = result(bc.classify(base+other), 'p-10')
            with self.subTest(field=field):
                self.assertEqual(before['percentiles'], after['percentiles']); self.assertEqual(before['sampleSizes'], after['sampleSizes'])

    def test_conflicting_sources_are_not_summed(self):
        rows = [row(i) for i in range(20)] + [row(10, source='OTHER', fouls=200)]
        results = [r for r in bc.classify(rows)['results'] if r['statistics']['playerCatalogId']=='p-10']
        self.assertTrue(all(r['status']=='REVISAO' for r in results))
        self.assertTrue(all(any('múltiplos' in x for x in r['explanation']) for r in results))

    def test_reproducible_including_reordered_input(self):
        rows = [row(i) for i in range(20)]
        self.assertEqual(bc.encoded(bc.classify(rows)), bc.encoded(bc.classify(list(reversed(rows)))))

    def test_duplicate_rows_and_invalid_statistics_fail(self):
        with self.assertRaises(ValueError): bc.classify([row(),row()])
        for changes in [{'minutes':-1}, {'yellowCards':True}, {'redCards':.5}, {'duelsWon':2,'duels':1}, {'position':'GK'}, {'collectedAt':'bad'}]:
            with self.subTest(changes=changes), self.assertRaises(ValueError): bc.classify([row(**changes)])

    def test_csv_and_json_preserve_nulls_and_produce_same_report(self):
        with tempfile.TemporaryDirectory() as tmp:
            rows = [row(1, redCards=None), row(2)]
            json_file = Path(tmp)/'input.json'; json_file.write_bytes(bc.encoded(rows))
            csv_file = Path(tmp)/'input.csv'
            with csv_file.open('w',newline='',encoding='utf-8') as stream:
                writer=csv.DictWriter(stream,fieldnames=bc.FIELDS);writer.writeheader();writer.writerows(rows)
            self.assertEqual(bc.classify(bc.read_rows(json_file)), bc.classify(bc.read_rows(csv_file)))

    def test_duplicate_json_properties_and_csv_headers_fail(self):
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'input.json';p.write_text('{"a":1,"a":2}')
            with self.assertRaises(ValueError):bc.read_rows(p)
            p=Path(tmp)/'input.csv';p.write_text('year,year\n1998,1998\n')
            with self.assertRaises(ValueError):bc.read_rows(p)


class ApprovalTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name)
        self.target='brasil/1998/clubes/c.json'
        self.path=self.root/self.target;self.path.parent.mkdir(parents=True)
        self.club={'schemaVersion':'1.1','catalogClubId':'synthetic','country':'BR','year':1998,'status':'DRAFT',
                   'players':[{'id':'p-0','position':'D','behavior':None,'isStar':False}], 'teamStrength':20}
        self.path.write_bytes(bc.encoded(self.club))
        (self.root/'brasil/1998/manifest.json').write_bytes(bc.encoded({'year':1998,'country':{'code':'BR','slug':'brasil'},'clubs':['clubes/c.json']}))
        self.report=bc.classify([row()]);self.rowid=self.report['results'][0]['rowId']

    def approved(self):
        return bc.review(self.report,self.rowid,3,'Revisão sintética justificada',self.root,self.target)

    def test_manual_requires_justification(self):
        with self.assertRaises(ValueError):bc.review(self.report,self.rowid,3,' ',self.root,self.target)

    def test_unapproved_report_cannot_be_applied(self):
        with self.assertRaises(ValueError):bc.apply_approved(self.report,self.rowid,self.root)
        self.assertEqual(self.club,bc.load_json(self.path))

    def test_approved_application_only_changes_behavior_and_is_idempotent(self):
        approved=self.approved()
        self.assertEqual('MANUAL',approved['results'][0]['manualDecision']['status'])
        self.assertEqual('APLICADO',bc.apply_approved(approved,self.rowid,self.root)['status'])
        expected=copy.deepcopy(self.club);expected['players'][0]['behavior']=3
        self.assertEqual(expected,bc.load_json(self.path));self.assertEqual('DRAFT',bc.load_json(self.path)['status'])
        self.assertEqual('JA_APLICADO',bc.apply_approved(approved,self.rowid,self.root)['status'])

    def test_changed_catalog_requires_new_review(self):
        approved=self.approved();self.club['teamStrength']=19;self.path.write_bytes(bc.encoded(self.club))
        with self.assertRaises(ValueError):bc.apply_approved(approved,self.rowid,self.root)

    def test_wrong_year_club_player_position_or_version_cannot_be_approved(self):
        for key,value in [('year',2026),('catalogClubId','foreign'),('schemaVersion','1.0')]:
            changed=self.club|{key:value};self.path.write_bytes(bc.encoded(changed))
            with self.subTest(key=key),self.assertRaises(ValueError):self.approved()
        changed=copy.deepcopy(self.club);changed['players'][0]['position']='G';self.path.write_bytes(bc.encoded(changed))
        with self.assertRaises(ValueError):self.approved()

    def test_modified_evidence_is_rejected(self):
        self.report['results'][0]['statistics']['fouls']=200
        with self.assertRaises(ValueError):self.approved()

    def test_path_traversal_external_paths_and_wrong_root_rejected(self):
        for relative in ['../escape.json','/tmp/escape.json','https://site/data','C:/data','a\\b','a/./b']:
            with self.subTest(relative=relative),self.assertRaises(ValueError):bc.safe_path(self.root,relative)

    def test_explicit_cli_classify_review_apply_and_receipts(self):
        source=self.root/'input.json';source.write_bytes(bc.encoded([row()]))
        common=['--root',str(self.root)]
        bc.main(['classify','--input',str(source),'--country','BR','--slug','brasil','--year','1998']+common)
        report=next(self.root.glob('_pesquisa/comportamento/brasil/1998/*.json'))
        relative=report.relative_to(self.root).as_posix()
        bc.main(['review','--report',relative,'--row-id',self.rowid,'--behavior','2','--justification','Decisão explícita','--target',self.target]+common)
        review=next(self.root.glob('_pesquisa/comportamento/brasil/1998/*.review.json'))
        bc.main(['apply','--report',review.relative_to(self.root).as_posix(),'--row-id',self.rowid]+common)
        self.assertEqual(2,bc.load_json(self.path)['players'][0]['behavior'])
        self.assertEqual(1,len(list(self.root.glob('_pesquisa/comportamento/brasil/1998/*.receipt.json'))))


if __name__ == '__main__':
    unittest.main()
