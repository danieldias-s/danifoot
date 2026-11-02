# Força inicial do clube

ClubInitialPower é um campo independente, entre 1 e 20. Não substitui força individual 1..50, reputação ou coeficientes defensivos.

Regra automática: max(1, 20 - floor(indiceGlobalBaseZero/2)). Posições 1/2 recebem 20, 3/4 recebem 19, e assim até 39/40 com 1; os demais ficam em 1. Todos continuam ordenados pelo detalhe do ranking e identificador.

A edição administrativa /api/world-catalog-016b/admin/club-power exige clube já projetado, temporada, valor 1..20 e evidência textual, além de acesso local autenticado e token administrativo 016A. Não aplica alteração ao save. ManualPower e ManualPowerEvidence persistem; não se redistribuem valores manuais só para refazer pares. Testes cobrem vários clubes manuais com força 20 e rejeição de 21.

O formulário público mostra força/ranking, mas ainda não oferece edição manual completa desses campos. O endpoint administrativo está disponível; ver pendências.
