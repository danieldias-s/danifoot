# Bolinhas da escalação e saldo fora da taça

Trabalho isolado na branch `codex/squad-clicks-cup-balance`, continuando o
checkpoint `300853f` da correção do intervalo. A prévia local usa a compilação
Web deste worktree e a API atualizada com essa correção. Nenhum merge, push ou
publicação em produção foi realizado.

## Escalação clássica

O clique na bolinha agora percorre diretamente:

1. Fora dos relacionados → titular.
2. Titular → reserva.
3. Reserva → fora dos relacionados.

O menu foi removido. A mesma ação existe na tela de táticas clássica. Um clique
pode deixar o rascunho com 12 titulares ou 10 reservas: nenhum outro atleta é
trocado ou retirado automaticamente. **ENVIAR TÁTICA** fica desabilitado até
corrigir a quantidade. A validação também impede duplicação, atletas
indisponíveis e uma relação sem o goleiro necessário. As regras existentes de
elenco reduzido permanecem: a quantidade exigida é a informada pelo servidor.

Os contadores mostram titulares e reservas. Após o treinador enviar a tática,
as bolinhas continuam bloqueadas como antes. Durante a edição do rascunho não
é enviado comando de escrita ao servidor; a escalação só é enviada ao confirmar
a tática. A versão atual mantém seu controle visual existente.

## Saldo do GB-030 na taça

Foi feita uma consulta somente leitura ao save **Daniel**, clube
**Equipe fictícia GB-030**, temporada 1, rodada 6. Havia 32 partidas de
`CupRoundOf64`; o clube não tinha partida nessa rodada.

| Saldo anterior | Entrada na rodada | Saldo posterior |
| ---: | ---: | ---: |
| R$ 676.427,28 | R$ 3.428,57 | R$ 679.855,85 |

O único lançamento desse clube na rodada foi `Sponsorship`:
**Patrocínio anual R$ 72.000,00: parcela 6/21**. Não entrou bilheteria, cota de
visitante ou prêmio, nem houve cobrança de salário por partida nessa rodada.
Os valores e os identificadores consultados estão em
[auditoria-saldo.json](auditoria-saldo.json).

Esse comportamento corresponde à regra aprovada em
[ECONOMY_BALANCE_FIX_012D.md](../ECONOMY_BALANCE_FIX_012D.md): o patrocínio anual
é parcelado pelo calendário e continua mesmo após eliminação. A rotina
`SettleMatchAndRoundFinancials` registra o patrocínio antes de conferir se o
clube disputou uma partida. Bilheteria e despesas de jogo dependem de partida
real. Saves antigos que ainda usam termos comerciais legados preservam seus
contratos até a atualização agendada. **Nenhuma regra econômica ou lançamento
do save do proprietário foi alterado por esta auditoria.**

## Validação

- **373 testes Flutter passaram**, incluindo os testes atualizados de clique
  direto e os fluxos existentes das duas aparências.
- O conjunto focado de escalação passou em 360, 390, 430, 768, 1366 e 1920
  pixels. O caso com banco cheio confirmou rascunhos de 12 titulares e 10
  reservas, envio bloqueado e liberação após ajuste manual.
- **52 testes de economia passaram**, incluindo dois novos casos com um clube
  fora da taça, nas políticas anual e legada. Foram conferidos o saldo real,
  apenas o crédito contratado de patrocínio, a bilheteria dos participantes e
  ausência de crédito duplicado ao repetir a liquidação.
- Análise Flutter sem problemas e compilação Web release concluída.

Os logs de análise, compilação, regressão e economia estão nesta pasta. Não
houve mudança em MatchEngine, contratos, salários ou evolução dos atletas.

## Navegador com API e PostgreSQL locais

Foram usadas contas e salas exclusivas de QA. O save do proprietário foi apenas
lido. As janelas Chromium mediram 1366×1100 e 390×1000; não houve teste em
aparelho físico.

- Reserva → fora → titular funcionou sem menu. Com 12 titulares, o envio ficou
  desabilitado. Ao retirar manualmente outro titular, foi habilitado novamente.
- A edição do rascunho não gerou requisições de escrita do jogo.
- Com um humano em campo e um espectador, o servidor confirmou automaticamente
  o espectador no intervalo. Uma única confirmação, do treinador em campo,
  retomou `RUNNING_SECOND_HALF`, minuto 46.
- Em outra sala, só com espectador, foram observados os minutos 44, 45, 46 e 47:
  no minuto 45 o estado já era `RUNNING_SECOND_HALF`, sem pausa ativa, modal de
  espera ou botão CONTINUAR.
- Nenhum `pageerror` foi registrado nas janelas de QA.

Medições em [evidence.json](evidence.json). Capturas:
[titulares excedidos no web](titulares-excedidos-web.png),
[troca no mobile](troca-mobile.png),
[espectador aguardando humano](espectador-aguarda-humano-mobile.png) e
[segundo tempo sem espera](segundo-tempo-sem-espera-mobile.png).

## Testar na prévia

Abra <http://127.0.0.1:5242/> e use **Ctrl+F5**. Na versão clássica, clique
diretamente na bolinha para mudar a relação. Se exceder a quantidade, retire
outro jogador antes de enviar a tática.
