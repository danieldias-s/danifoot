# Insolvência — 012D

Caixa negativo ou juros vencidos não pagos caracterizam crise financeira operacional. Principal ainda não vencido de um empréstimo clássico, sozinho, não implica insolvência. A avaliação ocorre depois da liquidação, das decisões financeiras da IA, do mercado e dos custos/prêmios sazonais.

O sistema registra entrada em crise, recuperação, abertura de leilão e ausência de atleta elegível. Não injeta dinheiro para resolver crise. Mantém dívida e caixa negativo quando a recuperação não é possível imediatamente. Juros pendentes continuam visíveis até pagamento real.

Há no máximo um leilão aberto por dívida por clube. Depois do fechamento, aguarda duas rodadas para nova tentativa. Ausência de comprador fecha como `AUCTION_UNSOLD`, conserva o vínculo e não apaga dívida. Ausência de jogador elegível registra `NO_ELIGIBLE_PLAYER` no máximo uma vez por clube/rodada e retorna sem repetição interna, permitindo ao mundo avançar.

O atleta elegível deve poder sair sem violar 11 registrados, um goleiro e dez de linha. Indisponibilidade temporária por suspensão/lesão é separada do cadastro: as regras de escalação com menos aptos da 012C permanecem. Prioridade obrigatória: maior força, maior salário contratado e menor ID estável. Proteção de negociação salarial não impede leilão por dívida.

Durante o leilão o atleta continua pertencendo ao clube e recebendo pelo vínculo. Se seu contrato vencer enquanto estiver reservado, a renovação preserva a permanência até a venda. Transferências paralelas e outro leilão do mesmo atleta são bloqueados. A liquidação revalida comprador, caixa, propriedade e ambos os elencos.

Recuperação é registrada quando caixa e juros vencidos deixam de ser negativos/pendentes. Dívida principal pode continuar registrada em um clube recuperado. Não se declara recuperação apenas porque um leilão abriu ou terminou.
