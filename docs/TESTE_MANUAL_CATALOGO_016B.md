# Teste manual local

URL_DO_JOGO: http://127.0.0.1:5176/
URL_DA_API: http://127.0.0.1:5175
SALA_DE_TESTE: 016B - Teste manual Daniel - Catálogo e cores
CÓDIGO: ELI4B3DF49E
CLUBE_DISPONÍVEL: Flamengo RJ
TREINADOR: Daniel 016B
ID_DISPOSITIVO: 016b-revisao-daniel

No Chrome, abra o jogo, informe esse apelido e **ID de dispositivo** e entre no lobby. Entre na sala acima. Ela tem capacidade 1, com sua identidade de teste já vinculada ao Flamengo; usar outro ID cria outro participante e a vaga reservada não deve ser apagada automaticamente.

Na configuração inicial, confirme a escolha de clube e use INICIAR CAMPEONATO como criador. Depois envie a tática/pronto. Com um humano necessário, a rodada deve iniciar automaticamente após seu envio. O vínculo e o dispositivo permitem recuperar a própria sessão após fechar a página.

Para executar novamente: `cd D:\elifoot_98_016B` e `.\scripts\run-local-world-catalog-016b.ps1 -NoBuild`. Esse runner usa portas/bancos próprios. Os artefatos de build precisam existir; sem NoBuild, recompila. Não interrompe processos com propriedade diferente.

STATUS_DO_SERVIDOR: conferir /api/health e /__world_catalog_016b_health. A disponibilidade depende de manter os processos locais ativos. TESTE_MANUAL: aguardando sua confirmação; não aprovado automaticamente.
