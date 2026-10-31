# Auditoria visual 014C

STATUS: PARTIAL. Matriz principal executada no navegador integrado real (IAB), com 14 screenshots originais em `audit-014c/browser-captures/`. Os 18 renders Flutter estão separados e não equivalem à aprovação humana da referência.

`tool/club_colors_capture_014c_test.dart` carrega as cenas gravadas por `scripts/smoke-club-colors-014c.py`, fontes locais e a aplicação real Flutter. Captura em 390x1000 e 1366x1000: Flamengo, Manchester City, jogos com times de cores distintas para ambos os treinadores, mundo feminino, Aparência, seletor visual e duas etapas da prévia. Arquivos em `audit-014c/automated-renders/`, com log dos dois testes.

Revisão das imagens automatizadas: Flamengo apresenta superfícies vermelhas em toda a área principal e menu. Manchester City apresenta sua paleta azul/branco (ou preferência privada azul). Os times mantêm cores individuais. Quatro divisões estão agrupadas e placares centrais. Nenhum overflow foi reportado nesses cenários. O menu horizontal clássico pode rolar no mobile, como na base aprovada. Não houve reconstrução do layout.

A revisão encontrou texto vermelho sobre fundo azul-marinho que não alcançava a meta ao clarear. Foi corrigida a escolha de escurecer/clarear pelo contraste máximo; um teste percorre as 17 paletas. Capturas foram regeneradas após a correção.

Executado no IAB: Flamengo e Manchester City em 1366x1000 e 390x1000; jogos das quatro divisões com paletas próprias; menu de Aparência; seletor; prévia com fundo preto; salvar; reload recuperando a preferência preta; restaurar padrão. Houve troca de sala/clube e verificação de desemprego neutro. Uma segunda sessão controlou Flamengo na mesma sala em que a primeira controlou Manchester City, mantendo temas próprios. Três rodadas foram preparadas pela API local usando o motor real e exibidas por SignalR, sem mock do quadro no navegador.

A inspeção detectou verde legado no seletor de clubes desconhecidos. O seletor passou a consultar ColorPalette verificada/personalizada e usar neutro na ausência. A suíte final passou 114 testes e uma sessão nova confirmou as amostras neutras. Algumas capturas anteriores a esse ajuste mostram telas que não foram alteradas por ele.

Pendente: reiniciar o processo completo de navegador/perfil; sequência real de contratação/demissão/retorno; comparação com a referência do proprietário, não anexada. Reload da página foi aprovado; reabrir aba IAB inicia sessão isolada, sem comprovar reinício do mesmo perfil. O controle nativo de Chrome não foi usado: a inspeção utilizou o navegador integrado disponível, sem contornar a restrição da sessão anterior.

Os renders usam dados primários gravados de HTTP (snapshot, clube, paletas e preferências), com respostas auxiliares de teste; as capturas IAB usam a aplicação e todas as chamadas reais ao servidor local.

Comando local de revisão: `scripts/run-local-014c.ps1 -NoBuild -ManualClock`, navegador em http://127.0.0.1:5156. Screenshots nativos eventualmente adicionados devem ficar separados das renderizações automatizadas e identificados no relatório.

## Capturas reais

- [appearance-desktop.jpg](audit-014c/browser-captures/appearance-desktop.jpg)
- [appearance-mobile.jpg](audit-014c/browser-captures/appearance-mobile.jpg)
- [blue-desktop.jpg](audit-014c/browser-captures/blue-desktop.jpg)
- [blue-mobile.jpg](audit-014c/browser-captures/blue-mobile.jpg)
- [browser-reload-restored.jpg](audit-014c/browser-captures/browser-reload-restored.jpg)
- [flamengo-desktop.jpg](audit-014c/browser-captures/flamengo-desktop.jpg)
- [flamengo-mobile.jpg](audit-014c/browser-captures/flamengo-mobile.jpg)
- [flamengo-second-trainer-same-room.jpg](audit-014c/browser-captures/flamengo-second-trainer-same-room.jpg)
- [mixed-matches-desktop.jpg](audit-014c/browser-captures/mixed-matches-desktop.jpg)
- [mixed-matches-mobile.jpg](audit-014c/browser-captures/mixed-matches-mobile.jpg)
- [picker-mobile.jpg](audit-014c/browser-captures/picker-mobile.jpg)
- [preview-mobile.jpg](audit-014c/browser-captures/preview-mobile.jpg)
- [unemployed-neutral.jpg](audit-014c/browser-captures/unemployed-neutral.jpg)
- [unknown-club-picker-neutral.jpg](audit-014c/browser-captures/unknown-club-picker-neutral.jpg)
