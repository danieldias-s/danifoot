param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if($root -ne 'D:\elifoot_98_016B'){throw 'Auditoria exclusiva da 016B.'}
$area=Join-Path $root 'artifacts/016b/lobby-audit'
$api='http://127.0.0.1:5173/api'
$context=Get-Content (Join-Path $area 'browser-room.json') -Raw | ConvertFrom-Json
$world=$context.world.id
$auth=$context.auth
$events=[Collections.Generic.List[object]]::new()
function Request([string]$path,$body=$null){
 if($null -eq $body){return Invoke-RestMethod ($api+$path) -TimeoutSec 30}
 return Invoke-RestMethod ($api+$path) -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 8 -Compress) -TimeoutSec 30
}
function Room([string]$id){$allRooms=Request '/worlds';return ($allRooms | Where-Object id -eq $id)}
function Record([string]$step,[string]$id,[int]$occupied,[int]$connected){
 $room=$null
 for($i=0;$i -lt 60;$i++){
  $room=Room $id
  if($room.currentHumans -eq $occupied -and $room.connectedHumans -eq $connected){break}
  Start-Sleep -Milliseconds 50
 }
 if($room.currentHumans -ne $occupied -or $room.connectedHumans -ne $connected){throw "Contagem divergente em $step"}
 if($room.roundState -ne 'PREPARING' -or $room.roundNumber -ne 1 -or $room.readyCounter.readyCount -ne 0){throw 'Auditoria não pode iniciar ou avançar partidas.'}
 $events.Add(@{step=$step;worldId=$id;occupied=$room.currentHumans;connected=$room.connectedHumans;capacity=$room.maxHumans;ready=$room.readyCounter.readyCount;necessary=$room.readyCounter.totalHumans;state=$room.roundState})
 return $room
}
function Connect(){
 $socket=[Net.WebSockets.ClientWebSocket]::new()
 $socket.ConnectAsync([uri]'ws://127.0.0.1:5173/hubs/gameroom',[Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
 $bytes=[Text.Encoding]::UTF8.GetBytes('{"protocol":"json","version":1}'+[char]30)
 $socket.SendAsync([ArraySegment[byte]]::new($bytes),[Net.WebSockets.WebSocketMessageType]::Text,$true,[Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
 return $socket
}
function HubCall($socket,[string]$method,[object[]]$arguments){
 $invocation=[guid]::NewGuid().ToString('N')
 $payload=@{type=1;invocationId=$invocation;target=$method;arguments=$arguments} | ConvertTo-Json -Depth 5 -Compress
 $bytes=[Text.Encoding]::UTF8.GetBytes($payload+[char]30)
 $socket.SendAsync([ArraySegment[byte]]::new($bytes),[Net.WebSockets.WebSocketMessageType]::Text,$true,[Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
 $timeout=[Threading.CancellationTokenSource]::new(10000)
 $buffer=[byte[]]::new(262144)
 $text=''
 try {
  while($true){
   $received=$socket.ReceiveAsync([ArraySegment[byte]]::new($buffer),$timeout.Token).GetAwaiter().GetResult()
   $text += [Text.Encoding]::UTF8.GetString($buffer,0,$received.Count)
   while($text.Contains([char]30)){
    $end=$text.IndexOf([char]30);$frame=$text.Substring(0,$end);$text=$text.Substring($end+1)
    if(-not $frame){continue}
    $message=$frame | ConvertFrom-Json
    if($message.type -eq 3 -and $message.invocationId -eq $invocation){
     if($message.error){throw $message.error}
     return $message.result
    }
   }
  }
 }finally{$timeout.Dispose()}
}
function Close($socket){
 if($null -eq $socket){return}
 try{$socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'audit complete',[Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null}finally{$socket.Dispose()}
}
$first=$null;$second=$null;$returned=$null
try {
 $before=Room $world
 if($before.currentHumans -eq 0){
  $clubs=Request "/worlds/$world/clubs"
  $club=$clubs | Where-Object name -eq 'Flamengo RJ' | Select-Object -First 1
  $joined=Request "/worlds/$world/join?coachId=$($auth.coachId)&nickname=Auditoria%20016B&deviceId=016b-lobby-browser&preferredClubId=$($club.id)" @{}
 }else{
  $joined=Request "/worlds/$world/join?coachId=$($auth.coachId)&nickname=Auditoria%20016B&deviceId=016b-lobby-browser" @{}
 }
 $canonical=$joined.coachId;$originalClub=$joined.clubId
 $null=Record 'HTTP join reserves one seat, no live transport' $world 1 0
 $first=Connect
 $null=HubCall $first 'JoinRoom' @($world,$canonical)
 $null=Record 'First websocket connected' $world 1 1
 $second=Connect
 $null=HubCall $second 'JoinRoom' @($world,$canonical)
 $null=Record 'Two connections of one trainer still count once' $world 1 1
 Close $first;$first=$null
 $null=Record 'Closing old tab preserves active tab' $world 1 1
 Close $second;$second=$null
 $null=Record 'Last transport disconnects, reservation survives' $world 1 0
 $renamed=Request '/auth/local-login' @{nickname='Auditoria 016B Renomeado';deviceId='016b-lobby-browser'}
 $restored=Request "/worlds/$world/join?coachId=$($renamed.coachId)&nickname=Auditoria%20016B%20Renomeado&deviceId=016b-lobby-browser" @{}
 if($restored.coachId -ne $canonical -or $restored.clubId -ne $originalClub){throw 'Identidade ou clube foi alterado na reconexão.'}
 $returned=Connect
 $null=HubCall $returned 'JoinRoom' @($world,$restored.coachId)
 $null=Record 'Renamed login recovered canonical coach and same club' $world 1 1
 $outsider=Request '/auth/local-login' @{nickname='Auditoria nova identidade';deviceId='016b-lobby-outsider'}
 $full=Invoke-WebRequest "$api/worlds/$world/join?coachId=$($outsider.coachId)&nickname=Novo&deviceId=016b-lobby-outsider" -Method Post -SkipHttpErrorCheck
 if([int]$full.StatusCode -ne 400 -or ($full.Content | ConvertFrom-Json).code -ne 'ROOM_FULL'){throw 'Sala lotada não foi rejeitada corretamente.'}
 $null=Record 'Full room rejects a distinct trainer' $world 1 1
 $secondRoomFile=Join-Path $area 'isolation-room.json'
 if(Test-Path $secondRoomFile){$other=Get-Content $secondRoomFile -Raw | ConvertFrom-Json}else{
  $other=Request '/worlds' @{worldName='016B - Auditoria isolamento (8 vagas)';maxHumans=8;seed=199820}
  $other | ConvertTo-Json -Depth 20 | Set-Content $secondRoomFile
 }
 $otherId=$other.gameWorldId
 $otherJoin=Request "/worlds/$otherId/join?coachId=$canonical&nickname=Auditoria%20016B&deviceId=016b-lobby-browser" @{}
 $null=HubCall $returned 'JoinRoom' @($otherId,$canonical)
 $null=Record 'Switch transport leaves only its previous room' $world 1 0
 $null=Record 'Other room has independent seat count' $otherId 1 1
 $null=HubCall $returned 'LeaveRoom' @($world,$canonical)
 $null=Record 'Wrong-room leave cannot remove current presence' $otherId 1 1
 $null=HubCall $returned 'JoinRoom' @($world,$canonical)
 $null=Record 'Returning to first room restores presence once' $world 1 1
 $null=Record 'Second room membership remains reserved' $otherId 1 0
 Close $returned;$returned=$null
 $null=Record 'Final disconnect preserves trainer and capacity' $world 1 0
 $summary=@{status='PASS';transport='Real HTTP and ClientWebSocket SignalR; no browser UI';worldId=$world;coachId=$canonical;clubId=$originalClub;fullRoomStatus=[int]$full.StatusCode;events=$events.ToArray()}
 $summary | ConvertTo-Json -Depth 16 | Set-Content (Join-Path $area 'live-session-evidence.json')
 Write-Output "PASS: $($events.Count) live HTTP/SignalR checks; same trainer and club preserved; no match advanced."
}finally{Close $first;Close $second;Close $returned}
