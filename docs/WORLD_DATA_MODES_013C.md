# Modos de dados — 013C

A interface envia um modo explícito e inicia em SOMENTE DADOS REAIS COMPLETOS, com consentimentos desmarcados. A troca de categoria, países ou modo limpa os consentimentos e a prévia. Uma configuração anterior só é reutilizada por escolha do usuário.

| Modo | Clubes | Atletas iniciais | Quando falta cobertura |
|---|---|---|---|
| REAL_COMPLETE | Somente identidades reais verificadas | Somente vínculos atuais datados e suficientes | Bloqueia. Não gera atletas nem muda de modo. |
| REAL_CLUBS_SIMULATED | Somente identidades reais verificadas | Preserva vínculos atuais; completa com fictícios mediante autorização | Bloqueia se faltarem clubes ou se a geração não for autorizada. |
| FREE_WORLD | Clubes reais; históricos/não verificados só por opção adicional | Mesma separação entre vínculo comprovado e personagem gerado | Clubes fictícios só com autorização explícita para equipes e atletas. |

Selecionar o modo B não equivale a autorizar geração: é necessário marcar AUTORIZO atletas fictícios. O controle do modo livre explicita que a autorização cobre equipes e atletas. A recusa não gera atletas nem libera a confirmação de um mundo incompleto.

REAL_CLUB_VERIFIED comprova identidade esportiva conforme a evidência do catálogo. CurrentSeasonConfirmed é uma medida separada da participação datada na temporada. Os dois indicadores aparecem na interface; a identidade de um clube feminino não é anunciada como certificação de atividade atual.

A seleção continua restrita aos países, gênero e temporada escolhidos. As quatro divisões têm oito clubes cada e os distritais, 96. A atribuição manual prevalece sobre o ranking. Os distritais admitem seleção manual, ranking dos restantes ou sorteio com seed.

Quando os grupos de ranking não são comparáveis, ou só existe ordenação técnica por identificador, a 013C apresenta ProvisionalSeededOrder-v1, confiança zero, desempate determinístico por seed. Não chama essa ordem de ranking esportivo. Ranking e método de origem também ficam preservados no snapshot.

O campo DataMode é aditivo e opcional no contrato do servidor para preservar clientes, saves e testes anteriores. Ausência do campo mantém o protocolo legado já autorizado da 013A/B; a interface nova sempre envia um dos três modos. Não há conversão automática dos mundos já criados.
