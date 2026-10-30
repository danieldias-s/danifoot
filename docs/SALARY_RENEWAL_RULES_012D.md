# Salários negociados e propostas — 012D

`SalaryPerRound` representa o salário contratado e efetivamente debitado quando o clube joga. `ReferenceSalaryPerRound` representa a referência calculada a partir da força, posição e estrela. Evolução de força e lesões atualizam a referência e o valor de mercado; preservam o salário vigente.

A referência é derivada dos atributos atuais, evitando cache salarial obsoleto após edição/importação. O valor de referência que fundamentou uma proposta permanece registrado no pedido; alterações futuras de força não reescrevem esse histórico nem o salário contratado.

Novos contratos por transferência, aceitação salarial, renovação preventiva ou renovação no vencimento registram salário anterior, salário acordado, duração, motivo, luvas e compromisso futuro. Um contrato negociado permanece protegido durante sua vigência. A migração protege contratos vigentes antigos por prudência, pois o histórico de negociação anterior não é recuperável.

Ganho efetivo de força e promoção podem registrar elegibilidade. Promoções valem na temporada seguinte, depois da troca de divisão. Há no máximo um pedido por clube por rodada e no máximo um pedido pendente por clube. A seleção é determinística: maior defasagem relativa, maior ganho de força e ID estável. A elegibilidade consumida não é recriada por repetição do mesmo processamento.

O pedido persistente tem `PENDING`, `ACCEPTED` ou `REFUSED`, motivo, gatilho, salário vigente/proposto/referência, rodada, temporada e resolução. A proposta é limitada a 10% de aumento, ou 5% em caixa negativo, e à referência adaptada da divisão (1ª 110%; 2ª 105%; 3ª 100%; 4ª 95%; Distrital 90%). Esses percentuais são escolhas explícitas de projeto. Não há indexador anual de inflação.

Aceitar estabelece o novo salário e pelo menos 20 partidas de contrato. Não existe crédito artificial de caixa nem cobrança duplicada do compromisso futuro. A folha é paga somente quando as partidas forem disputadas. Recusar exige nova verificação do elenco registrado: se a remoção deixaria menos de 11, um goleiro ou dez jogadores de linha, o sistema aceita a renovação obrigatória e informa `RENOVAÇÃO OBRIGATÓRIA: ELENCO MÍNIMO.`, mesmo com caixa negativo.

Se a saída for legal, o atleta fica livre e entra em leilão inicial zero. O ex-clube não recebe receita. Contratos no vencimento preservam a heurística anterior de renovar ou liberar e sua modalidade anterior de leilão. Renovação preventiva conserva o salário vigente e cobra as luvas anteriores; não reprifica salários por força.

A IA usa os mesmos comandos de propostas, crédito e quitação. Reserva três folhas, dois encargos bancários e operação do estádio; aceita aumentos quando comporta esse compromisso, respeitando renovação obrigatória. Cada decisão automática é idempotente por clube e rodada.
