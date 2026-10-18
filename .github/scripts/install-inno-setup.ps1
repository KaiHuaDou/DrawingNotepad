$ErrorActionPreference = 'Stop'

$version = '7.1.0'
$tag = 'is-' + ($version -replace '\.', '_')
$major = $version.Split('.')[0]
$installer = Join-Path $env:RUNNER_TEMP "innosetup-$version-x64.exe"

Invoke-WebRequest -Uri "https://github.com/jrsoftware/issrc/releases/download/$tag/innosetup-$version-x64.exe" -OutFile $installer

# -ArgumentList 的数组元素以空格拼接且不自动加引号：含空格的 /DIR 会被拆成多个参数，
# 因此不指定安装目录，使用安装包的默认目录。
$process = Start-Process -FilePath $installer -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
if ($process.ExitCode -notin @(0, 3010)) {
    throw "Inno Setup 安装失败：退出码 $($process.ExitCode)"
}

$candidates = @(Join-Path $env:ProgramFiles "Inno Setup $major\ISCC.exe")
if (${env:ProgramFiles(x86)}) {
    $candidates += Join-Path ${env:ProgramFiles(x86)} "Inno Setup $major\ISCC.exe"
}

$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "未找到 ISCC.exe。候选路径：$($candidates -join '; ')"
}

if ($env:GITHUB_ENV) {
    "ISCC=$iscc" >> $env:GITHUB_ENV
}
Write-Host "ISCC: $iscc"
