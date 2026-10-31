# Regressão efetiva

STATUS: PASS

| Suíte final | Aprovados/total | Falhas | TRX |
|---|---:|---:|---|
| elifoot.application.tests.dll | 6/6 | 0 | `docs\audit-014a\tests\final_net10.0_20261031000120.trx` |
| elifoot.domain.tests.dll | 371/371 | 0 | `docs\audit-014a\tests\final_net10.0_20261031000136.trx` |
| elifoot.integration.tests.dll | 132/132 | 0 | `docs\audit-014a\tests\final_net10.0_20261031000532.trx` |
| elifoot.simulation.tests.dll | 24/24 | 0 | `docs\audit-014a\tests\final_net10.0_20261031002557.trx` |

Total distinto observado: 533; exit integral: 0. Contagem dos TRX, sem somar casos sobrepostos de branches. Flutter: 70, analyze limpo, build Web aprovado. Node: dois casos aprovados. Nenhum teste removido. Assert.False(Any) virou DoesNotContain com condição idêntica para eliminar aviso xUnit.

Comandos: dotnet build/test Elifoot.slnx -c Release, com OutputPath=bin/IntegrationFinal/net10.0/ e AppendTargetFrameworkToOutputPath=false para isolar DLLs e manter profundidade dos testes legados. flutter analyze; flutter test; flutter build web; node --test tools/catalog-013b/openfootball-directory.test.mjs. Build Release padrão também executado antes do ajuste do pacote; build final isolado inclui o ajuste.

Primeira regressão detectou catálogo persistido indevidamente reutilizado por teste. Após usar bancos novos, suíte anterior passou 130/130. A final inclui dois casos de pacote (132). Ensaio anterior interrompido para validar código final completo. Logs intermediários/erros de scripts não são evidência de aprovação final; auditoria foi executada novamente após corrigir leitura de enum/caminho.

| Mundo | Caixa T100 | Dívida T100 | Mediana | Jogos reais | Minutos reais | Variação anual T76–100 |
|---|---:|---:|---:|---:|---:|---:|
| Seed012D | R$ 137.353.919,79 | R$ 3.270.104,68 | R$ 749.694,90 | 35100 | 3264000 | R$ -135.053,81 |
| Male | R$ 79.157.786,17 | R$ 4.366.066,93 | R$ 425.999,40 | 35100 | 3249330 | R$ -4.909.762,79 |
| Female | R$ 79.157.786,17 | R$ 4.366.066,93 | R$ 425.999,40 | 35100 | 3249330 | R$ -4.909.762,79 |

Após a conferência de compatibilidade dos pacotes antigos, a regressão curta inteira foi repetida: 515 casos distintos por testId aprovados na consolidação. Um caso de recarga precisava do snapshot produzido pelo ensaio longo; a pasta curta estava sem esse arquivo. Uma cópia de hash idêntico foi fornecida e o caso passou na reexecução. Exit bruto inicial 1 e exit da reexecução 0 preservados, sem mascarar a primeira falha. Os 18 ensaios longos aprovados mantêm fontes, fábrica de cenários e motores inalterados; o complemento modifica apenas campos/fallback de pacotes do editor e seus testes. Total distinto consolidado: 533. Hashes dos motores, replay 012C e baseline 012D em verification.json. Ledgers/séries gzip completos ficam localmente, com hashes e resumos versionados. Nenhum banco original usado.
