"""Record GET-only development data for the 015B Flutter preview renderer."""
from pathlib import Path
import importlib.util
import json

ROOT = Path(__file__).resolve().parents[1]
assert str(ROOT).lower() == r"d:\elifoot_98_015b"
spec = importlib.util.spec_from_file_location("review015b", ROOT / "scripts/review-classic-desktop-015b.py")
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)
context = json.loads((ROOT / "artifacts/015b/solo-context.json").read_text(encoding="utf-8-sig"))
person = context["participants"][0]
world, club, coach = context["world"]["id"], person["club"]["id"], person["auth"]["coachId"]
base = f"/worlds/{world}"
scene = {
    "coach": coach, "club": club, "nickname": person["auth"]["nickname"],
    "snapshot": review.api(base + "/snapshot"),
    "detail": review.api(base + f"/clubs/{club}"),
    "clubs": review.api(base + "/clubs"),
    "appearance": review.api(f"/world-creator/appearance/{world}/{club}", token=person["auth"]["sessionToken"]),
}
for name, path in {
    "calendar": "/calendar", "cup": "/cup", "scorers": "/top-scorers",
    "market": "/market", "ranking": "/ranking", "finances": f"/clubs/{club}/finances",
    "invitations": f"/invitations/{coach}",
}.items():
    scene[name] = review.api(base + path)
scene["standings"] = {tier: review.api(base + f"/standings/{tier}") for tier in [
    "PrimeiraDivisao", "SegundaDivisao", "TerceiraDivisao", "QuartaDivisao"
]}
review.save("mobile-scene.json", scene)
print(json.dumps({"club": scene["detail"]["name"], "round": scene["snapshot"]["roundNumber"],
                  "state": scene["snapshot"]["roundState"], "source": "GET-only API 5167; room not changed"}, ensure_ascii=False))
