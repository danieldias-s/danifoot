using Elifoot.Contracts;
namespace Elifoot.Domain;
public static partial class MatchSimulationEngine
{
    public static void ResolvePenaltyKick(MatchFixture match, Club attackingClub, Player penTaker,
        Player? defendingGk, double goalkeeperPower, bool isHomeAttacking, int minute,
        double convRoll, List<MatchEventRecord> events, Guid? decisionId = null)
    {
            var taker = new PenaltyTakerChoiceDto(penTaker.Id, penTaker.DisplayName, penTaker.Position, penTaker.Strength, penTaker.IsStar);
            double takerSkill = 0.74 + (penTaker.Strength - 25) * 0.004 + (penTaker.IsStar ? 0.06 : 0.0);
            double gkResist = goalkeeperPower * 0.003;
            double penConvProb = Math.Clamp(takerSkill - gkResist, 0.58, 0.88);

            if (convRoll < penConvProb)
            {
                RecordGoalScored(match, attackingClub, penTaker, isHomeAttacking, minute, isPenalty: true, events);
                match.Events[^1].PenaltyKick = new(decisionId, taker, "Goal", match.HomeGoals, match.AwayGoals);
            }
            else
            {
                double missRange = Math.Max(0.0001, 1.0 - penConvProb);
                double saveShare = Math.Clamp(0.50 + (goalkeeperPower - 25.0) * 0.008, 0.35, 0.72);
                bool isGoalkeeperSave = (convRoll - penConvProb) < (missRange * saveShare);

                var missEvt = new MatchEventRecord
                {
                    Minute = minute,
                    EventType = MatchEventType.PenaltyMissed,
                    PenaltyKick = new(decisionId, taker, isGoalkeeperSave ? "Saved" : "Wide", match.HomeGoals, match.AwayGoals),
                    ClubId = attackingClub.Id,
                    ClubName = attackingClub.Name,
                    PrimaryPlayerId = penTaker.Id,
                    PrimaryPlayerDisplayName = penTaker.DisplayName,
                    SecondaryPlayerId = isGoalkeeperSave ? defendingGk?.Id : null,
                    SecondaryPlayerDisplayName = isGoalkeeperSave ? defendingGk?.DisplayName : null,
                    Description = isGoalkeeperSave
                        ? $"{minute}' PÊNALTI DEFENDIDO! {defendingGk?.DisplayName ?? "Goleiro"} defende a cobrança de {penTaker.DisplayName} ({attackingClub.ShortName})!"
                        : $"{minute}' PÊNALTI PARA FORA! {penTaker.DisplayName} ({attackingClub.ShortName}) chuta para fora!"
                };
                match.Events.Add(missEvt);
                events.Add(missEvt);
            }
    }
}
