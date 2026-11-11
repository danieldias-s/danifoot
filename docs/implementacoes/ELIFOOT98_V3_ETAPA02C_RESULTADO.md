# ELIFOOT 98 MULTIPLAYER — V3 ETAPA 02C (RELATÓRIO FINAL CONSOLIDADO)
## ECONOMIA CLÁSSICA DO ELIFOOT 98 (`02C-1` + `02C-2`)

- **Data:** 2026-11-11
- **Branch:** `main`
- **Checkpoints preservados:**
  - Etapa 01: `d0489d3`, `2818cb3`
  - Etapa 02A: `2402e99`, `ced4e43`
  - Etapa 02B: `8df9b0c`, `b3fc9e3`
  - Subfase 02C-1 (Implementação da Economia Clássica): `82bdadb` (`feat(elifoot98): implement V3 stage 02C classical economy`)
- **Escopo:** Subfase `02C-1` (Auditoria de engenharia reversa no binário original `ELIFOOT.EXE` + implementação dos cálculos econômicos clássicos) e Subfase `02C-2` (Validação de longo prazo em `1`, `3`, `5`, `10` e `20` temporadas + suíte completa de regressão .NET e Flutter).

---

## 1. Resumo Executivo

A Etapa 02C reproduziu com fidelidade verificável os cálculos econômicos comprovados do executável original `ELIFOOT.EXE` (Elifoot 98 para Windows 3.1/95, NE 16-bit), preservando integralmente as regras já aprovadas do **Elifoot 98 Multiplayer** nas Etapas 01, 02A e 02B:

1. **Diferenciação explícita das 10 grandezas econômicas obrigatórias** (`ClassicPlayerEconomyEngine.EvaluatePlayerEconomy`):
   - (1) Força do jogador (`F = 1..50`)
   - (2) Salário atual (`SalaryPerRound`)
   - (3) Salário-base calculado (`S`, `SEG03:7FF4`)
   - (4) Valor de mercado individual (`V`, `SEG09:371A`)
   - (5) Preço candidato de contratação (`Q`, `SEG03:63FA` + escada de quantização `SEG03:6558..6640`)
   - (6) Preço final negociado (transferência efetiva / lance vencedor de leilão)
   - (7) Lance mínimo de leilão (`R$ 0` obrigatório / `R$ 500.000` padrão voluntário)
   - (8) Salário exigido na renovação (Etapa 02B preservada, alimentada pela referência salarial clássica)
   - (9) Interesse da IA na contratação (`SEG03:5BB1..5DAE`: nacionalidade $\times$ número de estrangeiros $N \times$ multiplicador `1.10` do asterisco `*`)
   - (10) Índice interno de inflação (`I`, inicial `2.0`, limites `[0.5, 10.0]`, exibição `round(I * 10) = 20`, evolução anual `SEG11:3680`).
2. **Versionamento explícito por mundo (`ClassicPlayerEconomyRules`):**
   - Mundos e saves existentes sem configuração explícita preservam `Policy = "MULTIPLAYER_CALIBRATED_012D"`, mantendo 100% de compatibilidade retroativa e conciliação idêntica ao centavo nos experimentos históricos (`R$ 18.107.128.451,84` em 100 temporadas).
   - Quando a economia clássica (`Policy = "CLASSICAL_ELIFOOT98_V3_02C"`) é ativada via `ClassicPlayerEconomyEngine.EnableClassicalEconomy`, todos os motores de domínio, aplicação, API e Flutter operam com as fórmulas clássicas comprovadas.
3. **Validação de longo prazo (`02C-2`):**
   - Simulações reproduzíveis executadas para **1, 3, 5, 10 e 20 temporadas** (com 8 técnicos humanos simulados + 120 clubes IA em `ScenarioB_EightHumanAgents`, além de 20 temporadas com 128 clubes IA em `ScenarioA_AllAi`), aprovadas sem falência generalizada artificial, sem hiperinflação fora de `[0.5, 10.0]`, sem salários ou preços negativos/absurdos, sem paralisação do mercado e com `0` violações de elenco ou escalação.

---

## 2. Hash e Identificação das Fontes Analisadas

| Artefato | Caminho | Tamanho (bytes) | SHA-256 | Identificação Técnica |
|---|---|---:|---|---|
| Executável original `ELIFOOT.EXE` | `C:\Users\Daniel\Downloads\ELIFOOT.EXE` | `836.356` | `1C5D59CC5E32D5C73480B47986F34CE3FA2D3D34B36BDF018076C67E4F941775` | Executável 16-bit New Executable (`NE`, linker `5.10`), Borland C++ / ObjectWindows (`BWCC` / `BC450RTL`), 26 segmentos de código/dados |
| Documentação técnica consolidada | `C:\Users\Daniel\Downloads\ELIFOOT_98_DOCUMENTACAO_TECNICA_CONSOLIDADA.md` | `78.414` | `840FEF091361BA4EA97742587F7EE04E0066DB61084661D766918D48C4ADB248` | Dossiê consolidado de engenharia reversa estática do `ELIFOOT.EXE` |
| Proposta Etapa 02 | `docs/implementacoes/ELIFOOT98_V3_ETAPA02_PROPOSTA.md` | `15.462` | Verificado no repositório | Especificação arquitetural da Etapa 02 (02A, 02B, 02C, 02D) |
| Resultado Etapa 02A | `docs/implementacoes/ELIFOOT98_V3_ETAPA02A_RESULTADO.md` | `20.918` | Verificado no repositório | Leilão voluntário de jogadores (`PASS`, commit `ced4e43`) |
| Resultado Etapa 02B | `docs/implementacoes/ELIFOOT98_V3_ETAPA02B_RESULTADO.md` | `31.845` | Verificado no repositório | Pedidos salariais espontâneos e renovação (`PASS`, commit `b3fc9e3`) |

---

## 3. Matriz Comparativa Completa: Atual vs Original

| # | Subsistema / Grandeza | Regra Anterior do Multiplayer (`012D`) | Regra do `ELIFOOT.EXE` Original | Evidência no Binário | Diferença Técnica | Risco | Alteração Autorizada e Aplicada? |
|---|---|---|---|---|---|---|---|
| 1 | **Força (`F`)** | Inteiro `1..50`, evolução por teto de divisão (`ClassicForceEvolutionEngine`) | Inteiro `1..50` (`offset +0x17`) | `SEG03:7516`, `SEG03:7FF4` | Nenhuma na escala (`1..50`) | Baixo | Mantida escala `1..50`; atualizado recálculo de `MarketPrice` pós-evolução |
| 2 | **Salário atual (`SalaryPerRound`)** | Persistido no atleta e descontado por rodada jogada | Persistido em `offset +0x22` e descontado na folha da rodada | `SEG03:7FF4`, `SEG09:33CA` | Nenhuma conceitual | Baixo | Preservado contrato vigente |
| 3 | **Salário-base calculado (`S`)** | `Round(((120 + F^1.72 * 5.2) * posMod * starMod)/50)*50` | `50*C*round((35*I*sqrt(R)*F^1.04*1000^0.04)/50)`, clamp `[50*C, 50000*C]`, piso no salário atual quando aplicável | **CONFIRMADO** em `SEG03:7FF4..80DC` | Original usa `F^1.04`, inflação `I`, `sqrt(R)` e NÃO aplica multiplicador de posição ou de asterisco no salário-base | Médio (escala `C=5` sem reescalar receitas quebraria clubes; usado `C=1` por padrão) | **SIM** (`ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary`) |
| 4 | **Valor de mercado (`V`)** | `Round((F^2.18 * 82 * posMod * starMod * behMod)/1000)*1000` (mín. `15.000`) | `round(2000 * C * I * F^1.04 * (1 - 0.04*A - 0.02*B + 0.02*D))` | **CONFIRMADO** em `SEG09:371A..37B6` | Original é quase-linear em `F` (`F^1.04`), penaliza comportamento `A` em `-4%` por nível (`0..5`), usa `B` (`-0.02*B`) e `D` (`+0.02*D`), sem bônus direto de `*` em `V` | Baixo | **SIM** (`ClassicPlayerEconomyEngine.CalculateClassicalMarketValue`) |
| 5 | **Atributos pessoais (`A`, `B`, `D`, `*`)** | `Behavior` e `IsStar` sorteados na geração; sem `B` e `D` explícitos | Derivados da soma `H` dos bytes do nome (`SEG03:7516`): `A_raw = 5 - trunc(sqrt(H % (6*L)))` (defensor `(A_raw+2)%6`), `D = (H%10)+1`, `B = H%11`, `* = (D>=8 && Pos in {M,A})` | **CONFIRMADO** em `SEG03:7516..7607` e `SEG14:07A4` | `A`, `B`, `D` são determinísticos pelo nome no `.EFT`, mas `Behavior`/`IsStar` já existentes nos mundos devem ser preservados | Baixo | **SIM** (suporte a `B` e `D` com fallback determinístico por nome sem alterar `Behavior`/`IsStar` existentes) |
| 6 | **Preço candidato de contratação (`Q`)** | `fair * 1.04` (listado) ou `fair * 1.12` (não listado) | Etapa nacionalidade `SEG03:63FA`: mesmo país `(Q0+30000)*1.60`, equiparado `(Q0+15000)*1.30`, estrangeiro `Q0`, seguida de quantização por faixas (`SEG03:6558..6640`) | **CONFIRMADO** em `SEG03:63FA..6640` e `SEG12:0A2F` | No original, mesmo país é mais caro (`*1.60` após `+30000`) e equiparado tem `+15000` e `*1.30` | Baixo | **SIM** (`ApplyClassicalNationalityPriceStage` + `QuantizeClassicalPrice`) |
| 7 | **Interesse da IA na contratação** | Utilidade baseada em `(Strength + StarBonus) / Price` | Multiplicador em `SEG03:5BB1..5DAE`: mesmo país `1.50 + 0.05*N`, equiparado `1.15 + 0.05*N`, estrangeiro `1.00`, e `* 1.10` quando jogador possui asterisco (`*`) | **CONFIRMADO** em `SEG03:5BB1..5DAE` | O multiplicador `1.10` do asterisco e o bônus `0.05*N` atuam na rotina de interesse/avaliação de contratação da IA | Baixo | **SIM** (`CalculateClassicalAiHiringInterestFactor`) |
| 8 | **Lance mínimo e leilões** | Obrigatório `R$ 0`; Voluntário padrão `R$ 500.000` (editável `>= 0`, duração `1..3` rodadas) | No original, venda é imediata com lances estocásticos e piso `500 * C` | `SEG03:6374..6640` vs Etapa 02A aprovada | No Multiplayer, o leilão assíncrono entre rodadas das Etapas 02A/02B é regra aprovada do projeto | Alto se alterado | **NÃO ALTERAR regras 02A/02B**; integrado `V` clássico como referência dos lances da IA |
| 9 | **Renovação e pedidos espontâneos** | Regras da Etapa 02B (`PreviewRenewal`, `RespondSalary`, `GenerateSalaryRequests`, bloqueio de venda na temporada) | Negociação anual no fim da temporada | Etapa 02B aprovada (`8df9b0c`, `b3fc9e3`) | Regras de fluxo/decisão da Etapa 02B são cláusulas pétreas | Alto se alterado | **NÃO ALTERAR fluxo 02B**; integrado `CalculateFairSalaryPerRound(world, player, club)` como referência salarial |
| 10 | **Índice interno de inflação (`I`)** | `InflationRate = 1.0` (sem evolução estocástica clássica ativa) | Inicial `I = 2.0` (`round(I*10) = 20`), limites `[0.5, 10.0]`, atualização anual `delta = 0.01*(U1 ± U2 - U3)` conforme `Y % 100 < 50` | **CONFIRMADO** em `SEG11:3680`, `SEG06:2F8F`, `SEG05:0D3C` | Evolução anual estocástica controlada com piso `0.5` e teto `10.0` | Baixo | **SIM** (`AdvanceInflationIndex` + `AdvanceWorldInflationAtSeasonEnd`) |

---

## 4. Classificação das Evidências

1. **CONFIRMADO (Validado instrução por instrução no binário `ELIFOOT.EXE`):**
   - `SEG03:7FF4..80DC`: Rotina de cálculo de salário-base `S` com `F^1.04`, `1000^0.04`, `35`, `50`, `sqrt(R)`, `I`, arredondamento `round(...)` (`CALL 23:244B`), multiplicação por `50 * C`, piso `50 * C`, teto `50000 * C` (`0xC350 * C`) e preservação de piso pelo salário vigente quando `arg_0 == 0`.
   - `SEG09:371A..37B6`: Rotina de avaliação individual `V = round(2000 * C * I * F^1.04 * (1 - 0.04*A - 0.02*B + 0.02*D))`.
   - `SEG03:7516..7607`: Rotina de derivação determinística de `A` (`0..5`, com deslocamento `(A+2)%6` para defensores), `D` (`1..10`), `B` (`0..10`) e asterisco (`D >= 8` para `M` e `A`) a partir da soma `H` dos bytes do nome.
   - `SEG14:07A4..07E2`: Rotina de de-ofuscação dos arquivos `.EFT`.
   - `SEG12:0A2F..0AD0`: Classificação de nacionalidade (`0` mesmo país, `1` equiparado PLOP / Bosman UE, `2` estrangeiro).
   - `SEG03:63FA..6640`: Etapa de nacionalidade no preço candidato (`(Q+30000)*1.60`, `(Q+15000)*1.30`, `Q`) e escada de quantização (`100k, 50k, 25k, 10k, 1k`).
   - `SEG03:5BB1..5DAE`: Rotina de interesse/avaliação de contratação com fatores `1.50 + 0.05*N`, `1.15 + 0.05*N`, `1.00` e multiplicador `1.10` para jogador com asterisco (`*`).
   - `SEG11:3680..3755`, `SEG06:2F8F`, `SEG05:0D3C`: Inicialização `I = 2.0`, exibição `round(I * 10)`, evolução anual por `Y % 100 < 50` com `0.01 * (U1 ± U2 - U3)` e clamp `[0.5, 10.0]`.
   - `SEG26:0062`: Tabela de moedas (`Escudos (contos)` com `C = 1`, `Reais` com `C = 5`).
2. **PROVÁVEL (Forte evidência estrutural, mas acoplada ao loop estocástico local do Windows 3.1):**
   - `R` (`team_ptr + 0x4CE`): Fator de prestígio/força relativa da equipe, inicializado em `1.0` no início de carreira. No Multiplayer foi parametrizado em `ClubFactorsR` com default `1.0`.
   - Amostras `U1, U2, U3` em `SEG11:3680`: Obtidas via PRNG Borland `SEG11:39BA` com módulo `16` (`0..15`). Implementado com `DeterministicRng` reproduzível por `(world.Seed, yearCounter)` no mesmo intervalo `0..15`.
3. **NÃO COMPROVADO (Hipóteses descartadas ou não aplicadas):**
   - Hipótese de que o asterisco (`*`) aumentaria diretamente o salário-base `S` ou o valor de mercado `V` em `+10%`: **DESCARTADA** (confirmado no assembly que `SEG03:7FF4` e `SEG09:371A` não leem o bit `0x20` do asterisco; apenas `SEG03:5CE7` multiplica por `1.10`, e `D >= 8` já contribui `+0.16..+0.20` em `V`).
   - Hipótese de que o comportamento `A` daria desconto direto no lance mínimo de leilão: **DESCARTADA** (`-0.04 * A` é termo interno de `V` em `SEG09:371A`).
   - Hipótese de que a nacionalidade alteraria diretamente o salário `S`: **DESCARTADA** (`SEG03:7FF4` não lê nacionalidade).

---

## 5. Fórmulas Confirmadas

### 5.1. Salário-Base Clássico (`SEG03:7FF4`)
$$S = \text{clamp}\left(50 \cdot C \cdot \text{round}\left(\frac{35 \cdot I \cdot \sqrt{R} \cdot F^{1.04} \cdot 1000^{0.04}}{50}\right),\ 50 \cdot C,\ 50000 \cdot C\right)$$
Quando calculado com proteção de salário vigente (`enforceCurrentSalaryFloor = true`, correspondente a `arg_0 == 0` em `SEG03:80B9`):
$$S_{\text{efetivo}} = \max(S,\ \text{SalárioAtual})$$

### 5.2. Valor de Mercado Individual (`SEG09:371A`)
$$V = \text{round}\left(2000 \cdot C \cdot I \cdot F^{1.04} \cdot (1 - 0.04 A - 0.02 B + 0.02 D)\right)$$
Onde:
- $F \in [1, 50]$: força nominal do jogador;
- $I \in [0.5, 10.0]$: índice interno de inflação (inicial $2.0$);
- $C \in \{1, 5\}$: fator monetário ($C = 1$ padrão calibrado com receitas operacionais do Multiplayer; $C = 5$ escala bruta `Reais` de `DS:0191`);
- $A \in \{0, 1, 2, 3, 4, 5\}$: código clássico de comportamento (`FairPlay = 0` .. `Sarrafeiro = 5`);
- $B \in [0, 10]$: modificador secundário individual (`H % 11`);
- $D \in [1, 10]$: modificador positivo de aptidão (`(H % 10) + 1`; gera asterisco `*` quando $D \ge 8$ em `M` ou `A`).

### 5.3. Etapa de Nacionalidade no Preço Candidato (`SEG03:63FA` + `SEG03:6558..6640`)
$$Q_{\text{nac}} = \begin{cases} (Q_0 + 30000) \cdot 1.60, & \text{Mesmo país (classe 0)} \\ (Q_0 + 15000) \cdot 1.30, & \text{País equiparado (classe 1: PLOP / UE)} \\ Q_0, & \text{Estrangeiro não equiparado (classe 2)} \end{cases}$$
Seguido da escada de quantização de `SEG03:6558..6640`:
- Acima de `500.000 * C`: múltiplos de `100.000 * C`
- Acima de `200.000 * C`: múltiplos de `50.000 * C`
- Acima de `100.000 * C`: múltiplos de `25.000 * C`
- Acima de `10.000 * C`: múltiplos de `10.000 * C`
- Caso contrário: múltiplos de `1.000 * C` (mínimo `500 * C`).

### 5.4. Fator de Interesse da IA na Contratação (`SEG03:5BB1..5DAE`)
$$M_{\text{IA}} = M_{\text{nac}}(N) \times \begin{cases} 1.10, & \text{se IsStar (`*`)} \\ 1.00, & \text{caso contrário} \end{cases}$$
Onde, sendo $N$ o número de jogadores não-nacionais no elenco do comprador:
$$M_{\text{nac}}(N) = \begin{cases} 1.50 + 0.05 N, & \text{Mesmo país (classe 0)} \\ 1.15 + 0.05 N, & \text{País equiparado (classe 1)} \\ 1.00, & \text{Estrangeiro (classe 2)} \end{cases}$$

### 5.5. Evolução Anual do Índice Interno de Inflação (`SEG11:3680`)
$$\Delta I = \begin{cases} 0.01 \cdot (U_1 + U_2 - U_3), & \text{se } (Y \bmod 100) < 50 \\ 0.01 \cdot (U_1 - U_2 - U_3), & \text{se } (Y \bmod 100) \ge 50 \end{cases}$$
$$I_{t+1} = \text{clamp}(I_t + \Delta I,\ 0.5,\ 10.0), \qquad I_{\text{exibição}} = \text{round}(10 \cdot I_{t+1})$$

---

## 6. Fórmulas Prováveis

- **Fator de equipe $R$ (`team_ptr + 0x4CE`):** Em `SEG03:801E`, o binário carrega `qword ptr [bx+0x4CE]`, extrai a raiz quadrada (`CALL 23:227B`) e multiplica na expressão de `S`. Na inicialização de mundo o valor neutro é `R = 1.0` ($\sqrt{R} = 1.0$). O domínio expõe `ClassicEconomyState.ClubFactorsR` (default `1.0` por clube) para permitir leitura e parametrização sem hipótese especulativa.
- **Amostras $U_1, U_2, U_3 \in [0, 15]$ em `SEG11:3680`:** Reproduzidas deterministicamente via `DeterministicRng` com semente derivada de `(world.Seed, yearCounter)`.

---

## 7. Hipóteses Descartadas ou Não Aplicadas

1. **Aplicação cega de $C = 5$ sem reescalar bilheteria/patrocínio:** No Multiplayer, o patrocínio (`R$ 72k..185k`/temporada) e os ingressos (`R$ 8..28`) operam na escala unitária ($C = 1$). Multiplicar apenas salários e valores por $5$ mantendo receitas em $C = 1$ causaria falência artificial generalizada e violaria a proibição de alterar estádios/público/bilheteria (Seções 9 e 20). Por isso, `MonetaryFactorC` tem default `1` no modo clássico do Multiplayer, enquanto `C = 5` permanece suportado e testado no motor para conferência histórica exata da tabela `DS:0191`.
2. **Desconto direto de comportamento no lance mínimo de leilão:** Não existe no original; `-0.04 * A` pertence exclusivamente ao cálculo de `V` (`SEG09:371A`).
3. **Aumento automático de `+10%` em todos os preços ou salários por causa do asterisco (`*`):** Descartado pelo disassembly de `SEG03:7FF4` e `SEG09:371A`.
4. **Criação de novo limite de estrangeiros na compra:** Proibido pela Seção 8; preservada a política de estrangeiros existente do Multiplayer.

---

## 8. Tabela de Força 1 a 50 com Salários

Comparação completa para $F = 1..50$ entre o salário do Multiplayer (`012D`, Meia sem asterisco e Atacante com asterisco) e o salário-base clássico $S$ do `ELIFOOT.EXE` (`SEG03:7FF4`, com $I = 2.0, R = 1.0$, para $C = 1$ e $C = 5$):

| Força (`F`) | Salário Atual Multiplayer (`M`, s/ `*`) | Salário Atual Multiplayer (`A`, c/ `*`) | Original Calculado `S` (`C=1, I=2.0, R=1.0`) | Dif. Abs. (`C=1` vs `M`) | Dif. % (`C=1` vs `M`) | Original Calculado `S` (`C=5, I=2.0, R=1.0`) | Dif. Abs. (`C=5` vs `M`) | Dif. % (`C=5` vs `M`) | Grau de Confiança |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | R$ 150 | R$ 150 | R$ 100 | -50 | -33.3% | R$ 500 | +350 | +233.3% | CONFIRMADO (`SEG03:7FF4`) |
| 2 | R$ 150 | R$ 150 | R$ 200 | +50 | +33.3% | R$ 1,000 | +850 | +566.7% | CONFIRMADO (`SEG03:7FF4`) |
| 3 | R$ 150 | R$ 200 | R$ 300 | +150 | +100.0% | R$ 1,500 | +1,350 | +900.0% | CONFIRMADO (`SEG03:7FF4`) |
| 4 | R$ 200 | R$ 200 | R$ 400 | +200 | +100.0% | R$ 2,000 | +1,800 | +900.0% | CONFIRMADO (`SEG03:7FF4`) |
| 5 | R$ 200 | R$ 250 | R$ 500 | +300 | +150.0% | R$ 2,500 | +2,300 | +1150.0% | CONFIRMADO (`SEG03:7FF4`) |
| 6 | R$ 250 | R$ 300 | R$ 600 | +350 | +140.0% | R$ 3,000 | +2,750 | +1100.0% | CONFIRMADO (`SEG03:7FF4`) |
| 7 | R$ 300 | R$ 300 | R$ 700 | +400 | +133.3% | R$ 3,500 | +3,200 | +1066.7% | CONFIRMADO (`SEG03:7FF4`) |
| 8 | R$ 300 | R$ 350 | R$ 800 | +500 | +166.7% | R$ 4,000 | +3,700 | +1233.3% | CONFIRMADO (`SEG03:7FF4`) |
| 9 | R$ 350 | R$ 400 | R$ 900 | +550 | +157.1% | R$ 4,500 | +4,150 | +1185.7% | CONFIRMADO (`SEG03:7FF4`) |
| 10 | R$ 400 | R$ 500 | R$ 1,000 | +600 | +150.0% | R$ 5,000 | +4,600 | +1150.0% | CONFIRMADO (`SEG03:7FF4`) |
| 11 | R$ 450 | R$ 550 | R$ 1,100 | +650 | +144.4% | R$ 5,500 | +5,050 | +1122.2% | CONFIRMADO (`SEG03:7FF4`) |
| 12 | R$ 500 | R$ 600 | R$ 1,200 | +700 | +140.0% | R$ 6,000 | +5,500 | +1100.0% | CONFIRMADO (`SEG03:7FF4`) |
| 13 | R$ 550 | R$ 650 | R$ 1,350 | +800 | +145.5% | R$ 6,750 | +6,200 | +1127.3% | CONFIRMADO (`SEG03:7FF4`) |
| 14 | R$ 600 | R$ 750 | R$ 1,450 | +850 | +141.7% | R$ 7,250 | +6,650 | +1108.3% | CONFIRMADO (`SEG03:7FF4`) |
| 15 | R$ 700 | R$ 800 | R$ 1,550 | +850 | +121.4% | R$ 7,750 | +7,050 | +1007.1% | CONFIRMADO (`SEG03:7FF4`) |
| 16 | R$ 750 | R$ 900 | R$ 1,650 | +900 | +120.0% | R$ 8,250 | +7,500 | +1000.0% | CONFIRMADO (`SEG03:7FF4`) |
| 17 | R$ 800 | R$ 950 | R$ 1,750 | +950 | +118.8% | R$ 8,750 | +7,950 | +993.8% | CONFIRMADO (`SEG03:7FF4`) |
| 18 | R$ 900 | R$ 1,050 | R$ 1,850 | +950 | +105.6% | R$ 9,250 | +8,350 | +927.8% | CONFIRMADO (`SEG03:7FF4`) |
| 19 | R$ 950 | R$ 1,150 | R$ 1,950 | +1,000 | +105.3% | R$ 9,750 | +8,800 | +926.3% | CONFIRMADO (`SEG03:7FF4`) |
| 20 | R$ 1,050 | R$ 1,250 | R$ 2,100 | +1,050 | +100.0% | R$ 10,500 | +9,450 | +900.0% | CONFIRMADO (`SEG03:7FF4`) |
| 21 | R$ 1,150 | R$ 1,350 | R$ 2,200 | +1,050 | +91.3% | R$ 11,000 | +9,850 | +856.5% | CONFIRMADO (`SEG03:7FF4`) |
| 22 | R$ 1,200 | R$ 1,450 | R$ 2,300 | +1,100 | +91.7% | R$ 11,500 | +10,300 | +858.3% | CONFIRMADO (`SEG03:7FF4`) |
| 23 | R$ 1,300 | R$ 1,550 | R$ 2,400 | +1,100 | +84.6% | R$ 12,000 | +10,700 | +823.1% | CONFIRMADO (`SEG03:7FF4`) |
| 24 | R$ 1,400 | R$ 1,650 | R$ 2,500 | +1,100 | +78.6% | R$ 12,500 | +11,100 | +792.9% | CONFIRMADO (`SEG03:7FF4`) |
| 25 | R$ 1,500 | R$ 1,750 | R$ 2,600 | +1,100 | +73.3% | R$ 13,000 | +11,500 | +766.7% | CONFIRMADO (`SEG03:7FF4`) |
| 26 | R$ 1,600 | R$ 1,850 | R$ 2,750 | +1,150 | +71.9% | R$ 13,750 | +12,150 | +759.4% | CONFIRMADO (`SEG03:7FF4`) |
| 27 | R$ 1,700 | R$ 1,950 | R$ 2,850 | +1,150 | +67.6% | R$ 14,250 | +12,550 | +738.2% | CONFIRMADO (`SEG03:7FF4`) |
| 28 | R$ 1,800 | R$ 2,100 | R$ 2,950 | +1,150 | +63.9% | R$ 14,750 | +12,950 | +719.4% | CONFIRMADO (`SEG03:7FF4`) |
| 29 | R$ 1,900 | R$ 2,200 | R$ 3,050 | +1,150 | +60.5% | R$ 15,250 | +13,350 | +702.6% | CONFIRMADO (`SEG03:7FF4`) |
| 30 | R$ 2,000 | R$ 2,350 | R$ 3,150 | +1,150 | +57.5% | R$ 15,750 | +13,750 | +687.5% | CONFIRMADO (`SEG03:7FF4`) |
| 31 | R$ 2,100 | R$ 2,450 | R$ 3,300 | +1,200 | +57.1% | R$ 16,500 | +14,400 | +685.7% | CONFIRMADO (`SEG03:7FF4`) |
| 32 | R$ 2,200 | R$ 2,600 | R$ 3,400 | +1,200 | +54.5% | R$ 17,000 | +14,800 | +672.7% | CONFIRMADO (`SEG03:7FF4`) |
| 33 | R$ 2,300 | R$ 2,700 | R$ 3,500 | +1,200 | +52.2% | R$ 17,500 | +15,200 | +660.9% | CONFIRMADO (`SEG03:7FF4`) |
| 34 | R$ 2,450 | R$ 2,850 | R$ 3,600 | +1,150 | +46.9% | R$ 18,000 | +15,550 | +634.7% | CONFIRMADO (`SEG03:7FF4`) |
| 35 | R$ 2,550 | R$ 3,000 | R$ 3,700 | +1,150 | +45.1% | R$ 18,500 | +15,950 | +625.5% | CONFIRMADO (`SEG03:7FF4`) |
| 36 | R$ 2,650 | R$ 3,150 | R$ 3,850 | +1,200 | +45.3% | R$ 19,250 | +16,600 | +626.4% | CONFIRMADO (`SEG03:7FF4`) |
| 37 | R$ 2,800 | R$ 3,300 | R$ 3,950 | +1,150 | +41.1% | R$ 19,750 | +16,950 | +605.4% | CONFIRMADO (`SEG03:7FF4`) |
| 38 | R$ 2,900 | R$ 3,450 | R$ 4,050 | +1,150 | +39.7% | R$ 20,250 | +17,350 | +598.3% | CONFIRMADO (`SEG03:7FF4`) |
| 39 | R$ 3,050 | R$ 3,600 | R$ 4,150 | +1,100 | +36.1% | R$ 20,750 | +17,700 | +580.3% | CONFIRMADO (`SEG03:7FF4`) |
| 40 | R$ 3,150 | R$ 3,750 | R$ 4,300 | +1,150 | +36.5% | R$ 21,500 | +18,350 | +582.5% | CONFIRMADO (`SEG03:7FF4`) |
| 41 | R$ 3,300 | R$ 3,900 | R$ 4,400 | +1,100 | +33.3% | R$ 22,000 | +18,700 | +566.7% | CONFIRMADO (`SEG03:7FF4`) |
| 42 | R$ 3,450 | R$ 4,050 | R$ 4,500 | +1,050 | +30.4% | R$ 22,500 | +19,050 | +552.2% | CONFIRMADO (`SEG03:7FF4`) |
| 43 | R$ 3,600 | R$ 4,200 | R$ 4,600 | +1,000 | +27.8% | R$ 23,000 | +19,400 | +538.9% | CONFIRMADO (`SEG03:7FF4`) |
| 44 | R$ 3,700 | R$ 4,350 | R$ 4,700 | +1,000 | +27.0% | R$ 23,500 | +19,800 | +535.1% | CONFIRMADO (`SEG03:7FF4`) |
| 45 | R$ 3,850 | R$ 4,550 | R$ 4,850 | +1,000 | +26.0% | R$ 24,250 | +20,400 | +529.9% | CONFIRMADO (`SEG03:7FF4`) |
| 46 | R$ 4,000 | R$ 4,700 | R$ 4,950 | +950 | +23.8% | R$ 24,750 | +20,750 | +518.8% | CONFIRMADO (`SEG03:7FF4`) |
| 47 | R$ 4,150 | R$ 4,850 | R$ 5,050 | +900 | +21.7% | R$ 25,250 | +21,100 | +508.4% | CONFIRMADO (`SEG03:7FF4`) |
| 48 | R$ 4,300 | R$ 5,050 | R$ 5,150 | +850 | +19.8% | R$ 25,750 | +21,450 | +498.8% | CONFIRMADO (`SEG03:7FF4`) |
| 49 | R$ 4,450 | R$ 5,200 | R$ 5,300 | +850 | +19.1% | R$ 26,500 | +22,050 | +495.5% | CONFIRMADO (`SEG03:7FF4`) |
| 50 | R$ 4,600 | R$ 5,400 | R$ 5,400 | +800 | +17.4% | R$ 27,000 | +22,400 | +487.0% | CONFIRMADO (`SEG03:7FF4`) |

---

## 9. Valor de Mercado

Tabela comparativa de $V$ (`SEG09:371A`) para forças de referência ($I = 2.0, B = 0, D = 1$):

| Força (`F`) | Valor Multiplayer (`012D`, `M`, Cavalheiro) | `V` Clássico `FairPlay` (`A=0, C=1`) | `V` Clássico `Cavalheiro` (`A=2, C=1`) | `V` Clássico `Sarrafeiro` (`A=5, C=1`) | `V` Clássico `Cavalheiro` (`A=2, C=5`) | Grau de Confiança |
|---:|---:|---:|---:|---:|---:|---|
| 1 | R$ 15,000 | R$ 4,080 | R$ 3,760 | R$ 3,280 | R$ 18,800 | CONFIRMADO (`SEG09:371A`) |
| 5 | R$ 15,000 | R$ 21,756 | R$ 20,050 | R$ 17,491 | R$ 100,251 | CONFIRMADO (`SEG09:371A`) |
| 10 | R$ 15,000 | R$ 44,736 | R$ 41,228 | R$ 35,964 | R$ 206,138 | CONFIRMADO (`SEG09:371A`) |
| 15 | R$ 32,000 | R$ 68,202 | R$ 62,853 | R$ 54,829 | R$ 314,263 | CONFIRMADO (`SEG09:371A`) |
| 20 | R$ 61,000 | R$ 91,988 | R$ 84,773 | R$ 73,951 | R$ 423,866 | CONFIRMADO (`SEG09:371A`) |
| 25 | R$ 99,000 | R$ 116,016 | R$ 106,917 | R$ 93,268 | R$ 534,583 | CONFIRMADO (`SEG09:371A`) |
| 30 | R$ 147,000 | R$ 140,238 | R$ 129,239 | R$ 112,740 | R$ 646,196 | CONFIRMADO (`SEG09:371A`) |
| 35 | R$ 206,000 | R$ 164,623 | R$ 151,712 | R$ 132,344 | R$ 758,558 | CONFIRMADO (`SEG09:371A`) |
| 40 | R$ 275,000 | R$ 189,148 | R$ 174,313 | R$ 152,060 | R$ 871,566 | CONFIRMADO (`SEG09:371A`) |
| 45 | R$ 356,000 | R$ 213,797 | R$ 197,028 | R$ 171,876 | R$ 985,142 | CONFIRMADO (`SEG09:371A`) |
| 50 | R$ 448,000 | R$ 238,555 | R$ 219,845 | R$ 191,780 | R$ 1,099,225 | CONFIRMADO (`SEG09:371A`) |

---

## 10. Comportamento dos Jogadores

Comparação controlada (`F = 40, I = 2.0, R = 1.0, C = 1, B = 0, D = 1`, mesmo clube e nacionalidade), validada pelo teste `BehaviorControlledComparison_SameStrengthClubInflation_SixBehaviors`:

| Código `A` | Comportamento | Parcela `-0.04 * A` | Multiplicador `(1 - 0.04*A - 0.02*B + 0.02*D)` | Salário-Base `S` (`C=1`) | Valor de Mercado `V` (`C=1`) | Preço Candidato `Q` (Mesmo País) | Efeito Esportivo na Partida |
|---:|---|---:|---:|---:|---:|---:|---|
| `0` | `FairPlay` | `0%` | `1.02` | `R$ 4.300` | `R$ 189.149` | `R$ 350.000` | Inalterado (0 cartões para FairPlay preservado) |
| `1` | `Cordeirinho` | `-4%` | `0.98` | `R$ 4.300` | `R$ 181.731` | `R$ 350.000` | Inalterado |
| `2` | `Cavalheiro` | `-8%` | `0.94` | `R$ 4.300` | `R$ 174.313` | `R$ 325.000` | Inalterado |
| `3` | `Caneleiro` | `-12%` | `0.90` | `R$ 4.300` | `R$ 166.896` | `R$ 325.000` | Inalterado |
| `4` | `Caceteiro` | `-16%` | `0.86` | `R$ 4.300` | `R$ 159.478` | `R$ 300.000` | Inalterado |
| `5` | `Sarrafeiro` | `-20%` | `0.82` | `R$ 4.300` | `R$ 152.061` | `R$ 300.000` | Inalterado |

---

## 11. Influência Econômica do Asterisco (`*`)

Comparação controlada (`F = 40, Pos = A, A = 2 (Cavalheiro), B = 0, I = 2.0, R = 1.0, C = 1`), validada pelo teste `AsteriskControlledComparison_Force40_WithAndWithoutStar`:

| Métrica | Jogador A (`F=40`, sem `*`, `D=5`) | Jogador B (`F=40`, com `*`, `D=8`) | Jogador B' (`F=40`, com `*`, mesmo `D=8` fixado) | Explicação Comprovada no Executável |
|---|---:|---:|---:|---|
| **Salário-base `S`** | `R$ 4.300` | `R$ 4.300` | `R$ 4.300` | `SEG03:7FF4` não lê o bit `0x20` (`IsStar`) nem `D`; o salário-base depende apenas de `F, I, R, C`. |
| **Valor de mercado `V`** | `R$ 189.149` (`D=5`) | `R$ 200.275` (`D=8`) | `R$ 200.275` (`D=8`) | `SEG09:371A` não multiplica `V` por `1.10`, mas como o asterisco no original decorre de `D >= 8` (`SEG03:75F2`), o termo `+0.02 * D` eleva o modificador de `V`. Para mesmo `D`, `V` é idêntico. |
| **Preço candidato `Q` (mesmo país)** | `R$ 350.000` | `R$ 375.000` | `R$ 375.000` | Decorre de `V` na etapa `(V + 30000) * 1.60` + quantização. |
| **Fator de interesse da IA (`SEG03:5CE7`)** | `1.50` (`N=0`) | `1.65` (`1.50 * 1.10`) | `1.65` (`1.50 * 1.10`) | `SEG03:5CE7` aplica explicitamente `* 1.10` quando `player_flags & 0x20 != 0` (`IsStar`). |

---

## 12. Influência Econômica da Nacionalidade

Cenários controlados A a E (`F = 35, V = R$ 161.366, I = 2.0, C = 1`), validados pelo teste `NationalityScenarios_A_To_E_CandidatePriceAndAiInterest`:

| Cenário | Jogador | Clube Comprador | Estrangeiros no Elenco (`N`) | Classe (`SEG12:0A2F`) | Fórmula Preço Candidato (`SEG03:63FA`) | Preço Bruto | Preço Quantizado (`Q`) | Fator Interesse IA (`SEG03:5BB1`) |
|---|---|---|---:|---|---|---:|---:|---:|
| **A** | Brasileiro (`BRA`) | Brasileiro (`BRA`) | `0` | `SameCountry (0)` | `(Q0 + 30000) * 1.60` | `R$ 306.186` | `R$ 300.000` | `1.50` |
| **B** | Argentino (`ARG`) | Brasileiro (`BRA`) | `0` | `Foreigner (2)` | `Q0` (sem acréscimo) | `R$ 161.366` | `R$ 150.000` | `1.00` |
| **C** | Português (`POR`) | Brasileiro (`BRA`) | `0` | `EquivalentCountry (1)` (PLOP) | `(Q0 + 15000) * 1.30` | `R$ 229.276` | `R$ 250.000` | `1.15` |
| **D** | Brasileiro (`BRA`) | 1) `BRA`, 2) `POR`, 3) `ITA` | `0` | `0`, `1`, `2` | `1.60x`, `1.30x`, `1.00x` | `306k / 229k / 161k` | `300k / 250k / 150k` | `1.50 / 1.15 / 1.00` |
| **E** | Brasileiro (`BRA`) | Brasileiro (`BRA`) | `N = 0, 2, 4` | `SameCountry (0)` | `(Q0 + 30000) * 1.60` | `R$ 306.186` | `R$ 300.000` | `1.50 / 1.60 / 1.70` (`1.50 + 0.05*N`) |

---

## 13. Índice Interno de Inflação

- **Valor inicial:** `I = 2.0` (`InflationDisplayIndex = round(2.0 * 10) = 20`), conforme `SEG06:2F8F`.
- **Limites rígidos:** Mínimo `0.5` (`DS:5D9E`, exibição `5`), Máximo `10.0` (`DS:5DA6`, exibição `100`), conforme `SEG11:3721..3755`.
- **Frequência e momento de atualização:** Atualizado **uma vez por temporada** na virada anual de fim de época (`CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance` -> `ClassicPlayerEconomyEngine.AdvanceWorldInflationAtSeasonEnd`), exatamente como no loop de transição anual do `ELIFOOT.EXE` (`SEG11:3680`).
- **Regra do contador de ano `Y`:** Para `(Y % 100) < 50` (ex.: `1999..2049`), aplica viés positivo `delta = 0.01 * (U1 + U2 - U3)`; para `(Y % 100) >= 50` (ex.: `1998`), aplica viés negativo `delta = 0.01 * (U1 - U2 - U3)`.
- **Trajetória observada na simulação de 20 temporadas (`V3_STAGE02C_HORIZON_20S`):** `I` evoluiu gradualmente dentro de `[1.98, 2.09]` (`InflationDisplayIndex` em `20..21`), sem hiperinflação ou deflação destrutiva.

---

## 14. Leilões e Preços Finais

- **Diferenciação preservada:**
  - `MarketValue (V)`: Avaliação individual calculada por `SEG09:371A`.
  - `CandidateSigningPrice (Q)`: Preço candidato para proposta/compra direta considerando nacionalidade (`SEG03:63FA`) e escada de quantização (`SEG03:6558..6640`).
  - `FinalNegotiatedPrice`: Valor efetivamente pago na transferência concluída ou no lance vencedor de leilão.
  - `MinimumAuctionBid`: `R$ 0` para leilões obrigatórios (insolvência financeira, recusa salarial, fim de contrato) e `R$ 500.000` como padrão editável para leilões voluntários (Etapa 02A).
- **Lances da IA em leilões:** A IA avalia os atletas em leilão utilizando `CalculateFairValue(world, auctionPlayer)` (alimentado por `V` clássico quando a economia clássica está ativa) e respeita seu `MaxSingleSigningBudget` e o limite de elenco (`11..24`).

---

## 15. Integração com a Etapa 02B

Todas as regras da Etapa 02B (`8df9b0c`, `b3fc9e3`) foram integralmente preservadas:
- `SalaryContractsEngine.GenerateSalaryRequests` e `RequestedSalary` consultam `MarketAndEconomyEngine.CalculateFairSalaryPerRound(w, player, c)`, que delega para `ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary` quando a economia clássica está ativa.
- `PreviewRenewal` e `RenewContract` continuam exigindo confirmação explícita quando `RequiredSalary > CurrentSalary`, bloqueando venda na temporada após renovação (`IsSeasonRenewed`) e enviando o jogador para leilão (`AuctionReason.SalaryRenewalRefused`) apenas quando um pedido espontâneo elegível é recusado e o elenco permite remoção.
- `MarketContractTermsEngine.SigningSalary(world, player, buyerClub)` calcula o salário de contratação com base na referência clássica sem reduzir o salário vigente do atleta.

---

## 16. Compatibilidade com Mundos Existentes

- **Saves e mundos anteriores (`Policy = "MULTIPLAYER_CALIBRATED_012D"`):** Carregam sem necessidade de migração destrutiva; todos os salários contratados, caixas, empréstimos e pedidos pendentes permanecem inalterados.
- **Atributos `PersonalModifierB`, `PersonalModifierD` e `Nationality`:** Adicionados como propriedades opcionais (`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`) em `Player`. Quando ausentes em um save antigo, `ClassicPlayerEconomyEngine` deriva `B` e `D` deterministicamente do nome (`SEG03:7516`) sem alterar `Behavior` ou `IsStar` já persistidos.

---

## 17. Arquivos Criados e Alterados

### Arquivos de Produção (.NET)
1. `src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs` **(NOVO)**: Motor centralizado da economia clássica (`SEG03:7FF4`, `SEG09:371A`, `SEG03:7516`, `SEG14:07A4`, `SEG12:0A2F`, `SEG03:63FA..6640`, `SEG03:5BB1..5DAE`, `SEG11:3680`).
2. `src/Elifoot.Domain/ClassicEconomyState.cs`: Adicionados `PlayerEconomyRules`, `InflationIndex`, `InflationDisplayIndex`, `EconomyYearCounter`, `InflationHistory` e `ClubFactorsR`.
3. `src/Elifoot.Domain/Catalog/PlayerProvenance.cs`: Adicionados `PersonalModifierB`, `PersonalModifierD` e `Nationality` em `Player`.
4. `src/Elifoot.Domain/MarketAndEconomyEngine.cs`: Sobrecargas `CalculateFairValue(world, player)` e `CalculateFairSalaryPerRound(world, player, club)` integradas ao mercado da IA, transferências, leilões e liquidação de rodada, além de proteção de reserva posicional para propostas não solicitadas na economia clássica.
5. `src/Elifoot.Domain/MarketContractTermsEngine.cs`: Sobrecarga `SigningSalary(world, player, buyerClub)` e `SalaryTermsToken(world, player)`.
6. `src/Elifoot.Domain/SalaryContractsEngine.cs`: Integração de `CalculateFairSalaryPerRound(w, player, c)` nos pedidos salariais da Etapa 02B.
7. `src/Elifoot.Domain/ClassicForceEvolutionEngine.cs` e `src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs`: Atualização de `MarketPrice` via `CalculateFairValue(world, player)`.
8. `src/Elifoot.Domain/CompetitionAndSeasonEngine.cs`: Evolução anual da inflação clássica em `FinalizeSeasonAndAdvance`.
9. `src/Elifoot.Contracts/TransferContractPreviewDto.cs`: Novos campos opcionais (`CalculatedBaseSalary`, `MarketValue`, `CandidateSigningPrice`, `NationalityClass`, `AiHiringInterestFactor`, `InflationIndex`, `InflationDisplay`).
10. `src/Elifoot.Application/TransferContractPreviewQuery.cs`, `src/Elifoot.Application/ClubAndMarketApplicationService.cs`, `src/Elifoot.Application/MarketQueriesApplicationService.cs`: Preenchimento das grandezas clássicas nos DTOs de mercado e prévia de contratação.

### Arquivos de Teste (.NET e Flutter) e Documentação
11. `tests/Elifoot.Domain.Tests/ClassicalEconomyV3Stage02CTests.cs` **(NOVO)**: 9 testes de domínio cobrindo todas as fórmulas do `ELIFOOT.EXE`.
12. `tests/Elifoot.Integration.Tests/ClassicalEconomyV3Stage02CIntegrationTests.cs` **(NOVO)**: Teste fim-a-fim de API, prévia de transferência, renovação 02B, leilão voluntário 02A e persistência com economia clássica.
13. `tests/Elifoot.Simulation.Tests/ClassicalEconomyV3Stage02CSimulationTests.cs` **(NOVO)**: Simulações multi-temporada (`1`, `3`, `5`, `10` e `20` temporadas com técnicos humanos simulados + `20` temporadas 100% IA).
14. `tests/Elifoot.Simulation.Tests/MultiSeasonAndBenchmarkTests.cs`: Ajuste de sincronização de `ClassicForceEvolutionEngine.PrepareRound(world)` após ações de mercado dos agentes humanos sintéticos.
15. `client/elifoot_flutter/test/classical_economy_v3_stage02c_test.dart` **(NOVO)**: Teste de widget validando prévia de contratação e confirmação salarial sob economia clássica.
16. `docs/implementacoes/ELIFOOT98_V3_ETAPA02C1_RESULTADO.md` **(NOVO)**: Relatório parcial da Subfase `02C-1`.
17. `docs/implementacoes/ELIFOOT98_V3_ETAPA02C_RESULTADO.md` **(NOVO)**: Este relatório final consolidado da Etapa `02C`.

---

## 18. Endpoints e Contratos

- `GET /api/worlds/{worldId}/clubs/{clubId}/market/players/{playerId}/contract-preview` (`TransferContractPreviewDto`):
  - Continua retornando `PlayerId`, `PlayerName`, `CurrentSalaryPerRound`, `SigningSalaryPerRound`, `MinimumContractRounds`, `ContractCostPerRound`, `MinimumTotalCommitment`, `RequiresConfirmation` e `SalaryTermsToken`.
  - Passou a expor também: `CalculatedBaseSalary`, `MarketValue`, `CandidateSigningPrice`, `NationalityClass`, `AiHiringInterestFactor`, `InflationIndex` e `InflationDisplay`.
- Endpoints de compra direta (`/market/buy`), proposta bilateral (`/market/offers`), leilão voluntário (`/market/voluntary-auctions`), resposta salarial (`/economy/salary-requests/{requestId}/respond`) e renovação (`/economy/players/{playerId}/renew-contract`) preservam seus contratos HTTP e validam os tokens salariais gerados com a política ativa do mundo.

---

## 19. Ajustes no Cliente Flutter

- O cliente Flutter consome os valores calculados pelo servidor em `TransferContractPreviewDto`, `RenewalContractPreviewDto` e `PlayerSummaryDto` sem duplicar fórmulas financeiras no cliente.
- Validado pelo teste de widget `client/elifoot_flutter/test/classical_economy_v3_stage02c_test.dart` e pela suíte completa de `505` testes Flutter (`0` falhas) e `flutter analyze` (`No issues found!`).

---

## 20. Testes Executados

| Suíte / Validação | Comando / Escopo | Total | Aprovados | Falhas | Ignorados | Status |
|---|---|---:|---:|---:|---:|---|
| **Build Release .NET** | `dotnet build -c Release --no-restore` | — | — | `0` erros / `0` avisos | — | **PASS** |
| **Elifoot.Domain.Tests** | Suíte completa (incluindo `ClassicalEconomyV3Stage02CTests`) | `625` | `625` | `0` | `0` | **PASS** |
| **Elifoot.Application.Tests** | Suíte completa | `55` | `55` | `0` | `0` | **PASS** |
| **Elifoot.Integration.Tests** | Suíte completa com PostgreSQL 18 isolado (incluindo `ClassicalEconomyV3Stage02CIntegrationTests`) | `296` | `296` | `0` | `0` | **PASS** |
| **Elifoot.Simulation.Tests (`02C-2`)** | `ClassicalEconomyV3Stage02CSimulationTests` (`1, 3, 5, 10, 20` temporadas humanos + `20` temporadas IA; total da assembly: `37` casos sendo `31` anteriores + `6` novos) | `6` (`37` na assembly) | `6` (`37` na assembly) | `0` | `0` | **PASS** |
| **Total Consolidado .NET** | `Domain (625) + Application (55) + Integration (296) + Simulation (37)` | `1.013` | `1.013` | `0` | `0` | **PASS** |
| **Flutter Analyze** | `flutter analyze` em `client/elifoot_flutter` | — | — | `0` issues | — | **PASS** |
| **Flutter Test (Completa)** | `flutter test` em `client/elifoot_flutter` (incluindo `classical_economy_v3_stage02c_test.dart`) | `505` | `505` | `0` | `0` | **PASS** |

---

## 21. Resultados de 1 Temporada

- **Experimento:** `V3_STAGE02C_HORIZON_1S` (`ScenarioB_EightHumanAgents`, seed `199802`, `1` temporada(s), `351` partidas oficiais concluídas)
- **Integridade de elencos e escalações:** `InvalidSquadLimitViolations = 0`, `InvalidLineupSelectionViolations = 0`, `DoubleSettledMatchFinancialViolations = 0`
- **Inflação interna (`I`):** `2.00` -> `2.00` (Exibição `round(I*10) = 20`, Contador `Y = 1999`)
- **Caixa agregado dos 128 clubes:** Inicial `R$ 138,800,000.00` -> Final `R$ 211,806,282.95` (Média `R$ 1,654,736.59`, Mediana `R$ 649,424.24`, Dívida bancária ativa `R$ 0.00`)
- **Mercado e transferências:** `99` compras (`80` IA-para-IA, `19` envolvendo técnicos humanos), Volume total `R$ 12,386,255.00`
- **Leilões:** `4` abertos (`0` voluntários, `0` por insolvência, `0` por recusa salarial, `0` não vendidos)
- **Contratos e pedidos salariais (Etapa 02B):** `239` pedidos espontâneos (`239` aceitos, `0` recusados), `2` renovações preventivas voluntárias humanas, `344` eventos totais no histórico contratual
- **Médias individuais ao fim do horizonte:** Salário médio `R$ 2,021.53/rodada` (`[R$ 1,000, R$ 5,665]`), Valor de mercado médio `V = R$ 75,348.76` (`[R$ 28,053, R$ 237,465]`), Preço candidato médio (mesmo país) `Q = R$ 310,724.23`

### Recorte por Divisão, Riqueza, Força de Elenco e Controle (IA vs Humanos Simulados)

| Recorte / Coorte | Clubes | Caixa Médio (R$) | Folha / Rodada (R$) | Valor de Elenco `V` (R$) | Força Média | Insolventes |
|---|---:|---:|---:|---:|---:|---:|
| `PrimeiraDivisao` | 8 | 10,238,844.29 | 78,405.12 | 2,748,693.00 | 37.61 | 0 |
| `SegundaDivisao` | 8 | 4,832,609.09 | 65,823.62 | 2,281,731.00 | 30.51 | 0 |
| `TerceiraDivisao` | 8 | 2,392,178.18 | 52,639.50 | 1,712,197.12 | 22.15 | 0 |
| `QuartaDivisao` | 8 | 1,103,628.34 | 41,025.00 | 1,330,352.25 | 17.51 | 0 |
| `Distrital` | 96 | 659,043.79 | 28,776.56 | 1,138,762.08 | 15.38 | 0 |
| `ClubesRicos_Top8` | 8 | 10,993,869.99 | 78,477.25 | 2,723,774.12 | 38.22 | 0 |
| `ClubesPobres_Bottom8` | 8 | 839,992.37 | 40,937.50 | 1,321,411.12 | 16.46 | 0 |
| `ElencosFortes_Top8` | 8 | 10,916,142.20 | 80,661.25 | 2,789,235.38 | 38.87 | 0 |
| `ElencosFracos_Bottom8` | 8 | 839,992.37 | 40,937.50 | 1,321,411.12 | 16.46 | 0 |
| `ClubesIA` | 120 | 1,498,320.02 | 34,308.61 | 1,286,262.32 | 17.54 | 0 |
| `TecnicosHumanosSimulados` | 8 | 4,000,985.04 | 68,582.88 | 2,444,183.62 | 29.23 | 0 |


---

## 22. Resultados de 3 Temporadas

- **Experimento:** `V3_STAGE02C_HORIZON_3S` (`ScenarioB_EightHumanAgents`, seed `199802`, `3` temporada(s), `1053` partidas oficiais concluídas)
- **Integridade de elencos e escalações:** `InvalidSquadLimitViolations = 0`, `InvalidLineupSelectionViolations = 0`, `DoubleSettledMatchFinancialViolations = 0`
- **Inflação interna (`I`):** `2.00` -> `1.98` (Exibição `round(I*10) = 20`, Contador `Y = 2001`)
- **Caixa agregado dos 128 clubes:** Inicial `R$ 138,800,000.00` -> Final `R$ 304,619,048.39` (Média `R$ 2,379,836.32`, Mediana `R$ 646,209.99`, Dívida bancária ativa `R$ 0.00`)
- **Mercado e transferências:** `219` compras (`197` IA-para-IA, `22` envolvendo técnicos humanos), Volume total `R$ 23,011,982.00`
- **Leilões:** `9` abertos (`2` voluntários, `3` por insolvência, `0` por recusa salarial, `0` não vendidos)
- **Contratos e pedidos salariais (Etapa 02B):** `503` pedidos espontâneos (`503` aceitos, `0` recusados), `6` renovações preventivas voluntárias humanas, `732` eventos totais no histórico contratual
- **Médias individuais ao fim do horizonte:** Salário médio `R$ 2,011.33/rodada` (`[R$ 800, R$ 5,940]`), Valor de mercado médio `V = R$ 67,948.20` (`[R$ 2,857, R$ 250,580]`), Preço candidato médio (mesmo país) `Q = R$ 278,259.02`

### Recorte por Divisão, Riqueza, Força de Elenco e Controle (IA vs Humanos Simulados)

| Recorte / Coorte | Clubes | Caixa Médio (R$) | Folha / Rodada (R$) | Valor de Elenco `V` (R$) | Força Média | Insolventes |
|---|---:|---:|---:|---:|---:|---:|
| `PrimeiraDivisao` | 8 | 18,803,790.86 | 80,939.38 | 2,446,220.38 | 33.42 | 0 |
| `SegundaDivisao` | 8 | 8,223,343.27 | 64,623.00 | 1,788,375.75 | 24.53 | 0 |
| `TerceiraDivisao` | 8 | 2,582,766.42 | 48,792.88 | 1,256,520.88 | 15.64 | 0 |
| `QuartaDivisao` | 8 | 724,658.58 | 37,068.75 | 853,891.00 | 11.56 | 1 |
| `Distrital` | 96 | 645,235.16 | 29,070.31 | 1,104,837.28 | 15.09 | 1 |
| `ClubesRicos_Top8` | 8 | 20,468,356.61 | 80,475.62 | 2,360,568.12 | 32.99 | 0 |
| `ClubesPobres_Bottom8` | 8 | 456,766.36 | 39,570.62 | 1,026,658.88 | 12.51 | 1 |
| `ElencosFortes_Top8` | 8 | 17,900,251.63 | 83,005.50 | 2,578,314.50 | 34.18 | 0 |
| `ElencosFracos_Bottom8` | 8 | 1,052,553.02 | 40,823.75 | 846,794.88 | 10.90 | 1 |
| `ClubesIA` | 122 | 2,010,703.82 | 34,143.27 | 1,152,077.50 | 15.86 | 2 |
| `TecnicosHumanosSimulados` | 6 | 9,885,530.43 | 79,443.83 | 2,711,831.33 | 32.45 | 0 |


---

## 23. Resultados de 5 Temporadas

- **Experimento:** `V3_STAGE02C_HORIZON_5S` (`ScenarioB_EightHumanAgents`, seed `199802`, `5` temporada(s), `1755` partidas oficiais concluídas)
- **Integridade de elencos e escalações:** `InvalidSquadLimitViolations = 0`, `InvalidLineupSelectionViolations = 0`, `DoubleSettledMatchFinancialViolations = 0`
- **Inflação interna (`I`):** `2.00` -> `1.99` (Exibição `round(I*10) = 20`, Contador `Y = 2003`)
- **Caixa agregado dos 128 clubes:** Inicial `R$ 138,800,000.00` -> Final `R$ 359,937,489.79` (Média `R$ 2,812,011.64`, Mediana `R$ 668,299.38`, Dívida bancária ativa `R$ 0.00`)
- **Mercado e transferências:** `323` compras (`293` IA-para-IA, `30` envolvendo técnicos humanos), Volume total `R$ 29,080,340.00`
- **Leilões:** `15` abertos (`4` voluntários, `7` por insolvência, `0` por recusa salarial, `0` não vendidos)
- **Contratos e pedidos salariais (Etapa 02B):** `780` pedidos espontâneos (`780` aceitos, `0` recusados), `10` renovações preventivas voluntárias humanas, `1117` eventos totais no histórico contratual
- **Médias individuais ao fim do horizonte:** Salário médio `R$ 1,996.44/rodada` (`[R$ 300, R$ 5,940]`), Valor de mercado médio `V = R$ 63,165.42` (`[R$ 2,871, R$ 247,106]`), Preço candidato médio (mesmo país) `Q = R$ 247,724.69`

### Recorte por Divisão, Riqueza, Força de Elenco e Controle (IA vs Humanos Simulados)

| Recorte / Coorte | Clubes | Caixa Médio (R$) | Folha / Rodada (R$) | Valor de Elenco `V` (R$) | Força Média | Insolventes |
|---|---:|---:|---:|---:|---:|---:|
| `PrimeiraDivisao` | 8 | 22,749,608.24 | 85,764.25 | 2,491,262.00 | 31.79 | 0 |
| `SegundaDivisao` | 8 | 7,828,530.25 | 59,083.62 | 1,412,231.25 | 18.57 | 0 |
| `TerceiraDivisao` | 8 | 6,295,900.10 | 46,191.38 | 826,687.88 | 11.80 | 0 |
| `QuartaDivisao` | 8 | 1,198,644.84 | 35,908.75 | 742,927.62 | 9.67 | 0 |
| `Distrital` | 96 | 576,625.23 | 29,085.42 | 1,062,509.58 | 14.56 | 5 |
| `ClubesRicos_Top8` | 8 | 26,865,988.25 | 80,612.50 | 2,030,232.62 | 27.99 | 0 |
| `ClubesPobres_Bottom8` | 8 | 649,189.24 | 38,813.88 | 892,112.38 | 11.01 | 0 |
| `ElencosFortes_Top8` | 8 | 21,240,919.20 | 85,959.88 | 2,562,559.75 | 32.46 | 0 |
| `ElencosFracos_Bottom8` | 8 | 1,226,707.70 | 38,742.00 | 584,486.62 | 7.46 | 0 |
| `ClubesIA` | 121 | 2,204,125.33 | 33,511.69 | 1,060,872.23 | 14.58 | 5 |
| `TecnicosHumanosSimulados` | 7 | 13,319,760.65 | 78,981.43 | 2,488,607.14 | 29.70 | 0 |


---

## 24. Resultados de 10 Temporadas

- **Experimento:** `V3_STAGE02C_HORIZON_10S` (`ScenarioB_EightHumanAgents`, seed `199802`, `10` temporada(s), `3510` partidas oficiais concluídas)
- **Integridade de elencos e escalações:** `InvalidSquadLimitViolations = 0`, `InvalidLineupSelectionViolations = 0`, `DoubleSettledMatchFinancialViolations = 0`
- **Inflação interna (`I`):** `2.00` -> `2.01` (Exibição `round(I*10) = 20`, Contador `Y = 2008`)
- **Caixa agregado dos 128 clubes:** Inicial `R$ 138,800,000.00` -> Final `R$ 434,883,196.83` (Média `R$ 3,397,524.98`, Mediana `R$ 594,657.81`, Dívida bancária ativa `R$ 0.00`)
- **Mercado e transferências:** `675` compras (`638` IA-para-IA, `37` envolvendo técnicos humanos), Volume total `R$ 58,867,451.00`
- **Leilões:** `31` abertos (`6` voluntários, `15` por insolvência, `6` por recusa salarial, `0` não vendidos)
- **Contratos e pedidos salariais (Etapa 02B):** `1454` pedidos espontâneos (`1448` aceitos, `6` recusados), `20` renovações preventivas voluntárias humanas, `2147` eventos totais no histórico contratual
- **Médias individuais ao fim do horizonte:** Salário médio `R$ 1,957.35/rodada` (`[R$ 300, R$ 5,940]`), Valor de mercado médio `V = R$ 54,853.52` (`[R$ 2,651, R$ 244,231]`), Preço candidato médio (mesmo país) `Q = R$ 197,957.03`

### Recorte por Divisão, Riqueza, Força de Elenco e Controle (IA vs Humanos Simulados)

| Recorte / Coorte | Clubes | Caixa Médio (R$) | Folha / Rodada (R$) | Valor de Elenco `V` (R$) | Força Média | Insolventes |
|---|---:|---:|---:|---:|---:|---:|
| `PrimeiraDivisao` | 8 | 29,657,011.10 | 85,542.12 | 2,227,683.62 | 27.31 | 0 |
| `SegundaDivisao` | 8 | 10,100,173.37 | 50,825.75 | 871,987.38 | 10.67 | 0 |
| `TerceiraDivisao` | 8 | 7,089,701.02 | 40,869.75 | 647,389.88 | 8.98 | 0 |
| `QuartaDivisao` | 8 | 1,533,766.50 | 33,849.75 | 626,642.75 | 7.93 | 1 |
| `Distrital` | 96 | 498,312.30 | 29,344.95 | 950,866.49 | 13.12 | 12 |
| `ClubesRicos_Top8` | 8 | 35,811,212.38 | 79,781.25 | 1,654,171.12 | 21.91 | 0 |
| `ClubesPobres_Bottom8` | 8 | 568,472.02 | 31,707.50 | 718,073.75 | 9.36 | 1 |
| `ElencosFortes_Top8` | 8 | 29,657,011.10 | 85,542.12 | 2,227,683.62 | 27.31 | 0 |
| `ElencosFracos_Bottom8` | 8 | 1,320,389.10 | 32,772.88 | 408,400.75 | 5.62 | 1 |
| `ClubesIA` | 122 | 2,436,119.89 | 33,134.01 | 917,887.86 | 12.59 | 13 |
| `TecnicosHumanosSimulados` | 6 | 22,946,095.13 | 77,244.17 | 2,381,748.83 | 27.23 | 0 |


---

## 25. Resultados de 20 Temporadas (Humanos Simulados + IA e 100% IA)

- **Experimento:** `V3_STAGE02C_HORIZON_20S` (`ScenarioB_EightHumanAgents`, seed `199802`, `20` temporada(s), `7020` partidas oficiais concluídas)
- **Integridade de elencos e escalações:** `InvalidSquadLimitViolations = 0`, `InvalidLineupSelectionViolations = 0`, `DoubleSettledMatchFinancialViolations = 0`
- **Inflação interna (`I`):** `2.00` -> `2.08` (Exibição `round(I*10) = 21`, Contador `Y = 2018`)
- **Caixa agregado dos 128 clubes:** Inicial `R$ 138,800,000.00` -> Final `R$ 493,562,433.40` (Média `R$ 3,855,956.51`, Mediana `R$ 385,693.42`, Dívida bancária ativa `R$ 0.00`)
- **Mercado e transferências:** `1118` compras (`1070` IA-para-IA, `48` envolvendo técnicos humanos), Volume total `R$ 78,393,746.00`
- **Leilões:** `66` abertos (`9` voluntários, `34` por insolvência, `19` por recusa salarial, `0` não vendidos)
- **Contratos e pedidos salariais (Etapa 02B):** `2801` pedidos espontâneos (`2782` aceitos, `19` recusados), `39` renovações preventivas voluntárias humanas, `3943` eventos totais no histórico contratual
- **Médias individuais ao fim do horizonte:** Salário médio `R$ 1,893.38/rodada` (`[R$ 300, R$ 6,105]`), Valor de mercado médio `V = R$ 45,450.64` (`[R$ 2,739, R$ 266,930]`), Preço candidato médio (mesmo país) `Q = R$ 168,188.00`

### Recorte por Divisão, Riqueza, Força de Elenco e Controle (IA vs Humanos Simulados)

| Recorte / Coorte | Clubes | Caixa Médio (R$) | Folha / Rodada (R$) | Valor de Elenco `V` (R$) | Força Média | Insolventes |
|---|---:|---:|---:|---:|---:|---:|
| `PrimeiraDivisao` | 8 | 37,116,014.24 | 79,389.38 | 2,210,044.50 | 24.61 | 0 |
| `SegundaDivisao` | 8 | 5,199,264.65 | 38,602.88 | 739,313.62 | 8.61 | 0 |
| `TerceiraDivisao` | 8 | 3,212,108.14 | 33,605.25 | 621,859.38 | 7.94 | 0 |
| `QuartaDivisao` | 8 | 6,342,733.33 | 31,395.88 | 507,203.62 | 7.45 | 2 |
| `Distrital` | 96 | 818,765.32 | 29,895.81 | 743,845.28 | 10.16 | 24 |
| `ClubesRicos_Top8` | 8 | 45,298,966.69 | 74,281.12 | 1,698,570.12 | 19.45 | 0 |
| `ClubesPobres_Bottom8` | 8 | 185,737.55 | 29,774.38 | 496,549.50 | 6.53 | 2 |
| `ElencosFortes_Top8` | 8 | 32,660,215.92 | 71,390.62 | 2,185,279.62 | 25.41 | 0 |
| `ElencosFracos_Bottom8` | 8 | 4,113,047.11 | 29,531.88 | 384,271.25 | 5.13 | 2 |
| `ClubesIA` | 123 | 1,987,894.56 | 31,654.06 | 742,498.93 | 9.93 | 25 |
| `TecnicosHumanosSimulados` | 5 | 49,810,280.52 | 88,099.20 | 2,541,829.60 | 28.41 | 1 |


### Comparativo Adicional — 20 Temporadas 100% IA (`V3_STAGE02C_ALL_AI_20S`, Seed `199801`)

## 25B. Resultados de 20 Temporadas — Cenário 100% IA

- **Experimento:** `V3_STAGE02C_ALL_AI_20S` (`ScenarioA_AllAi`, seed `199801`, `20` temporada(s), `7020` partidas oficiais concluídas)
- **Integridade de elencos e escalações:** `InvalidSquadLimitViolations = 0`, `InvalidLineupSelectionViolations = 0`, `DoubleSettledMatchFinancialViolations = 0`
- **Inflação interna (`I`):** `2.00` -> `2.09` (Exibição `round(I*10) = 21`, Contador `Y = 2018`)
- **Caixa agregado dos 128 clubes:** Inicial `R$ 138,800,000.00` -> Final `R$ 349,967,479.03` (Média `R$ 2,734,120.93`, Mediana `R$ 339,492.20`, Dívida bancária ativa `R$ 0.00`)
- **Mercado e transferências:** `1596` compras (`1596` IA-para-IA, `0` envolvendo técnicos humanos), Volume total `R$ 164,727,032.00`
- **Leilões:** `43` abertos (`0` voluntários, `31` por insolvência, `8` por recusa salarial, `0` não vendidos)
- **Contratos e pedidos salariais (Etapa 02B):** `2381` pedidos espontâneos (`2373` aceitos, `8` recusados), `0` renovações preventivas voluntárias humanas, `3973` eventos totais no histórico contratual
- **Médias individuais ao fim do horizonte:** Salário médio `R$ 1,795.54/rodada` (`[R$ 300, R$ 6,215]`), Valor de mercado médio `V = R$ 42,681.04` (`[R$ 2,673, R$ 240,713]`), Preço candidato médio (mesmo país) `Q = R$ 123,516.47`

### Recorte por Divisão, Riqueza, Força de Elenco e Controle (IA vs Humanos Simulados)

| Recorte / Coorte | Clubes | Caixa Médio (R$) | Folha / Rodada (R$) | Valor de Elenco `V` (R$) | Força Média | Insolventes |
|---|---:|---:|---:|---:|---:|---:|
| `PrimeiraDivisao` | 8 | 27,254,985.82 | 50,334.50 | 1,336,332.25 | 14.60 | 0 |
| `SegundaDivisao` | 8 | 9,290,543.22 | 42,249.62 | 880,019.62 | 10.65 | 0 |
| `TerceiraDivisao` | 8 | 3,996,809.66 | 30,224.00 | 661,025.12 | 8.48 | 0 |
| `QuartaDivisao` | 8 | 722,294.68 | 31,296.38 | 601,853.88 | 7.41 | 1 |
| `Distrital` | 96 | 206,775.12 | 30,176.04 | 732,630.56 | 10.03 | 25 |
| `ClubesRicos_Top8` | 8 | 35,355,130.13 | 52,790.12 | 1,319,717.25 | 14.82 | 0 |
| `ClubesPobres_Bottom8` | 8 | 217,554.92 | 30,670.62 | 611,685.25 | 7.80 | 1 |
| `ElencosFortes_Top8` | 8 | 29,409,999.95 | 48,221.62 | 1,374,879.50 | 15.82 | 0 |
| `ElencosFracos_Bottom8` | 8 | 740,341.78 | 31,013.25 | 427,422.38 | 5.35 | 1 |
| `ClubesIA` | 128 | 2,734,120.93 | 32,263.56 | 766,924.85 | 10.10 | 26 |
| `TecnicosHumanosSimulados` | 0 | 0.00 | 0.00 | 0.00 | 0.00 | 0 |


---

## 26. Riscos Residuais

1. **Escala monetária `C = 1` vs `C = 5`:** O `ELIFOOT.EXE` original define `C = 5` para `Reais` (`DS:0191`), mas também multiplica patrocinadores e bilheteria pelo mesmo fator monetário. Como as receitas de estádio e patrocínio do Multiplayer estão calibradas em escala unitária (`C = 1`) e sua alteração é vedada nesta etapa (Seções 9 e 20), o modo clássico padrão do Multiplayer utiliza `C = 1` (mantendo equilíbrio operacional comprovado em 20 temporadas), enquanto `C = 5` permanece parametrizável em `ClassicPlayerEconomyRules.MonetaryFactorC`.
2. **Fator de equipe `R` (`team_ptr + 0x4CE`):** Mantido no valor neutro `1.0` por padrão (`ClubFactorsR`), evitando introduzir variação especulativa não comprovada na evolução intra-temporada de `R`.

---

## 27. Veredito Final PASS / FAIL

**VEREDITO FINAL: PASS**

- Todas as fórmulas econômicas comprovadas do `ELIFOOT.EXE` (`S`, `V`, `A/B/D/*`, nacionalidade `Q`, interesse da IA e inflação `I`) foram auditadas no binário original, documentadas com endereços de segmento/offset, implementadas em serviço de domínio centralizado e validadas em `1`, `3`, `5`, `10` e `20` temporadas sem regressões nas Etapas 01, 02A e 02B.
