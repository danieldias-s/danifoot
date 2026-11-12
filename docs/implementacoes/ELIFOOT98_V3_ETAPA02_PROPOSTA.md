# Elifoot 98 Multiplayer V3 — Etapa 02: auditoria e proposta

**STATUS: PROPOSTA PARA APROVAÇÃO — NÃO IMPLEMENTADA.**

Data: 10/11/2026, America/Sao_Paulo. Projeto: D:/elifoot_98. Branch: main. Base auditada: **2818cb351f6f1ba6cdb1d4949eeeca7e81e00c47**.

## 1. Resumo executivo

A carreira e o mercado já possuem implementação substancial: treinadores persistentes, moral, demissão, vagas, convites, histórico, títulos, compra direta, negociação bilateral, quatro motivos de leilão, salários contratados, referência salarial, renovação sazonal, dívida e controle de caixa. A Etapa 02 não precisa recriar esses sistemas.

Os pontos prioritários são o ciclo de pedidos salariais entre temporadas, as diferenças de validação entre caminhos de compra/lance humano e IA, a apresentação do salário que resultará de uma contratação e a divergência entre documentos antigos e a política sazonal atual. Mudanças de fórmula, inflação, nacionalidade ou carreira dependem de escolha explícita do proprietário.

O estudo consolidado fornecido pelo usuário contém análises estáticas econômicas mais recentes que as pesquisas 012/012B. Foi localizada uma cópia de ELIFOOT.EXE em Downloads cuja identidade coincide com a referência. Isso permite identificar o objeto estudado; não converte tabelas condicionais em medições de partidas/saves nem comprova todo o encadeamento de preços, reajustes e demissões.

Esta execução realizou leitura de código, testes, relatórios e histórico Git, conferência de identidade do binário e cálculos aritméticos sem estado persistente. Não executou o original, serviços, comandos de mercado, testes com banco ou simulações de temporadas. A única entrega nova é este documento. Não houve implementação, migration, alteração de banco/save, commit ou push.

Preservados os commits da Etapa 01:

- **d0489d3a4f9618fb12d4ea1650ef597e835dd2fe**: implementação e testes.
- **2818cb351f6f1ba6cdb1d4949eeeca7e81e00c47**: relatório de encerramento.

## 2. Estado atual do projeto

### Método e classes de evidência

| Marca | Significado neste relatório |
| --- | --- |
| CÓDIGO | Comportamento localizado na main auditada; quando a conclusão decorre de leitura, isso não equivale a uma reprodução HTTP nesta execução. |
| C-CALC | Valor calculado nesta auditoria reproduzindo a expressão atual em aritmética, sem criar mundo ou alterar dados. |
| TESTE ANTERIOR | Evidência da validação concluída na Etapa 01; testes não foram repetidos nesta auditoria documental. |
| ORIGINAL-IDENTIDADE | Tamanho, hash e cabeçalho do executável conferidos diretamente nesta execução. |
| ORIGINAL-ESTUDO | Operação/endereço atribuídos à análise estática da documentação consolidada; não houve nova desmontagem integral nem rastreamento dinâmico nesta auditoria. |
| RELATO | Observação do usuário ou descrição histórica citada pelo estudo; fórmula/gatilho não comprovados integralmente. |
| S | Exemplo condicional ou cenário sintético. Não é preço observado em save do original. |
| N/D | Resultado não demonstrado pela evidência disponível. Não significa zero ou inexistência da mecânica. |

A documentação anexada foi tratada como referência. Recomendações de implementação nela contidas não constituem autorização para alterar o projeto. A solicitação atual autoriza somente auditoria e proposta.

### Mapa de fontes do código

Os identificadores C01–C20 são usados nas matrizes e nos módulos.

| ID | Fonte e responsabilidade |
| --- | --- |
| C01 | [Entities.cs](D:/elifoot_98/src/Elifoot.Domain/Entities.cs:69): mundo, clube, jogador, humanos, IA, emprego, convites, moral e títulos; Player começa na linha 132. |
| C02 | [CoachCareerEngine.cs](D:/elifoot_98/src/Elifoot.Domain/CoachCareerEngine.cs:35): compatibilidade, responsável pelo jogo, moral/pontos/títulos, dispensa, vaga, interino e contratação. |
| C03 | [CompetitionAndSeasonEngine.cs](D:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs:468): fechamento, promoções/rebaixamentos, demissões e mudança de temporada; ranking a partir da linha 673. |
| C04 | [MarketAndEconomyEngine.cs](D:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs:11): valor justo, salário de referência, liquidação, mercado IA, ExecuteTransfer e leilão legado. |
| C05 | [SalaryContractsEngine.cs](D:/elifoot_98/src/Elifoot.Domain/SalaryContractsEngine.cs:17): registro de contratos, renovação, elegibilidade, pedidos, respostas e finanças IA. |
| C06 | [SeasonContractsAndDebtEngine.cs](D:/elifoot_98/src/Elifoot.Domain/SeasonContractsAndDebtEngine.cs:6): proteção por temporada, luvas, juros de caixa negativo e vencimento de leilões especiais. |
| C07 | [VoluntaryAuctionEngine.cs](D:/elifoot_98/src/Elifoot.Domain/VoluntaryAuctionEngine.cs:8): criação, prazo global e liquidação voluntária. |
| C08 | [FinancialInsolvencyEngine.cs](D:/elifoot_98/src/Elifoot.Domain/FinancialInsolvencyEngine.cs:6): leilões especiais, crise e comando de lance. |
| C09 | [ClassicEconomyEngine.cs](D:/elifoot_98/src/Elifoot.Domain/ClassicEconomyEngine.cs:13): compatibilidade, crédito, juros bancários, quitação e reserva de caixa. |
| C10 | [ClassicEconomyState.cs](D:/elifoot_98/src/Elifoot.Domain/ClassicEconomyState.cs:5): políticas, recibos, pedidos, contratos e crises persistentes. |
| C11 | [OperatingEconomyEngine.cs](D:/elifoot_98/src/Elifoot.Domain/OperatingEconomyEngine.cs:15): política comercial versionada e orçamento/serviço da dívida IA. |
| C12 | [AiManagerBrain.cs](D:/elifoot_98/src/Elifoot.Domain/AiManagerBrain.cs:78): reserva operacional, lacunas/excedentes e orçamento de contratação; GetPlanningScore na linha 268 é planejamento de mercado, distinto da seleção A/M. |
| C13 | [ClubAndMarketApplicationService.cs](D:/elifoot_98/src/Elifoot.Application/ClubAndMarketApplicationService.cs:157): anúncios, compra, propostas, respostas, carreira e AutoMode. |
| C14 | [FinancialCommandsApplicationService.cs](D:/elifoot_98/src/Elifoot.Application/FinancialCommandsApplicationService.cs:10): lock da sala, proprietário, pronto, versão, recibo, salvamento e publicação. |
| C15 | [MarketQueriesApplicationService.cs](D:/elifoot_98/src/Elifoot.Application/MarketQueriesApplicationService.cs:7): DTOs autoritativos de anúncios, leilões e ofertas. |
| C16 | [Program.cs](D:/elifoot_98/src/Elifoot.Api/Program.cs:565) e [EconomyDtos.cs](D:/elifoot_98/src/Elifoot.Contracts/EconomyDtos.cs:5): rotas, comandos e enums/defaults. |
| C17 | [main.dart](D:/elifoot_98/client/elifoot_flutter/lib/main.dart:1007), [salary_request_modal.dart](D:/elifoot_98/client/elifoot_flutter/lib/salary_request_modal.dart:7), [market_screen.dart](D:/elifoot_98/client/elifoot_flutter/lib/market_screen.dart:254), [auction_config.dart](D:/elifoot_98/client/elifoot_flutter/lib/auction_config.dart:4), [squad_classic.dart](D:/elifoot_98/client/elifoot_flutter/lib/squad_classic.dart:105): fluxos financeiros e de plantel. |
| C18 | [shell_actions.dart](D:/elifoot_98/client/elifoot_flutter/lib/shell_actions.dart:317), [coach_finance_screens.dart](D:/elifoot_98/client/elifoot_flutter/lib/coach_finance_screens.dart), [api_client.dart](D:/elifoot_98/client/elifoot_flutter/lib/api_client.dart:844): carreira, finanças e chamadas HTTP. |
| C19 | [EfWorldRepository.cs](D:/elifoot_98/src/Elifoot.Infrastructure/EfWorldRepository.cs:97): snapshot JSON e sincronização do livro-caixa relacional a partir da linha 167. |
| C20 | [PlayerProvenance.cs](D:/elifoot_98/src/Elifoot.Domain/Catalog/PlayerProvenance.cs), [RuntimePlayerProvenance.cs](D:/elifoot_98/src/Elifoot.Domain/Catalog/RuntimePlayerProvenance.cs:6) e [PlayableWorldDtos.cs](D:/elifoot_98/src/Elifoot.Contracts/Catalog/PlayableWorldDtos.cs:9): procedência do catálogo, sem classificação econômica por nacionalidade. |

### Checkpoint de testes consultado

[Relatório da Etapa 01](D:/elifoot_98/docs/implementacoes/ELIFOOT98_V3_ETAPA01_RESULTADO.md) e [consolidação por caso distinto](D:/elifoot_98/artifacts/v3-stage01-close/validation-consolidated.json):

| Validação anterior | Resultado conferido |
| --- | ---: |
| Domínio .NET | 547 PASS |
| Aplicação .NET | 37 PASS |
| Integração .NET | 288 PASS |
| Simulação .NET | 31 PASS |
| Total .NET distinto, por testId | **903 PASS; 0 desfechos finais diferentes de Passed** |
| Flutter | **485 PASS** |
| Flutter analyze | **PASS, sem problemas** |
| Layout desktop | 1024×640, 1072×768, 1280×720 e 1360×900 aprovados no encerramento |

As contagens são herdadas do checkpoint, não resultados novos da Etapa 02. Uma suíte verde não demonstra ausência das lacunas de cenários identificadas abaixo.

O estado rastreado estava limpo antes da criação deste arquivo. Os arquivos não rastreados preexistentes e outros worktrees foram preservados. Não se consultaram endpoints: algumas leituras da aplicação fazem migração de snapshot/processamento e salvam estado, como GetCareerAsync.

## 3. Auditoria de treinadores

| Sistema | Comportamento atual e localização | Humano / IA | Testes existentes e limites |
| --- | --- | --- | --- |
| Identidade e vínculo | HumanCoach e AiManager têm identidade, clube atual, carreira e EmploymentHistory; Club guarda HumanCoachId/AiManagerId. Migração de carreira versão 8 corrige dupla associação e conserva agregados, C01/C02:35. | Mesmo adaptador Coach para carreira. Conta/perfil do humano é identidade de acesso; não constitui uma carreira global agregada entre todos os mundos. | CoachCareer008Tests: LegacyMigrationPreservesAggregatesAndMarksFinishedGamesWithoutInventingHistory; integrações de troca/reinício. |
| Responsável por resultado | Captura treinador e moral no início; crédito de pontos/resultado/título ao responsável registrado, C02:67–149. Fluxo externo controla aplicação única. | Compartilhado. Trocar de clube não transfere retroativamente resultado de jogo já iniciado. | CreditBelongsToKickoffCoachEvenAfterClubChanges_AndReplayReloadIsIdempotent; testes de byes e ordem da tabela. |
| Pontos e ranking | Liga: vitória 4/3/2/1/0 e empate 2/1,5/1/0,5/0 nas divisões 1ª/2ª/3ª/4ª/Distrital. Taça: vitória por fase 3/4/5/6/7/8; seis vitórias somam 33. Ranking lexicográfico: campeonatos, Taças, pontos, CoachId, C02:79 e C03. | Mesma regra; não é a pontuação histórica por mando descrita na referência. | ExactDecimalDivisionPoints, SixCupWinsEqual33AndTitleIsSeparate_NoByeTitle, RankingIsLexicographicAndTiesAreStableAcrossReloadAndUnemployment. |
| Moral | Inicial 70; limitada 0–100; verde ≥60, amarela ≥30, vermelha <30. Resultado, favoritismo, sequência, goleada e posição esperada afetam variação. Host configura faixas e recuperação; não há campo independente de reputação pessoal equivalente a Club.Reputation, C01:514/C02:101. | Compartilhado. Personalidade da IA entra em decisão de oportunidade, não substitui pontos/títulos. | MoraleBandsAndPreMatchRedDismissal; StrongFavoriteDemandsMoreThanUnderdog_AndRecentPoorFormMatters; integração das faixas. |
| Demissão | Moral vermelha no início do jogo + derrota; humano cai da 4ª para Distrital: demissão independente da moral; IA não interina rebaixada nas divisões avaliadas: AI_RELEGATION, C02:125 e C03:559–590. Não foi localizada demissão diretamente por dívida. | Assimetria de rebaixamento é explícita. Humano não administra Distrital. | DistrictRelegationDismissesRegardlessOfMorale_AndPromotedClubIsEligible; simulação de carreira e integrações. |
| Vaga/interino | Dispensa fecha emprego, limpa vínculo e submissão humana, abre vaga determinística e nomeia IA provisória; recém-demitido respeita duas rodadas antes de reassumir. Na falta de IA livre elegível, cria interino, C02:151–196. | Interino é IA; não implica expulsar o humano da sala. Desempregado conserva carreira e pode acompanhar as rodadas. | DismissedAiCannotImmediatelyReappointItselfAsInterim; CoachCareer008ReadinessTests e integração de contador. |
| Convite/contratação | Só moral verde. Desempregado espera duas rodadas; acesso a clubes superiores exige experiência: campeonato, 60 pontos ou 15 vitórias; sem isso, 3ª/4ª. Empregado espera três rodadas e exige melhoria de projeto; humano usa pragmatismo 50. Projeto considera divisão, reputação do clube, força média, log do caixa e capacidade. Vaga oferece a candidatos por ranking/moral/experiência, C02:198–278. | Humano decide aceitar/recusar; IA avalia e responde automaticamente. Até três convites pendentes por humano, um ativo por vaga, validade de três rodadas. Mesmo mecanismo de vínculo ao contratar. | OnlyGreenHumanReceivesOpportunity, EliteLeaderRejectsLowerProjectButSmallLeaderCanAcceptGreatStructure, SearchContinuesAfterAiRefusalAndPreservesPersonalityOnAcceptedMove; HTTP concorrente. |
| Continuidade | Troca preserva pontos, títulos, moral e personalidade; fecha emprego anterior, zera sequência/recentes do trabalho anterior e abre vaga no clube deixado, C02:296. Legado sem histórico não ganha fatos inventados. | Igual nos dados comuns. Não há salário de treinador, multa de saída ou contrato de treinador por dias identificados no fluxo auditado. | Reinício SQLite, exclusividade e preservação de personalidade em CoachCareer008Tests/IntegrationTests. |
| Interface e automação | Ranking, ficha, emprego, moral e convites em C18; aceitar/recusar atualiza estado. Host pode ligar IsAutoMode; orquestrador prepara escalação/pronto, C13:472 e MultiplayerRoundOrchestrator:428. | AutoMode mantém o humano como proprietário. Mercado/finanças IA selecionam HumanCoachId == null; portanto não são férias com delegação financeira integral. | career_api_test, coach_finance_reference_test, readiness; não foi localizada cobertura completa de férias financeiras. |

Oportunidades de aperfeiçoamento, sem implementação: definir política de rebaixamento para humanos/IA; decidir se reputação pessoal terá significado separado de moral/ranking; esclarecer continuidade apenas dentro do mundo; definir eventual delegação financeira. Não há base para inventar salários, multas ou novos critérios de demissão. Férias, remoção administrativa da sala e demissão esportiva são eventos diferentes; os dois primeiros não têm fluxo completo equivalente ao original localizado nesta auditoria.

A consulta GetCareerAsync chama EnsureMigrated, ProcessVacancies e SaveWorldAsync sob lock (C13:435). Isso é comportamento atual com efeitos persistentes em uma leitura; não foi exercitado nesta execução. Aceitação usa lock, valida pronto/fase, salva e publica, mas não tem o mesmo recibo financeiro durável de C14; repetição de convite já aceito é rejeitada.

## 4. Auditoria de transferências

### Modelo e regras atuais

| Conceito | Significado atual |
| --- | --- |
| Valor justo/base de referência | CalculateFairValue; depende de força, posição, comportamento, estrela e contador contratual. Não recebe mundo, temporada, país ou caixa vendedor. |
| MarketPrice | Campo armazenado, atualizado em geração/evolução/algumas operações. Não é necessariamente a cotação recalculada exposta no DTO e não deve ser confundido com preço de venda. |
| Salário contratado | SalaryPerRound; folha efetivamente cobrada. Pode divergir da referência. |
| Salário de referência | CalculateFairSalaryPerRound / ReferenceSalaryPerRound; calculado pelos atributos atuais. |
| Preço pedido | AskingPrice do anúncio, escolhido pelo humano ou política IA; comprador imediato paga esse valor. |
| Oferta / contraproposta | OfferedAmount / CounterAmount; não alteram propriedade antes do aceite válido. |
| Lance mínimo / atual | MinimumBid / CurrentBid do leilão; um lance efetivo zero usa HighestBidderClubId e não é ausência de proposta. |
| Preço final | Valor validado em ExecuteTransfer ou liquidação de livre; pode divergir dos demais valores. |

Para F = clamp(força, 1, 50), C04:11–65:

    V = round(max(15000, (12000 + 76 × F^2,65) × P × B × E × K))

P: A=1,12; M=1,05; G=1,03; D=0,98.
B: Fair Play=1,08; Cordeirinho=1,04; Cavalheiro=1; Caneleiro=0,95; Caceteiro=0,89; Sarrafeiro=0,82.
E: sem estrela=1; estrela em A/M=1,32; estrela em D/G=1,22.
K: contador ≤2: 0,75; ≤5: 0,88; ≥18: 1,06; demais: 1.
Arredondamento atual: Math.Round decimal, sem modo explícito, portanto ToEven. Os comentários aproximativos da curva não substituem a expressão.

| Fluxo | Implementação e proteções | Humano / IA e lacunas | Testes existentes |
| --- | --- | --- | --- |
| Anunciar/alterar/retirar | C13:157 e C14:51–60. Dono, fase, pronto, versão, preço positivo com centavos, proteção sazonal, reserva de leilão e elenco mínimo. Edição/publicação humana incrementa TransferListingVersion. | Humano escolhe preço. IA anuncia excedentes e aplica descontos por política própria; lote não usa o comando humano de anúncio. | MatchMarket015ATests: revisão, retirada/republicação, confirmação obsoleta, consentimento e modalidades incompatíveis. |
| Compra imediata | C13:192: anúncio/proprietário/preço/versão informados devem continuar válidos; capacidade considera arrematações abertas. C04:818 revalida ambas as partes, dinheiro e bloqueios. | Humano compra anúncio. IA também compra anúncios de humanos; anunciar é consentimento de venda pelo preço. Não há barreira absoluta à venda de força alta, exceto proteção/elenco/recursos. | FirstValidBuyerWinsAndDurableLedgerReceiptAndCancelledOffersSurviveRestart; UnrenewedAthleteCanBeSoldAndRenewalClosesTheListing. |
| Proposta e contraproposta | C13:214–341; preço positivo, caixa ao propor, vendedor pode recusar/contrapropor; comprador aceita contraproposta. Propostas não reservam dinheiro; aceite revalida recursos. | Resposta imediata de vendedor IA usa preço pedido ou 0,92×V para excedente e 1,12×V para outro; contrapropõe se oferta ≥80% dessa referência. Lote posterior IA usa 0,88×V ou 1,12×V e limiar de 82%. Políticas diferentes conforme caminho. | ListingRevisionCounterOfferAndIncompatibleModalitiesCannotBypassSellerConsent; testes de contraproposta na UI. Cobertura não demonstra equivalência das duas heurísticas IA. |
| Execução e contabilidade | C04:818–905: impede mesma parte, duplicidade, propriedade inválida, renovado, desvio de leilão, caixa insuficiente e elenco ilegal. Débito comprador e crédito vendedor de mesmo valor; metadados, histórico, anúncio encerrado e ofertas concorrentes canceladas. | Núcleo comum para venda de atleta vinculado; caminhos livres têm liquidação própria. IA conserva principal bancário adicional pela política operacional. | Integrações de concorrência, recibo, saldo e reinício; testes econômicos de conservação e replay. |
| Novo contrato na compra | C04:898–903: contador ao menos 16; RenewedForSeason limpo; salário recalculado pela referência; RecordContract protege salário negociado. | Não há negociação salarial com comprador no formulário. Salário anterior exibido pode diferir do salário que será firmado. Transferência não marca renovação sazonal automaticamente. | ClassicEconomy012DTests separa salário vigente/referência; falta caracterização explícita da prévia antes/depois com salários muito diferentes. |
| Mercado IA | C04:526–815/C12: atualiza plano, anuncia até dois excedentes; desconto de 10% após três rodadas com piso 78% de V. Anúncio novo: 88% de V em aperto ou 104% nos demais casos. Máximo uma contratação ativa por clube e oito transferências no lote de busca; esse limite não cobre separadamente todas as liquidações/leilões/ofertas do lote. | Exclui Distrital da busca ativa. Orçamento: max(0, caixa − reserva) × (0,35 + agressividade/200); reserva inicial max(180 mil, cinco folhas + parcela de manutenção). Busca preço ≤orçamento e ≤125% de V; prioriza anúncio e utilidade de força/estrela por preço. | MetricAiAudit007Tests, PlayableEconomyRegressionTests e simulações de mercado/economia. Testes longos são caracterização, não prova de ótimo econômico. |

A reserva por principal da IA e a decisão de contrair empréstimo não criam dinheiro próprio da IA. O humano usa caixa disponível após reservas de arremate; não recebe a mesma restrição estratégica de preservar principal. A diferença é política atual de gestão, não falha automática.

Há lacuna no compartilhamento de validações: compra imediata humana considera vagas já comprometidas por leilões; aceite bilateral e compras IA chegam a ExecuteTransfer, que verifica apenas o tamanho atual. Lances humanos passam por PlaceBid e consideram vagas reservadas; lances IA são atribuídos diretamente no lote e não passam por essa mesma checagem. A liquidação impede ultrapassar 24 atletas, mas pode frustrar arrematações previamente possíveis. Ver I02/I03.

### Caixa e economia associados

C04:195 liquida receitas, salários e contratos com chaves por jogo/rodada. Folha: soma de todos os atletas vinculados do clube que jogou, inclusive reservas/indisponíveis; uma cobrança por clube/rodada global, sem débito em folga. Banco clássico cobra juros por partida efetivamente disputada, com MatchId e sem cobrança retroativa. São periodicidades distintas, preservadas.

C09/C10: empréstimo clássico de R$ 500.000 e R$ 25.000 por partida por contrato; limite por divisão e indicador patrimonial da capacidade utilizável. São parâmetros adaptados do projeto, não prova de fórmula original. Dívida não desaparece com fim de contador. C06: caixa negativo gera encargo separado de 2% após rodada global oficialmente liquidada, sem somá-lo ao principal bancário como se fosse a mesma dívida.

C11: novos mundos usam CLASSIC_OPERATING_BALANCE_012D_FIX_V2. Patrocínio é orçamento **anual** de R$ 185.000/125.000/92.000/72.000/20.000 por divisão, parcelado pelo calendário global, inclusive folgas. Mundo legado pode manter termos anteriores até a temporada de atualização programada. Não se aplicam esses valores a cada rodada no padrão novo. Reserva de principal e exigência de capacidade operacional para empréstimo são estratégias específicas da IA. Estádio/bilheteria não são candidatos a alteração nesta proposta.

## 5. Auditoria salarial

### Referência, salário inicial e pagamento

C04:54–65:

    Sref = round((220 + 1,65 × F^2,08) × estrela × posição)
    estrela = 1,20 se marcada; 1 nos demais casos
    posição = 1,08 para A; 1 para G/D/M

Comportamento e nacionalidade não são entradas dessa função. Temporada e índice de inflação também não. Posição e estrela afetam a referência atual; isso diverge da rotina salarial descrita no estudo original, que não lê diretamente esses atributos.

WorldSeedFactory:269–284 inicializa SalaryPerRound pela referência e contratos sintéticos de 12–27 rodadas; a criação por catálogo também usa a função. Importação/edição pode conservar salários próprios; não se deve presumir igualdade de salário para todo atleta de mesma força. A forma de inicialização de um mundo não autoriza reescrever salários de saves existentes.

Mudança de força atualiza valor e referência derivada, preservando salário contratado: ClassicForceEvolutionEngine:285 e PlayerStrengthEvolutionEngine:65. A folha é debitada conforme §4. Não existe indexação anual de salário ou de V na política atual.

### Pedido, aceitação e recusa

| Passo | Regra atual | Diferença humano / IA, teste e lacuna |
| --- | --- | --- |
| Elegibilidade | Ganho de força ou promoção registra gatilho; contrato negociado ainda vigente impede nova elegibilidade por evolução. Promoção pode valer na temporada seguinte. Apenas não renovado, contador ≤0, não anunciado e não reservado, C05:58–99. | Regra de geração comum. Não ocorre para todo jogador a cada rodada. SalaryRequestSafetyTests e ClassicEconomy012DTests. |
| Frequência | Após jogos concluídos, no máximo um pedido por clube/rodada e um pendente por clube; ordena defasagem relativa, ganho de força e ID. Histórico/cursor/chaves persistidos. | Pending antigo também bloqueia novos pedidos. Não foi encontrado vencimento automático na troca de temporada. |
| Valor proposto | Referência × divisão: 1,10/1,05/1/0,95/0,90. Limita aumento normalmente a 10%, ou 5% com caixa negativo; pode ser menor se a referência limitar. Arredonda inteiro, C05:101. | Mesma expressão. Não é inflação anual nem aumento de 10% garantido a cada rodada. Ex.: salário 1.000 e referência divisional acima de 1.100 → 1.100; com caixa negativo → 1.050 [C-CALC, cenário sintético]. |
| Aceitar | Aplica exatamente RequestedSalary, protege até o fim da temporada habilitada, registra contrato/resolução/versionamento; não debita todo compromisso futuro nem cobra luvas nessa resposta, C05:126–136. | Humanos decidem na modal; IA considera três folhas, dois juros bancários e reserva operacional, além do compromisso projetado. Mesma operação de domínio para resposta. |
| Recusar com saída legal | Atleta fica livre imediatamente; sai de escalações preferidas; leilão salarial começa em zero; ex-clube não recebe o arremate, C05:139–145. | Núcleo comum. RefusalCreatesZeroAuctionForFreeAgentWithoutFormerClubRevenue e SalaryRequestSafetyTests. |
| Recusar sem poder remover | Humano: rejeita comando e mantém PENDING. IA: converte a decisão em renovação obrigatória, mesmo sem caixa, C05:123–137. | Diferença explícita. PendingAndInvalidRefusalRemainUnresolvedRatherThanSilentlyAccepting cobre humano; MinimumSquadMakesRefusalAMandatoryRenewalEvenWithoutCash invoca actor AI. |
| Persistência e UI | Estado PENDING/ACCEPTED/REFUSED; conta responsável, versão e timestamps. C14 valida dono/fase/pronto/versão e usa recibo durável. Modal aparece na página inicial/plantel seguro em PREPARING, C17/main:1009. | Modal informa genericamente que recusar envia a leilão, mas o servidor pode impedir por elenco mínimo. Filtro da modal não restringe temporada. Testes Flutter cobrem modo clássico/atual, resoluções e erro/repetição. |

### Renovação sazonal e legado

No padrão SeasonContractsEnabled=true, RenewedForSeason igual à temporada atual bloqueia anúncio e proposta nos comandos humanos, execução de transferência e leilão, inclusive por insolvência. A avaliação prévia de ofertas IA tem a lacuna I07. A renovação conserva salário por padrão e cobra luvas: salário × rodadas globais restantes / 10, arredondadas em centavos. Pode deixar caixa negativo, pois a checagem de saldo das luvas está condicionada ao modo legado. ProtectSeasonContract encerra anúncio e cancela ofertas abertas.

Na virada, contador de atletas vinculados é zerado; o marcador de temporada anterior deixa de proteger. O atleta continua no clube e recebe salário: contador zero no modo sazonal não significa rescisão/liberdade. A UI de renovação já explica validade até fim da temporada e bloqueio inclusive por dívida (main.dart:1918).

No modo legado desabilitado, ainda existem prazos em rodadas e heurística de renovar por 20 ou liberar ao vencer. Não são contratos em dias; não há autorização para modernizá-los. Campos de compatibilidade permanecem no modelo e não devem ser eliminados ao especificar a política sazonal.

O pedido continua registrando RenewalMatches=20 e TotalSalaryCommitment=RequestedSalary×20, embora a aceitação sazonal dure o restante da temporada. A IA usa esse horizonte para affordability. É necessário decidir se se trata de reserva conservadora de planejamento ou metadado contratual a corrigir; não existe cobrança integral adicional desses 20 jogos.

Fontes documentais: [SALARY_RENEWAL_RULES_012D.md](D:/elifoot_98/docs/SALARY_RENEWAL_RULES_012D.md), C05/C06/C10 e histórico c7a4cad/c9a91a8. O relatório antigo ainda afirma aceitação por pelo menos 20 partidas e recusa obrigatória genérica; isso não descreve integralmente a política sazonal e a distinção atual de atores.

## 6. Auditoria de leilões

| Motivo | Criação / duração atual | Vínculo e preço mínimo | Liquidação e ausência de comprador |
| --- | --- | --- | --- |
| Voluntary=3 | Humano cria por UI/API; duração escolhida 1–3 rodadas globais, padrão 2. Criado em PREPARING R7 com 2 termina após R8; em RESULTS R7, após R9. Cruza temporada, C07/C16. | Atleta permanece vinculado; padrão **R$ 500.000**, alterável para qualquer valor ≥0 com até duas casas decimais. O padrão não é piso obrigatório. | ExecuteTransfer; comprador debita e vendedor recebe exatamente o preço. Sem comprador ou revalidação inválida: fecha sem venda, mantém atleta. |
| SalaryRenewalRefused=1 | Recusa legal; duração determinística sorteada de 1 a 3, independente do RNG esportivo; vencimento por temporada/rodada oficial, C08:20. | Atleta livre; mínimo zero. | Contratação do livre; só débito comprador, sem receita do ex-clube. Sem comprador permanece livre; AUCTION_UNSOLD. |
| FinancialInsolvency=2 | Caixa <0 ou juros vencidos; no modo sazonal dura uma rodada global e exclui renovados. Um aberto por clube; não abre segundo no mesmo clube/rodada. Na rodada seguinte pode selecionar outro elegível, C08:33–63. | Mantém vínculo/salário; mínimo zero. Prioridade maior força, maior salário, menor ID; preserva 11/1 G/10 linha. Não se bloqueiam todos os jogadores de força alta. | ExecuteTransfer, débito/crédito reconciliados. Sem comprador mantém atleta e crise; não cria comprador nem perdoa dívida. |
| ContractExpiration=0 | Compatibilidade com contratos antigos: expiração gera mínimo de 65% de V e lógica anterior de prazo; novos fluxos sazonais não liberam automaticamente por contador zero, C04:319–368/908. | Atleta livre. Não recebe os defaults voluntários. | Arremate só debita comprador; fallback pode contratar gratuitamente para IA elegível, contador 14, mantendo modalidade legada. Não há crédito ao antigo clube. |

A liquidação voluntária e os especiais com ExpiresAtSeason exigem RESULTS e a marca de liquidação global, além do prazo. Leitura, reconexão ou finalização isolada de jogo não consomem duração.

Lance humano usa PlaceBid, saldo menos reservas de outros arremates e vagas comprometidas; incremento mínimo R$ 5.000 após primeiro lance. Voluntário aceita centavos; automáticos exigem inteiros. No lote IA, leilão de vencimento usa incremento percentual de 6%, enquanto outros motivos usam NextBid de R$ 5.000. A assimetria de incremento no legado precisa ser explicitada antes de qualquer uniformização.

IA participa por necessidade e avaliação privada limitada a 105% de V para lacuna ou 88% nos demais casos, teto no orçamento do plano e caixa com reserva. O lote considera até dez compradores candidatos, não todos os clubes indistintamente. Não cria um comprador artificial para salvar o vendedor.

A criação voluntária considera saídas já reservadas; a execução revalida propriedade, elenco e dinheiro. Esses controles evitam venda efetiva ilegal, mas não demonstram igualdade de todas as verificações de capacidade entre caminhos (I02/I03).

Testes: VoluntaryAuctionTests, VoluntaryAuctionIntegrationTests, SeasonContractsAndDebtTests, ClassicEconomy012DTests/IntegrationTests, auction_default_config_test, economy_012d_api_test e match_market_015a_test. Cobrem padrões, centavos, zero versus sem lance, duração, virada, persistência, mínimo de elenco, concorrência e lançamento único. Falta cobertura específica dos caminhos de reserva divergentes em I02/I03.

[AUCTION_DEFAULT_CONFIG.md](D:/elifoot_98/docs/AUCTION_DEFAULT_CONFIG.md) e commit **9690d95** documentam a funcionalidade incorporada. [FORCED_AUCTIONS_012D.md](D:/elifoot_98/docs/FORCED_AUCTIONS_012D.md) e [FINANCIAL_INSOLVENCY_012D.md](D:/elifoot_98/docs/FINANCIAL_INSOLVENCY_012D.md) preservam política anterior: cooldown de duas rodadas e possibilidade de leiloar renovado não devem ser apresentados como padrão sazonal atual.

## 7. Comparação com Elifoot 98 original

### Proveniência e alcance

Referência principal R1: [documentação técnica consolidada, versão 1.0 de 09/11/2026](C:/Users/Daniel/Downloads/ELIFOOT_98_DOCUMENTACAO_TECNICA_CONSOLIDADA.md), especialmente §§1, 4, 5, 6 e 9. SHA-256 da documentação lida: **840fef091361ba4ea97742587f7ee04e0066db61084661d766918d48c4adb248**.

Nesta auditoria foi lido, sem execução, [ELIFOOT.EXE](C:/Users/Daniel/Downloads/ELIFOOT.EXE):

- Tamanho: **836.356 bytes**.
- SHA-256: **1c5d59cc5e32d5c73480b47986f34ce3fa2d3d34b36bdf018076c67e4f941775**.
- Cabeçalho MZ; novo cabeçalho NE em offset 256; **26 segmentos**.
- Identidade coincidente com ELIFOOT(1).EXE descrito em R1. Não foram alterados seus bytes.

As pesquisas [012](D:/elifoot_98/docs/ORIGINAL_PLAYER_FORCE_RESEARCH_012.md), [012B](D:/elifoot_98/docs/PLAYER_FORCE_ORIGINAL_RESEARCH_012B.md) e [bancária 012D](D:/elifoot_98/docs/CLASSIC_BANK_RESEARCH_012D.md) são fontes históricas do projeto, com limites e datas próprios. A ausência de binário registrada em 30/10 não é conclusão válida sobre o ambiente atual. A [auditoria anterior do projeto](D:/elifoot_98/docs/analises/ELIFOOT98_AUDITORIA_ESTADO_ATUAL.md) foi consultada como índice e comparação; seu layout PARTIAL é superado pelo encerramento da Etapa 01.

**Original comprovado diretamente nesta execução:** identidade e estrutura do arquivo. **Fórmulas econômicas:** ORIGINAL-ESTUDO, com endereços e limites de R1; não foram revalidadas aqui instrução a instrução. **Valores econômicos medidos em execução controlada do original:** N/D em todas as tabelas abaixo. Os números da coluna S são cálculos condicionais do estudo ou exemplos sintéticos, separados dos resultados C-CALC.

### Operações atribuídas ao binário e perguntas ainda abertas

| Tema | ORIGINAL-ESTUDO / RELATO | Limite da conclusão |
| --- | --- | --- |
| Salário | SEG03:7FF4..8153: expressão compatível com força, inflação, escala monetária, fator de equipe e piso de salário anterior. Não lê diretamente comportamento, estrela, B/D ou classificação nacional do comprador. | Associação/gatilhos ainda parciais; salário inicial versus salário contratado após renovação precisa de rastreamento. Não prova reajuste automático toda vez que força muda. |
| Avaliação-base | SEG09:371A..37E3: V = arred(2000×C×I×F^1,04×(1−0,04A−0,02B+0,02D)). A em jogador+2D; B em +2F; D em +2B. | B não tem significado completo comprovado; D correlaciona-se à estrela. Não substituir esses campos por multiplicador livre de estrela ou salário. |
| Asterisco | SEG03:5BB1 contém ×1,10 em interesse de contratação. | Não é +10% em todo preço final, nem +10% de salário. D afeta base indiretamente. |
| Nacionalidade | SEG12:0A2F..0AEA compara jogadorDados+1B com clube comprador+4A; classes 0 doméstico, 1 equiparado, 2 estrangeiro comum. SEG03:63FA contém correção do preço candidato. | Não há tabela integral de equivalências validada nesta auditoria; papel do país vendedor, caminho humano/IA e aplicação em leilão permanecem abertos. |
| Inflação | DS:2B46; inicial I=2; faixa relatada 0,5–10; SEG05:0D3C..0E23 contém atualização pseudoaleatória. Tela exibe arred(I×10). | Exibir 20 não significa inflação de 20% ao ano. Não há série de saves controlados que permita atribuir I a temporadas específicas. |
| Renovação | R1 §5.7: relato de bloquear venda na temporada renovada e liberar na seguinte. | Não foi reconstruída a rotina completa; percentual fixo de aumento e destino exato dos pagamentos de todas as modalidades não demonstrados. |
| Finanças do vendedor | Não são argumento da avaliação-base acima; outras etapas podem considerar necessidade/interesse. | Não há coeficiente comprovado para multiplicar V pela dívida/caixa vendedor. Projeto atual usa caixa na estratégia de anúncio e crise. |
| Treinadores | Textos de convite, contratação, demissão e sucessor; guia descreve ranking por campeonatos/Taças/pontos e pontos por mando 3/2/2/1. | Textos não demonstram gatilho de demissão; não se comprovou igualdade integral de avaliação humano/IA. Não autoriza demissão automática por caixa negativo. |

No estudo, a expressão salarial candidata é:

    S = (50×C) × clamp(arred(35×I×sqrt(R)×F^1,04×1000^0,04 / 50), 1, 1000)

C é escala monetária, I inflação interna, R fator de equipe. Há também piso ligado ao salário anterior quando determinado campo é positivo. C=5 nos exemplos é hipótese de escala; não é configuração universal demonstrada. Regras exatas de arredondamento do executável e de aplicação do piso não são inferidas do Math.Round usado pelo projeto.

### Mesma força, comportamentos diferentes

C-CALC: F30, M, sem estrela, contador 10, variando apenas comportamento. S original: I=2, C=5, B=5, D=5; salário candidato com R=1, sem aplicar piso anterior.

| Comportamento | Atual V / Sref (C-CALC) | Original comprovado em save | Exemplo original condicional (S): V / salário | Ainda desconhecido |
| --- | ---: | --- | ---: | --- |
| Fair Play | 721.225 / 2.169 | N/D | 687.442 / 15.750 | Encadeamento até preço final |
| Cordeirinho | 694.513 / 2.169 | N/D | 659.944 / 15.750 | Idem |
| Cavalheiro | 667.800 / 2.169 | N/D | 632.447 / 15.750 | Idem |
| Caneleiro | 634.410 / 2.169 | N/D | 604.949 / 15.750 | Idem |
| Caceteiro | 594.342 / 2.169 | N/D | 577.451 / 15.750 | Idem |
| Sarrafeiro | 547.596 / 2.169 | N/D | 549.954 / 15.750 | Idem |

No projeto, comportamento muda V por multiplicador; não altera Sref diretamente. No estudo, comportamento altera parcela aditiva do coeficiente de V; ausência de leitura direta na rotina salarial não elimina possíveis efeitos indiretos ainda não mapeados.

### Mesma força, nacionalidade diferente

O projeto não tem atributo operacional que classifique o atleta como doméstico/equiparado/estrangeiro para compra. Country do clube e CountryCode de procedência do catálogo não implementam essa regra; procedência do clube inicial não deve virar nacionalidade do jogador por inferência.

| Relação com comprador | Código atual (CÓDIGO/C-CALC) | Original comprovado em save | Preço candidato isolado com Q=1.000.000 (S) | Ainda desconhecido |
| --- | --- | --- | ---: | --- |
| Mesmo país, classe 0 do estudo | Classe econômica ausente; função atual mantém V=667.800 para o jogador-base | N/D | (Q+30.000)×1,60 = 1.648.000 | Aplicação por modalidade e composição de Q |
| Equiparado, classe 1 do estudo | Classe econômica ausente; mesmo V=667.800 | N/D | (Q+15.000)×1,30 = 1.319.500 | Quais países/equivalências e sua direção |
| Estrangeiro comum, classe 2 | Classe econômica ausente; mesmo V=667.800 | N/D | Q = 1.000.000 | Restrições e preço final de leilão |

Comprador: país é referência da comparação do estudo. Jogador: precisa de nacionalidade própria. Vendedor: país não participa da comparação identificada, mas o encadeamento completo não foi demonstrado. Não se afirma que o vendedor seja irrelevante em toda operação.

R1 também relata fator de interesse de 1,50+0,05N para doméstico; 1,15+0,05N para equiparado; 1 para estrangeiro; N conta estrangeiros não equiparados. Esse fator **não deve ser multiplicado automaticamente** pela correção de preço candidato. O limite de cinco estrangeiros relatado é evidência para estudo, não autorização para implantar cotas ou modificar escalação/setores nesta etapa.

### Mesma força, com e sem estrela

Mesmos F30/M/Cavalheiro/contador 10 no código atual. No exemplo do estudo varia-se isoladamente a etapa de interesse, mantendo sua entrada em 1 milhão; não se simula a geração conjunta de D e da estrela.

| Caso | Atual V / Sref (C-CALC) | Original comprovado em save | Exemplo isolado de interesse (S) | Ainda desconhecido |
| --- | ---: | --- | ---: | --- |
| Sem estrela | 667.800 / 2.169 | N/D | 1.000.000 | Valor final/efeito conjunto de B e D |
| Com estrela | 881.497 / 2.603 | N/D | 1.100.000 | Não há bônus salarial direto demonstrado |

### Mesma força, salários contratados diferentes

Fixture sintética de F30/M/Cavalheiro/sem estrela/contador 10. Os salários vigentes abaixo são entradas escolhidas; não são amostra de save real. Sref e V são C-CALC da expressão atual e independem dessas três entradas.

| Salário vigente escolhido (S) | Atual Sref / V (C-CALC) | Original comprovado em save | Simulação de consequência atual | Ainda desconhecido no original |
| --- | ---: | --- | --- | --- |
| 1.000 | 2.169 / 667.800 | N/D | Força isolada não sobrescreve 1.000; transferência atual firma 2.169 | Piso anterior, evento e gatilho exatos |
| 2.169 | 2.169 / 667.800 | N/D | Transferência mantém coincidência com referência | Idem |
| 3.000 | 2.169 / 667.800 | N/D | Força isolada preserva 3.000; transferência pode reduzir para 2.169 | Idem |

Em nenhum dos três casos a existência de salário diferente autoriza recálculo retroativo ou altera V só pelo salário. Solicitação de aumento ainda depende de elegibilidade e de contador zero/não renovado; essa fixture de contador 10 não gera pedido automaticamente.

### Forças 1–50 e salário de referência

Atual: M, sem estrela, C-CALC. Colunas S: tabela condicional reproduzida por R1 §5.1 com I=2, R=1, C=5, sem piso salarial anterior. **Original comprovado por execução/save: N/D para todas as linhas.** Desconhecido em todas: parâmetros do save, momento do reajuste e regra exata de arredondamento. A coluna S não deve ser usada como tabela fixa aprovada de salário inicial.

| Força | Atual Sref | Original condicional (S) | Força | Atual Sref | Original condicional (S) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 222 | 500 | 26 | 1.668 | 13.750 |
| 2 | 227 | 1.000 | 27 | 1.786 | 14.250 |
| 3 | 236 | 1.500 | 28 | 1.909 | 14.750 |
| 4 | 249 | 2.000 | 29 | 2.037 | 15.250 |
| 5 | 267 | 2.500 | 30 | 2.169 | 15.750 |
| 6 | 289 | 3.000 | 31 | 2.307 | 16.500 |
| 7 | 314 | 3.500 | 32 | 2.449 | 17.000 |
| 8 | 345 | 4.000 | 33 | 2.597 | 17.500 |
| 9 | 379 | 4.500 | 34 | 2.749 | 18.000 |
| 10 | 418 | 5.000 | 35 | 2.906 | 18.500 |
| 11 | 462 | 5.500 | 36 | 3.068 | 19.250 |
| 12 | 510 | 6.000 | 37 | 3.235 | 19.750 |
| 13 | 562 | 6.750 | 38 | 3.407 | 20.250 |
| 14 | 619 | 7.250 | 39 | 3.584 | 20.750 |
| 15 | 681 | 7.750 | 40 | 3.766 | 21.500 |
| 16 | 747 | 8.250 | 41 | 3.953 | 22.000 |
| 17 | 818 | 8.750 | 42 | 4.145 | 22.500 |
| 18 | 894 | 9.250 | 43 | 4.342 | 23.000 |
| 19 | 974 | 9.750 | 44 | 4.544 | 23.500 |
| 20 | 1.059 | 10.500 | 45 | 4.751 | 24.250 |
| 21 | 1.148 | 11.000 | 46 | 4.963 | 24.750 |
| 22 | 1.243 | 11.500 | 47 | 5.180 | 25.250 |
| 23 | 1.342 | 12.000 | 48 | 5.402 | 25.750 |
| 24 | 1.446 | 12.500 | 49 | 5.629 | 26.500 |
| 25 | 1.554 | 13.000 | 50 | 5.861 | 27.000 |

Comparação de V por força, mantendo M/Cavalheiro/sem estrela/contador 10:

| Força | Atual V / Sref (C-CALC) | Original comprovado em save | Simulação | Ainda desconhecido |
| --- | ---: | --- | --- | --- |
| 1 | 15.000 / 222 | N/D | Não acrescentada | Preço final equivalente no original |
| 10 | 48.245 / 418 | N/D | Não acrescentada | Idem |
| 20 | 236.334 / 1.059 | N/D | Não acrescentada | Idem |
| 30 | 667.800 / 2.169 | N/D | Não acrescentada | Idem |
| 40 | 1.416.906 / 3.766 | N/D | Não acrescentada | Idem |
| 50 | 2.549.325 / 5.861 | N/D | Não acrescentada | Idem |

### Temporadas e efeito do contador contratual

Não se executou uma simulação de temporadas. As linhas seguintes mantêm os mesmos atributos e o mesmo contador; a independência da temporada decorre da assinatura e da expressão de C04.

| Temporada hipotética | Atual V / Sref (C-CALC, F30-base) | Original comprovado em save | Exemplo condicional (S) | Ainda desconhecido |
| --- | ---: | --- | --- | --- |
| 1 | 667.800 / 2.169 | N/D | Se I=2/R=1/C=5, salário candidato 15.750 | I real e salário contratado do save |
| 5 | 667.800 / 2.169 | N/D | Mesmos parâmetros mantidos → 15.750 | Não há série histórica para fixar I=2 |
| 10 | 667.800 / 2.169 | N/D | Mesmos parâmetros mantidos → 15.750 | Idem |
| 50 | 667.800 / 2.169 | N/D | Mesmos parâmetros mantidos → 15.750 | Idem |

O salário vigente e o preço negociado podem variar ao longo de uma partida mesmo com curva fixa. Isso não prova inflação do índice de referência.

| Contador atual, mesmos atributos F30-base | Atual V / Sref (C-CALC) | Original comprovado em save | Simulação | Ainda desconhecido |
| --- | ---: | --- | --- | --- |
| 0–2 | 500.850 / 2.169 | N/D | Variação isolada de K | Desconto contratual equivalente no original |
| 3–5 | 587.664 / 2.169 | N/D | Idem | Idem |
| 6–17 | 667.800 / 2.169 | N/D | Idem | Idem |
| ≥18 | 707.869 / 2.169 | N/D | Idem | Idem |

A mudança de temporada zera o contador no modo sazonal; portanto pode mudar V sem mudar força, salário ou país. É um efeito do modificador legado K, não um índice de inflação. Sua permanência na política sazonal exige decisão de produto.

## 8. Inconsistências encontradas

São achados por leitura de código/contratos/testes e raciocínio sobre cenários, não falhas novas reproduzidas em serviço nesta execução. Não foram corrigidos.

| ID / prioridade | Evidência e consequência | Cobertura/lacuna e decisão |
| --- | --- | --- |
| I01 — alta | C05:76 bloqueia geração se há qualquer PENDING do clube; C05:121 rejeita resposta de outra temporada; C03:635 não encerra/reemite pedidos. Um pedido humano não respondido que atravesse a temporada pode permanecer bloqueando novos pedidos. Modal main:1009 filtra clube/status, sem temporada, podendo voltar a apresentar pedido que o servidor rejeita. | Não foi localizada regressão para pedido pendente atravessando o fechamento real. Definir expiração, reemissão ou resposta retroativa; recomendar expirar com motivo e reavaliar sem alterar salário. |
| I02 — alta | C08:108 reserva vagas no lance humano; C04:767–804 atribui lance IA diretamente sem contar outras vagas já comprometidas. Caixa é reservado, mas vagas podem ser prometidas em excesso. | Liquidação limita a propriedade efetiva a 24; não é prova de elenco acima do máximo. Falta teste IA com 23 atletas e múltiplos arremates. Centralizar validação do lance mantendo estratégia separada. |
| I03 — alta | C13:208 protege vagas no COMPRAR AGORA; ExecuteTransfer em C04:857 verifica apenas elenco atual. Aceite de proposta e compra IA podem ocupar vaga comprometida por outro leilão. | Revalidar na liquidação evita 25º atleta, mas um compromisso anterior pode falhar. Falta cenário misto leilão+aceite bilateral/IA. Definir que compromisso reserva vaga e compartilhar checagem em todas as compras. |
| I04 — média | C10 mantém RenewalMatches=20 no pedido e compromisso; C05 aceita pelo restante da temporada e IA projeta 20. Documento salarial descreve pelo menos 20, superado pela política sazonal. | Diferenciar horizonte de planejamento de duração real; não implica débito duplicado. Acrescentar testes no último terço da temporada. |
| I05 — média | UI C17 apresenta salário do clube vendedor; C04:902 firma salário de referência na compra. Atleta de F30 com salário vigente 3.000 pode passar a 2.169 no exemplo da §7. | Doc 015A relata salários que coincidiram antes/depois; esses exemplos não provam conservação universal. Definir divulgação do salário pós-compra e regra de negociação. |
| I06 — média | K do valor justo deriva de contador em rodadas, enquanto posse e renovação são sazonais. Zerar contador na virada aplica K=0,75 a atletas que continuam empregados. | Não é erro aritmético nem inflação. Decidir manter compatibilidade ou mudar política somente com aprovação/versionamento. |
| I07 — média | C13:260 usa 0,92/1,12 e limiar 80% para resposta IA imediata; C04:604 usa 0,88/1,12 e 82% no lote. C04:678 não filtra renovação sazonal antes de avaliar/enviar oferta IA a humano. Execução final continua bloqueando renovado. | Assimetria/ruído de decisões, não venda ilegal demonstrada. Caracterizar os dois caminhos e decidir unificação de política; validações compartilhadas devem barrar alvo inválido antes de ofertar. |
| I08 — média | Documentos 012D dizem que insolvência inclui renovados, usa cooldown 2 e que recusa mínima sempre aceita. Código sazonal e testes posteriores protegem renovados, admitem próxima tentativa na rodada seguinte e distinguem humano/IA. | Usar política atual como baseline técnico; documentos antigos precisam de errata futura, sem reativar comportamento por engano. |
| I09 — média | AutoMode automatiza preparação esportiva; seleção financeira IA exige clube sem humano. Uma pendência salarial pode permanecer enquanto humano está automatizado. | AutoMode não é implementação completa de férias. Não ativar delegação financeira sem decisão. |
| I10 — média | Renovação sazonal permite luvas mesmo sem caixa e pode agravar crise; resposta salarial humana aceita aumento sem reserva estratégica obrigatória; IA usa reserva. | Comportamento atual, não defeito confirmado. Decidir se humano mantém autoridade para assumir compromisso e como a UI o informa. |
| I11 — baixa | Desconto de anúncio IA usa diferença de RoundNumber sem temporada e não incrementa TransferListingVersion no trecho do lote. Propostas do mercado são limitadas globalmente a 50 antes de filtro do cliente, C15:42. | Investigar anúncios atravessando temporada e ofertas humanas encobertas por volume. Preço/versão de sala ainda oferecem proteção; não foi demonstrada compra indevida. |
| I12 — média | Career GET processa vagas e salva; aceite não tem recibo financeiro de replay e salva com token de cancelamento da requisição, C13:412–458. | Lock protege concorrência local, mas perda de resposta não tem a mesma semântica de recibo de C14. Decidir contrato de repetição/consulta sem reescrever carreira existente. |

A inexistência de nacionalidade econômica, inflação global e B/D é divergência em relação ao estudo, não defeito automaticamente autorizado a corrigir. Igualmente, diferenças de pontuação do treinador e de rebaixamento são políticas atuais, não tarefas implícitas de implementação.

## 9. Regras comprovadas

### Projeto atual: baseline técnico a preservar até nova aprovação

- Seleção A/M e contagem de gols da Taça da Etapa 01 permanecem aprovadas. A usa força nominal + min(10, mérito); M usa só força nominal; algoritmo compartilhado humano/IA; mérito não aumenta força de campo.
- Separação de salário vigente e referência; evolução de força isolada não reescreve salário. Comportamento não entra diretamente na referência salarial atual.
- Renovação sazonal protege vendas/leilões na temporada atual e libera pela comparação com a temporada seguinte; posse e salário não terminam automaticamente com contador zero.
- Voluntário tem padrão 500 mil/2 rodadas, duração válida 1–3, mínimo alterável inclusive zero e centavos; prazo segue conclusão global.
- Controle de proprietário, fase, pronto, versão, recibo persistente e lançamento único nos comandos financeiros da aplicação.
- Transferência de atleta vinculado conserva o preço entre comprador/vendedor; contratação de livre não credita ex-clube.
- Elenco registrado: 11–24, pelo menos um goleiro e dez de linha. Isso não se confunde com atletas temporariamente aptos para escalar.
- Carreira preserva identidade, títulos, pontos e emprego; não inventa histórico legado ausente. Ranking lexicográfico é separado da tabela da Liga.
- Não há multiplicador geral temporal de inflação nos cálculos atuais; orçamento operacional tem política por mundo e atualização programada.

Esses itens são CÓDIGO e/ou TESTE ANTERIOR. A consolidação anterior contém, entre outras classes, 32 casos CoachCareer008Tests, 7 CoachCareer008IntegrationTests, 1 CoachCareer008ReadinessTests, 35 ClassicEconomy012DTests, 6 ClassicEconomy012DIntegrationTests, 4 SalaryRequestSafetyTests, 5 SeasonContractsAndDebtTests, 18 VoluntaryAuctionTests, 2 VoluntaryAuctionIntegrationTests, 13 MatchMarket015ATests, 15 OperatingEconomy012DFixTests, 4 respectivas integrações e 2 FinancialCommandDurability012DTests. Contagens de classes não são uma suíte nova nem devem ser somadas às 903 novamente.

### Original: o que foi efetivamente estabelecido

Identidade do executável foi conferida diretamente. R1 atribui operações estáticas aos endereços econômicos da §7 e distingue-as de relatos/hipóteses. Isso sustenta candidatos de pesquisa, sem comprovar uma cotação final obrigatória, equivalência integral de todas as nacionalidades, índice por temporada, reajuste por evento ou critérios completos de demissão.

Não foram obtidas novas observações dinâmicas do original. Os 903 casos do clone e os exemplos aritméticos não são comprovação de fidelidade histórica.

## 10. Regras pendentes de aprovação

### Aprovação, incorporação e proposta não são a mesma coisa

| Tema | Evidência documental/histórica | Classificação de autorização |
| --- | --- | --- |
| Preservar checkpoint, não implementar nesta execução | Pedido atual e relatório Etapa 01 | **Autorização explícita atual** para auditoria/documentação; implementação vedada. |
| A/M e gols da Taça | Autorização explícita da Etapa 01; d0489d3/2818cb3 | **Aprovado e fora de alteração** nesta proposta. |
| Leilão voluntário 500 mil/2, opções 1–3 | AUCTION_DEFAULT_CONFIG.md, commit 9690d95, código/testes | **Incorporado e documentado PASS**. R$500 mil é default, não piso. Não foi localizada aqui uma ata primária única de aprovação para cada parâmetro; não reinterpretar a referência do pedido como nova autorização de mudança. |
| Empréstimo clássico e separação salarial | CLASSIC_BANK_RESEARCH_012D.md declara parâmetros autorizados pelo usuário; 112ec75 | **Documentado como autorizado historicamente**, não como fórmula original comprovada. Preservar sem nova autorização para substituir. |
| Proteção sazonal e pedido persistente | c7a4cad de 02/11 e c9a91a8 de 05/11, código e testes posteriores | **Baseline incorporado e validado**. Prevalece na descrição do estado atual sobre prosa antiga; unificação de política exige decisão. |
| Carreira 008, pesos e moral | COACH_CAREER_AUDIT_008, COACH_MORALE_RULES_008, COACH_INVITATION_AI_008, COACH_RANKING_008 | **Implementado/documentado como política do projeto**. Isso não comprova origem no EXE nem autoriza novos critérios nesta missão. |
| Curvas originais, B/D, nacionalidade, inflação, férias financeiras | R1 §§5/6/9/10 e sugestões da auditoria anterior | **Pesquisa/proposta, sem autorização de implementação**. |
| Expirar pendência antiga, compartilhar validações faltantes, alterar compromisso de 20, prévia salarial | Achados desta auditoria | **Proposta nova**, depende de aprovação do módulo e comportamento esperado. |

Um commit ou status PASS prova incorporação/validação técnica, não substitui autorização primária do proprietário para alterar uma regra econômica. Quando esta auditoria não reencontrou a autorização primária, isso está indicado; não se afirma que ela nunca existiu.

### Decisões de comportamento ainda abertas

1. Pedidos pendentes na virada: expirar e reavaliar, transportar com nova identidade/valor ou permitir resposta ao anterior.
2. Mínimo de elenco: conservar recusa humana bloqueada e conversão automática da IA, ou usar uma experiência/decisão igual para ambos.
3. Horizonte 20: reserva estratégica explicitamente nomeada ou compromisso real pelo restante da temporada.
4. Contratação: salário atual/referência/negociado; informação necessária antes de confirmar compra e lance.
5. Valor justo: conservar curva atual e K ou adotar política alternativa versionada. Não transferir fórmula do estudo diretamente para todos os preços.
6. Nacionalidade: adiar ou introduzir apenas classificação econômica com país individual confiável, tabela direcional de equivalências e política para desconhecido; não derivar de nome ou clube.
7. Inflação: conservar ausência atual ou pesquisar/definir política explícita; decidir inicialização, escala, eventos e efeitos antes de qualquer índice.
8. Leilões: manter modalidades e defaults atuais; decidir uniformidade do incremento legado, prazo salarial e proteção dos renovados antes de modificar.
9. IA: igualdade dos validadores e limites legais; manter ou revisar separadamente estratégias privadas de preço/reserva/necessidade.
10. Carreira: manter pontos/divisões/moral/rebaixamento atuais ou aprovar mudanças específicas, sem recalcular conquistas antigas.
11. AutoMode: manter automação esportiva ou aprovar delegação financeira com poderes, decisões pendentes e retomada definidos.
12. Compatibilidade: política nova só em novos mundos, adesão explícita de existentes ou atualização programada em fronteira definida. Não converter saldos/contratos antigos silenciosamente.

## 11. Proposta de módulos

Todos os módulos abaixo são **candidatos, não implementados**. A existência de uma dependência não autoriza executar seu sucessor. Migração de snapshot significa transformação versionada do JSON; não implica migration EF ou SQL. Não há necessidade comprovada de alterar esquema relacional para os ajustes de validação descritos.

### M01 — Ciclo de pedidos e renovação sazonal

- **Objetivo:** resolver I01 e esclarecer I04/I08, preservando vínculo, salário vigente e fatos históricos.
- **Baseline já incorporado:** um pendente por clube; resposta autoritativa/idempotente; proteção sazonal; contador não é posse.
- **Decisões pendentes:** política de virada, estado/motivo de expiração, recusa mínima por ator e significado do horizonte 20.
- **Arquivos prováveis:** C05/C06/C10/C03, contratos de C16, C17/main e modal; testes SalaryRequestSafety, SeasonContractsAndDebt e integrações financeiras.
- **Migration:** nenhuma EF demonstrada. Se criado estado EXPIRED, preservar números existentes do enum, serialização antiga e resolução auditável; transformação de snapshot só se necessária e aprovada.
- **Riscos:** desbloquear pedido duas vezes, perder gatilho ou aceitar aumento retroativo; mudar salário ao carregar save.
- **Testes:** pendente cruza fechamento real, reload/replay, último jogo, promoção, transferência, elenco mínimo, humano/IA e resposta antiga concorrente.
- **Dependências:** decisão do proprietário sobre expiração/renovação; não depende de substituir fórmulas.
- **Aceitação:** nenhum pedido impossível de responder bloqueia indefinidamente o clube; uma resolução por pedido; salário/posse/histórico só mudam pelo comando aprovado.

### M02 — Validações comuns de compra, oferta e lance

- **Objetivo:** fechar I02/I03 e invalidar previamente alvos inelegíveis de I07, com a mesma regra legal para humano/IA.
- **Baseline já incorporado:** dinheiro real, reserva de caixa, máximo 24, piso do vendedor, renovação protegida e execução reconciliada.
- **Decisões pendentes:** quais compromissos reservam vaga/dinheiro; tratamento de compromisso legado incompatível. Recomenda-se manter ofertas bilaterais sem reserva e revalidar no aceite, preservando contrato atual.
- **Arquivos prováveis:** C04/C07/C08/C09/C13/C14; regras de elenco e testes de mercado/leilão/durabilidade.
- **Migration:** não prevista; regras de validação podem usar estado já existente. Se mudar política de reservas antigas, exige plano explícito de compatibilidade.
- **Riscos:** rejeitar liquidação do próprio lance por contá-lo duas vezes; reservar jogador ou caixa incorretamente; interferir na invalidação de pronto após venda.
- **Testes:** 23 atletas e dois leilões, compra direta/aceite/IA com vaga comprometida, novo lance substitui anterior, zero, insuficiência, último goleiro, simultaneidade, reenvio e falha de salvamento.
- **Dependências:** nenhuma fórmula nova; decisão sobre reserva.
- **Aceitação:** todos os caminhos aplicam os mesmos limites legais; lance/compra usa exclusão do compromisso que está liquidando; nenhuma duplicação de atleta/dinheiro ou perda silenciosa de reserva.

### M03 — Clareza de preço e contrato na interface

- **Objetivo:** resolver I05 e reduzir ambiguidade de I04/I09/I10, sem recalcular dinheiro no Flutter.
- **Baseline já incorporado:** servidor autoritativo, UI já oferece preço fixo/proposta/contraproposta/leilão, modal e renovação sazonal.
- **Decisões pendentes:** salário que será firmado e eventuais poderes de AutoMode; preservar inicialmente delegação esportiva se nada adicional for aprovado.
- **Arquivos prováveis:** C13/C15/C16/C17/C18; DTOs e testes Flutter de mercado, modal, leilão e referência financeira.
- **Migration:** não prevista para campos derivados de prévia; se persistir consentimento/negociação salarial, reavaliar snapshot e recibos.
- **Riscos:** prévia divergir de execução após mudança de força/estado; texto implicar recusa garantida ou prazo de 20 inexistente.
- **Testes:** salário vigente menor/igual/maior que pós-compra, preço distinto de V, contrato sazonal/legado, minimum zero/centavos, cancelamento, chave repetida, modal antiga, 320/390/1024/1360 e acessibilidade dos botões.
- **Dependências:** M01 para semântica de pedidos; não precisa de novas curvas.
- **Aceitação:** confirmação expõe custo real e salário aplicável fornecidos pelo servidor, com atualização/rejeição se condições mudarem; mensagem de recusa compatível com servidor.

### M04 — Política explícita de valor e salário

- **Objetivo:** somente após decisão, manter política atual documentada ou introduzir alternativa econômica versionada que separe base, referência salarial e preço final.
- **Baseline já incorporado:** curvas atuais, salário contratado preservado na evolução e ausência de inflação geral.
- **Decisões pendentes:** curva/escala, posição/estrela em salário, K contratual, B/D, pisos/quantização do estudo e efeitos nos novos contratos. A opção de manter tudo atual é válida.
- **Arquivos prováveis:** C04/C05/C10/C15/C16, pontos de criação WorldSeedFactory e catálogo; cálculos de referência nos pontos existentes, sem alterar evolução de força.
- **Migration:** campos opcionais de versão/política no snapshot se houver alternativa; nenhuma EF comprovada. Saves antigos conservam versão e salários; não recalcular ledger/histórico.
- **Riscos:** grande mudança de escala monetária, redução de patrimônio, alteração involuntária de folha ou de thresholds IA.
- **Testes:** forças 1–50, seis comportamentos, estrela/posição, salário anterior, limites de contador, precisão, transferência/renovação e snapshots antigos sem campos.
- **Dependências:** escolha formal de política e protocolo de evidência do original. Não depende de mudar motor esportivo.
- **Aceitação:** funções e unidades documentadas; política antiga reproduz baseline; alternativa só afeta mundos/operações expressamente aprovados.

### M05 — Nacionalidade econômica opcional

- **Objetivo:** se aprovado, representar país do atleta e classificação econômica para compra, separada da procedência do catálogo.
- **Baseline já incorporado:** atualmente não há sobretaxa/cota econômica por nacionalidade.
- **Decisões pendentes:** introduzir ou adiar; país desconhecido; fonte da nacionalidade individual; tabela de equiparação; comprador/vendedor; modalidades e estágio do preço; compatibilidade. Não inclui efeito esportivo, cotas ou táticas.
- **Arquivos prováveis:** C01/C20, contratos/catalogo, C04/C13/C15/C17 e importação autorizada.
- **Migration:** provável evolução aditiva de snapshot se criar nacionalidade própria; preenchimento antigo como desconhecido, nunca presumido pelo clube. Migration EF apenas se futura necessidade de consulta relacional for comprovada.
- **Riscos:** classificar brasileiros/portugueses por suposição, cobrar sobretaxa duas vezes ou alterar preço anunciado livremente.
- **Testes:** país comprador/jogador/vendedor variando independentemente; equivalência direcional; desconhecido; transferência preserva país; compra fixa versus oferta/leilão; sem efeito em A/M, setores ou gols.
- **Dependências:** decisão e tabela verificável; M04 se preço-base mudar. Pode ser inteiramente adiado.
- **Aceitação:** dado explícito e rastreável, uma classificação por operação, estágio do preço aprovado e casos desconhecidos compatíveis com save antigo.

### M06 — Contrato operacional dos leilões e estratégia IA

- **Objetivo:** consolidar política por motivo, prazos e validações; caracterizar e, somente se aprovado, unificar heurísticas de resposta IA de I07 e anúncio de I11.
- **Baseline já incorporado:** quatro enums existentes, defaults voluntários 500 mil/2, duração 1–3, prazo global, destinos contábeis distintos e nenhum comprador artificial.
- **Decisões pendentes:** incremento legado, prazo do salarial, proteção em dívida, política de anúncio/preço, limiares de resposta e alcance dos limites por lote. Recomenda-se preservar parâmetros atuais enquanto M02 corrige apenas validação legal.
- **Arquivos prováveis:** C04/C07/C08/C10/C12/C13/C15/C16, configurações de C17 e testes de leilões/IA.
- **Migration:** não prevista para padronização documental/validadores. Mudança de política deve deixar leilões abertos com condições originais; novas versões opcionais se necessário.
- **Riscos:** transformar default em piso, alterar leilão automático antigo, dar receita de livre ao ex-clube ou mudar prazo por reconexão.
- **Testes:** quatro motivos, criado antes/depois dos resultados, cruzamento sazonal, cents/zero, vendedor mínimo, força 50 vendável quando elegível, renovado protegido, sem comprador, reserva e repetição após reinício.
- **Dependências:** M02; qualquer nova estratégia exige aprovação separada de objetivos e métricas.
- **Aceitação:** uma venda/lançamento; mesma validação legal humano/IA; prazos/condições já abertos preservados; diferença estratégica explicitamente documentada.

### M07 — Contrato de carreira e consistência da consulta/resposta

- **Objetivo:** preservar continuidade e tornar explícitas política de demissão/convite e repetição, incluindo I12; não criar novas funcionalidades por inferência.
- **Baseline já incorporado:** identidade persistente, moral, pontos/títulos, emprego, exclusividade e interino.
- **Decisões pendentes:** manter regras esportivas atuais, eventual paridade de rebaixamento, semântica de consulta/replay e alcance de AutoMode. Reputação própria, férias financeiras, remoção administrativa, salários e multas de técnico ficam adiados salvo autorização específica.
- **Arquivos prováveis:** C01/C02/C03/C13/C16/C18, orquestrador apenas nos pontos de carreira e testes CoachCareer008/Readiness/career_api.
- **Migration:** nenhuma para preservar regras/ajustar consulta e repetição com dados atuais; eventuais recibos/versionamento de carreira exigiriam evolução aditiva do snapshot, sem inventar histórico.
- **Riscos:** alterar titular do jogo retroativamente, duplicar título/pontos, atribuir expulsão da sala à demissão, mudar desempate ou permitir salto de clube por reconexão.
- **Testes:** demissão por moral/rebaixamento, empregado/desempregado, recusa/expiração/aceite simultâneo, perda de resposta/reinício, convite vencido, fonte dos pontos/títulos e não duplicação por leitura.
- **Dependências:** decisões de carreira; independente de nacionalidade e novas fórmulas.
- **Aceitação:** mesmos agregados antigos, vínculo único, histórico fechado corretamente e resposta repetida com resultado definido; leitura não cria fatos inesperados conforme contrato aprovado.

### M08 — Evidências econômicas e compatibilidade de políticas

- **Objetivo:** medir consequências das escolhas aprovadas, diferenciando inflação de referência, composição dos atletas, folha, preços e caixa.
- **Baseline já incorporado:** ledger com chaves, políticas comerciais por mundo e medições históricas separadas.
- **Decisões pendentes:** se haverá índice econômico novo; unidade/escala, inicialização, atualização, abrangência e adesão de mundos. Não incluir mudança de estádio, ingresso, bilheteria ou calendário nesta etapa.
- **Arquivos prováveis:** testes de economia/simulação, contratos de diagnóstico existentes, documentação; C10/C11 somente se uma política adicional for expressamente aprovada.
- **Migration:** nenhuma para medir; eventual índice persistente requer versão/default explícitos do snapshot, sem backfill monetário ou migration EF presumida.
- **Riscos:** confundir valorização por força/K com inflação, concluir equilíbrio por conservação de uma venda, reusar resultado de simulação antiga como se fosse da nova política.
- **Testes:** controles de qualidade fixa e população variável, múltiplas seeds, humanos/IA, temporadas 1/5/10/25/50/100, dívida/juros/compromissos, mundos antigos e reload.
- **Dependências:** apenas os módulos de política que forem aprovados; caracterização atual pode preceder novas escolhas.
- **Aceitação:** evidência reproduzível identifica versão, seed, mundo, horizonte e componentes; não altera saldos existentes; nenhuma declaração de fidelidade original sem medição apropriada.

## 12. Ordem recomendada de implementação

1. Proprietário decide o recorte inicial e as políticas da §15. Esta proposta não inicia a sequência.
2. M01 e M02: ciclo de pendências e validação de compromissos; independentes de trocar curvas e de regras esportivas.
3. M03: alinhar informação/consentimento da UI às regras aprovadas; regressão nas resoluções existentes.
4. M06: documentação única e caracterização de modalidades/estratégia; alterações de coeficientes somente se aprovadas.
5. M07: consistência de consulta/replay e definição de carreira, com alterações esportivas de carreira restritas à autorização específica.
6. M04 e M05: opcionais, após decisões sobre fidelidade histórica e compatibilidade; nacionalidade não precisa bloquear correções dos validadores.
7. M08: caracterização antes das mudanças de política e comparação final após cada módulo aprovado.

Recomendação de recorte inicial: ciclo salarial, validadores e prévias da interface, mantendo as curvas, ausência de inflação/nacionalidade econômica e critérios de carreira atuais. Isso é recomendação para aprovação, não autorização executiva.

Cada módulo futuro deve ter implementação e revisão próprias, sem misturar outras etapas e sem modificar os commits já concluídos.

## 13. Riscos de regressão

| Risco | Proteção proposta para futura implementação |
| --- | --- |
| Save antigo mudar ao abrir | Defaults compatíveis/versionados; fixtures reais anonimizadas e cópias descartáveis; não renegociar salário, reatribuir país ou reconstruir passado por inferência. |
| Contabilidade duplicada | Débito/crédito com mesma identidade, recibos duráveis, reserva excluindo a própria operação, replay após salvar/falhar/reiniciar. |
| Venda comprometer elenco/pronto | Validar todas as modalidades com saídas e entradas reservadas, proprietário e renovação; conservar revalidação de pronto já existente. |
| Carreira perder autoria | Captura no início, títulos/pontos idempotentes, emprego anterior encerrado e histórico preservado. |
| Mudança econômica alterar resultados indiretos | Não modificar fórmulas esportivas; comparar comportamento econômico e elenco sem exigir placares iguais após transferências legitimamente diferentes. |
| Compatibilidade de enum/DTO | Conservar valores 0–3 dos motivos de leilão e 0–2 dos estados salariais; novos campos opcionais e leitores antigos caracterizados. |
| Custo salarial oculto | Prévia calculada no servidor, conferência da versão e regra de salário aprovada; cliente não calcula preço/salário para substituir servidor. |
| Confundir análise com aprovação | Matriz de decisão, versão da política e aceitação do módulo antes de implementação; exemplos S permanecem fora do código produtivo. |
| Alegar equilíbrio/fidelidade sem prova | Identificar seed/política/período, repetir somente testes pertinentes e separar clone, estudo estático e execução original. |

Preservação obrigatória: motor de gols/força efetiva, A/M, táticas/formações não aprovadas, evolução de força, estádios/bilheteria, calendário e toda a Etapa 01. Os pontos citados de C03/C04 não autorizam alterar funções vizinhas fora do módulo aprovado.

## 14. Critérios de aceitação

### Esta entrega documental

- [x] Código .NET/Flutter, testes existentes, documentação e histórico inspecionados.
- [x] Estado atual, caminhos humano/IA, lacunas e diferenças descritos por sistema.
- [x] Referência original identificada, com hash/limites de evidência e tabelas condicionais separadas.
- [x] Valores atuais calculados sem banco/save e sem afirmar nova simulação de temporadas.
- [x] Regras incorporadas, autorizações documentadas e propostas novas distinguidas.
- [x] Módulos com objetivo, baseline, decisões, arquivos, migration, risco, testes, dependências e aceitação.
- [x] Nenhuma implementação, migration, alteração de banco/save, commit ou push nesta execução.
- [x] Relatório da Etapa 01 e seus commits preservados.

### Uma implementação futura, somente após aprovação

- Cenários I01–I03 reproduzidos em ambiente descartável, corrigidos conforme decisão e testados por todos os caminhos.
- Testes relacionados e suíte .NET completa; Flutter analyze; suíte Flutter completa e resoluções 320/390/1024/1360 conforme telas afetadas.
- Regressões específicas de A/M humano/IA, pesos/teto/empate/elegibilidade e gols Liga/Taça continuam passando, sem mudar suas regras.
- Transferências/leilões com saldo, preço, propriedade, mínimo/máximo, zero/centavos e compromissos abertos; uma liquidação e recibo idempotente.
- Pedidos na virada, transferência, promoção, erro/reenvio/reinício e elenco mínimo com política aprovada.
- Renovação sazonal preserva posse/carreira e impede venda na temporada protegida; temporada seguinte libera conforme política; legado continua legível.
- Carreira não duplica resultados/títulos e não perde emprego/histórico por consulta, reconexão ou resposta perdida.
- Se mudar fórmula/nacionalidade/inflação: política versionada, evidência e escala aprovadas, testes comparativos independentes e plano para mundos existentes.
- Nenhuma migration EF sem necessidade demonstrada, revisão e estratégia não destrutiva. Nenhum teste em banco de partida real.
- Contagens/resultados novos registrados no relatório da implementação correspondente; números herdados não reutilizados como execução nova.

Não há status PASS de implementação da Etapa 02 a declarar. O checkpoint anterior permanece PASS; este documento permanece proposta.

## 15. Decisões necessárias do proprietário

| Decisão | Alternativas para aprovação | Recomendação e consequência |
| --- | --- | --- |
| D01 — recorte inicial | M01–M03; todos os módulos; seleção diferente | Aprovar primeiro ciclo salarial, validadores e prévias. Permite corrigir consistência sem trocar a economia inteira. |
| D02 — pedido antigo | Expirar com motivo e reavaliar; transportar; aceitar anterior | Expirar e reavaliar na temporada nova, sem aumento automático ou dupla resolução. Exige definir gatilho e compatibilidade do estado. |
| D03 — mínimo do elenco | Preservar humano bloqueado/IA obrigatória; mesma resposta para ambos | Preservar regra atual inicialmente, explicando a restrição humana. Uniformização posterior precisa de comportamento definido. |
| D04 — horizonte 20 | Reserva estratégica; duração real restante | Usar duração real em dados contratuais e, se desejado, campo distinto para reserva IA. Não debitar compromisso futuro integral. |
| D05 — salário ao contratar | Referência atual; salário do vendedor; negociação nova | Preservar regra atual e apresentar salário pós-compra do servidor. Negociação salarial nova amplia o escopo e requer autorização própria. |
| D06 — curvas e contrato no valor | Manter V/Sref/K atuais; política alternativa | Manter enquanto se decide escala e fidelidade. Retirar K ou adotar fórmula do estudo afeta patrimônio/orçamento e exige versionamento. |
| D07 — nacionalidade | Adiar; classificação econômica opcional | Adiar até haver dado individual confiável, tabela de equivalências e caminho de preço aprovado. Sem cotas nem efeito esportivo implícito. |
| D08 — inflação | Ausência atual; pesquisa dinâmica; índice novo de design | Manter ausência atual e pesquisar antes de escolher parâmetros. O 20 exibido no original não é taxa anual aprovada. |
| D09 — leilões e IA | Manter motivos/defaults/heurísticas; mudar itens nominados | Preservar condições abertas e defaults; aprovar igualdade dos validadores. Mudança dos limiares 80/82%, desconto, incremento legado ou duração deve ser explícita. |
| D10 — carreira e AutoMode | Preservar regras; alterar critérios específicos; delegar finanças | Preservar pontos/moral/rebaixamento atuais; tratar consulta/replay separadamente. Férias financeiras, reputação própria e multas ficam adiadas. |
| D11 — mundos existentes | Só novos; adesão explícita; mudança programada | Novas políticas econômicas opcionais com adesão explícita; ajustes de integridade sem reescrever dinheiro/histórico, após caracterizar compatibilidade. |

Aprovar esta documentação ou escolher um módulo não aprova automaticamente alternativas dos demais. O próximo comando de implementação deve nomear os módulos e resolver suas decisões pendentes.

**Status final: PROPOSTA PARA APROVAÇÃO — NÃO IMPLEMENTADA.** Branch main e checkpoint da Etapa 01 preservados; nenhuma funcionalidade da Etapa 02 foi implementada.
