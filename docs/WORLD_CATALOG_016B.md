# ELIFOOT_WORLD_CATALOG_AND_LOBBY_016B

STATUS: PARTIAL. Implementação e evidências locais disponíveis; autenticação real no fornecedor e auditoria visual no Chrome continuam pendentes.

WORKTREE: D:\elifoot_98_016B
BRANCH: feature/world-catalog-lobby-016b
BASE_COMMIT: 00b68c5, escolhido pelo usuário para preservar a 016B atual.
CHECKPOINT_COMMIT: consultar `git log -1` desta branch; este documento integra o próprio checkpoint.

O catálogo compartilhado fica no banco `elifoot_016b_013b_wc_catalog`; os saves ficam em `elifoot_016b_013b_wc_worlds`, PostgreSQL local 55442. O primeiro início copiou os bancos DEV por pg_dump/pg_restore, sem restaurar sobre os originais. Novos dados e perfis afetam novas criações; mundos confirmados conservam seu snapshot.

## Entrega e limites comprovados

- API_CONFIGURED: adaptador e configuração implementados; APIFOOTBALL_API_KEY ausente. API_AUTHENTICATED: não. API_PLAN: desconhecido. API_QUOTA: desconhecida; orçamento local máximo de 100 chamadas/dia, nenhuma chamada real nesta auditoria.
- COUNTRIES: oito opções iniciais BR, AR, GB, ES, DE, IT, FR, PT; diretório global extensível pela descoberta do fornecedor. Não representa uma carga mundial completa já efetuada.
- LEAGUES: descoberta persistente por país/temporada; categoria exige revisão fundamentada. Nenhuma liga atual nova obtida da API sem chave.
- REAL_CLUBS: prévia masculina 158 elegíveis, 128 selecionados; feminina 232 elegíveis, 128 selecionados. Clubes reais vieram do catálogo salvo existente.
- CURRENT_PLAYERS: masculino 0; feminino 1 vínculo datado previamente revisado. Não são novos resultados de API-Football. A temporada atual dos clubes femininos não está confirmada pelo fornecedor.
- HISTORICAL_PLAYERS: conservados como referência pelo fluxo anterior; nunca promovidos silenciosamente a atletas atuais. Consultar cobertura por país.
- FICTIONAL_PLAYERS: masculino 2.610; feminino 2.565, com consentimento REAL_CLUBS_SIMULATED. Todos têm origem FICTIONAL_GENERATED.
- WORLD_RANKING: classificação estimada do catálogo disponível, com origem/data/confiança; não ranking mundial oficial. CLUB_POWER_1_TO_20: dois clubes por nível nos primeiros 40, mínimo 1 depois, exceções manuais preservadas.
- SQUAD_PATTERN_23_TO_17 / SQUAD_CYCLE: 23,23,22,22,21,21,20,20,19,19,18,18,17,17; ciclo de 14. ELITE_CLUBS_23_PLAYERS: corte padrão 20, exige evidência de ranking com confiança positiva.
- CLUB_COLORS / COLORS_COVERAGE: paletas já verificadas carregadas da fonte local; mundo manual final contém 14 clubes com paleta verificada. Ausências ficam neutras, sem inventar cores.
- COUNTRY_WEEKLY_REFRESH: sete dias desde sucesso, renovação automática no worker; cache persistente, cursor por clube/página e fallback para última carga válida. RATE_LIMITS e API_FAILURE_HANDLING reutilizam 016A.
- MAX_COUNTRIES: 8 por mundo. MAX_WORLD_CLUBS: 128. FOUR_DIVISIONS: 4 x 8. DISTRICT_CLUBS: 96. CUP_COMPATIBILITY: rodada real da preliminar concluída pelo motor existente.
- MINIMUM_VALIDATION: 128 reais sem complemento; pelo menos 32 reais para distritais fictícios autorizados; mundo totalmente fictício exige duas autorizações explícitas. Elenco mínimo 11, um goleiro e dez de linha; máximo 24 preservado.
- CLASSIC_GAME_MODE: CLASSIC_SYNC. HUMAN_COUNT: 1 a 8; testes 1/2/4/6/8. MANUAL_CLUB_SELECTION, UNEMPLOYED, FOURTH_DIVISION_DRAW e HOST_START implementados no servidor. MULTIPLAYER_READY preserva envio sincronizado de táticas após o início pelo criador.
- POSTGRESQL / SNAPSHOT: rodada real salva e recuperada após reinício exclusivo da API 5175; SHA256 do snapshot permaneceu igual.
- OLD_SAVES_PRESERVED: cinco saves originais comparados ao backup inicial; conteúdo de jogo preservado. Um timestamp updatedAtUtc difere, sem alteração de rodada, versão, jogadores, resultados ou finanças.
- MATCH_ENGINE_PRESERVED / ECONOMY_PRESERVED / AUCTIONS_PRESERVED: diff contra 00b68c5 vazio nos motores e estados protegidos; checkpoints anteriores ancestrais preservados.

## Validação e pendências

Detalhes em INTEGRATION_TESTS_016B.md, VISUAL_AUDIT_016B.md e KNOWN_ISSUES_016B.md. DOTNET_TESTS: 727/727 por caso único após reexecuções documentadas. FLUTTER_TESTS: 215/215, análise zero e build aprovada. NODE_TESTS: 2/2.

100_SEASONS: há duas simulações específicas de 100 temporadas, uma por categoria, usando partidas do motor real e clubes/jogadores fictícios autorizados. REAL_BROWSER_TEST / DESKTOP / MOBILE / SCREENSHOTS: pendentes; controle de página bloqueado apesar da extensão detectada. Teste manual do usuário não foi aprovado por esta missão.

Não houve merge, push, publicação ou alteração de código em outro worktree.
