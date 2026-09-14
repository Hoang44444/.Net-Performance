# Chạy bộ thu thập của Visual Studio (VSDiagnostics.exe — cùng agent với Debug > Performance Profiler)
# cho từng cặp "bản thường / bản tối ưu" của lab RPG Battle Simulator, lưu .diagsession và bản đọc phân tích.
#
#   powershell -ExecutionPolicy Bypass -File tools\profile-rpg-battle.ps1 [-Chi valueref-thuong,equality-toiuu] [-ChiDoc]
#
# Mỗi phiên tạo 2 file trong docs\profiling\rpg-battle-simulator\:
#   <id>-cpu.diagsession   + <id>-cpu.txt     (CPU Usage, 4.000 mẫu/giây)
#   <id>-alloc.diagsession + <id>-alloc.txt   (.NET Object Allocation Tracking, ghi TỪNG object)
# File .diagsession mở trực tiếp được bằng Visual Studio (File > Open).
param(
    [string[]] $Chi = @(),
    [switch] $ChiDoc   # không thu thập lại, chỉ đọc lại các .diagsession đã có
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'Project'
$exe = Join-Path $project 'bin\Release\net10.0\Project.exe'
$out = Join-Path $repo 'docs\profiling\rpg-battle-simulator'
$reader = Join-Path $PSScriptRoot 'DiagSessionReader'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -prerelease -property installationPath
$collector = Join-Path $vs 'Team Tools\DiagnosticsHub\Collector'
$vsdiag = Join-Path $collector 'VSDiagnostics.exe'
$cfgCpu = Join-Path $collector 'AgentConfigs\CpuUsageHigh.json'
$cfgAlloc = Join-Path $collector 'AgentConfigs\DotNetObjectAllocBase.json'

# id, tham số phiên CPU (đủ dài để có mẫu), tham số phiên allocation (nhỏ hơn: agent ghi từng object), hàm vùng đo
$phien = @(
    @('valueref-thuong',        'valueref thuong 1000000 30',             'valueref thuong 100000 1',             'ChupSnapshot_Class'),
    @('valueref-toiuu',         'valueref toiuu 1000000 30',              'valueref toiuu 100000 1',              'ChupSnapshot_Struct'),
    @('params-thuong',          'params thuong 1000000 50',               'params thuong 100000 1',               'LuotDanh_Thuong'),
    @('params-toiuu',           'params toiuu 1000000 50',                'params toiuu 100000 1',                'LuotDanh_ToiUu'),
    @('memory-thuong',          'memory thuong 100000 30',                'memory thuong 100000 1',               'TaoNhanVatDayDu'),
    @('memory-toiuu',           'memory toiuu 100000 30',                 'memory toiuu 100000 1',                'TaoChiSoTran'),
    @('equality-thuong',        'equality thuong 1000000 30',             'equality thuong 100000 1',             'SoSanh_StatBlock_Thuong'),
    @('equality-toiuu',         'equality toiuu 1000000 30',              'equality toiuu 100000 1',              'SoSanh_StatBlock_ToiUu'),
    @('equality-item-thuong',   'equality item-thuong 1000000 5',         'equality item-thuong 50000 1',         'SoSanh_Item_Thuong'),
    @('equality-item-toiuu',    'equality item-toiuu 1000000 5',          'equality item-toiuu 50000 1',          'SoSanh_Item_ToiUu'),
    @('boxing-thuong',          'boxing thuong 1000000 5',                'boxing thuong 100000 1',               'ThongKe_KhoCu'),
    @('boxing-toiuu',           'boxing toiuu 1000000 5',                 'boxing toiuu 100000 1',                'ThongKe_KhoHienDai'),
    @('mutablestruct-thuong',   'mutablestruct thuong 10000 50',          'mutablestruct thuong 1000 1',          'ApDung_ChepRaGhiLai'),
    @('mutablestruct-toiuu',    'mutablestruct toiuu 10000 50',           'mutablestruct toiuu 1000 1',           'ApDung_Span'),
    @('specialtypes-thuong',    'specialtypes thuong 1000000 3',          'specialtypes thuong 100000 1',         'NapQuai_Thuong'),
    @('specialtypes-toiuu',     'specialtypes toiuu 1000000 3',           'specialtypes toiuu 100000 1',          'NapQuai_ToiUu'),
    @('ketqua-thuong',          'specialtypes ketqua-thuong 1000000 30',  'specialtypes ketqua-thuong 100000 1',  'DanhNhieuDon_Tuple'),
    @('ketqua-toiuu',           'specialtypes ketqua-toiuu 1000000 30',   'specialtypes ketqua-toiuu 100000 1',   'DanhNhieuDon_ValueTuple'),
    @('advisor-thuong',         'advisor thuong 1000000 200',             'advisor thuong 100000 1',              'DiChuyen_Class'),
    @('advisor-toiuu',          'advisor toiuu 1000000 200',              'advisor toiuu 100000 1',               'DiChuyen_Struct')
)

New-Item -ItemType Directory -Force $out | Out-Null
if (-not $ChiDoc) { dotnet build (Join-Path $project 'Project.csproj') -c Release -nologo -v q | Out-Null }
dotnet build (Join-Path $reader 'DiagSessionReader.csproj') -c Release -nologo -v q | Out-Null
$readerDll = Join-Path $reader 'bin\Release\net10.0\DiagSessionReader.dll'

# Sinh sẵn file dữ liệu để bước sinh file không lẫn vào phiên đo.
if (-not $ChiDoc) {
    & $exe specialtypes toiuu 1000000 0
    & $exe specialtypes toiuu 100000 0
}

function Thu-Thap([int] $soPhien, [string] $thamSo, [string] $cauHinh, [string] $fileRa) {
    if (Test-Path $fileRa) { Remove-Item $fileRa -Force }
    $thuMucMo = [IO.Path]::ChangeExtension($fileRa, $null).TrimEnd('.')
    if (Test-Path $thuMucMo) { Remove-Item $thuMucMo -Recurse -Force }

    & $vsdiag start $soPhien "/launch:$exe" "/launchArgs:$thamSo" "/loadConfig:$cauHinh" | Out-Null
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Get-Process Project -ErrorAction SilentlyContinue) -and $sw.Elapsed.TotalSeconds -lt 20) { Start-Sleep -Milliseconds 100 }
    while ((Get-Process Project -ErrorAction SilentlyContinue) -and $sw.Elapsed.TotalSeconds -lt 600) { Start-Sleep -Milliseconds 250 }
    & $vsdiag stop $soPhien "/output:$fileRa" | Out-Null
}

$so = 100
foreach ($p in $phien) {
    $id, $thamCpu, $thamAlloc, $ham = $p
    if ($Chi.Count -gt 0 -and $Chi -notcontains $id) { continue }

    Write-Host "== $id"
    $so++
    $cpu = Join-Path $out "$id-cpu.diagsession"
    if (-not $ChiDoc) { Thu-Thap $so $thamCpu $cfgCpu $cpu }
    "Phiên CPU Usage · Project.exe $thamCpu · file $id-cpu.diagsession`n" | Out-File -Encoding utf8 (Join-Path $out "$id-cpu.txt")
    dotnet $readerDll cpu $cpu --top 15 --ham $ham | Out-File -Encoding utf8 -Append (Join-Path $out "$id-cpu.txt")

    $so++
    $alloc = Join-Path $out "$id-alloc.diagsession"
    if (-not $ChiDoc) { Thu-Thap $so $thamAlloc $cfgAlloc $alloc }
    ".NET Object Allocation Tracking · Project.exe $thamAlloc · file $id-alloc.diagsession`n" | Out-File -Encoding utf8 (Join-Path $out "$id-alloc.txt")
    dotnet $readerDll alloc $alloc --top 12 | Out-File -Encoding utf8 -Append (Join-Path $out "$id-alloc.txt")
}

# Thư mục giải nén chỉ là trung gian để đọc; file gốc .diagsession được giữ lại.
Get-ChildItem $out -Directory | Remove-Item -Recurse -Force
Write-Host "Xong: $out"
