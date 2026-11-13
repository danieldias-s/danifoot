import csv
import shutil
import unittest
from collections import Counter
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
            selection = rows(research / "selecao_jogadores.csv")
            active = [r for r in selection if r["candidate"] == "true"]
            self.assertEqual(274, len(selection) - len(active))
            self.assertEqual({p["id"] for p in players}, {r["playerId"] for r in active})
            self.assertEqual(Counter(AWARD_OR_BEST_XI=13, LOCAL_HIGHLIGHT=226, PRE_EXISTING_BASELINE=6),
                             Counter(r["starClassification"] for r in active if r["playerId"] in stars))
            local = Counter(r["clubId"] for r in active if r["starClassification"] == "LOCAL_HIGHLIGHT")
            awarded = {r["clubId"] for r in active if r["starClassification"] == "AWARD_OR_BEST_XI"}
            self.assertTrue(all(1 <= count <= 3 for count in local.values()))
            self.assertTrue(awarded.isdisjoint(local))
            self.assertTrue(all(r["starCandidateNote"] for r in active
                                if r["starClassification"] in {"LOCAL_HIGHLIGHT", "AWARD_OR_BEST_XI"}))

            national = rows(research / "ranking_nacional.csv")
            world = {r["catalogClubId"]: r for r in rows(research / "ranking_mundial.csv")}
            self.assertEqual(set(range(1, 173)), {int(r["nationalPosition"]) for r in national})
            self.assertEqual(set(by_id), set(world))
            self.assertEqual(26, sum(not r["optaTeamId"] for r in national))
            self.assertEqual(146, len({r["optaTeamId"] for r in national if r["optaTeamId"]}))
            for row in national:
                club_id = row["catalogClubId"]
                other = world[club_id]
                self.assertEqual(
                    [row[k] for k in ("nationalPosition", "optaTeamId", "referenceDate", "source", "optaGlobalRank", "optaRating")],
                    [other[k] for k in ("nationalPosition", "optaTeamId", "referenceDate", "source", "globalRank", "rating")],
                )
                rank = int(row["nationalPosition"])
                strength = 20 - (rank - 1) // 2 if rank <= 32 else 1
                club = by_id[club_id]
                self.assertEqual(strength, int(row["teamStrength"]))
                if club_id == "real-madrid":
                    self.assertIsNone(club["teamStrength"])
                    self.assertIsNone(club["historicalRanking"])
                else:
                    self.assertEqual(strength, club["teamStrength"])
                    self.assertEqual(row["referenceDate"], club["historicalRanking"]["referenceDate"])
                    self.assertEqual(float(row["optaRating"]), club["historicalRanking"]["rating"])

            active_ids = {r["playerId"] for r in active}
            self.assertTrue(all(r["playerId"] in active_ids for r in rows(research / "fontes.csv") if r["playerId"]))
            pending = rows(research / "proveniencia_pendente.csv")
            self.assertEqual(active_ids, {r["playerId"] for r in pending})

            # A clean checkout can recover the untracked Madrid research from the versioned archive.
            (extracted / export.BEHAVIOR / "real-madrid" / "fontes-e-coleta-2026-27.json").unlink()
            previous = extracted / export.ARCHIVE
            previous.parent.mkdir()
            shutil.copyfile(first, previous)
            export.build(extracted, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())
