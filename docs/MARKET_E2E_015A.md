# Verificação real de ponta a ponta — 015A

Executada em 31/10–01/11/2026 no navegador real integrado, com interações de formulários e capturas desktop 1366×900 e mobile 390×844. Não se trata de renderização de cenas nem somente de widgets.

- Worktree exclusivo: `D:\elifoot_98_015A`; base dd7b783, branch feature/match-lock-market-ux-015a.
- API: http://127.0.0.1:5165; health `/api/health`.
- Flutter Web: http://127.0.0.1:5166; identificação `/__015a_health`.
- PostgreSQL DEV exclusivo: 127.0.0.1:55439, bancos com prefixo elifoot_015a_. Nenhum save anterior apagado.
- Sala real: `015A - Mercado e partidas`, convite ELIEF3B18BF, 128 clubes reais e jogadores simulados autorizados (REAL_CLUBS_SIMULATED).
- Treinadores persistidos: Daniel 015A/Flamengo RJ, Comprador 015A/FC Porto e Rival 015A/Arsenal FC.

A build final identifica ELIFOOT_015A, API 5165 e SHA256 de main.dart.js `02986dbb0626aeb1c24ee01ba58179199b6f0879b5a73261b2cf539631c39dc9`. O servidor de revisão usa no-store e a build não instala service worker; não foi apagado armazenamento do usuário.

Fluxos reais executados: criar/cancelar anúncio; alterar mínimo com centavos e durações 1/2/3; outro treinador dar lance personalizado e rejeitar lance baixo; anunciar preço fixo; cancelar confirmação; oferecer abaixo do preço; vendedor contrapropor; comprador aceitar; compra imediata em mobile; conferir proprietário, contrato, salário e dinheiro; filtros/ordens; espera de táticas; início automático; bloqueio de menu, voltar e rota direta; pausa e substituição; reload e recuperação; conclusão oficial e menus liberados com resultados por divisão.

Três leilões encerraram exatamente após R1/R2/R3, e o dinheiro das duas compras humanas foi reconciliado antes/depois. A IA participou com suas regras existentes. Após reinício real da API, saldos, jogadores, anúncios, ofertas, leilões e RESULTS foram iguais. Outra sala pausada recuperou GLOBAL_PAUSED no navegador após esse mesmo reinício e depois terminou em RESULTS.

A matriz HTTP executou três rodadas por sala com 2/4/6/8 humanos: aguardou N−1, iniciou somente em N, respeitou pausa, rejeitou ajuste de desempregado e concluiu RESULTS. Esses cenários não simulam oito navegadores físicos: utilizam clientes HTTP reais, complementados pela interação visual com três identidades e pela suíte SignalR existente.

Para abrir novamente no Chrome local: executar `powershell -ExecutionPolicy Bypass -File D:\elifoot_98_015A\scripts\run-local-015a.ps1 -NoBuild`, acessar http://127.0.0.1:5166 e usar apelido `Comprador 015A`, dispositivo `015a-comprador`, sala ELIEF3B18BF para recuperar FC Porto. Não substituir o dispositivo por um ID novo: ele representa outro treinador. O runner preserva processos sem registro de propriedade e utiliza somente as portas 015A.

A sala termina esta validação em RESULTS de R3. O Flamengo vendeu defensores nos testes e precisa adequar sua formação ou contratar antes da rodada seguinte; o Porto dispõe de 20 jogadores. O avanço é compartilhado: três humanos devem enviar táticas/pronto para começar automaticamente.

Os JSON públicos de evidência contêm dados do jogo de teste, sem tokens de sessão. Senhas, contextos autenticados, cluster e saves ficam apenas em artifacts/015a, ignorado pelo Git. Capturas da versão 014C não foram sobrescritas.

Ver [regressão](REGRESSION_015A.md), [limites](KNOWN_ISSUES_015A.md) e [entrega estruturada](DELIVERY_015A.md).
