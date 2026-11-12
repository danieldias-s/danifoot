# Elifoot 98 Multiplayer — auditoria do estado atual

**Diagnóstico:** 09/11/2026, referência de data America/Sao_Paulo.  
**Raiz auditada:** D:/elifoot_98.  
**Branch e revisão:** main — 451a916033cb9a3adc945e6eaf3bb02c3b072434.  
**Finalidade:** descrever o produto existente e fornecer evidências para decisões do proprietário. Nenhuma escolha de gameplay foi feita nesta auditoria.

## 1. Fotografia do projeto e limites da prova

### 1.1 Recorte e integridade

A análise cobre os arquivos da raiz acima, no commit indicado. O status inicial não apresentava arquivos versionados modificados ou preparados para commit. Havia arquivos locais não rastreados: .cursorrules.md, CLAUDE.md, dez entradas em .worktrees/ e material de auditorias em docs/audit-012, audit-012b, audit-012c, audit-012c-discipline, audit-012d e audit-012d-fix. Esses materiais não foram integrados nem removidos. As alterações ainda em outras worktrees não representam o código da main e não compõem este diagnóstico.

A única criação desta tarefa é este relatório. Não foram iniciados API, navegador, importador, relógio de salas ou serviços de banco; não houve consulta a saves, reindexação, migração, instalação, alteração de dependências ou operações Git de escrita. Os experimentos novos carregaram as DLLs Release existentes em um processo PowerShell/.NET 10.0.14 e trabalharam exclusivamente com objetos em memória. O status dos arquivos versionados continuou limpo depois dos experimentos.

O aplicativo declara versão **1.0.0+1** em client/elifoot_flutter/pubspec.yaml:19. Isso não identifica sozinho uma versão de regras: existem versões independentes para economia, disciplina, evolução, carreira, estatísticas e histórico nos snapshots.

### 1.2 Referência do original

Foi lido integralmente C:/Users/Daniel/Downloads/ELIFOOT_98_DOCUMENTACAO_TECNICA_CONSOLIDADA.md: 805 linhas; versão 1.0, consolidação de 09/11/2026; SHA-256 **840FEF091361BA4EA97742587F7EE04E0066DB61084661D766918D48C4ADB248**. Nas tabelas, **REF** designa esse arquivo, seção e linhas indicadas.

O documento relata estudos de um executável NE de 16 bits específico; não é documentação oficial e informa ausência de validação dinâmica integral do binário. Seus níveis são preservados: [BIN] = identificação estática relatada; [EXT] = fonte externa não revalidada integralmente; [OBS] = observação; [HIP] = hipótese; [PEND] = desconhecido. A seção 10 contém propostas de modelagem, não obrigações de implementação. A hipótese força + gols e os gatilhos históricos de demissão não foram convertidos em especificação.

### 1.3 Estrutura realmente encontrada

| Camada | Implementação e pontos de entrada | Evidência |
| --- | --- | --- |
| Backend | C#, ASP.NET Core net10.0, Minimal APIs REST, SignalR; entrada Program.cs e hub de sala | src/Elifoot.Api/Elifoot.Api.csproj:4; src/Elifoot.Api/Program.cs:12,122; src/Elifoot.Api/Hubs/GameRoomHub.cs:29 |
| Aplicação | Orquestração de rodada, comandos financeiros, consultas de clube/mercado, rankings e scouting | src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:12; FinancialCommandsApplicationService.cs:10; PlayerRankingsApplicationService.cs |
| Domínio | Motor por minuto, escalação/IA, disciplina, forças, economia, carreira, competições, histórico e políticas de catálogo | src/Elifoot.Domain/MatchEngine.cs:198; AiManagerBrain.cs:12; CompetitionAndSeasonEngine.cs:5; HistoryEngine.cs:9 |
| Persistência dos mundos | EF Core; PostgreSQL por padrão, SQLite opcional, repositório em memória opcional; snapshot JSON mais projeções relacionais | src/Elifoot.Api/Program.cs:23; src/Elifoot.Infrastructure/ElifootDbContext.cs:5,69; EfWorldRepository.cs:97 |
| Contas | Tabelas de conta, treinador, sessão, concessão e auditoria; cookies AccountTest | src/Elifoot.Infrastructure/ElifootDbContext.cs:73; AccountRecords.cs:67; src/Elifoot.Api/AccountLogin.cs:22,47 |
| Catálogo | Banco separado, drafts, montagens privadas, proveniência, fornecedores legados e catálogo histórico de arquivos | src/Elifoot.Api/WorldCreatorEndpoints.cs:54; src/Elifoot.Infrastructure/Catalog/WorldCreatorService.cs:361; LocalHistoricalCatalogFiles.cs:33 |
| Frontend | Dart/Flutter; Web e estrutura Android; shell compartilhado, apresentação atual/clássica, REST e realtime | client/elifoot_flutter/pubspec.yaml:30; lib/main.dart:49; lib/presentation_widgets.dart:33; lib/account_shell.dart:14 |
| Ferramentas | Gerador de seed, importador, iniciadores e scripts de QA; solução agrupa backend e testes | Elifoot.slnx; tools/seed-generator/Program.cs; tools/catalog-013b/importer/Elifoot.RealDataImporter.csproj; scripts/start-local-historical-preview.ps1 |
| Trabalhos em segundo plano | Relógio compartilhado de salas; worker de catálogo 016B condicionado à configuração | src/Elifoot.Api/Services/RoomClockBackgroundService.cs:24; WorldCatalogEndpoints016B.cs:13,69 |

Nos caminhos abreviados desta tabela, o diretório é o da primeira entrada da mesma célula/camada; nas evidências das matrizes abaixo os caminhos são explícitos e relativos à raiz auditada.

O arquivo appsettings.json configura AccountTest, PostgreSQL, criação automática do mundo inicial e relógio habilitado; esses são defaults do arquivo, não prova de quais parâmetros um processo já aberto recebeu. A API pode criar schema e seed ao iniciar; por isso não foi iniciada nesta tarefa (src/Elifoot.Api/appsettings.json:14; Program.cs:90).

Não há arquivos de migrations EF versionados na busca git ls-files '*Migrations*' '*Migration*'. O caminho encontrado usa EnsureCreated e rotinas SQL aditivas EnsureSchema, além de migrações lógicas de snapshots. Isso não equivale a uma cadeia tradicional de migrations EF (Program.cs:96; WorldCreatorEndpoints.cs:78; AccountRecords.cs:67).

### 1.4 O que foi executado ou apenas inspecionado

**Validação nova:** cenários S1, S2, S3, S5, S6, S7, S8 e parte de S9, além de X1, em memória, descritos na seção 5. S4 não é executável como comparação de três nacionalidades porque a regra correspondente não foi encontrada. Nenhum cenário novo comprova UI, transporte HTTP ou persistência em banco.

**Resultados anteriores da mesma conversa e revisão:** os arquivos em artifacts/main-sync-20261109-192940 foram lidos e seus resultados TRX confrontados com nomes de testes. Não foram reexecutadas essas suítes durante esta auditoria:

| Verificação anterior | Resultado efetivamente registrado | Limite |
| --- | --- | --- |
| Build .NET Release | Concluído; zero erros e zero avisos em dotnet-build.log | Compilação não comprova todos os fluxos |
| Domínio | 538 aprovados em test-results/domain.trx | Mistura testes de regras, cenários sintéticos e simulações locais; não é validação visual |
| Aplicação | 31 aprovados em test-results/application.trx | Inclui sincronismo/pausas/100 rodadas com repositório de teste; não são oito usuários reais em produção |
| Integração selecionada | 16 aprovados e 2 não executados em test-results/integration-final.trx | Conta, SQLite/HTTP de teste, SignalR e catálogo sintético; casos PostgreSQL ignorados |
| Flutter | 476 aprovados e 1 falha em flutter-tests.log | Falha no layout clássico de 1024 px |
| Repetição isolada do viewport | 2 aprovados e a mesma falha em flutter-viewport-recheck.log | Overflow horizontal de 48 px; não corrigido |
| Análise Flutter | A execução anterior retornou No issues found | Resultado observado na conversa; não substitui teste de layout |

As duas integrações não executadas foram LocalHistoricalCatalogIndexTests.ReindexPublishesAndAuditsInPostgres e WorldCreatorReviewTests.PostgresHoldsTheDraftLockUntilTheWorldIsDurablyPublished. Não são aprovações PostgreSQL.

Há evidência histórica de 100 temporadas em docs/evidence/DISTRICT_VS_DIV1_DIV2_100_SEASONS.md:3, explicitamente referente ao checkpoint cae3a65, seed 199801. Não foi reexecutada nem promovida a prova integral da revisão atual. Relatórios antigos de Android e imagens do repositório também não comprovam a compilação atual em aparelho ou navegador real.

**Limitações:** sem validação visual nova, sem Android real, sem PostgreSQL novo, sem serviços externos, sem leitura de saves persistentes e sem execução instrumentada do EXE original. Distribuição por várias instâncias de API não foi validada. A ausência de prova é indicada por item, sem transformar todo código encontrado em funcionamento end-to-end.

## 2. Legenda obrigatória

Cada item de matriz recebe exatamente um estado:

| Estado | Significado nesta auditoria |
| --- | --- |
| CONFIRMADO_FUNCIONANDO | Caminho real executado em cenário em memória ou teste identificado com resultado disponível; o alcance da prova fica indicado |
| IMPLEMENTADO_SEM_VALIDACAO | Implementação concreta localizada, sem execução suficiente do caminho correspondente |
| PARCIAL | Camadas, casos, dados ou consistência ainda incompletos; inclui defeito reproduzido quando indicado |
| DIFERENTE_DO_ORIGINAL | Regra atual identificada e diferente de referência razoavelmente estabelecida; não implica defeito nem decisão de mudança |
| NAO_IMPLEMENTADO | Ausência fundamentada na busca das entidades, cálculos e entradas pertinentes |
| INDETERMINADO | Evidência insuficiente ou inspeção segura não realizada |
| NAO_APLICAVEL | Fora do escopo atual de forma fundamentada; não usado como substituto para ausência de implementação |

Um item DIFERENTE_DO_ORIGINAL pode ter execução comprovada na coluna de evidência; seu estado continua único. “Referência inconclusiva” caracteriza a comparação com o original, não cria um oitavo estado. T01–T21 designam testes e resultados identificados na seção 6.8. S e X designam os experimentos desta auditoria.

## 3. Matriz — original × jogo atual × decisão do dono

Todos os valores são os encontrados no código, não novas propostas. **Decisão do dono = A DEFINIR em todas as linhas.**

### A — Motor das partidas

| ID | Mecânica | Elifoot 98 original — fonte e evidência | Jogo atual — regra/valor | Status | Evidência atual arquivo:linha/teste | Diferença/risco | Decisão do dono |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A01 | Força individual e improvisação | REF §2.3, L111–123 [EXT]: contribuição aproximada 2F, ajuste posicional e nacionalidade | F limitado a 1–50; setor fora da posição ×0.65; envolver goleiro ×0.35; craque depende do setor | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/MatchEngine.cs:144 — GetEffectivePlayerRating; T01/T02 | Escala própria; não reproduz a contribuição descrita no estudo | A DEFINIR |
| A02 | Goleiro e força coletiva | REF §2.1–2.3 [EXT], sem pesos equivalentes completos | Goleiro natural preferido; improvisado sofre ×0.35. Defesa efetiva=(D+0.45M)×mando×identidade×0.93^ausentes; ataque=(A+0.50M)×mando×identidade×0.93^ausentes×transição; pisos 5 nos totais e 1 no goleiro | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MatchEngine.cs:43,124; S2; T02 | Pesos atuais concretos; referência não estabelece sua equivalência histórica | A DEFINIR |
| A03 | Formações e 5-0-5 | REF §2.3 [EXT]: eficiência elevada relatada; não define esta adaptação | Dez presets; valida também combinações numéricas que somam dez. 5-0-5 permitido por flag, ataque ×0.82 e sem contribuição de meio-campo | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/TacticsAndSquadRules.cs:17,43; MatchEngine.cs:128; T02 | Teste caracteriza fórmulas; não demonstra que 5-0-5 seja ótimo ou que o equilíbrio seja o original | A DEFINIR |
| A04 | Mando esportivo | REF §2.2, L96–107 [EXT]: filtros Random(6000)/Random(7000) | ×1.06 no ataque e na defesa do mandante; identidade ofensiva/defensiva usa 1.06/0.97 nos respectivos totais | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/MatchEngine.cs:19,110; S2 | Seis por cento de setor não equivale aos 16.7% relativos de um filtro do estudo nem a ganho de vitória | A DEFINIR |
| A05 | Público como bônus esportivo | REF §2.2 [EXT]/[PEND]: bônus direto de lotação não identificado | ComputeSectorBreakdown e TryResolveAttackAttempt não leem público/capacidade; torcida entra na economia | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MatchEngine.cs:26,472; S2 | Ausência na cadeia inspecionada; não prova inexistência histórica de todo efeito no EXE | A DEFINIR |
| A06 | Tempo, seed e reprocessamento | REF §2.1 [EXT]: eventos probabilísticos | Um minuto por tick; SplitMix64 por seed, temporada, rodada, índice da partida, minuto/subpasso; LastSimulatedMinute impede repetir o mesmo minuto | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/DeterministicRng.cs:21; MatchEngine.cs:211,226; T02 | Reprodutibilidade depende também dos IDs e do estado inicial; não é seed do original | A DEFINIR |
| A07 | Criação e conversão de chances | REF §2.1–2.2 [EXT], cadeia integral não confirmada | Chance=clamp(0.044×(ataque/max(10,defesa))^0.85,0.008,0.14); impedimento 0.09 após chance. Gol=clamp(0.34×(potência/max(8,resistência))^0.90,0.06,0.72) | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/MatchEngine.cs:489,499,572; T01 valida monotonicidade, não calibração completa | Probabilidades são próprias; não houve nova estimativa de distribuição de placares | A DEFINIR |
| A08 | Quem finaliza e papel do asterisco | REF §3.3–3.4 [BIN]/[EXT]/[PEND]: efeito exato no gol aberto | Peso G/D/M/A=0.02/0.7/2.8/5.5; craque ×1.35 e ×(0.7+F/50). Finalização F×1.20 se craque, depois ×1.12 A, ×1.02 M ou ×0.82 demais; soma 8% do ataque | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MatchEngine.cs:558,641; T01 | Não usa gols anteriores; a referência do original é inconclusiva para estes multiplicadores | A DEFINIR |
| A09 | Pênaltis | REF §2 e §11 [EXT]/[PEND], sem fórmula completa | Conversão=clamp(0.74+(F−25)×0.004+0.06 se craque−poderG×0.003,0.58,0.88). Humano escolhe cobrador elegível; sorteio fica retido na pendência | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MatchPenaltyResolution.cs:5; MatchEngine.cs:533; src/Elifoot.Application/MatchInteractionCommands.cs:49; X1; T01 | Não confundir gol de pênalti durante jogo com desempate de shootout | A DEFINIR |
| A10 | Faltas e cartões | REF §4 [BIN]/[EXT]: seis códigos e ordem de risco; percentuais abertos | Pesos Fair Play/Cordeirinho/Cavalheiro/Caneleiro/Caceteiro/Sarrafeiro=0/0.35/0.75/1.45/2.35/3.60; falta clamp(0.095×peso médio,0,0.35), gatilho de cartão 0.0128×média; cautela após amarelo ×0.32 ou ×0.48 nos dois mais agressivos | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MatchEngine.cs:168,183,308; T03 | Não há bônus de desarme/finalização por agressividade na fórmula esportiva | A DEFINIR |
| A11 | Vermelho e suspensão | REF §4.2 [EXT]/[PEND], duração exata não consolidada | Vermelho direto condicionado ao cartão: 0.005/0.012/0.025/0.048/0.085; dois amarelos expulsam. Suspensão direta 1/2/3/4 jogos com cortes 50/80/95/100 | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MatchEngine.cs:349,389; DisciplineEngine.cs:27; T03; domain.trx — Discipline012CTests | Fair Play não entra nos candidatos a faltas intencionais; isso não o imuniza a lesão | A DEFINIR |
| A12 | Lesões | REF §2.4 e §4.2 [PEND]: sem taxa exata ou proteção por comportamento | Teste 0.0018 por equipe/minuto; atleta ativo sorteado sem peso de comportamento; sai do campo. ClassicAdapted usa 2–15 dias, perda total limitada por min(F−1,piso(F×dias/30)); legado usa rodadas | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/MatchEngine.cs:429; ClassicForceEvolutionEngine.cs:13,35,135 | Há testes de saída/recuperação no TRX; não houve nova validação da frequência e do ciclo completo em API | A DEFINIR |
| A13 | Substituições e pausa | REF §2.1 [EXT], não fixa estas quotas | 5 trocas/3 janelas; 6/4 na prorrogação; intervalos livres; expulso não é reposto, substituído não volta; pausa humana é global | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MatchEngine.cs:682; src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:876; T11 | Decisões próprias do multiplayer, sem equivalência histórica demonstrada | A DEFINIR |
| A14 | Queda, recuperação e fadiga | REF §2.4 [BIN]/[EXT]/[PEND]: evolução não reconstruída; stamina não provada | Pós-Liga concluída: participante elegível perde 1 com probabilidade 0.50; não participante cresce 1 abaixo do teto por divisão/posição. Taça não dispara esse ajuste; lesão diária é separada | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/ClassicForceEvolutionEngine.cs:186,196,226; ChampionshipStrengthLimits.cs:9; T10 | Não existe variável explícita de stamina na entidade Player; não atribuir a ela a queda de força | A DEFINIR |

Os tetos de recuperação atuais por posição 1–8 são: 1ª=[50,50,50,46,44,42,40,39]; 2ª=[38,37,36,34,32,30,28,27]; 3ª=[25,24,23,21,19,17,15,14]; 4ª=[12,11,10,8,6,4,2,1]; fora dessas divisões, teto 1. O teto limita crescimento, não reduz imediatamente um atleta acima dele (ChampionshipStrengthLimits.cs:7; ClassicForceEvolutionEngine.cs:239).

### B — Jogadores, escalação e artilharia

| ID | Mecânica | Elifoot 98 original — fonte e evidência | Jogo atual — regra/valor | Status | Evidência atual arquivo:linha/teste | Diferença/risco | Decisão do dono |
| --- | --- | --- | --- | --- | --- | --- | --- |
| B01 | Posição e disponibilidade | REF §3.2 [BIN]/[HIP]: posição e elegibilidade participam | G/D/M/A; disponível se lesões em dias e rodadas e suspensão ≤0; elenco 11–24, titulares até 11 aptos, banco até 9; um goleiro quando há goleiro apto | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/Entities.cs:137,182; TacticsAndSquadRules.cs:68,104; T02/T03 | Campo “condição” do estádio não é condição individual; escalações curtas são permitidas em indisponibilidade | A DEFINIR |
| B02 | Seleção da IA e padrões do servidor | REF §3.2, L133–149 [BIN]/[HIP]: possível F+gols | Score=F+5.5 para craque A/M ou +3.5 G/D−0.25×código do comportamento; preenche posição e depois vagas restantes; não consulta gols | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/AiManagerBrain.cs:259,326; src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:1080; S1 | Referência inconclusiva para afirmar “divergência de F+gols”; regra atual está provada | A DEFINIR |
| B03 | Autoescalação acionada na UI | REF §3.2 [HIP], cadeia humano/IA aberta | Filtra indisponíveis, ordena força decrescente e ID crescente; preenche setores e banco. Não usa score de craque/comportamento da IA | CONFIRMADO_FUNCIONANDO | client/elifoot_flutter/lib/shell_actions.dart:736,747; Flutter anterior; comparação estática com B02 | Dois critérios automáticos coexistem; discrepância importa quando força empata ou atributos variam | A DEFINIR |
| B04 | Sete atacantes, 7/2/0 gols | REF §3.1–3.2 [OBS]/[HIP] | Com três vagas e cinco atletas de força 50, os dois de força 45 saíram da seleção apesar de 7 e 2 gols; zerar gols manteve a seleção | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/AiManagerBrain.cs:326; S1 | Não reproduz o padrão observado pelo usuário no original; a explicação histórica F+gols permanece hipótese | A DEFINIR |
| B05 | Desempate e momento de troca | REF §3.2 [PEND]: momento da reordenação aberto | Backend OrderByDescending estável conserva ordem de entrada em empate do score; UI desempata por ID. Servidor prepara nova seleção na preparação da rodada; submissão humana é revalidada antes de iniciar | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/AiManagerBrain.cs:282; client/elifoot_flutter/lib/shell_actions.dart:747; src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:447,1073 | S1 valida um empate ordenado; não se auditou toda combinação de reordenação e troca em UI | A DEFINIR |
| B06 | Gols sazonais e de carreira | REF §3.4 [OBS]/[HIP]: reset sazonal explica observação | Gol de jogo incrementa SeasonGoals e CareerGoals; não-Liga incrementa também CupGoals. Inicialização zera SeasonGoals/CupGoals/SeasonMatches/cartões sazonais, conserva carreira | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MatchEngine.cs:616; CompetitionAndSeasonEngine.cs:85; S9 parcial; X1 | Shootout separado; gols não são adicionados ao score de seleção ou de chute | A DEFINIR |
| B07 | Total de gols no painel | REF §3.4: contadores devem ser separados | GetPanelStatistics usa max(SeasonGoals+CupGoals, gols dos buckets). Um gol real de pênalti de Taça gerou contadores 1/1 e painel 2 | PARCIAL | src/Elifoot.Domain/PlayerStatisticsEngine.cs:303; src/Elifoot.Application/ClubAndMarketApplicationService.cs:124; X1 | Inconsistência reproduzida em domínio; falta repetir o fluxo completo de tela após uma partida concluída | A DEFINIR |
| B08 | Asterisco de origem | REF §3.3 [BIN]: modificador D≥8 em ramo de criação M/A | IsStar é boolean independente; fábrica sorteia craques em posições de seu template; efeito esportivo em A08, monetário em C02 | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/Entities.cs:140; WorldSeedFactory.cs:254; MatchEngine.cs:16 | Não há modificador individual B/D equivalente no runtime; não inferir origem histórica da estrela | A DEFINIR |
| B09 | Seis comportamentos | REF §4.1 [BIN]: códigos 0–5 na mesma ordem | Enum 0–5 coincide; interfere em seleção, avaliação e disciplina. Lesão não lê classe; salário de referência não lê classe | CONFIRMADO_FUNCIONANDO | src/Elifoot.Contracts/Enums.cs:21; src/Elifoot.Domain/MatchEngine.cs:168; AiManagerBrain.cs:334; S3 | Nenhum buff de desarme ou proteção contra lesão foi encontrado na cadeia inspecionada | A DEFINIR |

### C — Economia e mercado

| ID | Mecânica | Elifoot 98 original — fonte e evidência | Jogo atual — regra/valor | Status | Evidência atual arquivo:linha/teste | Diferença/risco | Decisão do dono |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C01 | Salário de referência | REF §5.1 [BIN]/[HIP]: fórmula condicional com inflação I, escala C e fator R | round((220+1.65×clamp(F,1,50)^2.08)×1.20 se craque×1.08 se A,0); não lê comportamento, nome ou nacionalidade | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MarketAndEconomyEngine.cs:57; S3 | Não aplica a fórmula candidata da referência; seus parâmetros históricos não são uma tabela obrigatória | A DEFINIR |
| C02 | Avaliação individual | REF §5.2 [BIN]: round(2000×C×I×F^1.04×(1−0.04A−0.02B+0.02D)) | Base 12000+76F^2.65; craque A/M ×1.32, G/D ×1.22; posição A/M/G/D ×1.12/1.05/1.03/0.98; comportamento ×1.08/1.04/1/0.95/0.89/0.82; contrato ≤2 ×0.75, ≤5 ×0.88, ≥18 ×1.06; piso 15000, round 0 | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/MarketAndEconomyEngine.cs:11; S3; T04 | Preço de avaliação não é necessariamente preço pedido ou pago; estrela tem multiplicador explícito diferente do ramo de interesse do original | A DEFINIR |
| C03 | Modificadores por nome e inflação individual | REF §5.2, L268–276 [BIN]: B/D derivados do nome; §6 [BIN] | Não encontrados B/D nem transformação do nome usados no valor/salário. Alterar nome não altera essas funções | NAO_IMPLEMENTADO | src/Elifoot.Domain/Entities.cs:131; MarketAndEconomyEngine.cs:11,57; busca de atributos e chamadas | Há nome e proveniência, mas não os campos financeiros históricos | A DEFINIR |
| C04 | Nacional/equiparado/estrangeiro | REF §5.5–5.6 [BIN]: classes, sobretaxas e limite 5 | Não encontrada classificação de nacionalidade do atleta, Bosman/PLOP, sobretaxa doméstica ou quota de cinco no fluxo de compra; país do clube/proveniência não implementa essas regras | NAO_IMPLEMENTADO | src/Elifoot.Domain/Entities.cs:80,131; MarketAndEconomyEngine.cs:818; src/Elifoot.Domain/Catalog/PlayerProvenance.cs:6 | S4 não pode comparar preços por três classes inexistentes | A DEFINIR |
| C05 | Contratos e renovação | REF §5.7 [OBS]/[PEND], sem dias ou percentual fixo provado | Contador de rodadas/jogos; SeasonContractsEnabled=true. Renovação preventiva protege até fim da temporada; salário fica no valor negociado ou anterior; luvas=round(salário×rodadas restantes/10,2) | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/SalaryContractsEngine.cs:30; SeasonContractsAndDebtEngine.cs:9; T05/T09 | Há lesão em dias, mas contrato não é contrato por dias; renovação preventiva não implica +10% automático | A DEFINIR |
| C06 | Bloqueio após renovação | REF §5.7 [OBS]/[PEND]: bloqueio sazonal relatado | RenewedForSeason impede listar, negociar, vender e leiloar na mesma temporada; proteção remove anúncio e cancela ofertas pendentes | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/SeasonContractsAndDebtEngine.cs:6,15; MarketAndEconomyEngine.cs:848; VoluntaryAuctionEngine.cs:19; T09 | Compatível com observação histórica, sem confirmar o algoritmo do EXE | A DEFINIR |
| C07 | Compra e venda efetivadas | REF §5.4–5.7: avaliação/interesse/preço são etapas distintas | ExecuteTransfer usa o preço validado, debita comprador e credita vendedor; valida posse, saldo e limites de elenco; anúncio guarda AskingPrice | CONFIRMADO_FUNCIONANDO | src/Elifoot.Application/FinancialCommandsApplicationService.cs:51,67; src/Elifoot.Domain/MarketAndEconomyEngine.cs:818; T21 | T21 executa transferências de domínio; não prova sozinho a negociação inteira por API/UI | A DEFINIR |
| C08 | Leilão voluntário customizado | REF §5.7 [PEND], sem regra equivalente de prazo/valor | Padrão R$500000 e 2 rodadas globais; duração aceita 1–3; valor mínimo informado pode ser zero. Posse só muda na venda concluída; sem comprador encerra mantendo atleta | CONFIRMADO_FUNCIONANDO | src/Elifoot.Contracts/EconomyDtos.cs:18; src/Elifoot.Domain/VoluntaryAuctionEngine.cs:7,15,41; T09 | R$500000 é default, não piso obrigatório; não confundir com leilão por recusa/insolvência | A DEFINIR |
| C09 | Pedido de aumento | REF §5.7 [PEND]; decisão própria do projeto | No máximo um novo pedido por clube/rodada, sem outro pendente; exige elegibilidade e contrato encerrado. Pedido=round(min(referência×fatorDiv,max(S+1,S×(1+cap))),0); fatorDiv 1ª/2ª/3ª/4ª/distrital=1.10/1.05/1/0.95/0.90; cap 0.10, ou 0.05 com caixa negativo | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/SalaryContractsEngine.cs:63,99; ClassicEconomyState.cs:38; T05/T13 | Não significa um aumento garantido em toda rodada ou para cada jogador | A DEFINIR |
| C10 | Recusa de aumento e elenco mínimo | REF §5.7 [PEND]; regra customizada | Recusa elegível tira vínculo e cria leilão; prazo sorteado 1–3 rodadas. Humano não pode recusar se violar elenco mínimo; IA faz renovação obrigatória nesse caso | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/SalaryContractsEngine.cs:125; FinancialInsolvencyEngine.cs:21; T05/T13 | Recusa não leva incondicionalmente a leilão; há exceção de elenco mínimo | A DEFINIR |
| C11 | IA no mercado e uso de crédito | REF §5.6 [BIN], interesses históricos distintos | Plano considera lacunas, reservas e orçamento; compra limita preço relativo ao FairValue; maxBid privado por leilão; reserva principal emprestado e exige capacidade operacional para novo crédito | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MarketAndEconomyEngine.cs:682,785; OperatingEconomyEngine.cs:27,45; T04/T06 | Política financeira da IA não é idêntica à liberdade de comando humano | A DEFINIR |

| C12 | Ofertas e contrapropostas | REF §5.7 [PEND]: negociação existe, cadeia final não reconstruída | Oferta guarda valor próprio e status; vendedor pode contrapor, aceitação valida ator, preço e disponibilidade; chamadas usam Finance para versão/posse/recibo | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Application/FinancialCommandsApplicationService.cs:71,82,84; ClubAndMarketApplicationService.cs:234 | Não foi reexecutado o fluxo completo de proposta/contraproposta por API; T21 comprova apenas a transferência efetivada em domínio | A DEFINIR |

### D — Estádio, público e bilheteria

| ID | Mecânica | Elifoot 98 original — fonte e evidência | Jogo atual — regra/valor | Status | Evidência atual arquivo:linha/teste | Diferença/risco | Decisão do dono |
| --- | --- | --- | --- | --- | --- | --- | --- |
| D01 | Capacidade | REF §7.1 [BIN]: 5000×nível, níveis até 24 relatados | BuiltCapacity e UsableCapacity são assentos inteiros independentes de nível; condição estrutural altera utilizáveis | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/Entities.cs:113; MarketAndEconomyEngine.cs:407 | Não há máximo global de 120000 nem degraus de 5000 nessa estrutura | A DEFINIR |
| D02 | Ampliação e reforma | REF §7.1 [PEND]: custos exatos não consolidados | Ampliação R$85 por assento; aceita adicional maior que zero e até 30000; obra dura uma rodada. Reforma custa capacidade×danoPercentual×38, recupera até +15 pontos de condição após uma rodada | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/MarketAndEconomyEngine.cs:442,453,477; src/Elifoot.Application/FinancialCommandsApplicationService.cs:102 | Mensagem da ampliação fala mínimo 1000, mas condição aceita 1–999; diagnóstico estático, sem nova execução do endpoint | A DEFINIR |
| D03 | Desgaste | REF §7.1 [PEND] | Processamento anual: sem manutenção sorteia 1–6 pontos percentuais; manutenção paga pode reduzir para 0–1; condição limitada entre 35 e 100; IA considera ROI e caixa | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/MarketAndEconomyEngine.cs:377,392; T04 | Modelo próprio de conservação; não inferir origem no binário | A DEFINIR |
| D04 | Público esperado e efetivo | REF §7.2 [BIN]: elencos completos, indicadores 5B, sorte, Taça ×1.30, corte por capacidade | Demanda base da divisão ×(0.6+reputação/100)×campanha×atratividade visitante×condição×elasticidade×sorte 0.90–1.10×qualidade. Público=round(clamp(demanda,5% da capacidade útil,capacidade útil)) | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/MarketAndEconomyEngine.cs:98,124; OperatingEconomyEngine.cs:75; S7; T06 | Médias dos dois elencos entram no fator de qualidade, não na expressão 3Fc+Fv do original; divisão tem efeito direto | A DEFINIR |
| D05 | Qualidade dos elencos no público | REF §7.2 [BIN]: média de todos e pesos 3:1 | Política atual usa ((média mandante+média visitante)/(benchmark mandante+benchmark visitante))^2.2, com médias de todos os vinculados | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/OperatingEconomyEngine.cs:18,75; T06 | Participação simétrica das médias; benchmark depende da divisão; legado com expoente zero ignora fator | A DEFINIR |
| D06 | Ingresso | REF §7.3 [BIN]: médias do grupo, inflação, índice econômico; Taça ×1.50 | Base da divisão do mandante×(0.85+0.15×campanha×atratividade), arredonda centavos e limita R$5–65; multiplicador de competição atual é 1.25 | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/MarketAndEconomyEngine.cs:84,93,104; S7 | Não usa média de todo o grupo nem índice 79; condição por enum também afeta fases iniciais, ver D07 | A DEFINIR |
| D07 | Liga versus fases da Taça | REF §7.2–7.3 [BIN]: +30% demanda e +50% ingresso em Taça, sem tabela exclusiva de fases | Não há bônus de demanda explícito da Taça nesta função. CupRoundOf128/256/512/1024 recebem ×1.25 por terem enum ≥CupRoundOf16; CupRoundOf64 e CupRoundOf32 não recebem | PARCIAL | src/Elifoot.Domain/MarketAndEconomyEngine.cs:93; src/Elifoot.Contracts/Enums.cs:66; S7 | Condição ordinal não acompanha ordem esportiva das fases; reproduzida diferença 128 avos/64 avos | A DEFINIR |
| D08 | Destino da bilheteria | REF §7.4 [BIN]: Liga 100% mandante; Taça 50/50 | Liga 100% mandante; qualquer não-Liga dá 25% arredondados ao visitante e restante ao mandante | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/MarketAndEconomyEngine.cs:233; S7_SETTLED | Repartição atual 75/25 comprovada em lançamentos, não em mero texto | A DEFINIR |
| D09 | Custos e duas torcidas/rivalidade | REF §7.2 [BIN]: indicadores dos dois clubes; não soma fixa de torcidas | Mandante paga custo fixo por capacidade construída e variável por presente; defaults R$0.70/R$1.10. Não encontrados tamanhos de duas torcidas ou rivalidade usados na demanda | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/Entities.cs:120; MarketAndEconomyEngine.cs:136,249 | Reputação e campanha não equivalem automaticamente a torcida, indicador 5B ou rivalidade histórica | A DEFINIR |

Separação exigida por divisão: estes são **parâmetros diretos**, não intervalos garantidos de lotação ou bilheteria.

| Divisão | Ingresso-base direto | Demanda-base direta | Benchmark de força do fator de qualidade | Efeitos indiretos e limites |
| --- | ---: | ---: | ---: | --- |
| 1ª | R$24 | 26000 | 41 | Campanha, reputação dos dois clubes, elencos completos, estádio, preço e sorte modificam o resultado |
| 2ª | R$18 | 14500 | 33 | Mesma fórmula, sem faixa fixa de público |
| 3ª | R$14 | 8200 | 26.5 | Mesma fórmula, limitada pela capacidade útil |
| 4ª | R$11 | 4800 | 21 | Mesma fórmula, inclusive custos e repartição |
| Distrital | R$8 | 2200 | 15.5 | Participa da Taça; não constitui quinta Liga no calendário atual |

Evidência: src/Elifoot.Domain/MarketAndEconomyEngine.cs:84,108; OperatingEconomyEngine.cs:80. Na referência, REF §7.6 [BIN], não há esses multiplicadores explícitos por divisão na rotina de público.

### E — Banco e finanças

| ID | Mecânica | Elifoot 98 original — fonte e evidência | Jogo atual — regra/valor | Status | Evidência atual arquivo:linha/teste | Diferença/risco | Decisão do dono |
| --- | --- | --- | --- | --- | --- | --- | --- |
| E01 | Teto de crédito | REF §8.1 [BIN]: média de todo elenco×indicador interno×fatores×20000, menos dívida | Conta de empréstimos: base 2 na 1ª/2ª e 1 nas demais; indicador=assentos úteis×R$50; soma um limite por limiar atingido da divisão | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/ClassicEconomyEngine.cs:52; ClassicEconomyState.cs:42; S5 | Média da força, caixa, títulos e posição não são fatores diretos da função atual | A DEFINIR |
| E02 | Clubes da mesma divisão | REF §8.1 [BIN]: força média pode diferenciar limite | Na 4ª, médias 20 e 40, ambos com 10000 assentos úteis, receberam limite 2. Com 5000 úteis, limite 1 | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/ClassicEconomyEngine.cs:54; S5 | Estádio explica a diferença atual; não “força média mais alta” | A DEFINIR |
| E03 | Valor e crédito adicional | REF §8.1 [BIN]: teto monetário menos dívida | Cada novo contrato fixa principal R$500000; vagas disponíveis=max(0,limite−número de empréstimos não quitados). Limiar do indicador: 4ª [400000]; 3ª [500000,750000]; 2ª [1000000,1250000]; 1ª [1600000,1900000,2200000] | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/ClassicEconomyEngine.cs:57,72; ClassicEconomyState.cs:42; S6 | Conta de contratos não é exatamente limite financeiro menos dívida; parâmetros declarados não tornam o constructor genérico | A DEFINIR |
| E04 | Juros de novos empréstimos | REF §8.2 [BIN]: round(dívida×0.05) por jogo | R$25000 por partida oficial efetivamente jogada por empréstimo clássico de R$500000; guarda por MatchId, sem retroatividade | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/ClassicEconomyEngine.cs:83,94; S6; T05 | Equivale a 5% do principal original desse contrato, não a 5% do saldo total variável do original | A DEFINIR |
| E05 | Falta de caixa e capitalização | REF §8.2 [PEND]: principal constante era hipótese do exemplo | Paga juros até o caixa não negativo disponível; restante entra em UnpaidInterest e RemainingBalance. Próximo encargo clássico continua fixo em R$25000; não incide sobre juros acumulados | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/ClassicEconomyEngine.cs:105; T05 — UnpaidInterestIsRealDebtWithoutCompoundInterestOrGiftedCash | Dívida cresce por juros não pagos, sem juros compostos nesse modo | A DEFINIR |
| E06 | Quitação | REF §8.2 [BIN]: reduz caixa e dívida | Quitação integral entre partidas, validando posse, caixa disponível e chave; separa principal/juros em transações e histórico | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/ClassicEconomyEngine.cs:130; src/Elifoot.Application/FinancialCommandsApplicationService.cs:92; T05/T12 | Não foi localizado comando genérico de amortização parcial do empréstimo clássico | A DEFINIR |
| E07 | Caixa negativo | REF §8.2 [PEND]: tratamento aberto | Juros adicionais 2% sobre caixa negativo uma vez por rodada global concluída, independentes do juro bancário | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/SeasonContractsAndDebtEngine.cs:31; ClassicEconomyState.cs:34; T09 | Não é taxa do original comprovada; pode atingir clube ocioso em rodada de Taça concluída | A DEFINIR |
| E08 | Empréstimos legados | REF §8 não estabelece este segundo modo | LegacyInstallment conserva juros por saldo, prestação e prazo; campo default da entidade 0.015. Migração preserva contratos antigos e não inventa decomposição histórica | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/Entities.cs:478,488; ClassicEconomyEngine.cs:42,113 | Não aplicar 1.5% como taxa dos novos contratos clássicos; execução completa de save legado não feita aqui | A DEFINIR |
| E09 | Inflação e índices 5B/79 | REF §6 [BIN]: I inicia 2, varia 0.5–10; índices separados | Não encontrado índice global I atualizado no tempo nem equivalentes operacionais 5B/79 nas fórmulas auditadas; valores são recalculados por força, contrato e políticas atuais | NAO_IMPLEMENTADO | src/Elifoot.Domain/Entities.cs:5,69; MarketAndEconomyEngine.cs:11,57,84; busca Inflation/Infla | Reputação, indicador patrimonial e benchmark de força têm papéis diferentes e não devem ser renomeados como esses índices | A DEFINIR |
| E10 | Patrocínio e equilíbrio comercial | Sem fórmula correspondente em REF; mecanismo próprio | Política CLASSIC_OPERATING_BALANCE_012D_FIX_V2: orçamento ANUAL 185000/125000/92000/72000/20000, parcelado pelo calendário global, inclusive rodadas sem jogo; salários só se clube jogou | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/OperatingEconomyEngine.cs:17,55; MarketAndEconomyEngine.cs:279; T06 | Legado pode conservar política anterior até próxima temporada; não são esses valores a cada rodada no default novo | A DEFINIR |

### F — Treinadores humanos e IA

| ID | Mecânica | Elifoot 98 original — fonte e evidência | Jogo atual — regra/valor | Status | Evidência atual arquivo:linha/teste | Diferença/risco | Decisão do dono |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F01 | Ranking de carreira | REF §9.2 [EXT]: campeonatos, Taças, pontos, lexicográfico | Ordena LeagueTitles, CupTitles e CoachPoints desc; desempata CoachId crescente; reúne humanos e IA; acumula entre temporadas | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:676,728; CoachCareerEngine.cs:24 | Comentário fala quarto critério vitórias, mas código usa GUID | A DEFINIR |
| F02 | Pontos por resultado | REF §9.2 [EXT]: vitória fora/casa 3/2; empate fora/casa 2/1 | Liga: vitória 5−número da divisão, empate metade; sem bônus de mando. Taça: vitória 3/4/5/6/7/8 de 64 avos até final; fases 128/256/512/1024 caem em zero | DIFERENTE_DO_ORIGINAL | src/Elifoot.Domain/CoachCareerEngine.cs:79,104; T07 | Pontos da carreira não são os 3/1 da tabela; considerar fases novas sem pontuação explícita | A DEFINIR |
| F03 | Moral e campanha | REF §9.3–9.4 [PEND]: limiares exatos desconhecidos | Moral inicia 70, limitada 0–100; verde≥60, amarela≥30; delta depende de favorito/azarão, resultado, sequência sem vitória, goleada e posição esperada; desempregado recupera 6/rodada até verde | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/Entities.cs:513; CoachCareerEngine.cs:107,224; T07 | Não há regra simples “três derrotas sempre demitem”; referência inconclusiva | A DEFINIR |
| F04 | Demissão por desempenho | REF §9.3–9.4 [EXT]/[PEND]: má campanha relatada, gatilho aberto | Se moral CAPTURADA no início <30 e resultado é derrota, demite. Cair de 35 para 26 na derrota atual não dispara esse gatilho naquele jogo | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/CoachCareerEngine.cs:67,125; S8; T07 | Humano e IA usam a mesma rotina de resultado; moral de início e moral final são diferentes | A DEFINIR |
| F05 | Demissão por rebaixamento | REF §9.3–9.4 [PEND] | Humano é demitido ao cair da 4ª para distrital. IA efetiva, não interina, é demitida por rebaixamento em qualquer divisão tratada pelo finalizador | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:559,587; T07 | Tratamento humano/IA difere no fechamento; dívida não aparece como gatilho direto nessa rotina | A DEFINIR |
| F06 | Geração de convites | REF §9.2–9.4 [EXT]/[PEND]: ranking/reputação, detalhes abertos | Vaga aberta busca candidato com moral verde; score pondera ranking, moral, títulos, pontos e distância de divisão. Projeto usa divisão, reputação, força média, log do caixa e estádio | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/CoachCareerEngine.cs:198,239 | T07 valida casos de elegibilidade; não houve nova campanha controlada integral de convites humanos/IA | A DEFINIR |
| F07 | Elegibilidade e aceitação | REF §9.3 [PEND] | Desempregado tem cooldown 2 rodadas e experiência para clubes superiores; empregado tem cooldown 3 e exige melhora de projeto/divisão ou estrutura. Convite expira em 3; humano tem até 3 pendentes e decide; IA aceita só oportunidade elegível | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/CoachCareerEngine.cs:202,267,280; T07 — OnlyGreenHumanReceivesOpportunity | Não existe mapa fixo posição de ranking→divisão; recusa passa busca ao próximo candidato | A DEFINIR |
| F08 | Sucessor/interino | REF §9.1 e §9.4 [BIN]/[PEND]: notícia comprova sucessor, não algoritmo | Dispensa libera vínculo e abre vaga; procura IA livre verde com cooldown, por GUID; se não existe, cria interino equilibrado. Nova contratação mantém histórico e encerra vínculos | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/CoachCareerEngine.cs:151,180,296; S8 | Interino garante equipe, não é outro humano automaticamente assumindo | A DEFINIR |
| F09 | Férias e modo automático | REF §9.5 [EXT]: humano continua no clube sob gestão temporária | IsAutoMode existe e autoenvia escalação/pronto; há método de host que grava estado. Busca não encontrou ligação desse setter a endpoint/UI nem operação própria de férias com retorno | PARCIAL | src/Elifoot.Application/ClubAndMarketApplicationService.cs:472; MultiplayerRoundOrchestrator.cs:428; testes MultiplayerSynchronizationAndPauseTests.cs:378 | Teste altera a propriedade diretamente; isso não prova férias disponíveis ao usuário | A DEFINIR |
| F10 | Remoção manual de participante | REF §9.5 [BIN]/[EXT]: administração distingue remoção e demissão | Não encontrada rota/UI de remover treinador da sala com semântica administrativa; desconectar apenas atualiza presença | NAO_IMPLEMENTADO | src/Elifoot.Api/Hubs/GameRoomHub.cs:68; src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:106; busca RemoveCoach/Kick/HumanCoaches.Remove | Não considerar saída de conexão uma dispensa ou liberação de vaga | A DEFINIR |

### G — Temporadas, competições e progressão

| ID | Mecânica | Elifoot 98 original — fonte e evidência | Jogo atual — regra/valor | Status | Evidência atual arquivo:linha/teste | Diferença/risco | Decisão do dono |
| --- | --- | --- | --- | --- | --- | --- | --- |
| G01 | Divisões e clubes | REF não consolida um calendário completo como especificação | Quatro divisões nacionais de oito; restantes distritais. Criador exige 128=32+96; fábrica suporta mundos a partir de 64 e casos maiores | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/WorldSeedFactory.cs:107,128; Catalog/WorldCreatorEngine.cs:120,145; T08 | Não assumir que todo tamanho arbitrário terá o mesmo suporte dos tamanhos testados | A DEFINIR |
| G02 | Calendário | REF §2/§9 sem sequência completa comprovada | Para 128 novos: 14 rodadas de Liga e 7 eliminatórias, total 21; inicia Liga, alterna pares de Liga com Taça e termina na final. Temporadas legadas podem reter preliminares | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:14,61; S9 usa 64 e obtém 20; domain.trx — CupCalendarTests | Campo inicial TotalRoundsInSeason=22 não é total final calculado de um mundo novo de 128 | A DEFINIR |
| G03 | Sorteio e fases | REF não define a cadeia atual | Taça de novos mundos inclui todos na primeira fase, embaralhados por RNG contextual; fases seguintes usam vencedores e recusam fase anterior incompleta; empate eliminatório vai a prorrogação/shootout | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:181,204; MatchEngine.cs:841; T02/T11 | Evidência de fases completas inclui testes e auditoria histórica, não nova temporada visual | A DEFINIR |
| G04 | Classificação da Liga | REF não fixa nesta consolidação todos os critérios | Vitória 3, empate 1; desempate por vitórias, saldo, gols pró e índice determinístico | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/Entities.cs:101; CompetitionAndSeasonEngine.cs:415; T08 | Não confundir com pontuação do treinador, F02 | A DEFINIR |
| G05 | Acesso e descenso | REF sem algoritmo detalhado nesta consolidação | Sobem dois e descem dois entre divisões. Dois distritais sobem por vitórias de Taça, reputação e desempate determinístico | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:530,538; T07/T08 | Distritais não disputam Liga separada; ascensão não vem de tabela distrital | A DEFINIR |
| G06 | Premiação por clubes | REF não estabelece valores atuais | Campeão 1ª R$1.2M; Taça R$900 mil; vice Taça R$450 mil; cotas de posição nas divisões e acesso distrital R$380 mil; lançamentos com chave | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:502,518,577 | Valores inspecionados; não se executou fechamento completo novo para conferir cada prêmio | A DEFINIR |
| G07 | Reset e permanência | REF §3.1 [OBS]/[HIP]: reset de gols observado | Nova temporada zera contadores sazonais e tabela, conserva carreira, identidades, histórico e finanças; contratos vinculados são zerados pelo finalizador quando habilitado; calendário avança 21 dias no modo clássico | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:73,630; S9 apenas inicialização; T08/T09 | S9 não executou FinalizeSeasonAndAdvance; preservação de contrato no subteste não nega reset feito no finalizador | A DEFINIR |
| G08 | Força inicial e evolução anual antiga | REF §2.4 [PEND], sem esta tabela | 017B aplica referências por slot nas quatro divisões, faixas 39–50/26–38/13–25/1–12; top 3 da 1ª garante presença de 47–50. Não recalibra save na recarga. Método anual antigo existe, mas não há chamada dele em src/ | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs:10,27; PlayerStrengthEvolutionEngine.cs:14; CompetitionAndSeasonEngine.cs:406; T08 | Não deduzir execução anual pelo nome LegacySeasonal ou por existir um método antigo | A DEFINIR |
| G09 | Histórico | REF §9.1 [BIN]: eventos e telas históricas | Snapshot e History guardam temporadas de clube, vínculos, transferências, partidas e forças; marca legado parcial. Fixtures/transações transitórios antigos são podados da lista do mundo; histórico resumido e ledger relacional são estruturas separadas | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/HistoryEngine.cs:9,22,43; CompetitionAndSeasonEngine.cs:617; src/Elifoot.Infrastructure/EfWorldRepository.cs:167; T08 | Não prometer reconstrução de dados anteriores ausentes nem dizer que poda do snapshot apagou todo ledger do banco | A DEFINIR |

### H — Funcionalidades próprias do multiplayer

O original não é referência de obrigação para estes recursos; a coluna correspondente explicita isso.

| ID | Mecânica | Elifoot 98 original — fonte e evidência | Jogo atual — regra/valor | Status | Evidência atual arquivo:linha/teste | Diferença/risco | Decisão do dono |
| --- | --- | --- | --- | --- | --- | --- | --- |
| H01 | Login de teste com conta | Recurso próprio; não derivado da REF | Default AccountTest, conta e coachId de servidor persistentes, PasswordHasher, cookie HttpOnly/SameSite Strict com 12h, sessão revogável e CSRF; bloqueado em Production | CONFIRMADO_FUNCIONANDO | src/Elifoot.Api/AccountLogin.cs:22,40,47,93; Services/AccountSessionService.cs:18; T14 | Não é autenticação JWT de produção; LocalDev continua alternativa separada | A DEFINIR |
| H02 | Propriedade conta–treinador–mundo | Recurso próprio | AccountId possui perfis CoachId; mundo mantém participante com esse ID. Middleware valida ator e hub verifica propriedade; identidade não é retomada só por apelido; vínculo legado requer concessão de uso único | CONFIRMADO_FUNCIONANDO | src/Elifoot.Api/AccountLogin.cs:106,193; Services/AccountSessionService.cs:80,116; Hubs/GameRoomHub.cs:31; T14/T15 | Concessão administrativa não equivale a recuperar senha | A DEFINIR |
| H03 | Restauração de sessão e vaga | Recurso próprio | GET auth/session restaura conta; mesma identidade retoma sala cheia sem duplicar vaga. Presença conta conexões; desconectado mantém clube e lugar | CONFIRMADO_FUNCIONANDO | client/elifoot_flutter/lib/account_shell.dart:14; src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:73,106; RoomConnectionRegistry.cs:22; T15 | Estar offline não concede automaticamente modo IA nem libera a vaga | A DEFINIR |
| H04 | Lobby e início pelo host | Recurso próprio | 1–8 humanos; host configura capacidade antes de sorteio/início; escolhas manual, sorteio de 4ª ou desempregado; só host sorteia/inicia, com requisitos do lobby | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Application/WorldLobbyOrchestrator016B.cs:14,42,57,68; src/Elifoot.Domain/Catalog/WorldLobby016B.cs | Há testes existentes, mas a configuração completa de host não foi reexecutada via API nesta auditoria | A DEFINIR |
| H05 | Rodada compartilhada e consistência | Recurso próprio | Todos devem estar prontos, inclusive desempregados; nenhum humano não inicia; servidor revalida elenco e avança mesmo minuto para partidas ativas. Intervalo só aguarda humanos que participam | CONFIRMADO_FUNCIONANDO | src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:420,876,972; T11 | Locks são por processo; execução distribuída geral não foi validada | A DEFINIR |
| H06 | Persistência da partida | Recurso próprio | Cache em memória e snapshot em estados de checkpoint, a cada 15 minutos e em interações que exigem durabilidade; RoundVersion é token de concorrência; ledger tem unicidade por chave | CONFIRMADO_FUNCIONANDO | src/Elifoot.Infrastructure/EfWorldRepository.cs:105,124; ElifootDbContext.cs:109,124; T12/T14 | Reinício pode retornar ao último checkpoint; comentário no repositório não lista todos os checkpoints efetivos | A DEFINIR |
| H07 | Criador e cobertura | Recurso próprio | Seleção 1–8 países, anos disponíveis da categoria, quatro divisões e 96 distritais; modo histórico usa dados salvos. Habilitação exige Catalog013A e banco separado | CONFIRMADO_FUNCIONANDO | src/Elifoot.Infrastructure/Catalog/WorldCreatorService.cs:50,58; src/Elifoot.Api/WorldCreatorEndpoints.cs:19,54; T16/T17 | O appsettings básico não habilita catálogo; iniciador de prévia injeta a configuração | A DEFINIR |
| H08 | Catálogo real disponível na raiz | Recurso próprio; importação não prova existência de dados reais | catalogo/paises contém zero JSON de clubes nesta inspeção; há modelo/schema e .gitkeep. QA documenta dados sintéticos separados | PARCIAL | catalogo/paises/.gitkeep; catalogo/modelos/club.schema.json; docs/LOCAL_HISTORICAL_CATALOG_REPORT.md:12; contagem em seção 1 | Infraestrutura funciona com fixtures; não existe base canônica populada que autorize afirmar cobertura real atual | A DEFINIR |
| H09 | Prévia, distribuição e confirmação | Recurso próprio | Draft é do proprietário, expira; reassign cria nova prévia com elenco congelado. Confirmação conserva recibo e WorldId, salva mundo separadamente; PostgreSQL usa advisory lock | CONFIRMADO_FUNCIONANDO | src/Elifoot.Infrastructure/Catalog/WorldCreatorService.cs:331,361; T16/T17 | Prova HTTP e catálogo SQLite disponível; T17 usa mundos em memória. Caso de lock PostgreSQL foi ignorado, não aprovado | A DEFINIR |
| H10 | Fontes externas e tarefas de catálogo | Recurso próprio | Modo local desregistra fornecedores/importador/worker 016B; modo legado pode habilitar worker por país com atualização de sete dias; sincronização 013A também tem rotas sob configuração | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Api/Program.cs:48,129; WorldCatalogEndpoints016B.cs:13,69; WorldCreatorEndpoints.cs:195 | Nenhum fornecedor externo foi consultado; configuração/DTO não prova dados acessíveis | A DEFINIR |
| H11 | Aparências e responsividade | Recurso próprio; visual inspirado não prova mecânica histórica | Atual/Clássica compartilham estado; preferência por treinador; GameViewport limita largura a 1360. Uma falha conhecida em 1024 px na clássica | PARCIAL | client/elifoot_flutter/lib/presentation_widgets.dart:33; game_viewport.dart:3; test/game_viewport_test.dart:96; T18 | 476 testes passaram, mas isso não permite declarar todas as larguras aprovadas | A DEFINIR |
| H12 | Rankings de goleiros e defensores | Recurso próprio | Mínimo 20 jogos válidos na posição/categoria. Goleiros por gols sofridos/jogo; defensores por (amarelos+3×vermelhos)/jogo, depois taxas; usa histórico por competição/divisão | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/PlayerStatisticsEngine.cs:208,225,253; T19 | Ranking de defensores mede disciplina, não desarmes/defesas; histórico incompleto afeta elegibilidade | A DEFINIR |
| H13 | Artilharia, assistências e R$3M | Recurso próprio | Gols/assistências registrados em buckets; artilharia por gols e média nos empates de ordenação. Ao fechar temporada completa, cada líder empatado recebe título e seu clube ATUAL recebe R$3M; sem clube recebe zero | CONFIRMADO_FUNCIONANDO | src/Elifoot.Domain/PlayerStatisticsEngine.cs:9,219,377; T19/T20 | Prêmio não é dividido entre empatados; não se confunde com o contador de painel B07 | A DEFINIR |
| H14 | Scouting e observados | Recurso próprio | Busca/filtros de jogadores e lista pessoal persistente, validando dono da conta | CONFIRMADO_FUNCIONANDO | src/Elifoot.Api/Program.cs:559,566; src/Elifoot.Application/PlayerScoutingApplicationService.cs; T14 | Não dá acesso à lista privada de outra conta | A DEFINIR |
| H15 | Editor/importação/exportação | Recurso próprio | Rotas de editor, JSON/CSV, validação de pacotes, proveniência e montagens privadas; origem de elenco deve ser coerente | IMPLEMENTADO_SEM_VALIDACAO | src/Elifoot.Api/Program.cs:833,851,883; src/Elifoot.Domain/WorldEditorAndPackageService.cs; src/Elifoot.Domain/Catalog/CustomAssemblyPolicy.cs | Testes de roundtrip existem no TRX; não houve operação nova em arquivo/base do usuário | A DEFINIR |
| H16 | Android atual | Recurso próprio | Cliente Flutter e configuração Android presentes; fluxo nativo de transporte existe | IMPLEMENTADO_SEM_VALIDACAO | client/elifoot_flutter/android; client/elifoot_flutter/lib/api_client.dart; README.md:11 | Não houve compilação/instalação/aparelho real nesta auditoria; relatórios antigos não aprovam automaticamente esta revisão | A DEFINIR |
| H17 | Recuperação de senha/JWT de produção | Recurso próprio | Encontrados registro/login/sessão/logout/perfil/vínculo legado; não encontrado fluxo de reset de senha nem AddJwtBearer/validador JWT; variável ProductionJwtSecret é guard de inicialização | NAO_IMPLEMENTADO | src/Elifoot.Api/AccountLogin.cs:165; Program.cs:14; busca de rotas/handlers | Restauração da sessão de teste não é recuperação de senha nem autenticação de produção | A DEFINIR |

| H18 | Configuração efetiva de processos abertos | Recurso próprio | Defaults foram lidos; não foram consultados endpoints, ambientes de processos já abertos ou bancos persistentes para identificar qual configuração está rodando agora | INDETERMINADO | src/Elifoot.Api/Program.cs:12,23; src/Elifoot.Api/appsettings.json:14; recorte de execução da seção 1.4 | Código disponível e configuração de arquivo não provam o processo/serviço atualmente usado pelo jogador | A DEFINIR |

## 4. Análise transversal: camadas, execução e testes

As matrizes registram valores; esta seção descreve a cadeia que os efetiva, evitando repetir fórmulas.

| Subsistema | Regra e estado persistido | Execução efetiva e humano/IA | API/UI e cobertura | Virada de temporada e lacunas |
| --- | --- | --- | --- | --- |
| Partidas/escalação | Player, MatchFixture, listas de ativos, eventos, formações, cartões, pendências e LastSimulatedMinute no snapshot | RoomClockBackgroundService chama AdvanceClockTickAsync, que chama o motor para partidas não terminadas. IA escolhe/reage por AiManagerBrain; humano envia tática e comandos validados | Program.cs:337,384,390; main.dart/shell_actions.dart; T01–T03/T11. S1/S2 exercem métodos reais em memória | Sem prova nova de navegador/aparelho. UI e IA escolhem por critérios diferentes; 5-0-5 caracterizado, equilíbrio global não reestimado |
| Força/lesão/disciplina | ClassicForce, guardas de rodada, histórico de força, ForceInjuries, CardHistory e suspensão | Resultados da rodada completa aplicam classificação/carreira, depois evolução; dias avançam na preparação. Lesão de um episódio não acumula também a perda normal de participação daquele episódio | CompetitionAndSeasonEngine.cs:327,405; ClassicForceEvolutionEngine.cs:98,234; T10 e Discipline012CTests aprovados no TRX | Tetos acompanham posição de Liga concluída; na mudança de divisão referências antigas são limpas. Lesão pode cruzar temporada. Evolução anual antiga sem chamada de produção encontrada |
| Estatísticas/prêmios | PlayerCompetitionStatistics, AppliedPlayerStatisticsMatches, títulos, recibos e snapshots de prêmio | Captura escalação no início, reconstrói eventos cronologicamente e aplica uma vez; AwardSeason só fecha com condições válidas | PlayerStatisticsEngine.cs:69,377; PlayerRankingsEndpoints.cs; telas de ranking/história; T19/T20 | Buckets históricos permanecem por temporada/competição. Legado incompleto é sinalizado; não reconstruído por suposição. B07 é inconsistência específica, reproduzida |
| Mercado/contratos | Offers, Auctions, preços, duração, RenewedForSeason, SalaryRequests e ContractHistory no snapshot; ledger relacional | Finance obtém lock da sala, valida dono/versão/pronto, executa regra, conserva recibo e tenta salvar antes de publicar. IA compartilha execução de transferência, mas decide por orçamento próprio | FinancialCommandsApplicationService.cs:10; Program.cs:579,601,639,676,788; telas de mercado/modal salarial; T04/T05/T09/T12/T13 | Proteção sazonal expira pela comparação de temporada; contratos são zerados no fechamento habilitado. Pedidos podem continuar pendentes e precisam de tratamento de temporada; guard impede resposta de outra temporada |
| Estádio/bilheteria | Stadium, público/ingresso por MatchFixture, FinancialsSettled e transações com chave | CompleteAllMatchesAndTransitionToResults chama SettleMatchAndRoundFinancials; receita por partida, salário só de participantes, patrocínio por rodada global; obras progridem no ciclo de liquidação | Program.cs:691,770,776; coach_finance_screens.dart:380; T04/T06; S7 efetivou lançamentos | Desgaste e projetos no final da temporada. Fases usam enum numérico; validação de mínimo de ampliação diverge do texto. Não há inspeção visual nova |
| Banco/insolvência | BankLoans, histórico, ChargedMatches, UnpaidInterest, crédito, crises e OverdraftInterestRounds no snapshot; ledger separado | Mesma liquidação contabiliza jogos realmente disputados; AfterSportingRound gera pedidos, processa finanças da IA, juros de caixa e insolvência. Humano e IA usam o mesmo contrato, decisões de tomada/quitação diferem | Program.cs:755,782; FinancialCommandsApplicationService.cs:90; ClassicEconomyEngine.cs:94,130; T05/T06/T09/T12; S5/S6 | Dívida e caixa não são zerados; não há demissão financeira direta localizada. Falência pode gerar leilão de atleta elegível; elenco mínimo pode conservar crise |
| Carreira | HumanCoach/AiManager, Career, emprego, moral, convites, vagas e CoachTitles no snapshot | Captura treinador no início; aplica resultado ao responsável daquele jogo; dispensa/vaga/interino e busca de candidato na cadeia de resultados/fechamento | Program.cs:547,797,807,818,825; telas de carreira; T07 e S8 | Ranking e títulos acumulam; demissão por rebaixamento difere entre humano e IA. Férias e remoção administrativa não têm fluxo completo encontrado |
| Salas/identidade | Participante no snapshot; conta/perfil/sessão em tabelas relacionais; presença em registry de conexões | Hub valida vínculo e associa conexão; desconexão atualiza presença. Readiness usa participantes, não apenas sockets conectados; retomada conserva identidade | AccountLogin.cs:106; GameRoomHub.cs:29; Program.cs:164,209; account_shell.dart/lobby; T11/T14/T15 | Contas persistem fora do ciclo de temporada. Múltiplas instâncias e durabilidade minuto a minuto não estão provadas |
| Catálogo/montagem | Arquivos de catálogo, índice publicado, referências/hashes, drafts e montagens no banco de catálogo; mundo confirmado é snapshot independente | Seleciona/normaliza cobertura, congela preview, valida reassign e confirma com recibo. Início liga host/participantes separadamente | WorldCreatorEndpoints.cs:117–140; WorldCreatorService.cs:331,361; world_creator.dart; T16/T17 | Cadastro canônico vazio nesta raiz. Fixture sintética prova fluxo, não autenticidade/cobertura de dados reais. Lock PostgreSQL continua sem execução nova |
| Apresentação/histórico | Preferência individual em store próprio; história nos agregados do mundo | PresentationSurface muda apresentação sobre o mesmo shell; HistoryEngine observa dados confiáveis e marca recortes parciais | ProfileAppearanceEndpoints.cs; presentation_widgets.dart:33; history_views.dart; T08/T18 | Não muda regras esportivas por escolha de aparência. Falha 1024 px é conhecida; fidelidade visual ao original não foi medida |

O banco de mundos tem ef_worlds, ef_club_summaries, ef_financial_ledger, ef_season_histories e tabelas de conta; o catálogo tem schema separado e rotinas EnsureSchema. Campos de força, gols, moral, dívidas e guardas de regras vivem principalmente dentro de SnapshotJson, não em uma tabela relacional individual por atributo. Evidência: ElifootDbContext.cs:5,69,102 e EfWorldRepository.cs:124,167. Um DTO ou botão não foi tratado como prova de todos esses níveis.

## 5. Cenários reproduzíveis, resultados e alcance

Os experimentos carregaram as DLLs já existentes da revisão auditada; não chamaram repositório nem API. As instruções exatas estão em 6.8. Valores abaixo são observados, salvo a coluna de expectativa calculada indicada. O processo retornou código zero. Houve uma primeira tentativa interrompida pelo nome reservado HOME no PowerShell depois de S1; o nome foi corrigido apenas no comando em memória e a execução completa foi refeita.

| Cenário | Executado? | Ambiente/dados | Resultado observado | Esperado conforme implementação atual | Evidência | Lacunas |
| --- | --- | --- | --- | --- | --- | --- |
| S1 — sete atacantes | Sim, domínio | G+4D+3M e 7 A; todos força 50/Fair Play; A com estrela. 4-3-3. Depois A0=45/7 gols, A1=45/2, demais=50/0 | Inicial A0/A1/A2; depois A2/A3/A4. Scores dos exemplos 50.5 e 55.5. Zerar gols manteve A2/A3/A4 | Score ignora gols e privilegia cinco atletas ainda em 50 | AiManagerBrain.cs:259,326; saída S1 | Reset de gols foi aplicado em memória; não simula cinco jogos anteriores nem comprova hipótese do original |
| S2 — mando sem torcida | Sim, setores; sem Monte Carlo novo | Onze iguais F30, Fair Play, sem estrela, 4-3-3, identidade equilibrada | Ataque casa/fora=143.1/135; defesa=170.13/160.5. Fórmula de chances dá 0.03991099765/0.03614702946, razão 1.10412939 | Totais de casa ×1.06; razão de probabilidades decorre de ambos os lados da fórmula | MatchEngine.cs:110,489; S2; T01 | Probabilidades foram calculadas a partir dos setores; não são frequências empíricas de vitórias. Público não foi usado |
| S3 — comportamentos | Sim, funções reais; frequência nova não | Atacante F30, sem estrela, contrato 10, variando somente comportamento | Avaliações: 769306/740813/712321/676705/633965/584103; salário R$2343 em todos; pesos de falta 0/0.35/0.75/1.45/2.35/3.60 | Valor muda por multiplicador; salário não muda por classe | MarketAndEconomyEngine.cs:11,57; MatchEngine.cs:168; S3; T03 | Frequência de cartões não foi medida nesta execução; T03 anterior fornece prova de testes do motor, não percentuais do original |
| S4 — nacional/equiparado/estrangeiro | Não | Regra de classificação de atleta não encontrada | Não há resultado aprovado a atribuir às três classes | Seria necessário existir estado/regra de nacionalidade antes de variar apenas essa entrada | Entities.cs:131; ExecuteTransfer:818; C04 | Não simular classes fictícias por nome ou país do clube e apresentá-las como recurso existente |
| S5 — crédito e força média | Sim | Dois clubes de 4ª com 10000 assentos úteis e um atleta de força 20/40; sem dívida. Depois primeiro estádio útil=5000 | Ambos: indicador 500000, limite 2 e disponibilidade 2. Estádio reduzido: limite 1 | Média de força não é lida por AssessCredit; estádio é lido | ClassicEconomyEngine.cs:52; S5 | Elencos unitários isolam função de crédito; não são elencos válidos de partida nem caso de API |
| S6 — juros de dívida fixa | Sim | Caixa inicial R$1M; novo empréstimo R$500 mil; partida marcada disputada/90 min, objetos em memória | Caixa após tomada/um encargo=1475000; principal e saldo=500000; juros cobrados=25000. Repetir mesma partida não alterou valores | Encargo fixo por contrato/jogo; guarda impede duplicar | ClassicEconomyEngine.cs:72,94; S6; T05 | Partida sintética terminou por estado, sem simular seus 90 minutos; falta de caixa e dois contratos cobertos em T05 anterior |
| S7 — Liga/Taça/fases | Sim, cálculo e liquidação em memória | Mandante 1ª, reputações padrão, condição 100, capacidade 100000, seed 199801, S1/R1/índice1; fator de demanda 1. Liquidação adicional usa política legada de fator neutro | Público 27853 em todas. Liga e 64 avos: ingresso 24; 128 avos/oitavas/final: 30. Liga bruta 668472, toda do mandante. Final bruta 835590: mandante 626692.50, visitante 208897.50 | Sem bônus de demanda da Taça; condição ordinal dá ingresso extra; ledger divide 75/25 | MarketAndEconomyEngine.cs:70,233; S7 e S7_SETTLED | Não é toda configuração de política V2 nem toda fase/capacidade; custos foram lançados separadamente, números da coluna são receita bruta |
| S8 — demissão humana/IA | Sim, resultado sintético | Um humano e uma IA com moral de início 20; clubes equivalentes; cada um perde 0–1 para adversário equivalente | Ambos: moral 11, uma demissão, sem clube; antigo clube com interino | Mesmo gatilho vermelho de início + derrota nos dois tipos | CoachCareerEngine.cs:90,125,180; S8; T07 | Sem calendário completo, ofertas ou UI. Rebaixamento assimétrico e convites dependem das outras regras da matriz |
| S9 — virada | Parcial: inicialização nova | Mundo de 64 da fábrica; contador sazonal 7, Taça 2, carreira 19, força45 e contrato9; SeasonNumber colocado em 2 antes de InitializeSeasonFixtures | Sazonal/Taça=0/0; carreira19, força45, contrato9; calendário20 | Inicializador zera estatísticas sazonais. Contrato0 está no FINALIZADOR e não nesse método | CompetitionAndSeasonEngine.cs:61,635; S9; T08/T09 | Não se afirma ter executado FinalizeSeasonAndAdvance nem temporada completa neste cenário |
| X1 — total sazonal de um gol real | Sim, cadeia de domínio | ResolvePenaltyKick em Taça com convRoll0, um evento de gol; consulta GetPanelStatistics em seguida | Placar1, evento1, SeasonGoals1, CupGoals1, painel2, CareerGoals1 | O incremento duplo de contadores distintos é legítimo; a soma posterior provoca painel2 | MatchPenaltyResolution.cs:13; MatchEngine.cs:616; PlayerStatisticsEngine.cs:303; X1_REAL_GOAL | Não houve HTTP/tela/save. É defeito de consistência reproduzido nas funções atuais, não decisão de equiparar ao original |

Os nomes curtos de arquivo nesta tabela se referem a src/Elifoot.Domain/, já identificado nos IDs correspondentes da matriz; as duas rotinas principais de X1 estão explicitamente ligadas acima. Nenhum resultado de S1–S9 representa teste do executável original.

## 6. Resumo executivo para decisão do proprietário

### 6.1 O que funciona hoje, com provas

- O motor executa eventos e setores com RNG determinístico, disciplina e regras de alteração de escalação; T01–T03 e T11 têm resultados aprovados. S1/S2 confirmaram seleção e mando em funções reais.
- Contratos, juros fixos, proteção contra liquidação repetida, decisões salariais e controle de posse têm testes aprovados T05/T09/T12/T13. S5/S6 executaram crédito/juros.
- Contas de teste, retomada sem duplicar vaga, revogação e isolamento de propriedade foram executados pelos testes T14/T15; essa prova é de ambiente local de integração.
- Criação histórica multiano e prévia imutável funcionam com dados sintéticos nos testes T16/T17. Isso não valida uma base real populada.
- O ranking de jogadores possui elegibilidade de 20 jogos e recortes históricos testados; o prêmio de artilharia possui guardas de fechamento e repetição, T19/T20.
- O código compila e há resultados anteriores de 538 testes de domínio, 31 de aplicação e 16 integrações selecionadas aprovados. Esses totais não anulam os defeitos/limitações específicos abaixo.

### 6.2 O que existe, mas não foi verificado no alcance completo

- Distribuição de placares, equilíbrio de todas as táticas e frequência de lesões/cartões na revisão atual em nova amostra controlada.
- Fluxo completo de estádio, todos os prêmios de fechamento e finanças de save legado.
- Publicação de draft com duas APIs e lock PostgreSQL; PostgreSQL não foi executado nesta auditoria.
- Fontes externas, cronograma efetivo de worker em processo aberto, integridade de dados históricos reais e qualidade de importações atuais.
- Android em aparelho, build Web atual em navegador real e fidelidade visual das duas apresentações.

### 6.3 O que está parcial ou ausente

- Catálogo canônico sem JSON de clubes: modelo e infraestrutura presentes, conteúdo real ainda ausente neste diretório.
- Critérios de autoescalação diferentes entre botão da UI e IA/padrão do servidor.
- Total sazonal do painel duplica o componente de Taça no cenário X1; o ranking por buckets é um caminho distinto.
- Classificação ordinal de fase dá aumento de ingresso aos 128 avos e não aos 64 avos.
- Overflow clássico de 48 px em 1024 px, reproduzido no teste isolado anterior.
- Modo automático tem propriedade, execução e setter de serviço, mas não foi encontrada rota/UI que torne o setter acessível; férias não tem ciclo próprio completo.
- Ausentes: classificação nacional/equiparado/estrangeiro, suas sobretaxas/quotas, índices econômicos históricos I/5B/79, B/D derivados do nome, remoção administrativa de participante, recuperação de senha e handler JWT de produção.

### 6.4 Diferenças reais em relação ao original

A evidência comparativa mais firme no material fornecido é estática [BIN], ainda sujeita aos limites declarados pelo próprio estudo. Nesse nível, o projeto usa avaliação convexa própria com modificador explícito de estrela/contrato; crédito por estádio e divisão em número de contratos; estádio por assentos livres; demanda com parâmetros diretos por divisão; ingresso sem média de grupo/inflação; e Taça 75/25, em vez de 50/50. O encargo clássico de R$25000 é 5% de seu principal fixo, mas não implementa a fórmula geral de 5% sobre qualquer saldo de dívida variável.

Pelo guia histórico [EXT], os pontos de treinador por mando diferem da pontuação atual por divisão/fase. Pelo estudo externo do motor [EXT], os filtros de mando e contribuições por nacionalidade não correspondem à fórmula setorial atual.

O caso dos sete atacantes tem divergência de comportamento observado [OBS], mas a explicação força + gols continua [HIP]. Asterisco na finalização, evolução, gatilhos de demissão e convites permanecem **referência inconclusiva** quanto à regra exata do original. Não se recomenda tratá-los como obrigação de fidelidade por este relatório.

### 6.5 Contradições e inconsistências concretas

| ID | Achado | Evidência e alcance |
| --- | --- | --- |
| X01 | Um gol de Taça vira dois no total sazonal do painel | MatchEngine.cs:616 incrementa SeasonGoals e CupGoals; PlayerStatisticsEngine.cs:303 soma ambos; ClubAndMarketApplicationService.cs:124 expõe o valor do painel. X1_REAL_GOAL reproduz 1 evento/1 gol/2 no painel |
| X02 | Regra ordinal de ingresso não segue avanço esportivo | Enums.cs:73,77 e MarketAndEconomyEngine.cs:93; S7 observou R$30 nos 128 avos, R$24 nos 64 avos, R$30 nas oitavas |
| X03 | UI e servidor divergem no significado de automático | shell_actions.dart:747 ordena força/ID; AiManagerBrain.cs:326 usa estrela/comportamento; não é soma força+gols em nenhum dos dois |
| X04 | Mensagem de mínimo de ampliação não é a condição efetiva | MarketAndEconomyEngine.cs:499 verifica apenas adicional>0 e ≤30000; mensagem em :500 informa 1000–30000. Sem nova operação de API |
| X05 | Documento de patrocínio está desatualizado para a política V2 | docs/ECONOMIA_ESTADIOS.md:41 chama valores de receita por rodada incluindo 22 rodadas; OperatingEconomyEngine.cs:60 paga orçamento anual parcelado e calendário novo de128 tem21 |
| X06 | Documento de obra descreve prazo anterior | docs/ECONOMIA_ESTADIOS.md:37 fala três rodadas; MarketAndEconomyEngine.cs:492 fixa uma |
| X07 | README descreve default de autenticação anterior | README.md:134 diz LocalDev; src/Elifoot.Api/appsettings.json:14 diz AccountTest; AccountLogin.cs:40 define ambiente de teste |
| X08 | Comentário de ranking cita desempate inexistente | CompetitionAndSeasonEngine.cs:727 cita TotalWins; linha732 usa CoachId |
| X09 | Aparência clássica tem falha específica de largura | test/game_viewport_test.dart:96 e artifacts/main-sync-20261109-192940/flutter-viewport-recheck.log:7; demais testes aprovados não são prova dessa largura |
| X10 | Existência de código de modo automático é mais ampla que acessibilidade do produto | SetCoachAutoModeByHostAsync em ClubAndMarketApplicationService.cs:472 só apareceu na definição; testes alternam IsAutoMode diretamente. Ausência fundamentada de chamada em API/UI na busca realizada |

Nenhuma dessas inconsistências foi corrigida. X01/X02 são reproduções de domínio; X04 é incompatibilidade estática entre condição e mensagem; X09 tem resultado de teste existente. As categorias de prova não foram misturadas.

### 6.6 Decisões de produto pendentes, por impacto

As opções em cada linha são alternativas de avaliação, sem escolha feita pelo auditor. “Aproximar” só é tecnicamente bem definido quando a referência tem evidência suficiente.

| Prioridade/impacto | Decisão | Consequência a esclarecer | Opções | Escolha |
| --- | --- | --- | --- | --- |
| 1 — motor/justiça | Critério comum de autoescalação | Força pura, score atual da IA ou política que considere desempenho | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 2 — motor/justiça | Hipótese força+gols | Se entra apenas na seleção e qual contador, sem elevar chute automaticamente | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 3 — motor/justiça | Papel do asterisco | Separar seleção, setor, autoria do chute, conversão, valor e salário | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 4 — motor/justiça | Mando | Definir alvo medível de vantagem sem confundir setor, chance e vitória | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 5 — motor/justiça | 5-0-5 e formações livres | Manter penalidade/transição e decidir escopo de combinações válidas | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 6 — motor/justiça | Evolução e lesão | Queda pós-Liga, recuperação por teto, calendário e efeitos da Taça | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 7 — motor/justiça | Nacionalidade esportiva | Definir se haverá atributo individual e algum efeito no setor | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 8 — economia | Avaliação e salário | Escolher curva/escala e dependência de estrela, posição e contrato | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 9 — economia | Inflação e indicadores | Distinguir eventual índice monetário, de clube e de competição | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 10 — economia | Nacionalidade na compra | Decidir classes, sobretaxas, equiparação e limites de estrangeiros | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 11 — economia | Crédito | Quantidade de contratos por estádio/divisão versus teto monetário por elenco/indicador | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 12 — economia | Juros e amortização | Principal fixo, saldo variável, atraso, quitação e caixa negativo separados | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 13 — economia | Público e preço | Separar parâmetros diretos de divisão, elencos, capacidade e fase | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 14 — economia | Bilheteria da Taça | 75/25 atual ou outra repartição; custos tratados independentemente | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 15 — economia | Contrato e aumento | Prazo sazonal, luvas, cap, cadência, recusa e exceção de elenco mínimo | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 16 — economia | Leilões | Default versus piso de lance, duração e diferença entre motivos de leilão | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 17 — economia | Patrocínio/premiação | Orçamento anual e R$3M por artilheiro empatado; efeito no equilíbrio de longo prazo | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 18 — carreira | Pontuação e ranking | Pontos por divisão/fase ou mando; desempates; fases novas da Taça | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 19 — carreira | Demissão/convite/sucessão | Moral capturada, rebaixamento assimétrico e influência de projeto/finanças | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 20 — multiplayer | Desconexão, férias e remoção | Separar presença, vaga, comando temporário, retorno e remoção administrativa | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 21 — multiplayer | Durabilidade e hospedagem | Checkpoints, concorrência entre processos e escopo da autenticação de teste | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 22 — experiência/dados | Catálogo e identidades históricas | Cadastro canônico, proveniência, seleção multiano e força simulada | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |
| 23 — experiência | Estatísticas e apresentação | Definição única de gol sazonal, ranking de defensor e larguras suportadas | MANTER ATUAL / APROXIMAR DO ORIGINAL / HÍBRIDO / NOVA REGRA / ADIAR | A DEFINIR |

### 6.7 Prioridades de teste antes de mudar regras

| Prioridade | Natureza | Verificação necessária |
| --- | --- | --- |
| Alta | Defeito de consistência reproduzido | Um gol real de Taça, conclusão, API de clube/histórico e duas aparências: comparar painel, buckets, artilharia, carreira e reset sazonal |
| Alta | Condição ordinal reproduzida | Todas as fases 1024→final com mesmos clubes, seed, estádio e política de demanda; conferir preço e repartição no ledger |
| Alta | Defeito conhecido de layout | Repetir 1024 px clássico com fontes reais e principais telas; manter distinção entre widget test e navegador |
| Alta | Lacuna de justiça | Mesmo elenco, gols variados e empates de força/estrela/comportamento: comparar botão automático, padrão do servidor e IA antes/depois da temporada |
| Alta | Lacuna de calibração | Amostra determinística com times espelhados e mando trocado, vários seeds e formações; medir chances, gols e vitórias separadamente |
| Alta | Lacuna de persistência | Reinício antes/depois de checkpoint, decisão de pênalti/salário e liquidação; confirmar idempotência e retomada sem alterar dados reais |
| Alta | Lacuna de integração | Executar os dois testes PostgreSQL hoje ignorados em bancos descartáveis; não usar a contagem de aprovados SQLite como substituto |
| Média | Limite estático divergente | Ampliação 0/1/999/1000/30000/30001 via comando/API e mensagem; nenhuma mudança de regra prescrita |
| Média | Lacuna financeira | Mesma divisão com estádio/força/caixa/dívida variados, incluindo juros atrasados, legado, quitação e 2% de caixa negativo |
| Média | Lacuna de carreira | Campanhas equivalentes humano/IA, moral antes/depois, rebaixamento em cada divisão, demissão, sucessor e convites com expiração |
| Média | Lacuna de fechamento | Temporada real completa, gols/assistências/cartões, títulos empatados, prêmios, contratos, dívidas, força e vínculos após reload |
| Média | Lacuna de dados | Catálogo canônico real autorizado, variantes multiano, duplicidades, cobertura e congelamento da prévia; não confundir fixture de QA com cadastro |
| Média | Lacuna de plataforma | Android real e navegador atual com sessão/cookies, SignalR e reconexão; evidências antigas permanecem históricas |

Para questões [HIP]/[PEND] do original, esses testes caracterizam primeiro o projeto atual. Confirmar fidelidade histórica exigiria investigação separada do binário/saves; não há conclusão de que a escolha de produto deva aguardá-la ou reproduzi-la.

### 6.8 Índice de evidências, comandos e reprodução

#### Testes consultados e seus resultados

**D** = artifacts/main-sync-20261109-192940/test-results/domain.trx; **A** = application.trx no mesmo diretório; **I** = integration-final.trx no mesmo diretório. A referência é ao atributo testName e outcome do UnitTestResult, não apenas à declaração do teste. Os resultados foram vinculados aos símbolos; a leitura detalhada dos corpos e chamadas concentrou-se nos cenários citados. A identificação de um teste não estende sua cobertura a camadas que ele não exercita. Onde há teorias, os casos presentes no TRX estavam aprovados.

| ID | Arquivo:linha e símbolo | Resultado/prova disponível |
| --- | --- | --- |
| T01 | tests/Elifoot.Domain.Tests/DomainEngineAndRulesTests.cs:48 — MatchEngine_Monotonicity_StrongerDefense_StrongerGk_StarBonus_RedCard_AndBehaviorDiscipline | Passed em D; monotonicidade e efeitos controlados |
| T02 | tests/Elifoot.Domain.Tests/DomainEngineAndRulesTests.cs:431 — MatchEngine_Characterization_All10Formations_DefensiveFormula_DeterministicReplay_AndBugRegressions | Passed em D; fórmulas/formações/replay |
| T03 | tests/Elifoot.Domain.Tests/DomainEngineAndRulesTests.cs:94 — Discipline_Audit006_SingleCardPerFoul_SecondYellowBugFix_SentOffIsolation_AndFairPlayZeroFouls | Passed em D; complementado por Discipline012CTests no mesmo TRX |
| T04 | tests/Elifoot.Domain.Tests/DomainEngineAndRulesTests.cs:382 — MarketAndStadiumEconomy_FairValueConvex_Wear1To6Percent_AndBilateralTransfers | Passed em D; corpo inspecionado testa FairValue e desgaste. Apesar do nome, não executa transferência bilateral; para isso, ver T21. AiMarket_DoesNotBuyStrength11ForTwoMillion também está Passed em D |
| T05 | tests/Elifoot.Domain.Tests/ClassicEconomy012DTests.cs:69,192,225 — UnpaidInterestIsRealDebtWithoutCompoundInterestOrGiftedCash; EconomicReplaySurvivesSerializationWithoutDuplicateSalaryOrInterest; EveryActivePrincipalHasItsOwnFixedMatchInterest | Passed em D; casos adicionais de juros/recusa/renovação no mesmo arquivo |
| T06 | tests/Elifoot.Domain.Tests/OperatingEconomy012DFixTests.cs:55 — LowerGateDemandChangesOnlyRealAttendanceRevenueAndVariableCost; método :24 — SeasonWithoutGamesPaysOneAnnualCommercialBudgetAndNoUncontractedMatchWages | Passed em D; não equivalem a novo benchmark de 100 temporadas |
| T07 | tests/Elifoot.Domain.Tests/CoachCareer008Tests.cs:38,156,199 — MoraleBandsAndPreMatchRedDismissal; OnlyGreenHumanReceivesOpportunity; DistrictRelegationDismissesRegardlessOfMorale_AndPromotedClubIsEligible | Casos correspondentes Passed em D |
| T08 | tests/Elifoot.Domain.Tests/History017CTests.cs:68 — ClosingASeasonStoresEveryDivisionBeforeMovementWithoutInventingAnUnplayedCupTitle; InitialSquadStrength017BTests.cs:47 — QuickCreationUsesSameRulesAndKeepsHistoricFactoryProfileAvailable | Passed em D; provas de domínio, não temporada visual |
| T09 | tests/Elifoot.Domain.Tests/SeasonContractsAndDebtTests.cs:89 — NegativeCashInterestAndBankInterestAreIndependent; :10 — RenewalKeepsOwnershipAndWageAndBlocksSalesUntilSeasonChanges; tests/Elifoot.Domain.Tests/VoluntaryAuctionTests.cs:60 — ResultsCreationStartsAtNextRoundAndCrossesSeasonWithoutEarlyClosure | Passed em D; a renovação é exercitada no domínio; o teste muda SeasonNumber diretamente para conferir expiração do bloqueio |
| T10 | tests/Elifoot.Domain.Tests/ClassicForce012CTests.cs:150 — EveryCupStagePreservesStrengthForStartersUsedAndUnusedBenchAndEliminatedClubs; método :347 — OfficialRoundWaitsForAllResultsAndCapturesBothHalvesBeforePromotion | Casos correspondentes Passed em D |
| T11 | tests/Elifoot.Application.Tests/MultiplayerSynchronizationAndPauseTests.cs:106,341 — GlobalPause_8Humans_Minute37Freeze_Quotas5And3_ExtraTime6And4_AndRoomIsolation; Multiplayer_100ConsecutiveRounds_8Coaches_SyncCounter_Pauses_Substitutions_Reconnection_AndRoomIsolation | Passed em A; ambiente de testes da aplicação |
| T12 | tests/Elifoot.Application.Tests/FinancialCommandDurability012DTests.cs:10 — RetryAfterFailedSavePersistsTheOriginalLoanWithoutCreditingItAgain | Passed em A; guarda e tentativa de durabilidade |
| T13 | tests/Elifoot.Integration.Tests/AccountLoginTests.cs:21 — SalaryDecisionIsOwnedPersistentAndConcurrentTabsProduceOnlyOneAuction | Passed em I |
| T14 | tests/Elifoot.Integration.Tests/AccountLoginTests.cs:85,213 — RegistrationHashesPasswordsAssignsServerIdsAndSurvivesRestart; SignalRChecksAccountOwnershipSharesOneSeatAndLogoutRevokesTheLiveConnection | Passed em I; proteção de lista pessoal também Passed em I |
| T15 | tests/Elifoot.Integration.Tests/AccountLoginTests.cs:145 — SameAccountOnTwoDevicesResumesFullRoomWithoutDuplicatingSeatsAndRejectsAnotherAccount | Passed em I; testes de vínculo legado/CSRF também Passed em I |
| T16 | tests/Elifoot.Integration.Tests/WorldCreatorReviewTests.cs:69 — HttpReassignIsOwnedImmutableAndKeepsTheExactFrozenRosterBeforeConfirmation | Passed em I; lock PostgreSQL da mesma classe NotExecuted |
| T17 | tests/Elifoot.Integration.Tests/LocalHistoricalWorldTests.cs:17 — LocalApiDiscoversCoverageAndConfirmsAnIndependentMultiYearSnapshot | Passed em I; dataset sintético. Reindexação PostgreSQL NotExecuted |
| T18 | client/elifoot_flutter/test/game_viewport_test.dart:96 — Game stays centered through desktop and phone sizes (classic=true) | Failed em flutter-tests.log e flutter-viewport-recheck.log; 48 px à direita, viewport1024 |
| T19 | tests/Elifoot.Domain.Tests/PlayerRankings009Tests.cs:400,511 — Eligibility_ChangesExactlyAt20Matches; Defenders_DisciplineScorePrecedesRedRate_AndUsesActualCardCounts | Casos correspondentes Passed em D |
| T20 | tests/Elifoot.Domain.Tests/PlayerRankings009Tests.cs:971 — RepeatedSeasonClose_IsIdempotentAcrossJsonRestart; métodos :1039 — AwardSnapshots_RemainImmutableAfterTransfersRenamingContractExpiryAndNewSeason e :996 — AwardLedgerGuard_PreventsDuplicateCashWhenAwardRecordNeedsRecovery | Passed em D; prêmio e repetição testados sem nova tela nesta tarefa |

| T21 | tests/Elifoot.Domain.Tests/History017CTests.cs:37 — TransferIdentityAndCareerSurviveMultipleClubsAndSnapshotRestart | Passed em D; chama ExecuteTransfer duas vezes, observa histórico e recarrega JSON; não é mera alteração manual de ClubId |

Não foi atribuído resultado a teste inexistente de empate de artilharia com um nome presumido. O pagamento de R$3M por cada líder empatado vem da implementação AwardSeason e dos demais testes efetivamente consultados.

A revisão do corpo de T04 é um exemplo concreto de por que nomes de testes não bastam: ele menciona transferências bilaterais, porém só chama avaliação e desgaste. Da mesma forma, T08/History017CTests.cs:68 chama HistoryEngine.CloseSeason em cenário sintético, sem percorrer uma temporada real; T20/:1039 muda ClubId e SeasonNumber diretamente para provar imutabilidade, não uma transferência real ou o finalizador completo. Esses alcances foram mantidos separados de T21, que chama ExecuteTransfer.

#### Comandos e resultados desta auditoria

As leituras repetidas foram agrupadas por família para não reproduzir dezenas de blocos idênticos. Os comandos abaixo não incluem ações Git da tarefa anterior; eles não foram repetidos nesta tarefa.

| Comando/família executada | Resultado |
| --- | --- |
| Get-Content -LiteralPath do Texto colado.txt anexado | Pedido lido integralmente |
| Get-Content da REF em trechos 1–140, 141–420 e 421–805; Get-FileHash -Algorithm SHA256 | Referência integral lida e hash registrado na seção1 |
| git status --short --branch --untracked-files=no; git status --porcelain=v1 --untracked-files=no | Branch main e nenhum arquivo versionado alterado antes/depois dos experimentos |
| git log -1 --format='%H %ad %s' --date=iso-strict | Commit registrado na abertura |
| git ls-files --others --exclude-standard, agrupado por pasta | Inventário dos arquivos preexistentes da seção1 |
| rg --files src, testes, cliente e ferramentas, excluindo bin/obj/build/.dart_tool | Descoberta da estrutura; caminhos não foram presumidos como existentes |
| Get-Content com numeração de linhas dos arquivos citados nas matrizes | Inspeção dos métodos, defaults, chamadas e testes relevantes |
| rg -n de símbolos e temas: cálculo, seleção, gols, juros, nacionalidade, inflação, férias, remoção, JWT, AutoMode, endpoints, workers e migrations | Resultados descritos nas matrizes; buscas sem ocorrência não foram usadas isoladamente para declarar ausência |
| git ls-files '*Migrations*' '*Migration*' | Nenhum arquivo correspondente; schema concreto identificado pelas rotinas SQL/EnsureCreated |
| git ls-files -- catalogo; Get-ChildItem em catalogo/paises com filtro JSON | Modelos/.gitkeep versionados; zero JSON de clubes |
| Leitura XML dos três TRX e filtros por testName/outcome | Totais e testes T01–T21 confrontados; dois casos PostgreSQL sem execução |
| Leitura de dotnet-build.log, flutter-tests.log, flutter-viewport-recheck.log e documentos históricos citados | Build aprovado; falha de layout preservada como falha; relatórios antigos identificados por checkpoint |
| Dois comandos PowerShell abaixo, carregando Contracts/Domain Release com Add-Type -Path | Execução completa com código zero; resultados S/X na seção5; sem repositório/banco/serviço |
| Primeira tentativa do primeiro comando com variável HOME | Interrompida após S1 por variável reservada; corrigida apenas no texto do comando, sem arquivo criado |

Os primeiros levantamentos também procuraram nomes hipotéticos como ForceMath.cs, MatchSimulator.cs e TacticalEngine.cs; não existiam. A auditoria prosseguiu a partir de rg --files e dos arquivos concretos encontrados, sem atribuir implementação a esses nomes.

#### Reprodução dos cenários em memória

Pré-condição: executar da raiz D:/elifoot_98, usando as DLLs Release da revisão auditada já compiladas. Estes comandos não recompilam, não salvam objetos e não acessam dados persistentes. Os GUIDs aleatórios identificam apenas objetos novos; os resultados comparados não dependem de seus valores, salvo empates que preservam a ordem de inserção explicitada em S1. O primeiro X1 do comando principal é uma fixture de contadores; o segundo comando fortalece a reprodução ao produzir um gol pela rotina real do motor.

~~~powershell
$ErrorActionPreference='Stop'
Add-Type -Path (Resolve-Path 'src/Elifoot.Contracts/bin/Release/net10.0/Elifoot.Contracts.dll')
Add-Type -Path (Resolve-Path 'src/Elifoot.Domain/bin/Release/net10.0/Elifoot.Domain.dll')
$pool=[System.Collections.Generic.List[Elifoot.Domain.Player]]::new()
foreach($pos in @('G','D','D','D','D','M','M','M')) { $p=[Elifoot.Domain.Player]::new(); $p.Position=[Elifoot.Contracts.PlayerPosition]$pos; $p.Strength=50; $p.Behavior='FairPlay'; $pool.Add($p) }
for($i=0;$i -lt 7;$i++){ $p=[Elifoot.Domain.Player]::new(); $p.Name="A$i"; $p.Position='A'; $p.IsStar=$true; $p.Behavior='FairPlay'; $p.Strength=50; $pool.Add($p) }
$attackers=@($pool | Where-Object Position -eq 'A')
$spec=[Elifoot.Domain.FormationSpec]::new('4-3-3',4,3,3,$false)
$initial=[Elifoot.Domain.AiManagerBrain]::BuildStartersAndBench($pool,$spec)
$attackers[0].Strength=45; $attackers[0].SeasonGoals=7
$attackers[1].Strength=45; $attackers[1].SeasonGoals=2
$later=[Elifoot.Domain.AiManagerBrain]::BuildStartersAndBench($pool,$spec)
Write-Output ('S1 initial='+(($attackers|Where-Object {$initial.Item1.Contains($_.Id)}).Name -join ',')+' later='+(($attackers|Where-Object {$later.Item1.Contains($_.Id)}).Name -join ',')+' scores='+[Elifoot.Domain.AiManagerBrain]::GetSelectionScore($attackers[0])+','+[Elifoot.Domain.AiManagerBrain]::GetSelectionScore($attackers[2]))
foreach($p in $attackers){$p.SeasonGoals=0}
$after=[Elifoot.Domain.AiManagerBrain]::BuildStartersAndBench($pool,$spec)
Write-Output ('S1 after_goal_reset='+(($attackers|Where-Object {$after.Item1.Contains($_.Id)}).Name -join ','))
$eleven=[System.Collections.Generic.List[Elifoot.Domain.Player]]::new()
foreach($pos in @('G','D','D','D','D','M','M','M','A','A','A')){ $p=[Elifoot.Domain.Player]::new();$p.Position=[Elifoot.Contracts.PlayerPosition]$pos;$p.Strength=30;$p.Behavior='FairPlay';$eleven.Add($p) }
$homeBreakdown=[Elifoot.Domain.MatchSimulationEngine]::ComputeSectorBreakdown('4-3-3',$eleven,$true,[Elifoot.Contracts.ManagerTacticalIdentity]::Equilibrado,$true)
$awayBreakdown=[Elifoot.Domain.MatchSimulationEngine]::ComputeSectorBreakdown('4-3-3',$eleven,$false,[Elifoot.Contracts.ManagerTacticalIdentity]::Equilibrado,$true)
$ph=.044*[math]::Pow($homeBreakdown.EffectiveAttackTotal/$awayBreakdown.EffectiveDefenseTotal,.85)
$pa=.044*[math]::Pow($awayBreakdown.EffectiveAttackTotal/$homeBreakdown.EffectiveDefenseTotal,.85)
Write-Output ("S2 attack_home=$($homeBreakdown.EffectiveAttackTotal) attack_away=$($awayBreakdown.EffectiveAttackTotal) defense_home=$($homeBreakdown.EffectiveDefenseTotal) defense_away=$($awayBreakdown.EffectiveDefenseTotal) chance_formula_home=$ph chance_formula_away=$pa ratio="+($ph/$pa))
foreach($behavior in [enum]::GetValues([Elifoot.Contracts.PlayerBehavior])){ $p=[Elifoot.Domain.Player]::new();$p.Position='A';$p.Strength=30;$p.ContractRoundsRemaining=10;$p.Behavior=$behavior;Write-Output ("S3 $behavior value="+[Elifoot.Domain.MarketAndEconomyEngine]::CalculateFairValue($p)+" salary="+[Elifoot.Domain.MarketAndEconomyEngine]::CalculateFairSalaryPerRound($p)+" foul_weight="+[Elifoot.Domain.MatchSimulationEngine]::GetBehaviorFoulWeight($behavior)) }
$w=[Elifoot.Domain.GameWorld]::new()
foreach($strength in @(20,40)){ $c=[Elifoot.Domain.Club]::new();$c.Name="F$strength";$c.DivisionTier='QuartaDivisao';$c.Stadium.BuiltCapacity=10000;$c.Stadium.UsableCapacity=10000;$w.Clubs.Add($c);$p=[Elifoot.Domain.Player]::new();$p.ClubId=$c.Id;$p.Strength=$strength;$w.Players.Add($p);$credit=[Elifoot.Domain.ClassicEconomyEngine]::AssessCredit($w,$c);Write-Output ("S5 strength=$strength asset=$($credit.AssetIndicator) loans=$($credit.LoanLimit) available=$($credit.AvailableLoans)") }
$c=$w.Clubs[0];$c.Stadium.UsableCapacity=5000;$credit=[Elifoot.Domain.ClassicEconomyEngine]::AssessCredit($w,$c);Write-Output ("S5 same_strength_smaller_stadium loans=$($credit.LoanLimit)")
$bank=[Elifoot.Domain.GameWorld]::new();$borrower=[Elifoot.Domain.Club]::new();$borrower.CashBalance=1000000;$bank.Clubs.Add($borrower)
$borrow=[Elifoot.Domain.ClassicEconomyEngine]::TakeClassicLoan($bank,$borrower,'audit-memory-only')
$m=[Elifoot.Domain.MatchFixture]::new();$m.HomeClubId=$borrower.Id;$m.SeasonNumber=1;$m.RoundNumber=1;$m.IsFinished=$true;$m.CurrentMinute=90
$batch=[System.Collections.Generic.List[Elifoot.Domain.MatchFixture]]::new();$batch.Add($m)
[Elifoot.Domain.ClassicEconomyEngine]::SettleLoans($bank,$batch)
Write-Output ("S6 success=$($borrow.Item1) cash=$($borrower.CashBalance) principal=$($bank.BankLoans[0].OutstandingPrincipal) balance=$($bank.BankLoans[0].RemainingBalance) charged=$($bank.BankLoans[0].InterestCharged)")
[Elifoot.Domain.ClassicEconomyEngine]::SettleLoans($bank,$batch)
Write-Output ("S6 replay_cash=$($borrower.CashBalance) replay_charged=$($bank.BankLoans[0].InterestCharged)")
$homeClub=[Elifoot.Domain.Club]::new();$awayClub=[Elifoot.Domain.Club]::new();$homeClub.Stadium.BuiltCapacity=100000;$homeClub.Stadium.UsableCapacity=100000;$homeClub.Stadium.ConditionPercent=100
foreach($phase in @('League','CupRoundOf128','CupRoundOf64','CupRoundOf16','CupFinal')){$g=[Elifoot.Domain.MarketAndEconomyEngine]::CalculateMatchGateEconomics($homeClub,$awayClub,[Elifoot.Contracts.CompetitionType]$phase,199801,1,1,1,1);Write-Output ("S7 phase=$phase ticket=$($g.Item1) crowd=$($g.Item2) revenue=$($g.Item3)")}
$career=[Elifoot.Domain.GameWorld]::new();$career.CareerRulesVersion=8
$human=[Elifoot.Domain.HumanCoach]::new();$human.Career.Morale=20;$career.HumanCoaches.Add($human)
$ai=[Elifoot.Domain.AiManager]::new();$ai.Career.Morale=20;$career.AiManagers.Add($ai)
$opponent=[Elifoot.Domain.Club]::new();$career.Clubs.Add($opponent)
foreach($person in @($human,$ai)){ $club=[Elifoot.Domain.Club]::new();$career.Clubs.Add($club);$person.CurrentClubId=$club.Id;if($person -is [Elifoot.Domain.HumanCoach]){$club.HumanCoachId=$person.Id}else{$club.AiManagerId=$person.Id};$match=[Elifoot.Domain.MatchFixture]::new();$match.HomeClubId=$club.Id;$match.AwayClubId=$opponent.Id;$match.CompetitionType='League';$match.AwayGoals=1;[Elifoot.Domain.CoachCareerEngine]::ApplyMatchCareer($career,$match,$club,$opponent);Write-Output ("S8 type=$($person.GetType().Name) morale=$($person.Career.Morale) dismissals=$($person.Dismissals) current_club=$($person.CurrentClubId) provisional=$($club.HasProvisionalManager)") }
$season=[Elifoot.Domain.WorldSeedFactory]::CreateSeededWorld('Audit in memory',199801,64,1,200,60,$true,'AUDITMEM',[Elifoot.Contracts.ForceEvolutionMode]::LegacySeasonal,$false)
$player=$season.Players[0];$player.SeasonGoals=7;$player.CupGoals=2;$player.CareerGoals=19;$player.Strength=45;$player.ContractRoundsRemaining=9;$season.SeasonNumber=2
[Elifoot.Domain.CompetitionAndSeasonEngine]::InitializeSeasonFixtures($season)
Write-Output ("S9 initialization_only season=$($season.SeasonNumber) seasonal_goals=$($player.SeasonGoals) cup_goals=$($player.CupGoals) career_goals=$($player.CareerGoals) strength=$($player.Strength) contract=$($player.ContractRoundsRemaining) rounds=$($season.TotalRoundsInSeason)")
$statWorld=[Elifoot.Domain.GameWorld]::new();$statWorld.PlayerStatisticsVersion=9;$statWorld.DisciplineRulesVersion=1
$scorer=[Elifoot.Domain.Player]::new();$scorer.SeasonGoals=1;$scorer.CupGoals=1;$scorer.CareerGoals=1;$statWorld.Players.Add($scorer)
$panel=[Elifoot.Domain.PlayerStatisticsEngine]::GetPanelStatistics($statWorld,$scorer,$null)
Write-Output ("X1 synthetic_one_cup_goal season_counter=$($scorer.SeasonGoals) cup_counter=$($scorer.CupGoals) panel=$($panel.SeasonGoals) career=$($panel.CareerGoals)")
git status --porcelain=v1 --untracked-files=no
~~~

~~~powershell
$ErrorActionPreference='Stop'
Add-Type -Path (Resolve-Path 'src/Elifoot.Contracts/bin/Release/net10.0/Elifoot.Contracts.dll')
Add-Type -Path (Resolve-Path 'src/Elifoot.Domain/bin/Release/net10.0/Elifoot.Domain.dll')
foreach($phase in @('League','CupFinal')){
 $gateWorld=[Elifoot.Domain.GameWorld]::new()
 $hostClub=[Elifoot.Domain.Club]::new();$guestClub=[Elifoot.Domain.Club]::new()
 $hostClub.Stadium.BuiltCapacity=100000;$hostClub.Stadium.UsableCapacity=100000;$hostClub.Stadium.ConditionPercent=100
 $gateWorld.Clubs.Add($hostClub);$gateWorld.Clubs.Add($guestClub)
 $fixture=[Elifoot.Domain.MatchFixture]::new();$fixture.GameWorldId=$gateWorld.Id;$fixture.HomeClubId=$hostClub.Id;$fixture.AwayClubId=$guestClub.Id
 $fixture.SeasonNumber=1;$fixture.RoundNumber=1;$fixture.DeterministicMatchIndex=1;$fixture.CompetitionType=[Elifoot.Contracts.CompetitionType]$phase;$fixture.IsFinished=$true;$fixture.CurrentMinute=90
 $gateWorld.Fixtures.Add($fixture)
 [Elifoot.Domain.MarketAndEconomyEngine]::SettleMatchAndRoundFinancials($gateWorld,$gateWorld.Fixtures)
 $hostGate=($gateWorld.Transactions|Where-Object {$_.ClubId -eq $hostClub.Id -and $_.Category -eq 'TicketRevenue'}|Measure-Object Amount -Sum).Sum
 $guestGate=($gateWorld.Transactions|Where-Object {$_.ClubId -eq $guestClub.Id -and $_.Category -eq 'TicketRevenue'}|Measure-Object Amount -Sum).Sum
 Write-Output ("S7_SETTLED phase=$phase revenue=$($fixture.TicketRevenue) home=$hostGate away=$guestGate")
}
$goalWorld=[Elifoot.Domain.GameWorld]::new();$goalClub=[Elifoot.Domain.Club]::new();$goalWorld.Clubs.Add($goalClub)
$scorer=[Elifoot.Domain.Player]::new();$scorer.ClubId=$goalClub.Id;$scorer.Position='A';$goalWorld.Players.Add($scorer)
$cupMatch=[Elifoot.Domain.MatchFixture]::new();$cupMatch.HomeClubId=$goalClub.Id;$cupMatch.CompetitionType='CupRoundOf64'
$goalEvents=[System.Collections.Generic.List[Elifoot.Domain.MatchEventRecord]]::new()
[Elifoot.Domain.MatchSimulationEngine]::ResolvePenaltyKick($cupMatch,$goalClub,$scorer,$null,25,$true,10,0,$goalEvents)
$panel=[Elifoot.Domain.PlayerStatisticsEngine]::GetPanelStatistics($goalWorld,$scorer,$null)
Write-Output ("X1_REAL_GOAL events=$($goalEvents.Count) match_goals=$($cupMatch.HomeGoals) season_counter=$($scorer.SeasonGoals) cup_counter=$($scorer.CupGoals) panel=$($panel.SeasonGoals) career=$($panel.CareerGoals)")
git status --porcelain=v1 --untracked-files=no
~~~

#### Roteiro de evidências de produção para revisão futura

- **Motor e RNG:** src/Elifoot.Domain/MatchEngine.cs:26,144,198,472,641,682; MatchPenaltyResolution.cs:5; DeterministicRng.cs:21.
- **Seleção e limites:** src/Elifoot.Domain/AiManagerBrain.cs:259,326; TacticsAndSquadRules.cs:17,68; client/elifoot_flutter/lib/shell_actions.dart:736.
- **Força/lesão/disciplina:** src/Elifoot.Domain/ClassicForceEvolutionEngine.cs:98,135,186; ChampionshipStrengthLimits.cs:7; DisciplineEngine.cs:27.
- **Economia/mercado:** src/Elifoot.Domain/MarketAndEconomyEngine.cs:11,57,70,195,818; OperatingEconomyEngine.cs:17; VoluntaryAuctionEngine.cs:7.
- **Banco/contratos/crise:** src/Elifoot.Domain/ClassicEconomyEngine.cs:52,72,94,130; SalaryContractsEngine.cs:30,63,106; SeasonContractsAndDebtEngine.cs:31; FinancialInsolvencyEngine.cs:21.
- **Carreira/temporadas:** src/Elifoot.Domain/CoachCareerEngine.cs:79,90,151,198,239; CompetitionAndSeasonEngine.cs:14,61,327,474,676.
- **Estatísticas/histórico:** src/Elifoot.Domain/PlayerStatisticsEngine.cs:69,219,291,377; HistoryEngine.cs:9; src/Elifoot.Application/ClubAndMarketApplicationService.cs:124.
- **Execução/aplicação:** src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:420,876,1013,1033; FinancialCommandsApplicationService.cs:10; WorldLobbyOrchestrator016B.cs:14.
- **Persistência/autenticação:** src/Elifoot.Infrastructure/EfWorldRepository.cs:97; ElifootDbContext.cs:69; src/Elifoot.Api/AccountLogin.cs:22,37,106; Services/AccountSessionService.cs:18; Hubs/GameRoomHub.cs:29.
- **Catálogo:** src/Elifoot.Infrastructure/Catalog/LocalHistoricalCatalogFiles.cs:33; WorldCreatorService.cs:50,331,361; src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs:120; InitialSquadStrength017B.cs:10; src/Elifoot.Api/WorldCreatorEndpoints.cs:12.
- **UI/plataforma:** client/elifoot_flutter/lib/main.dart:49; presentation_widgets.dart:33; game_viewport.dart:3; account_shell.dart:14; coach_finance_screens.dart:380; test/game_viewport_test.dart:96.

