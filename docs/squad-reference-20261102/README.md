# Plantel & Táticas — referência desktop e mobile

Base: master `e335ca4`, em `D:\elifoot_98`. Implementação restrita à apresentação Flutter. Os demais worktrees, a API e os bancos existentes não foram modificados.

No desktop, a tela usa dois painéis claros: informações do treinador, esquema, campo com os atletas selecionados, setores e troca rápida à esquerda; plantel com filtro por posição e ações à direita. Em larguras intermediárias, os painéis passam a ocupar linhas separadas. O escudo genérico usa as cores recebidas do servidor; não representa uma nova coleção de escudos oficiais.

No mobile, a abertura apresenta resumo tático, titulares, banco e plantel; a aba Táticas mantém o campo acessível. A barra de moral foi preservada. Contrato, ficha e venda aparecem como ícones na tabela. O botão de venda abre o formulário existente, que permite leilão ou anúncio de preço fixo. Permanecem o mínimo inicial de R$ 500.000 e a duração inicial de duas rodadas globais para leilões voluntários.

Salário contratado e valor de mercado são apresentados em campos independentes, recebidos da API. Força, condição, escalação, salários e caixa não são preenchidos com os valores das imagens de referência. Nomes simulados podem omitir o prefixo repetitivo no espaço compacto; a identificação completa permanece na dica e na ficha. Não se inventam números de camisa: o número exibido na camisa tática é a força do atleta, conforme a referência.

As regras de envio de tática, início automático, bloqueio de partida, contratos, leilões e proteções de elenco continuam nos mecanismos existentes. Não houve alteração no motor, na fórmula defensiva, na economia ou no modelo de persistência.

## Verificação

Os resultados finais estão em [validation.json](validation.json). A regressão Flutter inclui mudança para 5-3-2 com alteração efetiva dos titulares, envio e início automático com um humano, bloqueio de menus durante partidas, condições de lesão/suspensão, salários contratados, filtros, leilões e sessões. Os testes existentes foram adaptados somente onde os controles visuais mudaram.

As cinco imagens abaixo foram renderizadas pelo Flutter com GETs reais da API local `5181`, na sala de teste do usuário, rodada 3, estado RESULTS, versão 348. A gravação não enviou comandos ao jogo. A conferência inclui a posição horizontal do botão de venda no mobile e os painéis lado a lado no desktop. A origem está em [scene-provenance.json](scene-provenance.json). Respostas completas e credenciais locais permanecem somente em `artifacts/master-playtest`, fora dos arquivos versionados.

O navegador retornou `Local browser use is disabled in Work settings`. As imagens são renders Flutter, não screenshots do Chrome. A aprovação manual do usuário permanece pendente.

- [Desktop 1536](flutter-renders/plantel-1536.png)
- [Desktop 1366](flutter-renders/plantel-1366.png)
- [Mobile: resumo, titulares e banco](flutter-renders/plantel-390.png)
- [Mobile: tabela de jogadores](flutter-renders/plantel-elenco-390.png)
- [Mobile: táticas](flutter-renders/taticas-390.png)

O teste local continua em `http://127.0.0.1:5182/#/plantel`, com API `http://127.0.0.1:5181`. A atualização substitui somente a build do servidor web registrado desta tarefa. Não recria salas, não troca sessões e não reinicia API ou PostgreSQL. Nenhuma publicação em produção foi realizada.
