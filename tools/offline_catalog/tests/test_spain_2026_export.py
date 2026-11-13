import csv
import shutil
import unittest
from collections import Counter
from decimal import Decimal
from pathlib import Path
from tempfile import TemporaryDirectory
from zipfile import ZipFile

from tools.offline_catalog import spain_2026_export as export


def rows(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


class Spain2026ExportTests(unittest.TestCase):
    def test_all_catalog_schema_paths_including_templates_and_examples(self):
        paths = dict(export.schema_paths(export.ROOT / "catalogos"))
        self.assertTrue(any("modelos" in p.parts and "template" in p.name for p in paths))
        self.assertTrue(any("exemplo-1.1" in p.parts for p in paths))
        self.assertEqual(172, sum(p.parent == export.ROOT / export.SPAIN / "clubes" for p in paths))

    def test_repeatable_complete_archive_and_extracted_catalog(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            first, second = root / "one.zip", root / "two.zip"
            export.build(export.ROOT, first)
            export.build(export.ROOT, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            extracted = root / "extracted"
            extracted.mkdir()
            entries, schema_count = export.verify(first, extracted)
            self.assertEqual(345, schema_count)
            with ZipFile(first) as archive:
                self.assertEqual(entries + 1, len(archive.namelist()))
            self.assertEqual(5, len(list((extracted / "catalogos/_schemas").glob("*.schema.json"))))

            season = extracted / export.SPAIN
            research = extracted / export.RESEARCH
            manifest = export.read_json(season / "manifest.json")
            clubs = [export.read_json(season / relative) for relative in manifest["clubs"]]
            by_id = {club["catalogClubId"]: club for club in clubs}
            self.assertEqual(172, len(clubs))
            self.assertEqual(172, len(by_id))
            self.assertEqual(172, len(list((extracted / "catalogos/identidades").glob("*/identidade.json"))))
            self.assertEqual("DRAFT", manifest["status"])
            players = [p for club in clubs for p in club["players"]]
            self.assertEqual(3905, len(players))
            self.assertEqual(3905, len({p["id"] for p in players}))
            self.assertEqual(Counter(G=364, D=1325, M=1056, A=1160), Counter(p["position"] for p in players))
            self.assertTrue(all(p["behavior"] is None for p in players))
            self.assertTrue(all(2 <= sum(p["position"] == "G" for p in c["players"]) <= 3 for c in clubs))
            stars = {p["id"] for p in players if p["isStar"]}
            self.assertEqual(245, len(stars))
            self.assertTrue(all(p["position"] in "MA" for p in players if p["id"] in stars))
            imports = extracted / export.IMPORT_RESEARCH
            national = rows(imports / "inventario_consolidado.csv")
            self.assertEqual(list(by_id), [r["clubId"] for r in national])
            self.assertEqual(Counter({"1": 20, "2": 22, "3": 40, "4": 90}),
                             Counter(r["category"] for r in national))
            for rank, row in enumerate(national, 1):
                club = by_id[row["clubId"]]
                self.assertEqual(rank, int(row["nationalPosition"]))
                self.assertEqual(20 - (rank - 1) // 2 if rank <= 32 else 1, club["teamStrength"])
                self.assertEqual(int(row["players"]), len(club["players"]))
                if row["category"] == "4":
                    self.assertIsNone(club["historicalRanking"])
                    self.assertIsNone(club["tieBreakPriority"])
                    continue
                ranking = club["historicalRanking"]
                self.assertIn("TRANSFERMARKT", ranking["source"])
                self.assertEqual(row["rankingSource"], ranking["source"])
                self.assertEqual(row["referenceDate"], ranking["referenceDate"])
                self.assertEqual(Decimal(row["marketValueEURMillions"]) / 100, Decimal(str(ranking["rating"])))
            self.assertEqual(15, by_id["real-madrid"]["historicalRanking"]["rating"])
            self.assertTrue((research / "ranking_mundial.csv").is_file())
            self.assertFalse(any("02_PREVIA" in p.name for p in extracted.rglob("*")))

            # A clean checkout can recover the untracked Madrid research from the versioned archive.
            (extracted / export.BEHAVIOR / "real-madrid" / "fontes-e-coleta-2026-27.json").unlink()
            previous = extracted / export.RESEARCH_ARCHIVE
            previous.parent.mkdir()
            shutil.copyfile(first, previous)
            export.build(extracted, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())
