"""Build and validate the portable Spain 2026/27 catalog package."""

import argparse
import hashlib
import json
import re
from shutil import copyfile
from pathlib import Path, PurePosixPath
from tempfile import TemporaryDirectory
from urllib.parse import urlsplit
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo

ROOT = Path(__file__).resolve().parents[2]
SPAIN = "catalogos/espanha/2026"
RESEARCH = "catalogos/_pesquisa/espanha/2026"
BEHAVIOR = "catalogos/_pesquisa/comportamento/espanha/2026"
ARCHIVE = "exports/ELIFOOT98_CATALOGO_ESPANHA_2026.zip"
CHECKSUMS = "EXPORT_MANIFEST_SHA256.txt"
DIALECT = "https://json-schema.org/draft/2020-12/schema"
README = """# Elifoot 98 · Espanha 2026/27

Catálogo offline V1.1 em DRAFT. Contém 172 clubes e 172 identidades.
Extraia o ZIP mantendo a estrutura de pastas e aponte o validador oficial
para a pasta extraída `catalogos` com `--CatalogOffline:Root=<pasta>`.
O estado DRAFT impede a criação de mundos com este catálogo.

Os SHA-256 cobrem todos os arquivos da exportação, incluindo este README.
O manifesto de hashes é a única exceção. A exportação usa ordem, data e
permissões fixas. Compare arquivos gerados com os mesmos bytes de origem
e a mesma versão de Python/zlib.

As propostas V1.6 incluídas são material de pesquisa. Cada comportamento
permanece pendente de aprovação. Consulte o relatório de qualidade em `reports/`.
"""


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def schema_paths(root):
    """Resolve schema paths against the document, including templates and examples."""
    for path in sorted(root.rglob("*.json")):
        doc = read_json(path)
        if not isinstance(doc, dict) or "$schema" not in doc:
            continue
        uri = doc["$schema"]
        if uri == DIALECT and path.parent == root / "_schemas":
            continue
        ref = urlsplit(uri)
        if ref.scheme or ref.netloc or not ref.path:
            raise ValueError(f"{path}: unexpected schema URI {uri}")
        target = (path.parent / ref.path).resolve()
        if not target.is_relative_to(root.resolve()) or not target.is_file():
            raise ValueError(f"{path}: schema does not resolve: {uri}")
        yield path, target


def members(root, research_archive=None):
    season = root / SPAIN
    manifest = read_json(season / "manifest.json")
    files = {season / "manifest.json"}
    madrid_research = set()
    for relative in manifest["clubs"]:
        if not re.fullmatch(r"clubes/[a-z0-9-]+\.json", relative):
            raise ValueError(f"Unexpected club path: {relative}")
        path = season / relative
        club_id = read_json(path)["catalogClubId"]
        if path.stem != club_id:
            raise ValueError(f"Club ID and filename differ: {relative}")
        files.add(path)
        files.add(root / "catalogos/identidades" / club_id / "identidade.json")
        folder = root / BEHAVIOR / club_id
        for name in ("disciplina-2026-27.csv", "entrada-v16-2026-27.json", "proposta-v16-2026-27.json"):
            files.add(folder / name)
            if club_id == "real-madrid":
                madrid_research.add(folder / name)
    for name in ("fontes-e-coleta-2026-27.json", "historico-complementar-2025-26.csv"):
        madrid_research.add(root / BEHAVIOR / "real-madrid" / name)
    files.update(madrid_research)
    for name in (
        "fontes.csv", "inventario_clubes.csv", "jogadores_excluidos.csv", "metadados.json",
        "pendencias.csv", "proveniencia_pendente.csv", "ranking_mundial.csv",
        "ranking_nacional.csv", "selecao_jogadores.csv",
    ):
        files.add(root / RESEARCH / name)
    files.update((root / "catalogos/_schemas").glob("*.schema.json"))
    files.update(root / relative for relative in (
        "docs/catalogos/ELIFOOT98_V16_APROVACAO_PROVENIENCIA.md",
        "reports/ELIFOOT98_PAIS_ESPANHA_2026_CADASTRO.md",
        "reports/ELIFOOT98_ESPANHA_2026_QUALIDADE.md",
        "reports/ELIFOOT98_V16_APROVACAO_RESULTADO.md",
        "reports/ELIFOOT98_V16_COMPATIBILIDADE.md",
    ))
    # The Madrid originals are also kept in the checked-in export for clean checkouts.
    packaged = {path for path in madrid_research if not path.is_file()} if research_archive else set()
    data = {path.relative_to(root).as_posix(): path.read_bytes() for path in files - packaged}
    if packaged:
        with ZipFile(research_archive) as archive:
            checksums = set(archive.read(CHECKSUMS).decode("utf-8").splitlines())
            for path in packaged:
                name = path.relative_to(root).as_posix()
                content = archive.read(name)
                if f"{sha256(content)}  {name}" not in checksums:
                    raise ValueError(f"Invalid archived research checksum: {name}")
                data[name] = content
    return data


def sha256(content):
    return hashlib.sha256(content).hexdigest().upper()


def manifest_bytes(files):
    return "".join(f"{sha256(data)}  {name}\n" for name, data in sorted(files.items())).encode("utf-8")


def build(root, output):
    files = members(root, root / ARCHIVE)
    files["EXPORT_README.md"] = README.encode("utf-8")
    files[CHECKSUMS] = manifest_bytes(files)
    with ZipFile(output, "w") as archive:
        for name, data in sorted(files.items()):
            info = ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, data, compress_type=ZIP_DEFLATED, compresslevel=9)


def verify(archive_path, destination):
    """Verify coverage and checksums before extracting into an empty directory."""
    if any(destination.iterdir()):
        raise ValueError(f"{destination}: extraction requires an empty directory")
    with ZipFile(archive_path) as archive:
        names = archive.namelist()
        for name in names:
            path = PurePosixPath(name)
            if path.is_absolute() or ".." in path.parts or "\\" in name or ":" in name or path.as_posix() != name:
                raise ValueError(f"Unsafe ZIP path: {name}")
        if len(names) != len(set(names)) or archive.testzip() is not None:
            raise ValueError("Duplicate entries or ZIP CRC failure")
        files = {name: archive.read(name) for name in names if name != CHECKSUMS}
        if manifest_bytes(files) != archive.read(CHECKSUMS):
            raise ValueError("ZIP SHA-256 manifest mismatch")
        archive.extractall(destination)
    expected = members(destination)
    expected["EXPORT_README.md"] = README.encode("utf-8")
    if files != expected:
        raise ValueError("Unexpected ZIP entries")
    return len(files), sum(1 for _ in schema_paths(destination / "catalogos"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / ARCHIVE)
    parser.add_argument("--extract", type=Path, help="Retain the verified extraction for the official validator")
    args = parser.parse_args()
    with TemporaryDirectory(dir=args.output.parent) as temporary:
        staging = Path(temporary)
        package = staging / args.output.name
        build(ROOT, package)
        destination = args.extract or staging / "extracted"
        destination.mkdir(parents=True, exist_ok=True)
        count, schemas = verify(package, destination)
        # Keep the destination's filesystem permissions when updating an existing export.
        copyfile(package, args.output)
        if args.output.resolve() == ROOT / ARCHIVE:
            for name in ("EXPORT_README.md", CHECKSUMS):
                (args.output.parent / name).write_bytes((destination / name).read_bytes())
    print(f"{count} checked entries, {schemas} resolved schema references, SHA-256 {sha256(args.output.read_bytes())}")


if __name__ == "__main__":
    main()
