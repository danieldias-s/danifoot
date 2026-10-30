# Mercado Bilateral e Inteligência Econômica dos Clubes (`MarketAndEconomyEngine`)

O sistema de mercado em `src/Elifoot.Domain/MarketAndEconomyEngine.cs` permite negociações bilaterais completas entre humanos e IAs: anúncio na lista de transferências, compra direta, proposta/contraproposta e leilões de fim de contrato.

## 1. Curva Convexa de Valor Justo (`CalculateFairValue`)

Para evitar distorções ou valores externos incompatíveis com a economia interna do jogo, o valor justo (`FairValue`) de um atleta de força $S \in [1, 50]$ é calculado por uma curva convexa:

$$\text{Base}(S) = 12\,000 + 76 \cdot S^{2.65}$$

$$\text{FairValue} = \text{Base}(S) \times \text{StarMod} \times \text{PositionMod} \times \text{BehaviorMod} \times \text{ContractMod}$$

- **Exemplos de calibração**:
  - Força `11` (sem `*`): ~`R$ 55.800`
  - Força `25` (sem `*`): ~`R$ 397.000`
  - Força `38` (sem `*`): ~`R$ 1.180.000`
  - Força `48*` (Atacante `Fair Play`): ~`R$ 3.420.000`
- A IA ordena alvos de compra maximizando a utilidade marginal por unidade monetária:
  $$U = \frac{\text{Strength} + (\text{IsStar} ? 6 : 0)}{\text{Price}}$$
  dentro de um shortlist de até seis candidatos por posição. Jogadores listados têm precedência sobre não listados; isto não constitui prova de uma escolha global ótima. O alvo precisa atender ao limiar de força da necessidade, ao orçamento e ao teto `Price <= 1.25 * FairValue`. Em 007, o exemplo força 11 por R$ 2 milhões foi rejeitado em teste controlado; nenhuma contratação da amostra excedeu esse teto. Não houve mudança na ordenação ou nos preços.

As compras por treinador incluem bilaterais e arremates pagos, separadamente dos lances e das tentativas. Cada nova compra grava os clubes, jogador, treinador responsável naquele momento, mecanismo, valor, temporada e rodada no lançamento financeiro canônico, com a justificativa e comparação com titular/reserva. `AiMarketMetrics.FromRecords` deriva os nove indicadores dos fatos, sem contadores independentes. Leilões de contratos expirados não têm clube vendedor; o ex-clube não recebe o lance. Contratação sem lance é registrada como `FreeAgentSigning`, de valor zero, e não como compra ou leilão vencido. Histórico completo de uma auditoria deve usar o arquivo arquivado: snapshots do mundo mantêm uma janela recente e não representam toda a carreira.

## 2. Operações Bilaterais e Leilões

1. **Lista de Transferências**:
   - Clubes de IA anunciam até 2 jogadores excedentes (`SurplusPlayerIds`) por semana e reduzem o preço pedido em `10%` caso o atleta permaneça 3 rodadas sem propostas.
   - Humanos podem comprar imediatamente atletas listados por IA, e IAs compram atletas listados por humanos se preencherem uma lacuna do `ManagerSquadPlan`.
2. **Propostas e Contrapropostas (`TransferOffer`)**:
   - Humanos e IAs podem enviar propostas diretas por qualquer jogador (desde que a venda não viole o mínimo de `11` atletas ou o único goleiro `G` do vendedor).
   - Se a oferta atingir o valor mínimo aceitável do clube de IA, é aceita imediatamente; se ficar entre `80%` e `100%` do mínimo, a IA responde com uma **contraproposta (`Countered`)**.
3. **Leilões (`AuctionItem`)**:
   - Quando o contrato de um atleta chega a `0` rodadas e não é renovado, ele entra em leilão público por 2 rodadas.
   - Cada clube de IA interessado calcula um `PrivateAiMaxBid` privado baseado no `FairValue` e no seu orçamento (`MaxSingleSigningBudget`), evitando loops artificiais de inflação contra humanos.
