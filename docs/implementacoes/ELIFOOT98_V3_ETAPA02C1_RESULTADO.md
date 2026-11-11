# Elifoot 98 Multiplayer V3 — Etapa 02C-1: Cálculos Econômicos Clássicos (Resultado Parcial)

**STATUS: PASS (02C-1 CONCLUÍDA — PRONTA PARA 02C-2)**

Data: 11/11/2026 (`America/Sao_Paulo`)
Diretório: `D:\elifoot_98`
Branch: `main`
Checkpoint inicial: `b3fc9e3` (`docs(elifoot98): record V3 stage 02B validation and commit`)

---

## 1. Resumo Executivo da Fase 02C-1

A Fase **02C-1** realizou a auditoria completa da base atual e a verificação instrução por instrução do executável original `ELIFOOT.EXE` (16-bit NE, `836.356` bytes, SHA-256 `1C5D59CC5E32D5C73480B47986F34CE3FA2D3D34B36BDF018076C67E4F941775`), centralizando os cálculos comprovados da economia clássica do Elifoot 98 em [`ClassicPlayerEconomyEngine.cs`](file:///D:/elifoot_98/src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs) e integrando-os aos serviços de domínio e aplicação ([`MarketAndEconomyEngine.cs`](file:///D:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs), [`SalaryContractsEngine.cs`](file:///D:/elifoot_98/src/Elifoot.Domain/SalaryContractsEngine.cs), [`MarketContractTermsEngine.cs`](file:///D:/elifoot_98/src/Elifoot.Domain/MarketContractTermsEngine.cs), [`CompetitionAndSeasonEngine.cs`](file:///D:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs), [`TransferContractPreviewQuery.cs`](file:///D:/elifoot_98/src/Elifoot.Application/TransferContractPreviewQuery.cs), [`ClubAndMarketApplicationService.cs`](file:///D:/elifoot_98/src/Elifoot.Application/ClubAndMarketApplicationService.cs), [`MarketQueriesApplicationService.cs`](file:///D:/elifoot_98/src/Elifoot.Application/MarketQueriesApplicationService.cs)).

Todos os **10 valores econômicos obrigatórios** foram explicitamente diferenciados, mantendo 100% de compatibilidade com saves e mundos existentes via versionamento de política econômica por mundo (`ClassicPlayerEconomyRules` em [`ClassicEconomyState.cs`](file:///D:/elifoot_98/src/Elifoot.Domain/ClassicEconomyState.cs)) e preservando integralmente os checkpoints das Etapas 01 (`d0489d3`, `2818cb3`), 02A (`2402e99`, `ced4e43`) e 02B (`8df9b0c`, `b3fc9e3`).

---

## 2. Matriz Comparativa (Auditoria Inicial)

| Eixo Econômico | Regra Atual (Baseline Multiplayer) | Regra do Original (`ELIFOOT.EXE`) | Evidência | Diferença | Risco | Alteração Autorizada? |
| --- | --- | --- | --- | --- | --- | --- |
| **1. Salário-base calculado (`S`)** | `Round((220 + F^2.08 * 1.65) * starMod * posMod)` (`starMod=1.20`, `posMod=1.08` para `A`) | `S = 50 * C * Round((35 * I * sqrt(R) * F^1.04 * 1000^0.04) / 50)` com clamp `[50*C, 50000*C]` e piso opcional `Max(S, S_atual)` | **CONFIRMADO** (`SEG03:7FF4..8153`, `SEG26:0062`) | No original, `S` é quase-linear (`F^1.04`), arredondado em múltiplos de `50*C`, e não depende de asterisco, posição, comportamento ou nacionalidade. | Alterar saves antigos mudaria contratos vigentes; `C=5` sem reescalar bilheteria quebraria clubes. | **SIM** (centralizado em `ClassicPlayerEconomyEngine` com versionamento por mundo e escala unitária `C=1` compatível com receitas do Multiplayer, além de suporte explícito a `C=5`). |
| **2. Valor de mercado individual (`V`)** | `Round(Max(15000, (12000 + F^2.65 * 76) * starMod * posMod * behMod * contractMod))` | `V = Round(2000 * C * I * F^1.04 * (1 - 0.04*A - 0.02*B + 0.02*D))` | **CONFIRMADO** (`SEG09:371A..37E3`) | No original, `V` usa `F^1.04`, inflação `I`, comportamento `A (0..5)` e modificadores individuais `B (0..10)` e `D (1..10)`, sem desconto por prazo de contrato `K`. | Confundir `V` com preço final de compra ou alterar mundos legados silenciosamente. | **SIM** (implementado em `ClassicPlayerEconomyEngine` separado do preço candidato `Q` e do preço final negociado, preservando saves antigos). |
| **3. Comportamento (`A = 0..5`)** | Multiplicador em `FairValue` (`1.08, 1.04, 1.00, 0.95, 0.89, 0.82`); sem efeito no salário-base. | Termo aditivo `-0.04 * A` (`0%, -4%, -8%, -12%, -16%, -20%`) dentro do modificador individual de `V`; sem efeito direto em `S`. | **CONFIRMADO** (`SEG09:376B..3789`, `SEG03:7579..75CF`) | No original, `0 = Fair Play` é `0%` e `5 = Sarrafeiro` é `-20%` no fator `(1 - 0.04*A - 0.02*B + 0.02*D)`. | Alterar efeito esportivo no motor de partidas (proibido). | **SIM** (aplicado exclusivamente na avaliação econômica `V`, sem tocar no motor de partidas). |
| **4. Asterisco (`*`)** | Multiplica `FairValue` (`1.32`/`1.22`) e `FairSalary` (`1.20`). | Multiplica por `1.10` o fator de interesse de contratação (`SEG03:5D8F..5DAE`); não multiplica `S` nem `V` diretamente (correlacionado a `D >= 8` para `M/A` na geração `SEG03:761B`). | **CONFIRMADO** (`SEG03:5BB1..5DAE`, `SEG03:761B..7630`) | No original, o multiplicador `1.10` atua na rotina de interesse de contratação, não como sobretaxa universal de 10% sobre todos os preços/salários. | Alterar bônus esportivo do asterisco (proibido). | **SIM** (implementado no fator de interesse da IA `CalculateClassicalAiHiringInterestFactor`, preservando bônus esportivos). |
| **5. Nacionalidade e Preço Candidato (`Q`)** | Sem sobretaxa econômica por nacionalidade; preço pedido IA `Fair * 1.12`. | Classificação `SEG12:0A2F` (`0=Mesmo país`, `1=Equiparado PLOP/Bosman`, `2=Estrangeiro`). Em `SEG03:63FA`: Mesmo país `(Q+30000)*1.60`, Equiparado `(Q+15000)*1.30`, Estrangeiro `Q`, mais quantização em degraus. Em `SEG03:5BB1`: interesse `1.50+0.05*N`, `1.15+0.05*N`, `1.00`. | **CONFIRMADO** (`SEG12:0A2F..0AEA`, `SEG12:0F1C..0F7A`, `SEG03:63FA..6640`, `SEG03:5BB1..5DAE`) | Duas rotinas distintas no original: etapa de preço candidato (`SEG03:63FA`) e rotina de interesse com contagem de estrangeiros `N` (`SEG03:5BB1`). | Criar limite novo de estrangeiros (proibido) ou misturar as duas rotinas. | **SIM** (implementadas separadamente a etapa de preço candidato `Q` e a rotina de interesse da IA, sem criar limite novo de estrangeiros). |
| **6. Índice Interno de Inflação (`I`)** | Inexistente como índice explícito; política operacional fixa por temporada. | `I_inicial = 2.0` (exibição `Round(I*10) = 20`), limites `[0.5, 10.0]`. Atualizado 1x por temporada no fim da época (`SEG05:0D3C`): se `Y % 100 < 50`, `delta = 0.01*(U1+U2-U3)`, senão `0.01*(U1-U2-U3)`. | **CONFIRMADO** (`SEG11:3680`, `SEG06:2F8F`, `SEG05:0D3C..0E23`) | O indicador `20` exibido é `Round(2.0 * 10)`, e não taxa anual de 20%. A variação anual real de `I` é da ordem de `±0.01..0.02`. | Alterar estádios ou bilheteria (proibido) ou permitir manipulação pelo cliente Flutter. | **SIM** (implementado no servidor em `ClassicEconomyState` e `CompetitionAndSeasonEngine` com RNG determinístico, afetando `S` e `V` sem alterar estádios/bilheteria). |
| **7. Transformação de Nome (`H, A, B, D, *`)** | Atributos sorteados na geração do mundo. | Desofuscação `.EFT` (`SEG14:07A4`: `s[i] = (s[i] - s[i-1]) mod 256`) e soma `H` dos bytes do nome (`SEG03:7516`): `A_raw = 5 - trunc(sqrt(H % (6*L)))` (defensor `(A_raw+2)%6`), `D = (H%10)+1`, `B = H%11`, `IsStar = (D>=8 && pos in M,A)`. | **CONFIRMADO** (`SEG14:07A4`, `SEG12:13FE`, `SEG03:7516..766C`) | Transformação determinística baseada nos bytes do nome visível sem `'*'`. | Regenerar atributos de jogadores existentes em saves antigos (proibido). | **SIM** (implementado em `ClassicPlayerEconomyEngine` sem sobrescrever `Behavior` ou `IsStar` de jogadores existentes). |

---

## 3. Tabela Completa de Força 1 a 50 (Salário e Valor de Mercado)

Parâmetros de referência do original: `I = 2.0` (exibição `20`), `R = 1.0`, `C = 1` (escala unitária compatível com receitas operacionais do Multiplayer / `Escudos` em `DS:0062`) e `C = 5` (tabela `Reais` em `DS:0191`), modificador individual neutro `(1 - 0.04*A - 0.02*B + 0.02*D) = 1.00`.

| Força (`F`) | Salário Atual Multiplayer (`M`, sem `*`) | Salário Atual Multiplayer (`A`, com `*`) | Salário Clássico Original (`C=1`) | Dif. Absoluta (`C=1` vs `M`) | Dif. % (`C=1` vs `M`) | Salário Clássico Original (`C=5`) | Dif. Absoluta (`C=5` vs `M`) | Dif. % (`C=5` vs `M`) | Valor de Mercado Clássico `V` (`C=1`) | Valor de Mercado Clássico `V` (`C=5`) | Grau de Confiança |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 1 | 222 | 287 | 100 | -122 | -54.95% | 500 | +278 | +125.23% | 4,000 | 20,000 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 2 | 227 | 294 | 200 | -27 | -11.89% | 1,000 | +773 | +340.53% | 8,225 | 41,125 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 3 | 236 | 306 | 300 | +64 | +27.12% | 1,500 | +1,264 | +535.59% | 12,539 | 62,695 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 4 | 249 | 323 | 400 | +151 | +60.64% | 2,000 | +1,751 | +703.21% | 16,912 | 84,561 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 5 | 267 | 346 | 500 | +233 | +87.27% | 2,500 | +2,233 | +836.33% | 21,330 | 106,649 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 6 | 289 | 374 | 600 | +311 | +107.61% | 3,000 | +2,711 | +938.06% | 25,783 | 128,916 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 7 | 314 | 408 | 700 | +386 | +122.93% | 3,500 | +3,186 | +1014.65% | 30,266 | 151,332 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 8 | 345 | 447 | 800 | +455 | +131.88% | 4,000 | +3,655 | +1059.42% | 34,776 | 173,878 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 9 | 379 | 492 | 900 | +521 | +137.47% | 4,500 | +4,121 | +1087.34% | 39,307 | 196,536 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 10 | 418 | 542 | 1,000 | +582 | +139.23% | 5,000 | +4,582 | +1096.17% | 43,859 | 219,296 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 11 | 462 | 599 | 1,100 | +638 | +138.10% | 5,500 | +5,038 | +1090.48% | 48,429 | 242,147 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 12 | 510 | 661 | 1,200 | +690 | +135.29% | 6,000 | +5,490 | +1076.47% | 53,016 | 265,081 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 13 | 562 | 729 | 1,350 | +788 | +140.21% | 6,750 | +6,188 | +1101.07% | 57,618 | 288,092 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 14 | 619 | 803 | 1,450 | +831 | +134.25% | 7,250 | +6,631 | +1071.24% | 62,235 | 311,174 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 15 | 681 | 883 | 1,550 | +869 | +127.61% | 7,750 | +7,069 | +1038.03% | 66,864 | 334,322 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 16 | 747 | 968 | 1,650 | +903 | +120.88% | 8,250 | +7,503 | +1004.42% | 71,506 | 357,532 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 17 | 818 | 1,060 | 1,750 | +932 | +113.94% | 8,750 | +7,932 | +969.68% | 76,160 | 380,800 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 18 | 894 | 1,158 | 1,850 | +956 | +106.94% | 9,250 | +8,356 | +934.68% | 80,825 | 404,123 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 19 | 974 | 1,262 | 1,950 | +976 | +100.21% | 9,750 | +8,776 | +901.03% | 85,500 | 427,498 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 20 | 1,059 | 1,372 | 2,100 | +1,041 | +98.30% | 10,500 | +9,441 | +891.50% | 90,184 | 450,922 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 21 | 1,148 | 1,488 | 2,200 | +1,052 | +91.64% | 11,000 | +9,852 | +858.19% | 94,879 | 474,393 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 22 | 1,243 | 1,610 | 2,300 | +1,057 | +85.04% | 11,500 | +10,257 | +825.18% | 99,582 | 497,909 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 23 | 1,342 | 1,739 | 2,400 | +1,058 | +78.84% | 12,000 | +10,658 | +794.19% | 104,293 | 521,467 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 24 | 1,446 | 1,873 | 2,500 | +1,054 | +72.89% | 12,500 | +11,054 | +764.45% | 109,013 | 545,067 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 25 | 1,554 | 2,014 | 2,600 | +1,046 | +67.31% | 13,000 | +11,446 | +736.55% | 113,741 | 568,706 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 26 | 1,668 | 2,161 | 2,750 | +1,082 | +64.87% | 13,750 | +12,082 | +724.34% | 118,477 | 592,383 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 27 | 1,786 | 2,314 | 2,850 | +1,064 | +59.57% | 14,250 | +12,464 | +697.87% | 123,219 | 616,096 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 28 | 1,909 | 2,474 | 2,950 | +1,041 | +54.53% | 14,750 | +12,841 | +672.66% | 127,969 | 639,844 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 29 | 2,037 | 2,640 | 3,050 | +1,013 | +49.73% | 15,250 | +13,213 | +648.65% | 132,725 | 663,627 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 30 | 2,169 | 2,812 | 3,150 | +981 | +45.23% | 15,750 | +13,581 | +626.14% | 137,488 | 687,442 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 31 | 2,307 | 2,990 | 3,300 | +993 | +43.04% | 16,500 | +14,193 | +615.21% | 142,258 | 711,289 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 32 | 2,449 | 3,174 | 3,400 | +951 | +38.83% | 17,000 | +14,551 | +594.16% | 147,033 | 735,167 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 33 | 2,597 | 3,365 | 3,500 | +903 | +34.77% | 17,500 | +14,903 | +573.85% | 151,815 | 759,075 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 34 | 2,749 | 3,563 | 3,600 | +851 | +30.96% | 18,000 | +15,251 | +554.78% | 156,602 | 783,011 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 35 | 2,906 | 3,766 | 3,700 | +794 | +27.32% | 18,500 | +15,594 | +536.61% | 161,395 | 806,976 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 36 | 3,068 | 3,977 | 3,850 | +782 | +25.49% | 19,250 | +16,182 | +527.44% | 166,194 | 830,969 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 37 | 3,235 | 4,193 | 3,950 | +715 | +22.10% | 19,750 | +16,515 | +510.51% | 170,998 | 854,988 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 38 | 3,407 | 4,416 | 4,050 | +643 | +18.87% | 20,250 | +16,843 | +494.36% | 175,807 | 879,033 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 39 | 3,584 | 4,645 | 4,150 | +566 | +15.79% | 20,750 | +17,166 | +478.96% | 180,621 | 903,103 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 40 | 3,766 | 4,881 | 4,300 | +534 | +14.18% | 21,500 | +17,734 | +470.90% | 185,440 | 927,198 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 41 | 3,953 | 5,123 | 4,400 | +447 | +11.31% | 22,000 | +18,047 | +456.54% | 190,263 | 951,317 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 42 | 4,145 | 5,372 | 4,500 | +355 | +8.56% | 22,500 | +18,355 | +442.82% | 195,092 | 975,460 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 43 | 4,342 | 5,627 | 4,600 | +258 | +5.94% | 23,000 | +18,658 | +429.71% | 199,925 | 999,625 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 44 | 4,544 | 5,889 | 4,700 | +156 | +3.43% | 23,500 | +18,956 | +417.17% | 204,763 | 1,023,813 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 45 | 4,751 | 6,157 | 4,850 | +99 | +2.08% | 24,250 | +19,499 | +410.42% | 209,605 | 1,048,023 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 46 | 4,963 | 6,432 | 4,950 | -13 | -0.26% | 24,750 | +19,787 | +398.69% | 214,451 | 1,072,255 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 47 | 5,180 | 6,713 | 5,050 | -130 | -2.51% | 25,250 | +20,070 | +387.45% | 219,302 | 1,096,508 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 48 | 5,402 | 7,001 | 5,150 | -252 | -4.66% | 25,750 | +20,348 | +376.68% | 224,156 | 1,120,781 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 49 | 5,629 | 7,295 | 5,300 | -329 | -5.84% | 26,500 | +20,871 | +370.78% | 229,015 | 1,145,075 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |
| 50 | 5,861 | 7,596 | 5,400 | -461 | -7.87% | 27,000 | +21,139 | +360.67% | 233,878 | 1,169,388 | CONFIRMADO (`SEG03:7FF4` / `SEG09:371A`) |

---

## 4. Validações Executadas na Fase 02C-1

- **Build Release (`dotnet build -c Release --no-restore`):** PASS (`0 Aviso(s)`, `0 Erro(s)`).
- **`Elifoot.Domain.Tests` (suíte completa):** PASS (`625` aprovados, `0` falhas — inclui os `9` novos testes de `ClassicalEconomyV3Stage02CTests`).
- **`Elifoot.Application.Tests` (suíte completa):** PASS (`55` aprovados, `0` falhas).
- **`Elifoot.Integration.Tests` (direcionados 02A + 02B + 02C + 012D):** PASS (`16` aprovados, `0` falhas — inclui `ClassicalEconomyV3Stage02CIntegrationTests`).
- **`flutter analyze`:** PASS (`No issues found!`).
- **`flutter test` (suíte completa):** PASS (`505` aprovados, `0` falhas — inclui `classical_economy_v3_stage02c_test.dart`).
