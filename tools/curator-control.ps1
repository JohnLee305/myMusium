[CmdletBinding()]
param([int]$Port=4181,[string]$ArchivePath,[switch]$SmokeTest)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Net.Http
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
if(-not $ArchivePath){$ArchivePath=Join-Path $root 'museum-archive'}
$archive=(Resolve-Path -LiteralPath $ArchivePath).Path
$url="http://127.0.0.1:$Port"
$script:worker=$null
$script:probe=$null
$script:nextProbe=[datetime]::MinValue
$script:online=$false
$script:phase=0
$script:passed=$false
$script:actionName=''
$script:deadline=[datetime]::Now.AddSeconds(60)
$handler=New-Object System.Net.Http.HttpClientHandler
$handler.UseProxy=$false
$client=New-Object System.Net.Http.HttpClient($handler)
$client.Timeout=[timespan]::FromSeconds(2)
if($Port -lt 1024 -or $Port -gt 65535 -or ($SmokeTest -and $Port -eq 4181)){throw 'Invalid test port'}

$form=New-Object Windows.Forms.Form
$form.Text='VR 박물관 관리'
$form.ClientSize=New-Object Drawing.Size(620,365)
$form.StartPosition='CenterScreen'
$form.FormBorderStyle='FixedDialog'
$form.MaximizeBox=$false
$form.BackColor=[Drawing.ColorTranslator]::FromHtml('#F5F3EB')
$form.Font=New-Object Drawing.Font('Malgun Gothic',10)
$form.AutoScaleMode='Dpi'
function Label([string]$text,[int]$x,[int]$y,[int]$w,[int]$h,[int]$size=10) {
 $label=New-Object Windows.Forms.Label
 $label.Text=$text;$label.Location=New-Object Drawing.Point($x,$y);$label.Size=New-Object Drawing.Size($w,$h)
 $label.Font=New-Object Drawing.Font('Malgun Gothic',$size)
 $label.ForeColor=[Drawing.ColorTranslator]::FromHtml('#243B35')
 $form.Controls.Add($label);return $label
}
$title=Label '박물관 관리 서버' 28 24 560 40 20
$subtitle=Label '내 PC에서 전시품과 방별 테마를 관리합니다.' 30 72 555 27
$status=Label '● 상태 확인 중…' 30 119 555 35 15
$info=Label '잠시 기다려 주세요.' 30 160 555 46
function Button([string]$text,[int]$x,[int]$w) {
 $button=New-Object Windows.Forms.Button
 $button.Text=$text;$button.Location=New-Object Drawing.Point($x,217);$button.Size=New-Object Drawing.Size($w,46)
 $button.FlatStyle='Flat';$button.BackColor=[Drawing.ColorTranslator]::FromHtml('#E4EBE1')
 $button.Enabled=$false;$form.Controls.Add($button);return $button
}
$startButton=Button '서버 켜기' 30 165
$stopButton=Button '서버 끄기' 210 165
$openButton=Button '관리페이지 열기 ↗' 390 200
$footer=Label "이 창을 닫아도 서버는 유지됩니다. 끄려면 ‘서버 끄기’를 누르세요.`r`n관람 앱은 관리 서버가 꺼져 있어도 사용할 수 있습니다." 30 285 560 60 9
function RunAction([string]$action) {
 if($script:worker){return}
 $script:actionName=$action
 $stamp=[datetime]::Now.ToString('yyyyMMdd-HHmmss-fff')
 $script:outFile=Join-Path $root "logs/control-$stamp.log"
 $script:errFile=Join-Path $root "logs/control-$stamp-error.log"
 $file=Join-Path $PSScriptRoot "$action-admin.ps1"
 $argsList=@('-NoProfile','-ExecutionPolicy','Bypass','-File',"`"$file`"",'-Port',"$Port")
 if($action -eq 'start'){$argsList+=@('-NoBrowser','-ArchivePath',"`"$archive`"")}
 $startButton.Enabled=$false;$stopButton.Enabled=$false;$openButton.Enabled=$false
 $status.Text=if($action -eq 'start'){'● 서버를 켜는 중…'}else{'● 서버를 끄는 중…'}
 $info.Text='작업이 끝나면 버튼이 다시 활성화됩니다.'
 try {
  $script:worker=Start-Process -FilePath "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList $argsList -WindowStyle Hidden -RedirectStandardOutput $script:outFile -RedirectStandardError $script:errFile -PassThru
  [void]$script:worker.Handle
 } catch {$info.Text='실행하지 못했습니다: '+$_.Exception.Message;$script:nextProbe=[datetime]::MinValue}
}
$startButton.Add_Click({RunAction 'start'})
$stopButton.Add_Click({RunAction 'stop'})
$openButton.Add_Click({try{Start-Process $url}catch{$info.Text='브라우저를 열지 못했습니다: '+$_.Exception.Message}})
$timer=New-Object Windows.Forms.Timer
$timer.Interval=200
$timer.Add_Tick({
 try {
  if($script:worker) {
   if(-not $script:worker.HasExited){return}
   $script:worker.WaitForExit();$exit=$script:worker.ExitCode;$script:worker.Dispose();$script:worker=$null
   if($exit -ne 0) {
    $info.Text=if($script:actionName -eq 'stop'){'종료하지 못했습니다. 저장 중이면 잠시 뒤 다시 눌러 주세요.'}else{'시작하지 못했습니다. 실행 로그를 확인해 주세요.'}
    $info.Text+="`r`n"+$script:errFile
    if($SmokeTest){throw "Action failed: $script:errFile"}
   } else {$info.Text=if($script:actionName -eq 'start'){'관리페이지 열기를 누르면 브라우저가 열립니다.'}else{'서버를 껐습니다. 필요할 때 다시 켤 수 있습니다.'}}
   $script:nextProbe=[datetime]::MinValue
  }
  if($script:probe -and $script:probe.IsCompleted) {
   $script:online=$false;$conflict=$false
   try {
    $response=$script:probe.GetAwaiter().GetResult()
    if($response.IsSuccessStatusCode) {
     $health=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
     $script:online=$health.service -eq 'vr-museum-curator' -and $health.archive -eq $archive
     $conflict=-not $script:online
    } else {$conflict=$true}
    $response.Dispose()
   } catch {if(-not $script:probe.IsFaulted -and -not $script:probe.IsCanceled){$conflict=$true}}
   $script:probe.Dispose();$script:probe=$null
   $status.Text=if($conflict){'● 다른 프로그램이 이 주소를 사용 중입니다'}elseif($script:online){'● 켜져 있음'}else{'● 꺼져 있음'}
   $status.ForeColor=if($script:online){[Drawing.ColorTranslator]::FromHtml('#245D50')}else{[Drawing.ColorTranslator]::FromHtml('#68736C')}
   $startButton.Enabled=-not $script:online -and -not $conflict
   $stopButton.Enabled=$script:online;$openButton.Enabled=$script:online
   if($info.Text -eq '잠시 기다려 주세요.'){$info.Text=if($script:online){'관리페이지 열기를 누르면 브라우저가 열립니다.'}else{'서버 켜기를 누른 뒤 관리페이지를 열어 주세요.'}}
   if($SmokeTest) {
    if($script:phase -eq 0) {if($script:online -or $conflict){throw 'Test port occupied'};$script:phase=1;$startButton.PerformClick()}
    elseif($script:phase -eq 1 -and $script:online) {
     if(-not $stopButton.Enabled -or -not $openButton.Enabled -or $startButton.Enabled){throw 'Online buttons invalid'}
     $bitmap=New-Object Drawing.Bitmap($form.Width,$form.Height);$form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$form.Width,$form.Height)))
     $bitmap.Save((Join-Path $root 'logs/curator-control.png'));$bitmap.Dispose()
     $script:phase=2;$stopButton.PerformClick()
    }
    elseif($script:phase -eq 2 -and -not $script:online){$script:phase=3;$startButton.PerformClick()}
    elseif($script:phase -eq 3 -and $script:online){$script:phase=4;$stopButton.PerformClick()}
    elseif($script:phase -eq 4 -and -not $script:online){$script:passed=$true;$form.Close()}
   }
  }
  if(-not $script:worker -and -not $script:probe -and [datetime]::Now -ge $script:nextProbe) {
   $script:probe=$client.GetAsync("$url/api/health")
   $script:nextProbe=[datetime]::Now.AddSeconds(2)
  }
  if($SmokeTest -and [datetime]::Now -gt $script:deadline){throw 'UI test timeout'}
 } catch {
  $info.Text=$_.Exception.Message
  if($SmokeTest){$script:failure=$_.Exception.Message;$form.Close()}
 }
})
$form.Add_FormClosing({param($sender,$eventArgs) if($script:worker){$eventArgs.Cancel=$true;$info.Text='켜기/끄기가 끝난 뒤 창을 닫아 주세요.'}})
$form.Add_Shown({$timer.Start()})
[Windows.Forms.Application]::Run($form)
$timer.Stop();$timer.Dispose();$client.Dispose();$form.Dispose()
if($SmokeTest){if($script:passed){Write-Output 'CONTROL_UI_PASS: offline -> on -> off -> on -> off; status/buttons/screenshot';exit 0}else{Write-Error "CONTROL_UI_FAIL: $script:failure";exit 1}}
