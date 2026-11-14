# Multiplayer · Etapa 2.1 · Sincronização de dados da sala

**PASS funcional no ambiente controlado.**

## Comportamento

Após confirmar a gravação, a API publica `WorldDataChanged` com o ID do mundo, temporada, rodada, estado, versão e áreas afetadas. Listagens, propostas, compras, leilões, decisões salariais, contratos, empréstimos, estádios, mudanças de carreira e convites passam pelo fluxo de atualização. Uma proposta que a IA aceita imediatamente também invalida elenco, finanças e clubes. Eventos contêm apenas metadados de invalidação. Os endpoints continuam aplicando as verificações de conta, treinador, participação e clube.

O Flutter acompanha a maior versão recebida por área, descarta eventos de outras salas e rodadas e invalida leituras em andamento. Recarrega imediatamente as áreas visíveis e consulta as demais ao navegar. Trocas de clube e transições de estado usam a projeção da rodada. As escolhas e a capacidade do lobby inicial sinalizam a nova versão por `ReadyCounterUpdated`, com consulta periódica de recuperação no lobby. As transições de relógio, pausa e resultados continuam no fluxo da Etapa 2. Preferências de aparência e observação individual seguem no fluxo do próprio treinador.

Alguns comandos do próprio treinador ainda fazem uma atualização explícita para exibir a confirmação via HTTP. Se ela coincidir com o evento do servidor, pode haver duas consultas à mesma área. O teste de widget verificou que a resposta atrasada e a duplicata do evento são descartadas.

`MarketUpdated` continua disponível para clientes anteriores, inclusive os que já usam `JoinRoomV2`. O Flutter atualizado ignora o evento de compatibilidade quando recebe invalidações específicas. A sequência incremental da rodada segue independente da versão por área. A percepção de lacuna, a reconexão e a consulta periódica existente recuperam dados que podem ter sido perdidos.

A gravação das mutações econômicas e de carreira ocorre sob o bloqueio do mundo. Em falha, a versão pendente fica indisponível para leitura até a confirmação. Um comando autorizado repetido com a mesma chave recupera o recibo, sem repetir a mutação. O relógio também tenta confirmar a gravação pendente. Caso o processo caia, o reinício lê a versão durável. A entrega SignalR pode falhar depois do commit e a reconciliação cobre essa janela.

## Validação

- Dois treinadores receberam a mesma versão da compra após uma falha de inserção induzida no SQLite. O snapshot devolveu HTTP 503 antes da confirmação. A compra repetida não gerou outro evento e o terceiro cliente, inscrito em outro mundo, recebeu zero eventos. Os dois lançamentos financeiros foram gravados uma vez e a compra sobreviveu ao reinício.
- As suítes selecionadas cobriram disputa por vaga com lances simultâneos, compras, decisões salariais e contratos, troca de clube, replay e reconexão. Na disputa por um convite, o treinador correto e o replay concorrente receberam sucesso, o segundo treinador teve a tentativa recusada e sua conexão recebeu a invalidação de participantes. Os testes de conta cobriram posse do clube e continuidade entre dispositivos. [Carreira](../artifacts/stage3-storage/career-verified.log)
- O teste de widget injetou eventos de outro mundo, de área oculta, duplicados e fora de ordem. Uma resposta HTTP atrasada foi descartada após uma versão mais nova. Passaram 19 testes Flutter direcionados. Na suíte completa, 576 passaram e reapareceram as oito falhas já registradas na Etapa 1, com os mesmos cenários de tela de partida, plantel móvel e espera após a rodada.
- A seleção anterior ao trabalho de armazenamento passou com 35 aprovações e um teste comercial ignorado pelo fixture. A seleção posterior abrangeu também lobby e ambos os formatos SQLite. [Backend antes da Etapa 3](../artifacts/stage21-dotnet-final.log) · [Flutter direcionado](../artifacts/stage3-storage/flutter-targeted-verified.log) · [Flutter completo](../artifacts/stage3-storage/flutter-full.log) · [Integração posterior](../artifacts/stage3-storage/integration.log)

## Pendências de escala

Continuam abertas as medições de Flutter sob rede degradada com WebSocket real, atraso e perda durante várias rodadas, FPS, RAM do cliente e carga comparável antes/depois com 50 mundos persistidos. Os dois casos PostgreSQL que dependem do runner 015B isolado ficaram fora da seleção desta etapa. A troca de sessão e os dados do evento foram validados em localhost. [Etapa 1](MULTIPLAYER_ETAPA_01.md) · [Etapa 2](MULTIPLAYER_ETAPA_02.md)

O checkpoint anterior às mudanças contém o HEAD, o estado do Git, os patches e hashes dos arquivos copiados. [Manifesto SHA-256](../artifacts/stage3-checkpoint-9684bc0c29bf/sha256.csv)
