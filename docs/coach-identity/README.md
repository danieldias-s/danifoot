# Acesso e identificação dos treinadores

03/11/2026 — branch `codex/coach-identity-ux`, a partir de `0a562e0`, no worktree `D:/elifoot_98/.worktrees/world-creator-visual`.

A tela anterior misturava nome, identificador interno do aparelho, edição da sessão e lista de treinadores. O botão de novo treinador gerava outro identificador sem pedir um nome reconhecível. Isso dificultava distinguir acessos, especialmente quando todos se chamavam Treinador 1.

Agora o painel destaca o treinador selecionado, mostra os acessos salvos por nome e clube/campeonato quando conhecidos e marca o atual com **EM USO**. Ao criar outro, o usuário escolhe o nome e o acesso é salvo após a autenticação, antes da consulta das salas. Cancelar a criação conserva a seleção anterior. Os identificadores deixam de aparecer no fluxo normal; a recuperação manual antiga continua disponível em uma seção recolhida.

Dentro do campeonato, uma faixa amarela **VOCÊ • nome • clube** fica visível acima da lista dos outros participantes, inclusive no celular. A lista de treinadores da sala também marca **VOCÊ**, comparando o ID do treinador, e não o apelido. Assim, nomes repetidos não confundem a identificação.

O salvamento mantém os campos e as chaves de armazenamento existentes e acrescenta apenas os nomes do último clube/campeonato para apresentação. Perfis antigos sem esses dados recebem uma identificação de perfil salvo até que o contexto seja conhecido. O acesso continua local ao navegador/aparelho; não foi criado um cadastro de conta remoto. O código do domínio, API, banco e regras do jogo não foi alterado.

## Verificação

- **294 testes Flutter aprovados**, incluindo dez novos testes de criação, cancelamento, recuperação, fechamento/retorno, troca de perfis, falha da consulta de salas e seis larguras. Os testes antigos foram atualizados apenas para o texto público “ENTRAR NO LOBBY”.
- **Flutter analyze** sem erros ou avisos novos; permanece o `info unnecessary_const` preexistente em `lib/live_match_view.dart:241:28`.
- **Build Web release aprovado** e servido em `http://127.0.0.1:5222`.
- **Aplicação real com API local:** treinador antigo reconhecido; criação de outro por nome; ambos salvos; navegador de teste fechado e reaberto; novo acesso recuperado automaticamente; troca para o anterior; retorno ao mesmo clube sem duplicação do participante.
- **Identificação real:** VOCÊ aparece na faixa do campeonato e apenas no participante correspondente na sala, mesmo com três participantes chamados Treinador 1.
- **Larguras:** 360, 390, 430, 768, 1366 e 1920 px; largura do documento igual à do viewport em todas. Testes mobile usam viewport emulado.

As verificações de navegador foram feitas em um processo Chromium próprio, com perfil QA, sem acessar o armazenamento do navegador pessoal ou da aba do usuário. Apenas a identidade QA anterior e um novo treinador de revisão foram usados. Não houve alteração de outros participantes nem avanço de rodada.

## Capturas

| Fluxo | Captura |
| --- | --- |
| Lobby desktop | [Abrir](captures/lobby-desktop.png) |
| Lobby mobile | [Abrir](captures/lobby-mobile.png) |
| Criação por nome | [Abrir](captures/create-mobile.png) |
| Treinadores salvos e EM USO | [Abrir](captures/saved-trainers-desktop.png) |
| VOCÊ no campeonato desktop | [Abrir](captures/active-trainer-desktop.png) |
| VOCÊ no campeonato mobile | [Abrir](captures/active-trainer-mobile.png) |

[Verificações e hashes](verification.json), [testes Flutter](flutter-tests.log), [análise](flutter-analyze.log), [build Web](build-web.log).

## Arquivos

- `lib/local_trainer_panel.dart`: painel, lista dos acessos e formulários de criação/recuperação.
- `lib/main.dart`: integração, salvamento após autenticação e identificação persistente do treinador no campeonato.
- `lib/shell_actions.dart`: ações de criar/selecionar/recuperar e metadados do último clube/campeonato.
- `lib/lobby_room_status.dart`: marcação do participante atual.
- `test/local_trainer_ux_test.dart`: dez testes novos; doze arquivos de teste existentes tiveram apenas o rótulo do botão atualizado.

O diretório principal contém outro trabalho de lobby ainda sem commit, preservado. Esta correção permanece na branch isolada. Sem merge ou publicação.

Para testar na prévia, atualize a página. Se estiver no campeonato, use **TROCAR SALA**. Escolha **CRIAR NOVO TREINADOR**, digite o nome e confirme **CRIAR E SALVAR**. Para voltar ao anterior, escolha-o em **SEUS TREINADORES SALVOS** e depois use **RETOMAR MINHA SESSÃO** na sala.
