using System.Security.Cryptography;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Application;

public sealed partial class MultiplayerRoundOrchestrator
{
    private WorldLobbyView016B LobbyView(GameWorld w)=>WorldLobby016B.View(w,_connections?.ConnectedCoaches(w.Id) ?? w.HumanCoaches.Where(c=>c.IsConnected).Select(c=>c.Id).ToHashSet());
    public async Task<WorldLobbyView016B> InitialLobbyView(Guid id,CancellationToken ct=default) {
        var gate=GetLock(id);await gate.WaitAsync(ct);try {
            var w=await _repository.GetWorldAsync(id,ct)??throw new KeyNotFoundException("Sala não encontrada.");return LobbyView(w);
        }finally{gate.Release();}
    }
    private static WorldLobby016B LobbyHost(GameWorld w,Guid actor) {
        var lobby=w.InitialLobby??throw new ArgumentException("Sala anterior sem lobby configurável.");
        if(lobby.HostCoachId!=actor || !w.HumanCoaches.Any(c=>c.Id==actor&&c.IsHost))throw new UnauthorizedAccessException("Somente o criador da sala pode executar esta ação.");
        return lobby;
    }
    public async Task<WorldLobbyView016B> ChooseInitialCareer(Guid id,Guid actor,LobbyChoice016B choice,CancellationToken ct=default) {
        var gate=GetLock(id);await gate.WaitAsync(ct);try {
            var w=await _repository.GetWorldAsync(id,ct)??throw new KeyNotFoundException("Sala não encontrada.");
            var lobby=w.InitialLobby??throw new ArgumentException("Sala sem lobby inicial.");
            var coach=w.HumanCoaches.SingleOrDefault(c=>c.Id==actor)??throw new UnauthorizedAccessException("Treinador não pertence à sala.");
            if(!Enum.IsDefined(choice.Choice)||choice.Choice==InitialCareerChoice016B.PENDING)throw new ArgumentException("Escolha como começar.");
            if(choice.Choice!=InitialCareerChoice016B.MANUAL&&choice.ClubId!=null)throw new ArgumentException("Esta opção não permite informar clube.");
            if(lobby.Choices.GetValueOrDefault(actor)==choice)return LobbyView(w);
            if(lobby.StartedAt!=null||lobby.DrawCompletedAt!=null)throw new ArgumentException("Escolhas bloqueadas após o sorteio ou início.");
            if(choice.Choice==InitialCareerChoice016B.MANUAL) {
                if(choice.ClubId==null)throw new ArgumentException("Escolha um clube disponível.");
                var result=TryAssignClubInternal(w,coach,choice.ClubId.Value);
                if(!result.Success)throw new ArgumentException(result.Error);
            } else if(coach.CurrentClubId is Guid old) {
                var club=w.Clubs.Single(c=>c.Id==old);club.HumanCoachId=null;coach.CurrentClubId=null;
                coach.IsReadyForRound=false;coach.SubmittedFormation=null;coach.SubmittedStarterIds.Clear();coach.SubmittedBenchIds.Clear();
                var job=coach.EmploymentHistory.LastOrDefault(e=>e.EndSeason==null);
                if(job!=null){job.EndSeason=w.SeasonNumber;job.EndRound=w.RoundNumber;job.ReasonForLeaving="INITIAL_LOBBY_CHOICE";}
                CoachCareerEngine.OpenVacancy(w,club);CoachCareerEngine.EnsureProvisional(w,club);
            }
            lobby.Choices[actor]=choice;lobby.Version++;w.RoundVersion++;
            await _repository.SaveWorldAsync(w,ct);await _publisher.PublishReadyCounterUpdatedAsync(w.Id,BuildReadyCounterDto(w),ct);return LobbyView(w);
        }finally{gate.Release();}
    }
    public async Task<WorldLobbyView016B> DrawInitialClubs(Guid id,Guid actor,string key,CancellationToken ct=default) {
        if(string.IsNullOrWhiteSpace(key)||key.Length>160)throw new ArgumentException("Identificador de operação obrigatório.");
        var gate=GetLock(id);await gate.WaitAsync(ct);try {
            var w=await _repository.GetWorldAsync(id,ct)??throw new KeyNotFoundException("Sala não encontrada.");var lobby=LobbyHost(w,actor);
            if(lobby.DrawCompletedAt!=null)return LobbyView(w);
            if(!LobbyView(w).CanDraw)throw new ArgumentException(string.Join(" ",LobbyView(w).Issues.Append("O sorteio ainda não está disponível.")));
            var clubs=w.Clubs.Where(c=>c.DivisionTier==DivisionTier.QuartaDivisao&&c.HumanCoachId==null).OrderBy(c=>c.Id).ToList();
            var coaches=w.HumanCoaches.Where(c=>lobby.Choices[c.Id].Choice==InitialCareerChoice016B.FOURTH_DIVISION_DRAW).OrderBy(c=>c.Id).ToList();
            if(clubs.Count<coaches.Count)throw new ArgumentException("Não há clubes livres suficientes na quarta divisão.");
            for(int i=clubs.Count-1;i>0;i--){int j=RandomNumberGenerator.GetInt32(i+1);(clubs[i],clubs[j])=(clubs[j],clubs[i]);}
            for(int i=0;i<coaches.Count;i++){AssignCoachToClubObject(w,coaches[i],clubs[i]);lobby.DrawAssignments[coaches[i].Id]=clubs[i].Id;}
            lobby.DrawCompletedAt=DateTime.UtcNow;lobby.CommandReceipts.Add("draw:"+key);lobby.Version++;w.RoundVersion++;
            await _repository.SaveWorldAsync(w,ct);await _publisher.PublishReadyCounterUpdatedAsync(w.Id,BuildReadyCounterDto(w),ct);return LobbyView(w);
        }finally{gate.Release();}
    }
    public async Task<WorldLobbyView016B> StartInitialChampionship(Guid id,Guid actor,string key,CancellationToken ct=default) {
        if(string.IsNullOrWhiteSpace(key)||key.Length>160)throw new ArgumentException("Identificador de operação obrigatório.");
        var gate=GetLock(id);await gate.WaitAsync(ct);try {
            var w=await _repository.GetWorldAsync(id,ct)??throw new KeyNotFoundException("Sala não encontrada.");var lobby=LobbyHost(w,actor);
            if(lobby.StartedAt!=null)return LobbyView(w);
            var view=LobbyView(w);if(!view.CanStart)throw new ArgumentException(string.Join(" ",view.Issues.Append("Conclua as escolhas e o sorteio antes de iniciar.")));
            lobby.StartedAt=DateTime.UtcNow;lobby.CommandReceipts.Add("start:"+key);lobby.Version++;w.RoundVersion++;
            foreach(var coach in w.HumanCoaches)coach.IsReadyForRound=false;
            await _repository.SaveWorldAsync(w,ct);await _publisher.PublishReadyCounterUpdatedAsync(w.Id,BuildReadyCounterDto(w),ct);return LobbyView(w);
        }finally{gate.Release();}
    }
    public async Task<WorldLobbyView016B> ConfigureInitialCapacity(Guid id,Guid actor,LobbyCapacity016B command,CancellationToken ct=default) {
        if(command.MaxHumans is <1 or >8 || string.IsNullOrWhiteSpace(command.IdempotencyKey))throw new ArgumentException("Capacidade deve ser de 1 a 8, com identificador de operação.");
        var gate=GetLock(id);await gate.WaitAsync(ct);try {
            var w=await _repository.GetWorldAsync(id,ct)??throw new KeyNotFoundException("Sala não encontrada.");var lobby=LobbyHost(w,actor);
            string key="capacity:"+command.IdempotencyKey;if(lobby.CommandReceipts.Contains(key))return LobbyView(w);
            if(lobby.StartedAt!=null||lobby.DrawCompletedAt!=null||command.MaxHumans<w.HumanCoaches.Count)throw new ArgumentException("Capacidade não pode remover participantes ou mudar após sorteio/início.");
            w.MaxHumans=command.MaxHumans;lobby.CommandReceipts.Add(key);lobby.Version++;w.RoundVersion++;
            await _repository.SaveWorldAsync(w,ct);await _publisher.PublishReadyCounterUpdatedAsync(w.Id,BuildReadyCounterDto(w),ct);return LobbyView(w);
        }finally{gate.Release();}
    }
}
