# Expansão de cobertura 013B

STATUS: PARTIAL. Referência esportiva: 2026-10-30. O aumento de identidades não representa 128 elencos reais atuais completos.

Worktree: D:\elifoot_98_013A; branch feature/real-football-data-013b; base d00fc26. D:\elifoot_98 e a 012D não foram modificados por esta entrega. Sem merge, contratação de API ou publicação.

## Resultado mensurável

REAL_MALE_CLUBS: 158 verificados; 181 históricos; 652 candidatos não verificados; 991 registros masculinos no total.

REAL_FEMALE_CLUBS: 232 verificados; 16 históricos; 159 candidatos não verificados; 407 registros femininos no total.

REAL_MALE_PLAYERS: 1981 identidades reais distintas em 1996 vínculos documentados; 0 utilizáveis como atuais.

REAL_FEMALE_PLAYERS: 1098 identidades reais distintas em 1105 vínculos documentados; 1 utilizável como atual. Os vínculos documentais podem ser antigos ou incertos.

COMPLETE_ROSTERS: 0. INCOMPLETE_ROSTERS: 1398. GENERATED_IN_CATALOG: 0. FIXTURES_IN_REAL_METRICS: 0.

Os 41 atletas do piloto e seus quatro snapshots continuam arquivados. A contagem anterior aceitava vínculos abertos como atuais; a nova contagem estrita corrige esse critério, preservando as evidências anteriores.

| País | Masculino total / verificado / histórico / não verificado | Feminino total / verificado / histórico / não verificado | Vínculos documentados M / F | Atuais utilizáveis M / F | Status |
|---|---:|---:|---:|---:|---|
| Brasil | 82 / 20 / 22 / 40 | 33 / 1 / 1 / 31 | 272 / 20 | 0 / 0 | PARTIAL |
| Argentina | 67 / 0 / 30 / 37 | 43 / 34 / 0 / 9 | 318 / 9 | 0 / 0 | PARTIAL |
| Inglaterra / ligas inglesas | 208 / 44 / 49 / 115 | 55 / 55 / 0 / 0 | 249 / 270 | 0 / 0 | PARTIAL |
| Espanha | 128 / 20 / 22 / 86 | 82 / 50 / 4 / 28 | 191 / 170 | 0 / 1 | PARTIAL |
| Alemanha | 258 / 18 / 18 / 222 | 69 / 29 / 6 / 34 | 458 / 330 | 0 / 0 | PARTIAL |
| Itália | 100 / 20 / 20 / 60 | 69 / 23 / 5 / 41 | 220 / 136 | 0 / 0 | PARTIAL |
| França | 87 / 18 / 18 / 51 | 38 / 26 / 0 / 12 | 106 / 160 | 0 / 0 | PARTIAL |
| Portugal | 61 / 18 / 2 / 41 | 18 / 14 / 0 / 4 | 182 / 10 | 0 / 0 | PARTIAL |

BRAZIL: PARTIAL — nenhum elenco atual completo.

ARGENTINA: PARTIAL — nenhum elenco atual completo.

ENGLAND: PARTIAL — nenhum elenco atual completo.

SPAIN: PARTIAL — nenhum elenco atual completo.

GERMANY: PARTIAL — nenhum elenco atual completo.

ITALY: PARTIAL — nenhum elenco atual completo.

FRANCE: PARTIAL — nenhum elenco atual completo.

PORTUGAL: PARTIAL — nenhum elenco atual completo.

GB mantém o código legado da interface para Inglaterra e usa o recorte das ligas inglesas; inclui clubes galeses que disputam esse sistema. A liga norte-irlandesa Q2949826 foi retirada do recorte. Seleções nacionais identificadas foram excluídas.

## Dados e implementação

REAL_CLUB_VERIFIED confirma identidade esportiva com evidência de país/categoria/competição; não certifica elenco nem atividade atual. CurrentSeasonConfirmed é separado. REAL_CLUB_HISTORICAL indica participação apenas em temporada passada ou dissolução conhecida; não significa necessariamente que o clube esteja extinto. REAL_CLUB_UNVERIFIED é candidato de diretório antigo ou evidência de liga/categoria insuficiente. FICTIONAL_CLUB identifica complemento gerado autorizado. Históricos e não verificados ficam fora da montagem padrão e exigem escolha explícita. As equipes femininas verificadas nesta carga têm associação de liga sem período certificado; todas mostram atividade atual não certificada.

WIKIDATA: entidades estruturadas CC0; descoberta feminina separada, correspondências com QID e 32 consultas de vínculos em lotes de até 16 clubes, LIMIT 2400. Uma segunda coleta limitada a 100 atletas por consulta verifica P21; não se infere a categoria de um atleta apenas pela organização contratante. Vínculos sem categoria compatível ou nome/posição válidos não entram na normalização.

OPENFOOTBALL: 339 identidades masculinas com participação datada em 2025/2025-26/2026/2026-27, mais diretório não verificado. Apenas 158 têm participação documentada nas temporadas que começam em 2026. Não há arquivo argentino 2026 no recorte obtido; os 30 argentinos de 2025 estão históricos, fora da montagem padrão.

Identidades: 76 mapeamentos aplicados entre provedores, com país, categoria, QID, liga e nomes alternativos; propostas ambíguas não são fundidas. Corrigido o parser de diretório para não anexar um clube sem vírgula às variantes do anterior. O lookup de aliases em memória usa índice por chave.

Contratos: início e fim explícitos precisam cobrir a referência; fim ausente é OpenEndedUnconfirmed. Dois vínculos simultâneos datados do mesmo atleta em Real Madrid e Manchester City foram marcados Unknown e retirados da escalação. Isso preserva a evidência e evita escolher arbitrariamente um clube.

REAL_RANKING: indisponível; nenhum ranking oficial/Opta importado. ESTIMATED_RANKING: 339 clubes masculinos com Elo local, K=20, base 1500, calculado de 4819 resultados reais CC0, excluindo futuros, jogos sem placar, duplicados e conflitos. Grafos domésticos desconectados não são calibrados mundialmente; método e aviso explícitos, confiança máxima 0,35. Equipes sem partidas ficam não classificadas. Nenhum ranking masculino é usado no feminino.

## Mundos, cache e persistência

O modo somente real bloqueia a confirmação por falta de elencos. Os ensaios controlados criaram 128 identidades reais por categoria, com AllowFictionalPlayers=true explícito, sem clubes fictícios, históricos ou não verificados. Não são mundos com elencos reais completos. O masculino representa sete países, pois não há argentinos com temporada atual confirmada no recorte; o feminino representa os oito países. Detalhes e hashes: audit-013b-coverage/coverage.json.

LICENSES_VERIFIED: CC0 para todo dado importado. CACHE: PostgreSQL exclusivo em 127.0.0.1:55434, bancos elifoot_013b_catalog e elifoot_013b_worlds. FALLBACK: falha de fonte simulada no PostgreSQL conserva o mesmo snapshot; pacote incompleto não o substitui. Reinício real do PostgreSQL compara hashes de todos os snapshots e mundos; depois o serviço é recriado e confirma novos mundos a partir dos dados salvos. Backups antes/depois e SHA-256 em restart-verification.json. Nenhum mundo anterior é sobrescrito.

Os elencos com categoria conferida usam um novo escopo de proveniência, wikidata-reviewed-memberships, mantendo as versões anteriores. A correção de recorte esportivo/geográfico teve diminuição de contagem revisada explícita, com snapshots antigos conservados. Respostas parciais comuns continuam bloqueadas.

## Validação e checkpoint

DOTNET_TESTS: 441 PASS, incluindo a baseline 420 e 21 casos novos; build Release com zero avisos/erros. FLUTTER_TESTS: 65 PASS, baseline 63 mais dois novos, com teste de reset dos consentimentos ampliado. Flutter analyze sem problemas; flutter build web concluído. Node: dois testes de parser PASS. Logs finais em audit-013b-coverage.

O runner mantém a exclusão herdada de MultiSeasonAndBenchmarkTests (100 temporadas e benchmarks extensos). Não se declara execução integral desses ensaios; a regressão curta e os testes específicos foram executados com PostgreSQL real isolado.

CHECKPOINT_COMMIT: consultar o commit desta entrega na branch feature/real-football-data-013b (git log -1 --format=%H). A entrega do chat informa o hash efetivo; base preservada d00fc26.

KNOWN_ISSUES: nenhum elenco atual completo; atividade atual feminina não certificada; diretórios e vínculos comunitários incompletos; descoberta masculina por país limitada a 900 linhas, com saturação registrada; identidade por região e clubes poliesportivos ainda exige revisão; nenhum ranking oficial ou calibração mundial. Cobertura continua PARTIAL independentemente dos testes aprovados.
