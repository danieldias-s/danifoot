# Auditoria visual 014C

STATUS: PARTIAL. Existem evidências executáveis e 18 imagens do renderer Flutter com respostas reais de HTTP/PostgreSQL. Não são screenshots de navegador nem aprovação humana da referência.

`tool/club_colors_capture_014c_test.dart` carrega as cenas gravadas por `scripts/smoke-club-colors-014c.py`, fontes locais e a aplicação real Flutter. Captura em 390x1000 e 1366x1000: Flamengo, Manchester City, jogos com times de cores distintas para ambos os treinadores, mundo feminino, Aparência, seletor visual e duas etapas da prévia. Arquivos em `audit-014c/automated-renders/`, com log dos dois testes.

Revisão das imagens automatizadas: Flamengo apresenta superfícies vermelhas em toda a área principal e menu. Manchester City apresenta sua paleta azul/branco (ou preferência privada azul). Os times mantêm cores individuais. Quatro divisões estão agrupadas e placares centrais. Nenhum overflow foi reportado nesses cenários. O menu horizontal clássico pode rolar no mobile, como na base aprovada. Não houve reconstrução do layout.

A revisão encontrou texto vermelho sobre fundo azul-marinho que não alcançava a meta ao clarear. Foi corrigida a escolha de escurecer/clarear pelo contraste máximo; um teste percorre as 17 paletas. Capturas foram regeneradas após a correção.

Pendente: executar a matriz no navegador real, reiniciar o navegador, conferir a sequência real de mudança de clube e comparar com a imagem de referência do proprietário. Nenhuma referência gráfica foi anexada nesta missão. A sessão anterior reportou bloqueio de confiança da URL no controle nativo; não se contornou essa restrição.

Comando local de revisão: `scripts/run-local-014c.ps1 -NoBuild -ManualClock`, navegador em http://127.0.0.1:5156. Screenshots nativos eventualmente adicionados devem ficar separados das renderizações automatizadas e identificados no relatório.
