# Pênaltis humanos e substituições compartilhadas — 015B

Data: 01/11/2026. Worktree exclusivo `D:\elifoot_98_015B`, branch `feature/classic-desktop-ui-015b`, base `9002054`. STATUS: **PARTIAL** somente pela captura Chrome e aprovação manual pendentes; implementação e verificações automatizadas concluídas.

## Comportamento entregue

- Botão **SUBSTITUIR** no cabeçalho e na área do clube, tanto no PC quanto no celular. Abre uma janela clássica compartilhada; o servidor congela os jogos da sala. Listas EM CAMPO/SAI e BANCO/ENTRA mostram posição, jogador e força, lado a lado no desktop e empilhadas no celular.
- Abrir, observar, enviar a mesma formação ou confirmar Continuar custa **zero paradas**. Cada treinador que realmente troca atleta ou altera formação gasta uma oportunidade nessa janela. Várias alterações na mesma janela gastam somente uma oportunidade por treinador. Erros de validação não consomem quota. Os limites anteriores (3 paradas/5 substituições, 4/6 na prorrogação) permanecem.
- Intervalos de 45, 90 e 105 minutos são gratuitos em paradas. **Toda substituição efetuada conta**, inclusive no intervalo. Expulsos não são substituíveis, atleta que saiu não retorna e a proteção do goleiro permanece.
- Pênalti a favor de um humano durante o jogo abre **PÊNALTI / CHUTAR** com jogadores aptos em campo. Os demais treinadores veem **AGUARDE**, com o treinador e clube cobrador. Todos os jogos da sala permanecem no mesmo minuto até a escolha; atualizações, reconexões e tempo de espera não cobram automaticamente.
- Conforme a resposta explícita do proprietário, a **disputa de pênaltis após a prorrogação continua automática**, mantendo a seleção anterior de melhores cobradores por craque/posição/força.
- A janela não pode ser descartada para fugir da decisão. O estado oficial mantém o bloqueio de navegação e de operações financeiras durante a partida. Outras salas seguem independentes. Pausas táticas mantêm o prazo de retomada já existente; a decisão de pênalti não usa esse prazo.

## Defeitos confirmados e correções

A abertura da parada debitava a oportunidade do solicitante mesmo sem alteração. Esse débito foi removido; o motor já validava e debitava as alterações reais de cada equipe e passa a ser a única origem dessa cobrança. A interface anterior embutida na tela ao vivo foi substituída pela janela central acessível em PC/mobile.

O repositório EF só gravava minutos periódicos e estados de pausa. O teste efetivo com reinício mostrou que isso perdia pênaltis pendentes e a retomada entre checkpoints. Agora decisões de pênalti, recibos de alteração/continuar e retomadas forçam gravação imediata; pênaltis pendentes também são checkpoints obrigatórios. O sinal transitório de gravação não é serializado. A modalidade de checkpoint periódico permanece para os demais ticks.

A decisão armazena o sorteio já consumido, goleiro, força de defesa e elegíveis no save privado. O DTO público não expõe o sorteio nem a força interna do goleiro. O comando é processado sob o mesmo lock por sala e tem recibos persistentes por comando e por decisão; duas escolhas concorrentes produzem uma única cobrança. Repetições exatas retornam sucesso sem novo gol, evento ou débito. Escolha de outro jogador após a decisão é rejeitada. Há validação do treinador, clube, decisão atual e aptidão do atleta.

Os jogos simulam o mesmo minuto oficial; decisões simultâneas são enfileiradas. O próximo minuto e as transições de 45/90/105/120 aguardam todas as cobranças. Após recarregar no minuto final, o roster transitório necessário à evolução 012C é reconstruído a partir do estado oficial antes do fechamento. Nenhuma fórmula defensiva, probabilidade de conversão, economia, remuneração ou regra de leilão foi alterada. A alteração pontual do MatchEngine foi necessária ao novo pedido de escolha humana e reaproveita a fórmula anterior sem novo sorteio.

## Validação realizada

| Verificação | Resultado |
|---|---:|
| Application .NET | 17/17 |
| Domain .NET | 409/409 |
| Integration .NET | 202/202 |
| Simulation .NET | 26/26 |
| Total .NET | **654/654**, zero ignorados |
| Flutter completo | **211/211**, zero ignorados |
| Flutter analyzer | zero issues |
| Catálogo Node | 2/2 |
| Renderer de cenas reais gravadas | 1/1, dez PNG |

Um clube cujo jogo já terminou não pode alterar escalação nas pausas de outros clubes que ainda disputam a prorrogação; ele recebe a janela de espera com Continuar. Essa proteção foi validada no servidor e na interface.

Os testes novos verificam pausa sem edição, alteração inválida e sem efeito, vários editores, duas trocas na mesma janela, intervalo, recuperação, recibos persistentes, corrida entre cobradores, dois pênaltis no mesmo minuto, fronteiras 45/90/120 e disputa final automática. Os casos HTTP foram executados separadamente em SQLite e PostgreSQL, com descarte do cache e nova instância da aplicação. Os testes de interface cobrem 320/390/768/1440 px, seleção sem cobrança imediata, Chutar habilitado após escolher, Aguarde sem botão de cobrança, Substituir e contadores.

A primeira execução integrada encontrou seis drivers antigos que presumiam avanço sem resposta humana. Os drivers de teste passaram a enviar explicitamente o comando do cobrador usando sua identidade, e os 202 testes de integração foram repetidos integralmente com sucesso. Isso não adiciona escolha automática ao cliente ou servidor de jogo. Domain e Simulation passaram na execução completa inicial; Application e Integration foram repetidas após a proteção final para clubes com jogo encerrado; os arquivos TRX de cada resultado aprovado estão identificados por hash em `evidence-match-interactions-015b/dotnet-tests.json`.

As simulações de 100 temporadas foram novamente executadas nesta alteração, com 35.100 partidas por cenário:

| Cenário | Caixa final | Dívida final | Violações de escalação/elenco/dupla liquidação |
|---|---:|---:|---:|
| Balanço 012D FIX | R$ 137.353.919,79 | R$ 3.270.104,68 | 0 / 0 / 0 |
| Clubes reais masculino | R$ 86.753.694,92 | R$ 0 | 0 / 0 / 0 |
| Clubes reais feminino | R$ 103.405.567,05 | R$ 0 | 0 / 0 / 0 |

Os metadados SourceCheckpoint nesses cenários são históricos; os relatórios foram gerados pela execução atual na 015B. As simulações offline mantêm a resolução automática de pênaltis; os testes multiplayer exercitam as novas decisões humanas.

## Evidência oficial HTTP e imagens

Sala exclusiva **015B QA - Pênaltis e substituições / ELI0EC6DAC6**, mundo `6ceb9f35-2248-4ab9-add1-d73ca37fc6c2`, com Flamengo RJ e FC Porto, clubes reais e jogadores simulados autorizados. Somente salas QA foram avançadas por estes testes. Uma tentativa inicial de preparação foi preservada em outra sala; nenhum save foi apagado.

O teste verificou abertura com zero débito, troca com 1 substituição/1 parada, repetição idempotente e troca no intervalo com 2 substituições/1 parada. Um pênalti naturalmente sorteado para o FC Porto em R16, minuto 16, decisão `fe917414-1c52-4975-9b95-367a9adf0011`, congelou 375 solicitações de tick sem alteração de minuto, versão ou jogos. A API foi encerrada e reiniciada de fato. A mesma decisão foi recuperada; outro treinador foi rejeitado, e a cobrança escolhida e repetida produziu uma única resolução. Resumo em `http-review.json`.

Dez imagens em `evidence-match-interactions-015b/automated-renders` mostram abertura, alteração aplicada, intervalo, escolha de pênalti e espera, em PC e mobile. Foram inspecionadas. São **renderizações Flutter das respostas reais gravadas, não capturas Chrome nem de aparelho físico**. As evidências anteriores foram preservadas. Contextos privados/autenticação ficam somente em artifacts e não são versionados.

## Acesso ao teste manual

Jogo: [http://127.0.0.1:5168/](http://127.0.0.1:5168/). Atualizar com **Ctrl+F5** para carregar o JavaScript novo. API: [http://127.0.0.1:5167/api/health](http://127.0.0.1:5167/api/health). Banco DEV próprio: 55440.

Sala individual do proprietário: **015B - Teste individual Daniel / ELIC18AA38C**, treinador **Daniel 015B**, dispositivo **015b-daniel**, clube **Flamengo RJ**. Antes/depois do reinício do serviço, a sala permaneceu em R6, minuto 90, RESULTS, versão 659. Ela não foi avançada por esta tarefa. O estado pode mudar quando o proprietário jogar.

Os processos de API/Web substituídos foram verificados por porta, PID, horário de início, raiz e comando. PostgreSQL e serviços de outros worktrees foram preservados. Sem merge, push ou publicação em produção.

Pendência concreta: o uso do navegador retorna **“Local browser use is disabled in Work settings”**. As capturas reais e a aprovação manual do proprietário continuam pendentes. Nenhum sucesso automatizado equivale à confirmação do seu teste manual.
