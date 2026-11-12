import copy
import json
import sys
from pathlib import Path
import unittest
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from behavior_v16 import classify,DEFAULT_CONFIG


def row(i,**changes):
    return dict(dict(playerCatalogId=f'synthetic-{i:03}',position='M',initialDivision=1,
                     comparisonGroup='synthetic-2025-source',baseBehavior=2,minutes=900,
                     fouls=i,yellowCards=2,redCards=1),**changes)


class BehaviorV16Tests(unittest.TestCase):
    def test_exact_index_and_base_preserved(self):
        sample=row(10);before=copy.deepcopy(sample)
        result=classify([sample])['results'][0]
        self.assertAlmostEqual(1.9,result['index'])
        self.assertEqual(2,result['baseBehavior']);self.assertEqual(before,sample)

    def test_distribution_aggregate_each_initial_division(self):
        for division,targets in [(1,[20,20,25,20,10,5]),(2,[20,20,25,20,10,5]),
                                 (3,[15,15,20,25,15,10]),(4,[15,15,20,25,15,10]),(5,[10,10,15,30,20,15])]:
            with self.subTest(division=division):
                results=classify([row(i,initialDivision=division) for i in range(100)])['results']
                self.assertEqual(targets,[sum(r['effectiveBehaviorProposal']==b for r in results) for b in range(6)])

    def test_position_cohorts_are_separate(self):
        results=classify([row(i,position=p,playerCatalogId=f'{p}-{i}',fouls=i+(1000 if p=='G' else 0))
                          for p in ['G','D','M','A'] for i in range(20)])['results']
        self.assertEqual(1,len({tuple(r['effectiveBehaviorProposal'] for r in results if r['position']==p) for p in ['G','D','M','A']}))

    def test_minimum_missing_and_zero_are_not_silently_classified(self):
        for field,value in [('minutes',899),('minutes',0),('minutes',None),('fouls',None),('redCards',None)]:
            with self.subTest(field=field,value=value):
                result=classify([row(1,**{field:value})])['results'][0]
                self.assertEqual('REVISAO',result['status']);self.assertIsNone(result['effectiveBehaviorProposal'])
        self.assertEqual('SUGERIDO',classify([row(1,minutes=900)])['results'][0]['status'])

    def test_ties_small_cohorts_and_order_are_deterministic(self):
        samples=[row(i,fouls=2) for i in range(3)]
        a=classify(samples);b=classify(samples[::-1]);self.assertEqual(a,b)
        self.assertEqual(1,len({r['effectiveBehaviorProposal'] for r in a['results']}))
        self.assertEqual(a,json.loads(json.dumps(a)))

    def test_configuration_weights_and_minimum_are_explicit(self):
        config=copy.deepcopy(DEFAULT_CONFIG);config['weights']['yellowCards']=3
        self.assertNotEqual(classify([row(1)])['results'][0]['index'],classify([row(1)],config)['results'][0]['index'])
        config['minimumMinutes']=899
        with self.assertRaises(ValueError):classify([row(1)],config)

    def test_invalid_data_and_identity_are_rejected(self):
        for changes in [dict(minutes=-1),dict(fouls=float('nan')),dict(position='X'),dict(baseBehavior=6),dict(comparisonGroup='')]:
            with self.subTest(changes=changes),self.assertRaises(ValueError): classify([row(1,**changes)])
        with self.assertRaises(ValueError):classify([row(1),row(1)])
