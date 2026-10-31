# Testes e evidências — 013B

Baseline preservada: checkpoint 40e71b4, 383 casos .NET e 59 Flutter. A 013B acrescenta sete casos de domínio, 30 de integração/protocolos e quatro Flutter: totais finais aprovados, 420 .NET e 63 Flutter.

Mocks sintéticos exercitam API-Football, Sportmonks e football-data.org nos dois gêneros; identidade/Wikidata; autenticação 401/403; 429 e quotas; timeout/cancelamento; páginas inconsistentes; HTTP 200 com erro; maxlag; posições; homônimos, várias ligas e revisão explícita; licenças; retenção; transferência com datas; roster parcial; conservação do último elenco completo; ranking estimado/antigo e cache após reinício lógico.

O domínio cobre 128 clubes/oito países, forças reproduzíveis e variadas, paridade de escala, manual/automático, distritais, seleção equilibrada acima de 24, goleiro, consentimento para gerar atletas, identidade canônica duplicada e ranking Elo reproduzível. A regressão herdada cobre fallback parcial, persistência PostgreSQL, snapshot, mundo existente, economia/força/disciplinas 012C e fluxo Flutter de desktop/celular.

A carga REAL é um ensaio separado, nunca contabilizada como fixture: Wikidata via API, dois clubes e 41 atletas persistidos em quatro snapshots sem IsFixture. Evidências portáveis em real-snapshots.json, resumo em real-import-summary.json, cobertura por gênero e consulta offline em real-offline-pilot.json. Não há dados reais pagos nem ranking oficial testado.

```powershell
cd D:\elifoot_98_013A
.\scripts\test-real-data-013b.ps1 -Action Test
cd client\elifoot_flutter
flutter analyze
flutter test
flutter build web --release --pwa-strategy=none --dart-define=API_BASE_URL=http://127.0.0.1:5134
```

O runner fornece todas as conexões isoladas e recusa fallback implícito ao PostgreSQL principal. Ensaios longos de 100 temporadas/benchmarks permanecem excluídos; as regras não foram alteradas. Logs finais em docs/audit-013b; a entrega consolidada confirma os resultados efetivamente concluídos, não apenas as contagens esperadas.
