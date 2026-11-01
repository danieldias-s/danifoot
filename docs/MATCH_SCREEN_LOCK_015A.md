# Bloqueio da partida — 015A

A base abria o acompanhamento uma vez por rodada, mas permitia sair pelos menus. O botão de lance também executava uma oferta calculada sem formulário. A inspeção inicial ocorreu em `dd7b783`, com `9690d95` incorporado.

O shell agora mantém um Router único. Somente snapshots autoritativos, com proteção contra versões atrasadas, controlam o bloqueio. As fases RUNNING_FIRST_HALF, HALFTIME_PAUSED, RUNNING_SECOND_HALF, GLOBAL_PAUSED, EXTRA_TIME_BREAK, RUNNING_EXTRA_TIME e EXTRA_TIME_HALFTIME forçam `/partida`. Menu, voltar, rota direta, aparência e troca de sala respeitam o mesmo estado; diálogos abertos são fechados ao começar o jogo. A restauração da sessão aguarda o snapshot antes de disponibilizar a navegação.

PREPARING permite os menus enquanto os demais treinadores enviam táticas. RESULTS e FINISHED liberam os menus e abrem o resumo uma vez. O mercado continua protegido também no servidor.

No navegador real: uma tática enviada deixou 1/3 e PREPARING; os dois demais prontos iniciaram automaticamente a partida e abriram o acompanhamento. Foram testados rota `/#/mercado`, voltar e reload durante GLOBAL_PAUSED. Uma substituição Flame-10 por Flame-07 foi efetivada e o contador passou a 1/5. Depois de continuar os treinadores e completar os dois tempos, o servidor salvou RESULTS e a interface liberou os menus com resultados por divisão e cores individuais.

A revisão identificou ainda o escape por TROCAR SALA; a build final bloqueia tanto o botão como o callback. O cenário final com dois humanos foi pausado, recarregado, reiniciado na API e acessado por `/#/calendario` e voltar: permaneceu em `/partida`, com TROCAR SALA desabilitado. A rodada foi concluída depois da captura.

Limites de substituição e janelas não foram modificados: 5 no tempo normal e 1 adicional na prorrogação. Os testes existentes de multiplayer e prorrogação passaram. Desempregados acompanham, mas não alteram clube alheio; rejeição conferida por HTTP nos mundos com 4, 6 e 8 humanos.

Evidências: [desktop final](evidence-015a/desktop-final-live-lock.png), [mobile final](evidence-015a/mobile-final-live-lock.png), [espera](evidence-015a/desktop-tactic-waiting.png), [resultado](evidence-015a/desktop-final-results.png), [matriz HTTP](evidence-015a/multiplayer-http.json). As capturas intermediárias que ainda mostravam o escape ficaram fora das evidências finais.
