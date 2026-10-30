# Pendências e limites — 012

**STATUS: PARTIAL.** As correções técnicas não completam a reconstrução histórica.

1. **Perda nominal por contusão pendente (prioridade máxima).** Relato histórico confirma interesse da mecânica, mas não há fórmula de perda/recuperação verificável. Lesão ainda funciona como indisponibilidade e efeito temporário de finalização do projeto. Não foi fabricado desconto para declarar cumprimento.
2. **Evolução original por rodada pendente.** FUN_17fb_4a41 foi investigada; avaliador, base do clube e elegibilidade de reservas não estão integralmente publicados. README e pseudocódigo discordam nos limites. Política sazonal da 005 foi preservada com correção do mínimo 1.
3. **Goleiros.** Guarda extra de 50% tem evidência técnica pública, mas não foi misturada ao sistema sazonal sem o restante do avaliador. No projeto, goleiros continuam na mesma política dos demais.
4. **Crescimento lento de força 1.** Controle de 30 temporadas chega a 22 jogando ou 15 sem entrar. A estratégia é viável em longo prazo; não se declara compatibilidade com a velocidade histórica sugerida por relatos. Não há novo bônus de classificação.
5. **Indisponibilidade com exceção legada.** IA/validador possuem fallback de emergência para completar time com indisponíveis. A 012 não o criou nem ampliou. A regra do original para isso não foi confirmada.
6. **Duração de lesões.** Só diminui em jogos do clube. Eliminados da Taça e distritais podem permanecer afastados por várias rodadas globais. Probabilidade 0,0018 e duração 1–3 são parâmetros já existentes, não resultados da pesquisa original.
7. **Efeitos sazonais de divisão.** Alvos 37/30/25/21/17 e perda por força acima do alvo são balanceamento anterior. Uma transferência a divisão baixa não corta força imediatamente, mas pode influenciar ajuste futuro. Não se afirma fidelidade disso.
8. **Cobertura histórica.** Eventos iniciam no primeiro processamento novo, sem preencher retrospectivamente. Divisão no evento é a vigente após promoções/rebaixamentos do fechamento. As forças perdidas em versões anteriores não podem ser reconstruídas.
9. **Escopo dos ensaios.** Seeds reproduzíveis e motor real verificam o clone. Não há testes executados no Elifoot 98 original. SQLite/TestServer e regressão multiplayer não são novo ensaio de produção, Android ou navegador. Nenhuma publicação foi feita.
10. **Custo de histórico.** Retenção integral amplia o JSON com os anos. Não foi apagado histórico para ocultar esse custo. RNG legado depende da ordem da lista de jogadores, como antes.
11. **Oferta inicial de força 1.** A seed padrão da comparação tem mínimo 10 e zero jogadores força 1. Editor/importação permitem criá-los; os testes configuram um e executam a compra real. Geração de elencos/mercado foi preservada. Não se declara que toda sala padrão já ofereça um força 1 para compra.

Detalhes: [pesquisa](ORIGINAL_PLAYER_FORCE_RESEARCH_012.md), [evolução](PLAYER_FORCE_EVOLUTION_012.md), [lesões](INJURY_STRENGTH_LOSS_012.md), [limites](DIVISION_FORCE_LIMITS_012.md), [histórico](PLAYER_FORCE_HISTORY_012.md), [testes e relatório final](PLAYER_FORCE_TESTS_012.md).
