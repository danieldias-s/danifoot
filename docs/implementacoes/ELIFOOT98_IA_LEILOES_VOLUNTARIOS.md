# Leilões voluntários da IA

**Status PASS.** Implementação validada por testes automatizados e simulações.

## Auditoria

O lote semanal em `MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch` já atualizava o plano de elenco, anunciava excedentes a preço fixo, processava propostas, comprava jogadores e dava lances. A publicação voluntária entrava por `ClubAndMarketApplicationService.CreateVoluntaryAuctionAsync` e usava `VoluntaryAuctionEngine.Create`. A reserva do jogador, a proteção de elenco, os lances e a liquidação passavam pelas validações compartilhadas do mercado.

O fluxo de insolvência em `FinancialInsolvencyEngine` registra uma venda quando o jogador ainda pertence ao clube. O fluxo de recusa salarial em `SalaryContractsEngine` coloca um atleta livre no mercado. Uma troca de treinador altera o controle do clube e conserva os leilões em aberto. A nova estratégia consulta o controle atual e identifica quem abriu cada leilão para aplicar a cota ao clube certo.

## Estratégia

- O servidor avalia a publicação no fechamento financeiro de cada rodada global. A IA considera leiloar quando tem pelo menos três excedentes elegíveis, quando o caixa disponível está abaixo da reserva do plano ou quando o jogador está anunciado há pelo menos três rodadas concluídas.
- A seleção exige que o plano de elenco classifique o jogador como excedente. Titulares, craques, atletas com propostas em aberto e jogadores impedidos pelas regras de venda ficam fora da seleção. Entre os candidatos, a IA prioriza quem está fora do banco e quem tem menos minutos registrados pelo clube na temporada.
- A projeção usa os clubes que o lote de lances já considera. Ela calcula a capacidade de compra com base na necessidade da posição ou no ganho de força, nas vagas disponíveis, no teto do plano e no caixa livre depois da reserva. O retorno estimado é o lance mínimo quando há um interessado e o segundo maior teto quando há pelo menos dois. A IA compara essa estimativa ao preço fixo ajustado pelo tempo no mercado ou pela urgência.
- O lance mínimo parte do maior valor entre 88% do preço fixo e um piso sobre o valor justo. Esse piso começa em 96% do valor justo, ou 92% quando há urgência. Ele cai seis pontos para anúncios antigos e dois pontos quando há ao menos três excedentes elegíveis, e sobe dois pontos para reservas do banco. O sistema arredonda o valor em reais. O anúncio humano continua com o padrão de R$ 500.000.
- O prazo é de uma rodada com urgência, três quando há dois interessados capazes de sustentar retorno acima do preço fixo e duas nos demais casos. A publicação usa o mesmo motor de leilões dos treinadores humanos, que retira o anúncio fixo e mantém o jogador no clube até a liquidação.
- Cada clube pode manter um leilão voluntário aberto pela IA e precisa esperar duas rodadas concluídas entre aberturas. Os limites atravessam a virada de temporada. A identidade da operação por clube, temporada e rodada e o registro do lote evitam publicações duplicadas. A janela de encerramento usa o calendário já aplicado aos leilões humanos.

## Simulação

Foram comparadas duas sementes antes e depois da implementação. Cada execução teve 21 rodadas oficiais, 351 partidas simuladas pelo motor e 128 clubes sob controle da IA, incluindo os distritais. O mercado executou a cada rodada, na mesma ordem do servidor. O total de anúncios a preço fixo inclui os anúncios iniciais.

| Indicador | Antes · 199801 | Depois · 199801 | Antes · 199802 | Depois · 199802 |
| --- | ---: | ---: | ---: | ---: |
| Anúncios a preço fixo | 147 | 156 | 166 | 182 |
| Vendas a preço fixo | 78 | 83 | 79 | 74 |
| Negociações diretas | 90 | 85 | 89 | 94 |
| Leilões voluntários abertos | 0 | 27 | 0 | 47 |
| Leilões com lances | 0 | 27 | 0 | 47 |
| Leilões concluídos com venda | 0 | 24 | 0 | 37 |
| Leilões encerrados sem comprador | 0 | 0 | 0 | 0 |
| Leilões ainda em curso ao fim da amostra | 0 | 3 | 0 | 10 |
| Receita dos vendedores em leilão | R$ 0 | R$ 12.878.686 | R$ 0 | R$ 16.415.224 |
| Violações de elenco | 0 | 0 | 0 | 0 |

Os débitos e créditos das 61 vendas em leilão foram reconciliados. Os testes da simulação conferiram a cota de anúncios ativos, o intervalo entre aberturas e a ausência de anúncio fixo simultâneo para o mesmo jogador.

## Testes

| Suíte | Executados | Aprovados | Reprovados |
| --- | ---: | ---: | ---: |
| Domínio, mercado, economia e contratos | 142 | 142 | 0 |
| Aplicação e sincronização multiplayer | 34 | 34 | 0 |
| Integração HTTP, SignalR, SQLite e PostgreSQL de QA | 45 | 45 | 0 |
| Simulação após a mudança | 2 | 2 | 0 |
| Total | 223 | 223 | 0 |

Os números consideram testes distintos e o resultado da última execução de cada caso. As duas execuções usadas para obter a referência anterior são adicionais. Os arquivos TRX e os resultados por semente estão em `artifacts/ai-voluntary-auctions`.

## Arquivos

| Arquivo | Alteração |
| --- | --- |
| `src/Elifoot.Domain/AiVoluntaryAuctionEngine.cs` | Seleção, projeção de demanda, lance mínimo, duração e cota. |
| `src/Elifoot.Domain/MarketAndEconomyEngine.cs` | Integra a decisão ao lote semanal e compartilha os cálculos de preço e teto de lance. |
| `src/Elifoot.Domain/VoluntaryAuctionEngine.cs` | Publica leilões da IA pelo mesmo fluxo de validação e reserva. |
| `src/Elifoot.Domain/ClassicEconomyState.cs` e `Entities.cs` | Registram a rodada processada, a autoria da abertura e a idade do anúncio fixo. |
| `src/Elifoot.Domain/WorldSeedFactory.cs` e `src/Elifoot.Application/ClubAndMarketApplicationService.cs` | Inicializam a idade de anúncios de mundos novos e de anúncios manuais. |
| `tests/Elifoot.Domain.Tests/AiVoluntaryAuctionTests.cs` | Cobre escolha de modalidade, lances da IA, proteções e ciclo do leilão. |
| `tests/Elifoot.Integration.Tests/VoluntaryAuctionIntegrationTests.cs` | Cobre publicação pelo relógio da sala, dois clientes, lance humano, reinício e liquidação. |
| `tests/Elifoot.Simulation.Tests/AiVoluntaryAuctionSimulationTests.cs` | Mede o mercado em temporadas completas e verifica invariantes. |
| `docs/implementacoes/ELIFOOT98_IA_LEILOES_VOLUNTARIOS.md` | Registra a auditoria e a validação. |

O histórico de idade de anúncios anteriores à atualização usa a rodada registrada no save, que pode corresponder ao último reajuste de preço. A previsão de demanda usa os clubes consultados pela IA na rodada. Os treinadores humanos podem participar dos mesmos leilões pelo mercado.
