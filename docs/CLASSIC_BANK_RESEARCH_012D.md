# Pesquisa bancária e contratos — 012D

O relatório integral foi lido antes da implementação e preservado em [RESEARCH_ONLY_REPORT.md](audit-012d/RESEARCH_ONLY_REPORT.md). A sessão 012C estava concluída; seu estado foi preservado no commit local `50e9c12` antes da primeira alteração econômica. O manifesto de entrada e as cópias dos arquivos estão em `audit-012d/baseline`.

## Evidência e classificação

| Regra | Classificação | Evidência |
|---|---|---|
| Elenco registrado de 11–24, pelo menos 1 goleiro e 10 de linha | HISTORICALLY_VERIFIED, documentação histórica espelhada | [FAQ de 24/02/2000](https://www.geocities.ws/ri_keller/ellifoot.html) |
| Juros por partida e incentivo a quitar rapidamente | HISTORICALLY_REPORTED, relato de jogador | [Guia de 09/09/2010](https://elifoot98.blogspot.com/2010/09/evite-emprestimos.html) |
| Renovação preventiva e pedidos de aumento | HISTORICALLY_REPORTED | [Guia de 30/05/2010](https://elifoot98.blogspot.com/2010/05/renovacao-de-contratos.html) |
| Ultimatos salariais e leilões | HISTORICALLY_REPORTED | [Entrevista/retrospectiva de 06/04/2017](https://www.record.pt/record-gaming/retrogaming/detalhe/30-anos-de-elifoot-um-jogo-pioneiro-e-um-papa-babado) |
| Principal de R$ 500.000 e juros de R$ 25.000 por partida | CLASSIC_ADAPTED_DESIGN: parâmetros autorizados pelo usuário | Valores exatos não demonstrados pelas fontes consultadas |
| Fórmula patrimonial individual de crédito e limiares por divisão | CLASSIC_ADAPTED_DESIGN | Fórmula histórica UNKNOWN |
| Juros, leilão inicial zero, destino do arremate e proteção do elenco | CLASSIC_ADAPTED_DESIGN | Sem reprodução alegada do executável original |

Não foi autenticado um executável histórico nem descoberta uma fórmula bancária original. O [roteiro público de pesquisa](https://github.com/trsthales/desvendando-elifoot-98/blob/main/ROADMAP.md) também não fornece essa fórmula completa. As regras do adendo prevalecem sobre propostas da pesquisa: insolvência inclui atletas com contrato renovado e prioriza força, salário e identificador estável.

## Defeitos confirmados no código de entrada

1. `BankLoan.IsPaidOff` considerava o fim do contador uma quitação, mesmo com saldo positivo.
2. Evolução de força e perda por lesão recalculavam `SalaryPerRound` sem negociação.
3. A interface do mercado estimava salário em 3,5% do valor justo, apesar de receber o salário real.
4. Comandos bancários, contratuais, de estádio e mercado não compartilhavam o semáforo da rodada; renovação e empréstimo não tinham recibo persistente de repetição.
5. O limite bancário era o mesmo para todos os clubes; não havia modalidades de leilão salarial e por insolvência.

A modalidade preexistente de leilão por vencimento foi mantida, inclusive lance inicial de 65% do valor justo, ausência de crédito ao ex-clube e contratação gratuita de fallback. Acrescentou-se apenas defesa contra liquidar um leilão antigo de um jogador que já voltou a ter dono, além de respeitar caixa reservado.

O MatchEngine, a fórmula defensiva e outros projetos estão fora do escopo. Não houve publicação.
