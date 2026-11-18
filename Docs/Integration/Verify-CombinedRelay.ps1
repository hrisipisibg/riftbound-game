param(
    [ValidateSet('Ui','InvalidCode','Resilience','Combat','Authority','Progression','LateJoin','FullAcceptance','ClientLeave','Capacity')]
    [string[]]$Cases=@('Ui','InvalidCode','Resilience','Combat','Authority','Progression','LateJoin','FullAcceptance','ClientLeave','Capacity'),
    [ValidatePattern('^Builds[/\\][A-Za-z0-9_-]+$')]
    [string]$BuildDirectory='Builds/RiftboundCombined02',
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskExe=Join-Path $taskRoot ($BuildDirectory+'/Riftbound.exe')
if(-not(Test-Path -LiteralPath $taskExe)){throw 'Build the combined executable first.'}
$taskManagedHash=(Get-FileHash -LiteralPath (Join-Path $taskRoot ($BuildDirectory+'/Riftbound_Data/Managed/Assembly-CSharp.dll')) -Algorithm SHA256).Hash
$taskSceneHash=(Get-FileHash -LiteralPath (Join-Path $taskRoot ($BuildDirectory+'/Riftbound_Data/level0')) -Algorithm SHA256).Hash
$taskStamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$taskProfileRun=[Guid]::NewGuid().ToString('N').Substring(0,8)
$taskFolder=Join-Path $taskRoot ".code-check/integration/relay-$taskStamp"
New-Item -ItemType Directory -Path $taskFolder | Out-Null
$script:taskChildren=[System.Collections.Generic.List[object]]::new()
$script:taskResults=[System.Collections.Generic.List[object]]::new()
function Start-Game([string]$role,[string]$flags){
    $taskLog=Join-Path $taskFolder "$role.txt"
    $taskRoleHash=[Security.Cryptography.SHA256]::Create()
    try{$taskRoleDigest=([BitConverter]::ToString($taskRoleHash.ComputeHash([Text.Encoding]::UTF8.GetBytes($role)))).Replace('-','').Substring(0,8).ToLowerInvariant()}
    finally{$taskRoleHash.Dispose()}
    $taskProfile='int_'+$taskProfileRun+'_'+$taskRoleDigest
    $taskArgs='-batchmode -nographics -ugsProfile '+$taskProfile+' '+$flags+' -logFile "'+$taskLog+'"'
    $taskProc=Start-Process -FilePath $taskExe -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru
    $taskChild=[pscustomobject]@{role=$role;process=$taskProc;log=$taskLog}
    $script:taskChildren.Add($taskChild)
    $script:taskChildren | ForEach-Object {[pscustomobject]@{role=$_.role;pid=$_.process.Id;log=$_.log}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskFolder 'processes.json')
    Write-Host "$role started PID=$($taskProc.Id)"
    return $taskChild
}
function Read-Log($child){
    if(-not(Test-Path -LiteralPath $child.log)){return ''}
    $taskStream=[IO.File]::Open($child.log,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $taskReader=[IO.StreamReader]::new($taskStream)
    try{return $taskReader.ReadToEnd()}finally{$taskReader.Dispose()}
}
function Get-Code($child){
    $taskLimit=[DateTime]::UtcNow.AddSeconds(40)
    while([DateTime]::UtcNow -lt $taskLimit -and -not $child.process.HasExited){
        $taskMatch=[regex]::Match((Read-Log $child),'Host session created\. Code=([A-Z0-9]{4,8})')
        if($taskMatch.Success){return $taskMatch.Groups[1].Value}
        Start-Sleep -Milliseconds 500
    }
    throw "No host-ready receipt for $($child.role); inspect its private log."
}
function Wait-Games($children,[int]$seconds=115){
    $taskLimit=[DateTime]::UtcNow.AddSeconds($seconds)
    while([DateTime]::UtcNow -lt $taskLimit){
        if(@($children | Where-Object {-not $_.process.HasExited}).Count -eq 0){break}
        Start-Sleep -Seconds 1
    }
    foreach($taskChild in $children){
        if(-not $taskChild.process.HasExited){throw "Timeout for $($taskChild.role), PID=$($taskChild.process.Id)"}
        $taskChild.process.WaitForExit()
    }
}
function Save-Results{
    $script:taskResults | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskFolder 'results.json')
    $taskText=@('# Combined standalone online verification','','Headless automated Sessions/Relay checks on the exact combined executable. Not manual GUI, rendered performance or two-PC cross-network acceptance. Raw logs remain local; join codes/authentication details are not published.','',"Run: $taskStamp",'', '| Scenario | Result | Exit codes | Sanitized assertions |','| --- | --- | --- | --- |')
    $taskIdentity=@("Build: $BuildDirectory/Riftbound.exe", "Source supplied by build owner: $SourceCommit", "Managed Assembly-CSharp SHA256: $taskManagedHash", "Packaged level0 SHA256: $taskSceneHash",'')
    $taskText=$taskText[0..5]+$taskIdentity+$taskText[6..($taskText.Length-1)]
    foreach($taskRow in $script:taskResults){$taskText+='| '+$taskRow.scenario+' | '+$taskRow.result+' | '+$taskRow.exits+' | '+(($taskRow.assertions -join '; ').Replace('|','/'))+' |'}
    $taskText+=@('','Untested scenarios are NOT RUN, including any remaining requested cases after a runner failure. Expected session-full rejection is checked separately from successful participants. No runtime assertion is modified by this runner.','', ('Private local evidence folder: '+$taskFolder))
    $taskText | Set-Content -LiteralPath (Join-Path $taskRoot 'Docs/Integration/ONLINE_TEST_RESULTS.md')
}
try{
    foreach($taskCase in $Cases){
        $taskStart=$script:taskChildren.Count
        $taskTimes=@{Combat=25;Authority=50;Progression=65;LateJoin=35;FullAcceptance=65;ClientLeave=55;Capacity=25}
        if($taskCase -in @('Ui','InvalidCode','Resilience')){
            $taskFlags=@{Ui='-coopUiSmoke';InvalidCode='-coopInvalidCodeSmoke';Resilience='-coopResilienceSmoke'}
            $null=Start-Game $taskCase $taskFlags[$taskCase]
        }else{
            $taskFlag=if($taskCase -eq 'Capacity'){'-coopMinPlayers 4'}else{"-coop$($taskCase)Smoke"}
            $taskHost=Start-Game "$taskCase-H" "-coopOnlineHost $taskFlag -coopSmoke $($taskTimes[$taskCase])"
            $taskCode=Get-Code $taskHost
            if($taskCase -eq 'LateJoin'){Start-Sleep -Seconds 3}
            $taskClientCount=if($taskCase -eq 'Capacity'){4}else{1}
            for($taskClient=1;$taskClient -le $taskClientCount;$taskClient++){
                $null=Start-Game "$taskCase-C$taskClient" "-coopOnlineJoin $taskCode $taskFlag -coopSmoke $($taskTimes[$taskCase])"
            }
            $taskCode=$null
        }
        $taskGroup=@($script:taskChildren | Select-Object -Skip $taskStart)
        Wait-Games $taskGroup
        $taskLines=@();$taskExitText=@();$taskPass=$true;$taskOk=0;$taskFull=0
        foreach($taskChild in $taskGroup){
            $taskRaw=Read-Log $taskChild
            $taskExit=$taskChild.process.ExitCode
            $taskExitText+="$($taskChild.role)=$taskExit"
            $taskAssertions=@(($taskRaw -split '\r?\n') | Where-Object {$_ -match '^(PASS|FAIL) '} | Select-Object -Unique)
            $taskLines+=($taskAssertions | ForEach-Object {[regex]::Replace($_,'(?i)\bcode(?:s|[12])?=[A-Z0-9]+(?:->[A-Z0-9]+)?','code=[redacted]')})
            if($taskExit -eq 0 -and ($taskAssertions | Where-Object {$_ -match '^PASS '})){$taskOk++}
            elseif($taskCase -eq 'Capacity' -and $taskChild.role -notlike '*-H' -and $taskExit -eq 1 -and $taskRaw -match '(?i)session is full'){$taskFull++}
            else{$taskPass=$false}
        }
        if($taskCase -eq 'Capacity'){
            $taskPass=$taskPass -and $taskOk -eq 4 -and $taskFull -eq 1
            $taskLines+="successfulParticipants=$taskOk; extraRejectedForFullSession=$taskFull (client admission order may vary)"
        }
        $taskResult=if($taskPass){'PASS'}else{'FAIL'}
        $script:taskResults.Add([pscustomobject]@{scenario=$taskCase;result=$taskResult;exits=($taskExitText -join ', ');assertions=$taskLines})
        Save-Results
        Write-Host "$taskCase $taskResult exits=$($taskExitText -join ', ')"
    }
}finally{
    foreach($taskChild in $script:taskChildren){if(-not $taskChild.process.HasExited){Stop-Process -Id $taskChild.process.Id}}
    Save-Results
}
if(@($script:taskResults | Where-Object result -eq 'FAIL').Count -gt 0){exit 1}
