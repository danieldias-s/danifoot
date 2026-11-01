# DRY RUN — 016A

Executado por HTTP contra a API real em 5171 e pela interface Flutter em 5172, PostgreSQL 18 exclusivo em 55441. As consultas Wikidata são reais; fixtures ficam em testes separados.

O DRY RUN registra orçamento, cache, auditoria e relatório, mas não escreve snapshots, clubes, jogadores ou aliases no catálogo. O relatório expõe país/categoria, ID externo/Wikidata, temporada, data, posições, duplicações, faltas e classificações. Uma prévia sem dados aplicáveis não pode ser aplicada. Prévia expira em 24 horas e exige nova execução explícita.

Fluxo validado: VER COBERTURA → VER IDs DOS CLUBES → TESTAR IMPORTAÇÃO — DRY RUN → revisão → APPLY → VER RELATÓRIO. O token local do administrador vai apenas em cabeçalho para a API local, fica em memória e é mascarado. A chave API-Football nunca vai ao navegador. Endpoints administrativos exigem loopback + token; ambiente Production é recusado.

Cache persistente: 12 recursos Wikidata, reutilizados após reinício e novas prévias, zero chamadas extras. Atualização externa só quando solicitada explicitamente e dentro do orçamento. 429/maxlag/cota encerram a tentativa; não existe polling externo ao abrir jogos. Limite API-Football interno de até 100/dia, espaçamento mínimo de 6,1s e cabeçalhos efetivos respeitados. Status sem chave é NOT_CONNECTED.

Os primeiros logs do contador de registros SPARQL mostraram zero porque contavam só o objeto raiz. O contador foi corrigido para bindings e arrays de jogadores; os logs antigos foram conservados. As contagens reais do piloto vêm do payload persistido e do SQL, não desses valores antigos. Foram 2.655 linhas SPARQL brutas (5×500 + 155), sem equivalência automática a atletas.

[Persistência](evidence-016a/persistence-http-report.json) · [Repetição de novas prévias](evidence-016a/cached-replay-final.json) · [Resultado](IMPORT_RESULTS_016A.md).
