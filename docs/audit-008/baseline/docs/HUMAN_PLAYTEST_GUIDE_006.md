# ELIFOOT 98 — GUIA RÁPIDO DE PLAYTEST HUMANO (2 A 8 JOGADORES) (006)

**TASK:** `ELIFOOT_DISCIPLINE_AND_GAMEPLAY_AUDIT_006`  
**PROJECT:** `D:\elifoot_98`

Este guia prático permite que **2 a 8 jogadores humanos** realizem uma sessão de playtest real do **Elifoot Classic Multiplayer** via navegador Web (mesma máquina com múltiplas abas/janelas ou em rede local LAN).

---

## 1. Pré-Requisitos e Inicialização Rápida (2 Minutos)

1. **Banco de Dados PostgreSQL Ativo:**
   ```powershell
   docker compose up -d postgres
   ```
2. **Iniciar o Backend .NET 10 (`http://localhost:5098`):**
   ```powershell
   dotnet run --project src/Elifoot.Api/Elifoot.Api.csproj --urls "http://0.0.0.0:5098"
   ```
3. **Iniciar o Cliente Flutter Web (`http://localhost:8080`):**
   ```powershell
   cd src/elifoot_client
   flutter run -d web-server --web-hostname 0.0.0.0 --web-port 8080
   ```
   *(Para jogar em rede local LAN, os demais jogadores acessam `http://<IP-DO-HOST>:8080` no navegador).*

---

## 2. Passo a Passo para Criar e Entrar na Sala (2 a 8 Jogadores)

1. **Jogador 1 (Host):**
   - Abra `http://localhost:8080`.
   - Digite o nome do seu treinador (ex.: `Treinador 1`) e clique em **Criar Novo Mundo / Sala**.
   - Anote o **Código da Sala (`RoomCode`)** de 6 caracteres exibido no topo da tela (ex.: `ELI98A`).
2. **Jogadores 2 a 8:**
   - Abram uma nova janela/aba anônima (ou acessem de outro dispositivo na LAN).
   - Digitem o nome do treinador, informem o **Código da Sala (`RoomCode`)** e cliquem em **Entrar na Sala**.
   - Cada jogador receberá automaticamente um clube inicial da **4ª Divisão** (fiel à progressão clássica do Elifoot 98) e seu token de sessão exclusivo.

---

## 3. Roteiro de Jogo por Rodada (15 Minutos para 3 a 5 Rodadas)

### Fase A — Preparação (`PREPARING`)
1. **Aba Elenco & Tática:**
   - Escolha uma das 10 formações clássicas (`4-4-2`, `4-3-3`, `3-4-3`, `5-3-2`, `5-4-1`, `4-5-1`, `3-5-2`, `4-2-4`, `3-6-1` ou `5-0-5`).
   - Escale **11 titulares** (`1` Goleiro `G` + `10` jogadores de linha) e até **7 reservas**.
   - Verifique cartões amarelos acumulados, suspensões e lesões.
2. **Aba Mercado & Leilões:**
   - Faça propostas diretas por jogadores de outros clubes ou dê lances nos atletas disponíveis em leilão.
   - Venda reservas excedentes caso precise reforçar o caixa.
3. **Aba Estádio & Finanças:**
   - Ajuste o preço do ingresso de acordo com a divisão e fase do time (`R$ 10` a `R$ 30`).
   - Se a condição do estádio cair abaixo de `85%`, execute manutenção preventiva.
4. **Confirmar Pronto (`Estou Pronto`):**
   - Quando todos os jogadores humanos clicarem em **Pronto**, o Host inicia a rodada simultânea.

### Fase B — Partida Simultânea ao Vivo (`LIVE_FIRST_HALF` / `LIVE_SECOND_HALF`)
1. Acompanhe o placar simultâneo de todas as partidas da divisão, o minuto atual (`1'..90'`), a posse de bola e os eventos narrados:
   - Gols normais e gols de pênalti;
   - `"PÊNALTI DEFENDIDO!"` pelo goleiro vs `"PÊNALTI PARA FORA!"`;
   - Cartões amarelos, 2º amarelo e vermelhos diretos.
2. **Pausa Global (`Pausar Jogo`):**
   - Qualquer treinador humano com pausas disponíveis pode clicar em **Pausar** para congelar a rodada (`GLOBAL_PAUSED`).
   - Durante a pausa ou no **Intervalo (`HALFTIME`)**, faça até **3 substituições** e mude a formação tática (ex.: mudar para `5-4-1` para segurar vitória ou `3-4-3`/`4-2-4` para buscar empate).
   - Clique em **Retomar** para continuar a rodada.

### Fase C — Pós-Rodada (`POST_ROUND`)
1. Confira a tabela de classificação atualizada das 4 divisões, os resultados da Taça, a receita de bilheteria e o ranking de treinadores.
2. Teste a **Reconexão**: feche a aba de um jogador e reabra usando a opção **Reconectar com Token/Sala** para confirmar que o clube e a escalação permanecem intactos.

---

## 4. Checklist de Avaliação Humana Pós-Sessão

Ao final de 5 a 10 rodadas, avaliem em grupo:
- [ ] A quantidade de cartões vermelhos (`~0,16` por jogo) e pênaltis (`~0,20` por jogo) pareceu justa e natural?
- [ ] As mudanças táticas no intervalo/pausa global tiveram impacto perceptível na posse e nas chances criadas?
- [ ] A disputa por jogadores no leilão e a gestão de caixa/estádio geraram boas decisões estratégicas entre os amigos?
- [ ] O tempo de espera na pausa global foi rápido e fluido para todos os participantes?
