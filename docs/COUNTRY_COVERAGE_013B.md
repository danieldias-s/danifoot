# Cobertura efetivamente importada — 013B

Referência do piloto: vínculos declarados no Wikidata em 30/10/2026, temporada de catálogo 2026. Não é uma certificação do elenco oficial atual.

| País | Clubes M | Atletas utilizáveis M | Clubes F | Atletas utilizáveis F |
|---|---:|---:|---:|---:|
| Brasil | 0 | 0 | 0 | 0 |
| Argentina | 0 | 0 | 0 | 0 |
| Inglaterra | 0 | 0 | 0 | 0 |
| Espanha | 1 | 35 | 1 | 6 |
| Alemanha | 0 | 0 | 0 | 0 |
| Itália | 0 | 0 | 0 | 0 |
| França | 0 | 0 | 0 | 0 |
| Portugal | 0 | 0 | 0 | 0 |

Equipes esportivas distintas: Real Madrid Club de Fútbol, Q8682, liga Q324867; Real Madrid Femenino, Q96791589, liga Q40068. O vínculo país P17=Q29 e os tipos P31 foram verificados pela API antes da ativação.

O masculino retornou 40 identidades candidatas, 35 com nome/posição utilizáveis; a prévia seleciona 24 equilibrados. O feminino retornou oito candidatas e seis utilizáveis, sem goleiro: não pode ser confirmado sem resolver a insuficiência. Dados ausentes não foram inventados. Contagens de elenco completo significam cumprimento dos mínimos do jogo, não completude oficial da fonte.

Cada categoria ainda tem apenas um clube disponível: faltam 127 para o mundo de 128. Os demais países estão preparados na seleção e nos contratos de adapters, porém não foram carregados. Não se presumiu cobertura feminina igual à masculina.

Evidências: `docs/audit-013b/real-import-summary.json`, `real-Male-coverage.json`, `real-Female-coverage.json`, `real-offline-pilot.json` e os quatro pacotes em `real-snapshots.json`. `POST /api/world-creator/coverage` fornece liga, categoria, temporada, contagens, fonte, data, restrições e lacunas. Outros países podem ser incluídos por manifestos revisados ou importação autorizada.
