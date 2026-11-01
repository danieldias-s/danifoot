"""Read-only PostgreSQL inventory and explicit HTTP replay in the isolated 016A DEV environment."""
from pathlib import Path
import subprocess,os,json,urllib.request,hashlib,sys
ROOT=Path(__file__).resolve().parents[1]
assert str(ROOT).lower()==r"d:\elifoot_98_016a"
AREA=ROOT/"artifacts/016a"; BASE="http://127.0.0.1:5171/api"
ENV=os.environ.copy(); ENV["PGPASSWORD"]=(AREA/"pg-password.txt").read_text().strip()
PSQL=r"C:/Program Files/PostgreSQL/18/bin/psql.exe"
def sql(query,world=False):
    db="elifoot_016a_013b_"+("worlds_dev" if world else "catalog_dev")
    p=subprocess.run([PSQL,"-h","127.0.0.1","-p","55441","-U","elifoot016a","-d",db,"-tAX","-v","ON_ERROR_STOP=1"],input=query,env=ENV,capture_output=True,text=True,encoding="utf-8")
    if p.returncode:raise RuntimeError(p.stderr)
    return [json.loads(line) for line in p.stdout.splitlines() if line.strip()]
def api(path,body=None):
    req=urllib.request.Request(BASE+path,headers={"Content-Type":"application/json","X-Catalog-Admin-Token":(AREA/"admin-token.txt").read_text().strip()},data=None if body is None else json.dumps(body).encode())
    with urllib.request.urlopen(req,timeout=120) as r:return json.load(r)
def save(name,obj): (AREA/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding="utf-8")
def inventory():
    snapshots=sql("""SELECT row_to_json(t) FROM (SELECT "Id", "ScopeKey", "Version", "Hash", "IsFixture", "PayloadJson"::jsonb AS payload FROM "wc013a_CatalogSnapshot" ORDER BY "Id") t;""")
    pilot=[s for s in snapshots if "016a:" in s["ScopeKey"]]
    aliases=sql("""SELECT row_to_json(t) FROM (SELECT a."ExternalId",a."ClubId",i."Name",i."CountryCode",i."Gender",a."ReviewEvidence" FROM "wc013a_CatalogClubIdentityAlias" a JOIN "wc013a_CatalogClubIdentity" i ON i."Id"=a."ClubId" WHERE a."Provider"='wikidata' AND a."ExternalId" IN ('Q17479','Q35933','Q8682','Q7156','Q128446','Q522899') ORDER BY a."ExternalId") t;""")
    players=sql("""SELECT row_to_json(t) FROM (SELECT p."ExternalId",p."Name",p."Position",p."Strength",s."ScopeKey",p."SnapshotId" FROM "wc013a_CatalogPlayer" p JOIN "wc013a_CatalogSnapshot" s ON s."Id"=p."SnapshotId" WHERE s."ScopeKey" LIKE '%016a:%' ORDER BY p."ExternalId",p."SnapshotId") t;""")
    raw=sql("""SELECT row_to_json(t) FROM (SELECT "Provider", "Resource", "FetchedAt",md5("Payload") AS hash,"Payload"::jsonb AS payload FROM "wc013a_Import016ACache" ORDER BY "Provider","Resource") t;""")
    cache=[dict(provider=c["Provider"],resource=c["Resource"],fetchedAt=c["FetchedAt"],hash=c["hash"],rawRows=len(c["payload"].get("results",{}).get("bindings",[]))) for c in raw]
    worlds=sql("""SELECT row_to_json(t) FROM (SELECT "Id", "Name", "RoundNumber", "RoundState", "RoundVersion",md5("SnapshotJson") AS hash FROM ef_worlds ORDER BY "Id") t;""",True)
    return dict(totalSnapshots=len(snapshots),priorSnapshots=sum("016a:" not in s["ScopeKey"] for s in snapshots),pilotSnapshots=[{k:v for k,v in s.items() if k!="payload"} for s in pilot],aliases=aliases,players=players,cache=cache,worlds=worlds)
if __name__=="__main__":
    if "--replay" in sys.argv:
        before=inventory();save("inventory-before-replay.json",before)
        run=json.loads((AREA/"pilot-squads-apply.json").read_text(encoding="utf-8-sig")); result=api("/catalog-016a/apply/"+run["id"],{})
        assert result["applied"] and result["snapshotIds"]==run["snapshotIds"]
        after=inventory(); assert before==after,"Idempotent replay changed the persisted catalog or worlds"
        raw=(AREA/"world-hashes-before-restart.jsonl").read_bytes(); text=raw.decode("utf-16" if raw.startswith(b"\xff\xfe") else "utf-8-sig")
        old={r["Id"]:r["hash"] for r in (json.loads(line) for line in text.splitlines() if line.strip())}
        assert old=={r["Id"]:r["hash"] for r in after["worlds"]},"Restart or catalog operations changed a frozen world"
        coverage=api("/catalog-016a/coverage");report=api("/catalog-016a/reports/"+run["id"])
        save("persistence-http-report.json",dict(restartRecovered=True,idempotentApply=True,catalogUnchanged=True,worldSnapshotsUnchanged=True,worldHashes=old,providerStatuses=coverage["providers"],recoveredReportId=report["id"]))
        print("PASS: actual PostgreSQL restart, cached API report, idempotent APPLY, catalog and world hashes preserved.")
    else:
        result=inventory();save("inventory-final.json",result)
        print(json.dumps(dict(snapshots=result["totalSnapshots"],priorSnapshots=result["priorSnapshots"],pilotSnapshots=len(result["pilotSnapshots"]),confirmedAliases=len(result["aliases"]),storedPlayerRows=len(result["players"]),uniquePlayers=len(set(p["ExternalId"] for p in result["players"])),positionCounts={p:sum(r["Position"]==p for r in result["players"]) for p in ["G","D","M","A"]},cacheResources=len(result["cache"]),cacheRawRows=[r["rawRows"] for r in result["cache"] if r["rawRows"]],worlds=len(result["worlds"])),ensure_ascii=False))
