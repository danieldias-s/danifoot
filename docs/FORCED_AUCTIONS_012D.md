# Modalidades de leilão — 012D

| Motivo | Lance inicial | Vínculo durante leilão | Destino do pagamento | Sem comprador |
|---|---:|---|---|---|
| `ContractExpiration` | 65% do valor justo, regra anterior | atleta livre | contratação do livre; sem receita ao antigo clube | fallback anterior preservado |
| `SalaryRenewalRefused` | R$ 0 | livre após saída legal | contratação do livre; sem receita ao antigo clube | permanece livre; `AUCTION_UNSOLD` |
| `FinancialInsolvency` | R$ 0 | clube vendedor até venda efetiva | débito do comprador = crédito do vendedor | mantém vínculo e dívida; `AUCTION_UNSOLD` |

Um lance real de zero é distinto da ausência de lances: `HighestBidderClubId` identifica a proposta. Após o primeiro lance, o incremento mínimo dos novos leilões é R$ 5.000. A IA participa com avaliação e teto privados, necessidade de elenco, reserva de caixa e recursos próprios; nenhum comprador é criado para resolver insolvência. O incremento percentual anterior da IA permanece para vencimentos contratuais.

Lances vencedores abertos reservam caixa para evitar comprometimento duplicado. O comando verifica ainda a soma das vagas de elenco já comprometidas por outras arrematações. Na liquidação o saldo e o elenco são revalidados. Repetir a liquidação de um leilão fechado não movimenta caixa novamente.

Na venda por insolvência, os lançamentos `AuctionPurchase`/`AuctionSale` compartilham a identificação da transferência e têm soma zero. Lance zero válido gera dois registros de valor zero e muda a propriedade legalmente; ausência de lance nunca muda a propriedade. Na contratação de livre existe somente débito do comprador, sem crédito ao ex-clube. O ex-clube é contexto histórico, não beneficiário.

API, interface e auditoria expõem motivo, lance mínimo e encerramento. Eventos bancários, contratos e crise são mantidos no snapshot; arquivos comprimidos da auditoria conservam o livro-caixa observado nas 100 temporadas mesmo quando o mundo poda registros transitórios antigos.
