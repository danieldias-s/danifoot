# Regressão econômica — 012D

Evidências em `docs/audit-012d`; entrada preservada no commit `50e9c12`. O MatchEngine e a fórmula defensiva devem ter SHA-256 idêntico ao manifesto de entrada.

## Verificações específicas

`ClassicEconomy012DTests`: término de prazo legado com saldo residual; cobrança única por Liga/preliminar/final; não cobrança por folga, adiamento, cancelamento ou jogo não iniciado; criação posterior à partida; juros vencidos sem capitalização; quitação sem duplicação; crédito individual 1/2 na quarta; propostas persistentes e negociação protegida; promoção; recusa e livre sem receita; renovação obrigatória sem caixa; prioridade e dinheiro da venda por insolvência; leilão sem comprador; dívida sem atleta elegível; reserva de caixa; migração conservadora.

`ClassicEconomy012DIntegrationTests`: HTTP e SignalR reais de TestServer com 2/4/6/8 participantes; comandos simultâneos com a mesma chave; lances que disputam o mesmo caixa; decisão salarial única; resposta do servidor com modalidade/dívida; restauração SQLite em memória e repetição do comando após reinício. Bancos são descartáveis e independentes dos arquivos de jogo.

Flutter: salário real do servidor, principal fixo de R$ 500.000, dívida/juros/limite individual, propostas e quitação. Testes do cliente verificam repetição após resposta perdida, versão da sala e corpos de aceitação/recusa/quitação. As expectativas antigas de R$ 300.000 e de reprificação salarial automática foram atualizadas para a especificação 012D.

## Sincronização e repetição

Todos os comandos de mercado, banco, contratos e estádio passam pelo mesmo semáforo da sala usado pelo relógio. Fases permitidas: `PREPARING` e `RESULTS`; participante pronto deve retirar o pronto antes de alterar finanças. A versão esperada, quando enviada, precisa coincidir. Recibos persistentes são consultados antes da versão/fase para permitir repetir uma operação já concluída sem efeito adicional. Reutilizar uma chave com outro comando/conteúdo é rejeitado.

A interface mantém a chave após falha de transporte e usa a versão recebida nas finanças. A API mantém compatibilidade com clientes antigos sem chave/versão opcional, gerando uma chave por solicitação; esses clientes precisam enviar uma chave para repetição entre requisições independentes.

## Execução reproduzível

Definir `ELIFOOT_AUDIT_OUTPUT` para uma pasta nova e `FORCE012_MODE=candidate`. Executar a suíte de simulação antes da integração 009 que consome `real-season-world.json`; depois executar os demais projetos. Evitar recompilar o projeto de simulação enquanto um testhost usa suas DLLs no Windows. `dotnet test Elifoot.slnx --no-build --no-restore` permite a regressão conjunta depois de um build íntegro e da criação desse artefato.

Os resultados definitivos, contadores e limitações serão acrescentados após o término das execuções. Uma tentativa inicial registrou oito expectativas salariais antigas; outra detectou dependência de artefato 009 e a reflexão do teste sobre o método interno de bloqueio. O método interno foi preservado e a API usa um acesso público ao mesmo semáforo.

## Resultado final verificado

Build Release: zero erros e avisos. Solução Release completa: 408/408 testes aprovados. Após a revisão das chaves do histórico e da repetição após falha de persistência, passaram novamente 338 testes de domínio, seis de aplicação e 45 de integração. Os casos novos incluem dois clubes reutilizando uma chave de comando e falha de gravação seguida de repetição sem crédito duplicado. A tabela consolida 410 testes distintos aprovados em Release, sem contar os reprocessamentos em duplicidade. Flutter: 61/61; análise sem apontamentos. A execução da solução usa `ELIFOOT_AUDIT_OUTPUT=docs/audit-012d/solution-final`, com o artefato real 009 disponível antes de seu consumidor.

| Suíte | Aprovados | Falhas | Evidência |
|---|---:|---:|---|
| elifoot.simulation.tests.dll | 21 | 0 | `docs\audit-012d\tests\solution-release_net10.0_20261030184852.trx` |
| Elifoot.Domain.Tests.dll | 338 | 0 | `docs\audit-012d\tests\domain-review-final.trx` |
| Elifoot.Application.Tests.dll | 6 | 0 | `docs\audit-012d\tests\application-review-final.trx` |
| Elifoot.Integration.Tests.dll | 45 | 0 | `docs\audit-012d\tests\integration-review-final.trx` |

O SHA-256 do MatchEngine permanece `01179d6a0f0a073b69ff7bf0589674b664f960fbedcba6e1d5907ea55e18bb71`. Também foram comparados disciplina, táticas, IA, carreira, estatísticas e estado da força. Nos dois motores de evolução, a única alteração é remover a escrita automática do salário; a referência é calculada a partir dos atributos atuais, inclusive após edição/importação.

`verification.json` e `independent-reconciliation.json` registram os controles. A solução inclui replay de duas temporadas clássicas, testes bancários após restauração e concorrência HTTP/SignalR em 2/4/6/8 participantes, além da disputa de IA com humanos. O status geral permanece PARTIAL pelo balanceamento monetário, apesar de as verificações funcionais passarem.
