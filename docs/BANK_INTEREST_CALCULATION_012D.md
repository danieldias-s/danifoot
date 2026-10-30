# Juros, principal e quitação — 012D

`ClassicFixed`: entrada real de R$ 500.000, principal pendente de R$ 500.000 e R$ 25.000 de juros por partida oficial efetivamente concluída pelo clube. Liga, preliminares e demais fases da Taça seguem a mesma regra. Não há amortização automática do principal nem juros sobre juros. A quitação integral exige caixa disponível real.

Uma partida precisa estar finalizada, ter minuto disputado maior que zero e não estar cancelada ou adiada. Folgas, dias virtuais, pausas, partidas não iniciadas e repetição da liquidação não geram juros. Cada empréstimo guarda os IDs cobrados; a criação exclui partidas já terminadas e registra a primeira rodada elegível, impedindo cobrança retroativa.

Os juros vencidos são pagos até o caixa existente; a parte não paga permanece em `UnpaidInterest` e no saldo devedor. A dívida total é principal pendente + juros não pagos. Exemplo: caixa R$ 1.000 na partida → pagamento real R$ 1.000; juros pendentes R$ 24.000; dívida R$ 524.000. Outra partida sem caixa → juros pendentes R$ 49.000; dívida R$ 549.000. O segundo encargo continua sendo R$ 25.000.

Na quitação, os juros pendentes e o principal são lançados separadamente em `LoanInterestPayment` e `LoanPrincipalRepayment`. Lances já reservados não podem financiar a quitação. Um recibo persistente impede repetir o débito.

`LegacyInstallment` é o valor padrão para snapshots sem modalidade. Conserva saldo, taxa e prestação originais. O prazo pode chegar a zero, passando a `OVERDUE`, mas somente saldo zero significa `PAID`. Prestações seguem a regra original de juros sobre saldo e amortização até a quitação; a dívida residual nunca desaparece pelo contador. Não há conversão automática para empréstimo clássico.

Defeito reproduzido: principal legado de R$ 500.000, juros de 1,5% sobre saldo e vinte prestações de R$ 28.750 deixam R$ 8.622,06 após o prazo. A expressão anterior declarava essa operação paga; a correção mantém o saldo vencido até recebimento real.

Na migração, o saldo de entrada legado é preservado como `LegacyOpeningBalance`. Não se inventa a composição histórica entre principal e juros já capitalizados: ela é desconhecida. O histórico a partir da migração separa encargos, juros pagos, amortização e fluxo de caixa. A auditoria usa:

`saldo final = saldo inicial + juros cobrados − juros pagos − principal pago`.

Para empréstimos clássicos novos, o saldo inicial é R$ 500.000. A série de 100 temporadas verifica essa identidade a cada rodada e conserva eventos bancários além da poda dos lançamentos transitórios.
