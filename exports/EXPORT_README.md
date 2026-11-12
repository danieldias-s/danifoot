# ELIFOOT 98 — Exportação Offline: Espanha 2026/27 (Schema V1.1 DRAFT)

- País: Espanha (`ES`, slug `espanha`)
- Ano do Catálogo: `2026` (temporada-alvo `2026/27`, estatísticas de referência `2025/26`)
- Data de Corte (`squadSnapshotDate` / `rankingReferenceDate`): `2026-11-12`
- Gênero: `Male`
- Status: `DRAFT` (`behavior: null` nos 171 novos clubes, `isStar: false` sem aprovação automática; cadastro pré-existente de `real-madrid` preservado intacto)
- Categorias inventariadas: 4 divisões, 172 clubes (20 LaLiga + 22 Segunda + 40 Primera Federación + 90 Segunda Federación), incluindo 31 clubes filiais/B/C documentados.

## Conteúdo do Pacote
- `catalogos/espanha/2026/**` (`manifest.json` + 172 arquivos em `clubes/`)
- `catalogos/identidades/<clubId>/identidade.json` (172 identidades referenciadas)
- `catalogos/_pesquisa/espanha/2026/**` (`metadados.json`, CSVs de inventário, fontes, rankings, seleção, excluídos e pendências)
- `catalogos/_pesquisa/comportamento/espanha/2026/**` (`disciplina-2026-27.csv`, `entrada-v16-2026-27.json` e `proposta-v16-2026-27.json` por clube)
- `reports/ELIFOOT98_PAIS_ESPANHA_2026_CADASTRO.md`
- `EXPORT_README.md`
- `EXPORT_MANIFEST_SHA256.txt`

## Como Validar Integridade
No diretório onde o pacote foi extraído:
1. Verifique os hashes SHA-256 listados em `EXPORT_MANIFEST_SHA256.txt`.
2. Execute o validador oficial contra a raiz de `catalogos/`:
   `dotnet Elifoot.Api.dll offline-catalog-validate --CatalogOffline:Root=<caminho>/catalogos`
