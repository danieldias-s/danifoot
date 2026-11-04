# Cores, escalação, Banco e Estádio

Entrega local em 04/11/2026, na branch isolada `codex/classic-squad-bank-stadium`, worktree `D:/elifoot_98/.worktrees/account-login-lobby`. Checkpoint de implementação: `1054e8a`. Base anterior preservada: `b3a8077`. Sem merge ou publicação.

## O que mudou

- **Cores:** a apresentação clássica passa a usar os mesmos tokens de painéis, linhas, seleção, texto, cabeçalhos e botões da versão atual, incluindo as variações Clássico, Escuro e Alto contraste. O cabeçalho de janela também passa a verde. O layout clássico continua separado da lógica compartilhada do jogo. As cores registradas dos clubes continuam disponíveis nas partidas.
- **Bolinha do jogador:** abre Titular, Reserva e Fora dos relacionados. Com onze titulares, promover um reserva abre a escolha do titular a substituir, respeitando compatibilidade de goleiro. Se os nove lugares de reserva estiverem ocupados, aparece orientação; ninguém é retirado automaticamente. Jogadores indisponíveis e escalação bloqueada não permitem edição. A mudança é preparação local: a tática só é enviada pelo controle existente.
- **Banco inferior:** abre **BANCO — EMPRÉSTIMOS**, usando crédito, dívidas, juros e ações existentes. A consulta aos reservas fica em **Equipa → Reservas do elenco...**. Contratar empréstimo continua exigindo a confirmação financeira.
- **Estádio:** mostra um desenho de campo e arquibancadas. A capacidade construída determina a quantidade de fileiras; condição e capacidade utilizável alteram conservação e setores escuros; obras em andamento mostram um guindaste e as rodadas restantes informadas pelo servidor. Reformar e ampliar continuam usando as operações e confirmações existentes.

O desenho é uma representação esquemática inspirada no pedido, não uma reprodução de um recurso gráfico original do Elifoot 98. Os números apresentados são do cadastro do clube.

## Verificação

| Verificação | Resultado observado | Evidência |
|---|---|---|
| Flutter analyze | Sem problemas | [analyze.log](analyze.log) |
| Regressão Flutter completa | 350 testes aprovados | [regression.log](regression.log) |
| Build Web release final | Compilado com êxito | [build.log](build.log) |
| Plantel, Banco e Estádio responsivos | Testes em 360, 390, 430, 768, 1366 e 1920 px; sem exceções | Regressão e [evidence.json](evidence.json) |
| Desenho variável | Pixels diferentes para capacidade, conservação e obra ativa | Teste ClassicStadiumPainter, incluído na regressão |
| Navegador Chromium | Troca de goleiros, retirada/recolocação, Banco, confirmação cancelada, estádio e troca de aparência | [evidence.json](evidence.json) |
| Dados do campeonato | Snapshot, listagem de clubes e detalhe do clube idênticos antes/depois; hashes registrados | [evidence.json](evidence.json) |

A conferência de navegador usou uma conta e uma sala de QA criadas para esta tarefa. Nenhum save do proprietário foi utilizado. Depois de entrar nessa sala, as interações testadas não enviaram comandos de escrita do jogo. Nenhum empréstimo ou obra foi executado no navegador; esses controles preservam as regras existentes. O bloqueio após pronto, o limite de reservas e as operações econômicas foram cobertos pelos testes Flutter.

Validação móvel: Web responsiva e testes Flutter. Não houve teste em telefone físico. Não foram alterados servidor, MatchEngine, força, evolução, contratos, custos, juros ou persistência de saves.

## Capturas reais

- Plantel: [clássica desktop](plantel-desktop.png), [versão atual desktop](plantel-versao-atual.png), [clássica mobile](plantel-mobile.png).
- Jogador: [menu da bolinha](alterar-jogador.png), [menu mobile](alterar-jogador-mobile.png), [troca com titular](trocar-titular.png).
- Banco: [desktop](banco-desktop.png), [mobile](banco-mobile.png), [confirmação financeira](banco-confirmacao.png).
- Estádio: [desktop](estadio-desktop.png), [mobile](estadio-mobile.png).

## Como testar

Abra [a prévia local](http://127.0.0.1:5242/), atualize a página, entre com sua conta e retome a sala. Na VERSÃO CLÁSSICA (ANTIGA), clique na bolinha ao lado do jogador. O Banco inferior agora abre empréstimos. Estádio está no rodapé desktop e em Equipa no menu mobile.

Os serviços foram reiniciados após a recuperação do PostgreSQL existente. Para iniciar novamente após desligamento, execute `scripts/INICIAR-CONTAS.cmd` nesse worktree.
