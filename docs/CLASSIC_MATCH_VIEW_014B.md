# Painel clássico de partidas — 014B

Implementação em `client/elifoot_flutter/lib/classic_match_board.dart`, integrada em `main.dart`. O menu tem sete opções; Jogos ao Vivo é uma etapa automática. Os detalhes continuam disponíveis na linha do confronto.

Liga agrupa `matches` pelo `divisionTier` recebido. A cena real da terceira rodada tem 16 partidas: quatro em cada divisão. Taça usa `competitionType` e o nome oficial `competitionLabel`, sem classificar distritais como liga. A primeira preliminar gravada tem 32 confrontos; o número deriva da lista do servidor. Grupos grandes são expansíveis e todos os jogos permanecem na lista.

Fundo verde próprio, cabeçalhos amarelos, placar central de 74 px, nomes alinhados e linhas compactas (mínimo 38 px), largura máxima 940 px no desktop. No celular os nomes longos usam elipse e tooltip; o placar permanece visível, sem exigir campo tático nem rolagem horizontal do painel. O menu horizontal mantém seu comportamento anterior.

A linha do treinador recebe destaque amarelo e informa atletas em campo quando o snapshot os fornece. Detalhes mantêm eventos completos, minuto, gols, cartões, lesões, formações, substituições, prorrogação e pênaltis. Ajustes e confirmação da pausa usam os comandos existentes. Cada partida tem identidade própria para armazenar sua expansão; abrir o grupo da Taça não abre todos os detalhes.

Validação: cenas reais por `scripts/record-classic-ui-014b.py`; testes do painel em 320/390/1366 px e matriz de telas até 1920 px; imagens em `audit-014b/automated-renders`. São renderizações automatizadas de widgets reais com respostas gravadas, não capturas do Chrome nem auditoria humana. Referências visuais mencionadas no anexo não estavam anexadas.
