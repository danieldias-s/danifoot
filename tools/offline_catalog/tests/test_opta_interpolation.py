import copy
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from opta_interpolation import interpolate


class OptaInterpolationTests(unittest.TestCase):
    def setUp(self):
        self.above = dict(clubId='synthetic-a', rank=10, rating=90, source='OPTA', referenceDate='2026-01-01', evidence='local synthetic fixture')
        self.below = dict(clubId='synthetic-b', rank=20, rating=80, source='OPTA', referenceDate='2026-01-01', evidence='local synthetic fixture')

    def test_linear_result_preserves_anchors_and_marks_estimate(self):
        before = copy.deepcopy(self.above)
        result = interpolate(12, self.above, self.below)
        self.assertEqual(88, result['historicalRanking']['rating'])
        self.assertEqual('OPTA_INTERPOLATED', result['historicalRanking']['source'])
        self.assertEqual('ESTIMATED', result['evidence']['classification'])
        self.assertEqual(before, self.above)

    def test_no_extrapolation(self):
        for rank in [0, 10, 20, 21]:
            with self.subTest(rank=rank), self.assertRaises(ValueError):
                interpolate(rank, self.above, self.below)

    def test_rejects_incompatible_or_untraceable_anchors(self):
        for field, value in [('source', 'ClubElo'), ('source', 'OPTA_INTERPOLATED'),
                             ('referenceDate', '2025-01-01'), ('evidence', ''),
                             ('rating', float('nan')), ('rating', 101), ('clubId', 'synthetic-a')]:
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                interpolate(12, self.above, dict(self.below, **{field: value}))
