# Correção do teste manual — formação e sala individual

Base: 456cd4008ad352c46b5c476922a8f74d067e56c6, branch feature/classic-desktop-ui-015b, somente D:\elifoot_98_015B.

O proprietário alterou de 4-4-2 para 5-3-2 e a visualização manteve quatro defensores e quatro meias. Confirmado: os dois controles de formação mudavam apenas a string; a escalação e o desenho permaneciam agrupados pela posição nominal.

Agora a troca de esquema ajusta os setores com atletas aptos, mantendo primeiro os titulares já escolhidos em cada posição e promovendo reservas somente para completar as vagas necessárias. No caso 4-4-2 → 5-3-2 do teste, dez titulares foram preservados: entrou o quinto defensor e saiu um meia. O treinador pode revisar e alterar a escalação antes de enviar. Lesionados e suspensos não entram; faltas de atletas continuam explícitas e não são preenchidas com jogadores inventados.

O campo usa as vagas da formação selecionada. Se o treinador fizer uma seleção manual fora das posições, o atleta continua com sua posição nominal e o aviso de desvio setorial permanece. O esquema não muda posição, força, salário ou regra do motor.

A tática da captura já havia sido recebida pelo servidor. A espera 1/3 era correta para aquela sala: Daniel 015B, Comprador 015B e Rival 015B estavam cadastrados. Esses dois últimos eram identidades da auditoria, não outros usuários. Nenhuma regra multiplayer foi alterada para ignorar treinadores ou considerá-los prontos por estarem desconectados.

Foi criada uma sala separada com somente Daniel, preservando o save anterior e seu estado:

- Nome: 015B - Teste individual Daniel
- Código: ELIC18AA38C
- Treinador: Daniel 015B
- Dispositivo: 015b-daniel
- Clube: Flamengo RJ
- Estado entregue: preparação R3, 0/1 táticas
- Jogo: http://127.0.0.1:5168/

Após atualizar a página, usar TROCAR SALA e entrar nessa sala individual. Escolher 5-3-2, conferir o campo e ENVIAR TÁTICA. Com uma pessoa cadastrada, o servidor inicia a rodada ao receber 1/1; a navegação abre automaticamente a partida ao vivo. O envio não exige outro comando de início.

Validação: cinco testes novos cobrem dropdown em 390/1440 px, botão rápido, titulares preservados, exclusão de lesionados/suspensos, desenho dos setores, payload enviado e abertura automática da partida com 1/1. Regressão completa Flutter: 173/173, analyzer zero issues.

Uma sala QA real separada, com clubes reais e PostgreSQL DEV 55440, recebeu 5-3-2 com 1G/5D/3M/2A. Passou de PREPARING 0/1 para RUNNING_FIRST_HALF 1/1, persistiu formação e os onze IDs enviados e concluiu em RESULTS. A sala individual de Daniel ficou sem enviar tática para permitir o teste manual pelo proprietário. Evidência: evidence-015b-playtest-fix/one-human-proof.json.

Nenhum arquivo de src/.NET, MatchEngine, fórmula defensiva ou economia mudou. A regressão anterior de 640 .NET, 2 Node e simulações de 100 temporadas continua registrada; não se afirma que foi repetida por esta correção exclusiva do Flutter. A auditoria visual final 015B continua pendente porque o uso do navegador no Work está desabilitado; os testes e a API real não foram apresentados como aprovação visual ou manual do proprietário.
