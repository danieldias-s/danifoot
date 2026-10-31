# Criador e catálogo

STATUS: PASS

Masculino/feminino, oito países, ranking automático, divisões personalizadas, distritais aleatórios/manuais: quatro divisões de oito e 96 distritais. Configuração por proprietário, prévia persistida e confirmação idempotente mantidas. Atualizar catálogo não modifica mundo confirmado.

Quatro mundos HTTP: duas categorias de fixtures e duas com clubes reais + complemento fictício consentido. Todas as 512 equipes inspecionadas: salários/contratos positivos, força 1–50, estádio válido, 11–24 atletas, goleiro e dez de linha, IDs únicos. Catálogo real separado das fixtures; status, estimativa, proveniência e consentimento preservados.

Cópia do coverage-bundle.json 013B com hash igual ao original, sem nova coleta/API paga. Dados: 1.398 registros incluindo candidatos/históricos; somente 390 identidades verificadas (158 M/232 F), 3.079 atletas documentais distintos e 3.101 vínculos documentados. Um vínculo atual utilizável, zero elencos atuais completos. Identidade verificada não certifica atividade atual/elenco oficial.

PostgreSQL exclusivo 55435, senha/usuário/dados próprios em artifacts/014a. Bancos de mundos, catálogos real/fixtures e testes separados. Nenhum banco original ou produção usado. Campos JSON aditivos, sem migration destrutiva. Saves completos 009/012D recuperados e reiniciados em bancos próprios.

Reprodução: test-integration-014a.ps1 -Action Start; build Release; run-integration-014a.ps1 -Fixtures -ApiPort 5140 para carga inicial. Reiniciar sem -Fixtures usa catálogo salvo e preserva versão mais nova. -RealCatalog -ApiPort 5141 usa o catálogo real próprio. Validação final usa -BuildDirectory IntegrationFinal. verify-restart-integration-014a.ps1 confere reinício; stop-integration-014a.ps1 encerra somente PIDs próprios conferidos e, opcionalmente, seu PostgreSQL. Credenciais e volumes ficam fora do Git.
