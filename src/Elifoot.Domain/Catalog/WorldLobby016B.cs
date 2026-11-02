using Elifoot.Contracts;
namespace Elifoot.Domain;

public partial class GameWorld
{
    // Null for every legacy save: existing rooms keep their original lifecycle.
    public WorldLobby016B? InitialLobby { get; set; }
    public string GameModeId { get; set; } = "CLASSIC_SYNC";
}
public sealed class WorldLobby016B
{
    public Guid HostCoachId { get; set; }
    public Dictionary<Guid,LobbyChoice016B> Choices { get; set; } = new();
    public Dictionary<Guid,Guid> DrawAssignments { get; set; } = new();
    public DateTime? DrawCompletedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public long Version { get; set; } = 1;
    public HashSet<string> CommandReceipts { get; set; } = new(StringComparer.Ordinal);
    public static WorldLobbyView016B View(GameWorld world,IReadOnlySet<Guid> online)
    {
        var lobby=world.InitialLobby ?? throw new ArgumentException("Esta sala usa o fluxo clássico anterior, sem configuração inicial pendente.");
        var participants=world.HumanCoaches.OrderBy(c=>c.Id).Select(c=> {
            var choice=lobby.Choices.GetValueOrDefault(c.Id) ?? new(InitialCareerChoice016B.PENDING);
            return new LobbyParticipant016B(c.Id,c.Nickname,online.Contains(c.Id),choice.Choice,c.CurrentClubId,
                world.Clubs.FirstOrDefault(x=>x.Id==c.CurrentClubId)?.Name);
        }).ToList();
        var issues=new List<string>();
        if(world.GameModeId!="CLASSIC_SYNC")issues.Add("Modo de jogo não implementado.");
        if(world.HumanCoaches.Count!=world.MaxHumans)issues.Add($"Aguardando participantes: {world.HumanCoaches.Count}/{world.MaxHumans}.");
        if(!world.HumanCoaches.Any(c=>c.Id==lobby.HostCoachId))issues.Add("O criador ainda não entrou na sala.");
        if(participants.Any(p=>!p.Connected))issues.Add("Todos os participantes precisam estar conectados.");
        if(participants.Any(p=>p.Choice==InitialCareerChoice016B.PENDING))issues.Add("Todos os participantes precisam definir como começar.");
        int draws=participants.Count(p=>p.Choice==InitialCareerChoice016B.FOURTH_DIVISION_DRAW);
        int available=world.Clubs.Count(c=>c.DivisionTier==DivisionTier.QuartaDivisao&&c.HumanCoachId==null);
        if(lobby.DrawCompletedAt==null&&available<draws)issues.Add("Não há clubes livres suficientes na quarta divisão.");
        if(participants.Any(p=>p.Choice==InitialCareerChoice016B.MANUAL&&p.ClubId==null))issues.Add("Escolha manual sem clube atribuído.");
        bool ready=issues.Count==0;
        var phase=lobby.StartedAt!=null?WorldLobbyPhase016B.IN_GAME:world.HumanCoaches.Count==0?WorldLobbyPhase016B.CONFIGURING:
            !ready?WorldLobbyPhase016B.WAITING_FOR_PLAYERS:draws>0&&lobby.DrawCompletedAt==null?WorldLobbyPhase016B.READY_FOR_DRAW:
            lobby.DrawCompletedAt!=null?WorldLobbyPhase016B.DRAW_COMPLETED:WorldLobbyPhase016B.READY_TO_START;
        return new(world.Id,world.GameModeId,lobby.HostCoachId,phase,world.MaxHumans,participants.Count,
            participants.Count(p=>p.Connected),ready&&draws>0&&lobby.DrawCompletedAt==null&&lobby.StartedAt==null,
            ready&&(draws==0||lobby.DrawCompletedAt!=null)&&lobby.StartedAt==null,lobby.DrawCompletedAt!=null,lobby.Version,participants,issues);
    }
}
