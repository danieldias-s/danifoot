# Validação da força inicial 017B

Base `8dda6d1`, descendente do checkpoint aprovado `c7a4cad`. A branch 017A apontava para `8dda6d1` e continha alterações locais em andamento. A 017B partiu desse commit e manteve a 017A em seu próprio worktree.

A API 017B criou a **Liga 017B - Validação Persistida** no PostgreSQL DEV isolado, a partir de 128 clubes verificados de um catálogo restaurado. A fonte masculina ofereceu zero atletas com vínculo atual utilizável para esse modo, por isso a API gerou 2.610 atletas fictícios autorizados. Os 694 atletas das quatro divisões foram comparados com a prévia e exportados da primeira gravação em `ef_worlds`. O relatório inclui os dados individuais dos 32 clubes. [Relatório por clube](evidence-017b/INITIAL_SQUAD_STRENGTH_017B.md) · [CSV](evidence-017b/INITIAL_SQUAD_STRENGTH_017B.csv)

| Divisão | Clubes | Atletas | Mínima | Máxima | Média | Violações |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1ª | 8 | 184 | 39 | 50 | 44,69 | 0 |
| 2ª | 8 | 184 | 26 | 38 | 32,67 | 0 |
| 3ª | 8 | 166 | 13 | 25 | 19,94 | 0 |
| 4ª | 8 | 160 | 1 | 12 | 6,26 | 0 |

O FC Porto, o FC Barcelona e o Bayern München tiveram os quatro valores 47, 48, 49 e 50 em cada elenco. O CSV abre com 13 colunas e 694 IDs de jogadores distintos.

No Chrome, dois treinadores do mesmo campeonato assumiram o Real Betis (4ª divisão) e o FC Porto (1ª divisão). O anfitrião iniciou a sala e ambos abriram Plantel & Táticas. As tabelas renderizadas conferiram com o CSV em nome, posição e força para os 18 atletas do Betis e os 23 do Porto. [Real Betis](evidence-017b/plantel-real-betis.jpg) · [FC Porto](evidence-017b/plantel-fc-porto.jpg)

A API foi reiniciada e recarregou o campeonato e uma cópia histórica do save «Rankings 009 — temporada real» em PostgreSQL. Os hashes gravados permaneceram iguais nos dois registros. A consulta a Criciúma Carvoeiro devolveu as 18 forças do elenco histórico. O banco do save do Flamengo ficou fora do ambiente 017B. [Evidência de reinício](evidence-017b/restart-evidence.json)

O exportador recebe o resultado de `SELECT json_build_object('createdAtUtc', "CreatedAtUtc", 'world', "SnapshotJson"::json) FROM ef_worlds WHERE "Id" = <world-id>` e o JSON da prévia. Ele confere as faixas e a identidade dos atletas antes de emitir os dois arquivos.

```powershell
node scripts/export-initial-squad-strength-017b.mjs artifacts/017b/audit-persisted.json artifacts/017b/audit-preview.json docs/evidence-017b
```
