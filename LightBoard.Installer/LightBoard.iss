; 轻白板（LightBoard）安装包脚本
; 需要 Inno Setup 7.0.2 或更高版本编译
;
; 编译命令（AppVersion 缺省为 0.0.0，CI 从 CHANGELOG 提取后传入）：
;   ISCC -DAppVersion=1.2.3 LightBoard.iss                 框架依赖版，含 .NET 运行时检测
;   ISCC -DAppVersion=1.2.3 -DSelfContained LightBoard.iss 自包含版，无运行时检测
;
; 产物来源（两个变体各自读取自己的发布目录，与发布、构建的先后顺序无关）：
;   框架依赖版 ← LightBoard/bin/publish
;     dotnet publish LightBoard -p:PublishProfile=FolderProfile -c Release
;   自包含版 ← LightBoard/bin/publish-with-runtime
;     dotnet publish LightBoard -p:PublishProfile=FolderProfile -c Release --self-contained -p:PublishDir=bin/publish-with-runtime/
;
; 框架依赖版在运行时缺失时，在 PrepareToInstall 阶段从 aka.ms 下载最新 .NET 10 Desktop Runtime
; 并静默安装，普通模式与静默模式（/SILENT、/VERYSILENT）行为一致。

#ifndef AppVersion
    #define AppVersion "0.0.0"
#endif

#ifdef SelfContained
    #define VariantSuffix "-with-runtime"
    #define PublishDir "..\LightBoard\bin\publish-with-runtime"
#else
    #define VariantSuffix ""
    #define PublishDir "..\LightBoard\bin\publish"
#endif

#define AppName "LightBoard"
#define AppDisplayName "轻白板"
#ifndef SelfContained
    #define DesktopRuntimeUrl "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
    #define DesktopRuntimeName "windowsdesktop-runtime-win-x64.exe"
#endif

[Setup]
AppId={{57F024C3-01AE-49FB-935E-F82E13059603}}
AppVersion={#AppVersion}
AppName={#AppDisplayName}
AppVerName={#AppDisplayName} {#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppDisplayName}
UninstallDisplayIcon={app}\{#AppName}.exe
OutputDir=Output
OutputBaseFilename=LightBoard-Setup{#VariantSuffix}-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; x64 安装器：{pf} 等路径不经过 WOW64 重定向，与 win-x64 应用匹配
SetupArchitecture=x64
MinVersion=10.0
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
SetupLogging=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
; 发布目录可能残留应用运行时生成的数据（本地从发布目录直接启动过）：不得打入安装包
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "\settings.json,\error.log,\XpsCache,\recover"

[Icons]
Name: "{autoprograms}\{#AppDisplayName}"; Filename: "{app}\{#AppName}.exe"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#AppName}.exe"

[Run]
Filename: "{app}\{#AppName}.exe"; Description: "{cm:LaunchProgram,{#AppDisplayName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\settings.json"; Check: DeleteUserData
Type: filesandordirs; Name: "{app}\XpsCache"; Check: DeleteUserData
Type: filesandordirs; Name: "{app}\recover"; Check: DeleteUserData

[Code]
var
    DeleteUserDataAsked: Boolean;
    DeleteUserDataValue: Boolean;

// [UninstallDelete] 的 Check 条目：存在数据文件才询问，默认「否」即保留；一次卸载只问一次
function DeleteUserData: Boolean;
begin
    if not DeleteUserDataAsked then
    begin
        DeleteUserDataAsked := True;
        DeleteUserDataValue := FileExists(ExpandConstant('{app}\settings.json')) or DirExists(ExpandConstant('{app}\XpsCache')) or DirExists(ExpandConstant('{app}\recover'));
        if DeleteUserDataValue then
            DeleteUserDataValue := MsgBox('是否删除数据文件？', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
    end;
    Result := DeleteUserDataValue;
end;

// 自包含发布已内置运行时，跳过运行时检测与安装
#ifndef SelfContained
const
    DesktopRuntimeUrl = '{#DesktopRuntimeUrl}';
    DesktopRuntimeName = '{#DesktopRuntimeName}';
    DesktopRuntimeMajor = 10;
    ManualDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0';

var
    RuntimeChecked: Boolean;
    RuntimePresent: Boolean;

// 目录中存在主版本号匹配的子目录（如 10.0.13）即认为已安装。
function DirHasDesktopRuntime(const DotNetRoot: String): Boolean;
var
    FindRec: TFindRec;
    FxDir: String;
begin
    Result := False;
    FxDir := AddBackslash(DotNetRoot) + 'shared\Microsoft.WindowsDesktop.App';
    if not DirExists(FxDir) then
        Exit;
    if FindFirst(AddBackslash(FxDir) + '*', FindRec) then
    try
        repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') and
            (Copy(FindRec.Name, 1, Pos('.', FindRec.Name + '.') - 1) = IntToStr(DesktopRuntimeMajor)) then
        begin
            Result := True;
            Break;
        end;
        until not FindNext(FindRec);
    finally
        FindClose(FindRec);
    end;
end;

// 机器级 C:\Program Files\dotnet 与 dotnet-install 脚本的 per-user 安装位置
function DesktopRuntimeInstalled: Boolean;
begin
    if not RuntimeChecked then
    begin
        RuntimePresent :=
        DirHasDesktopRuntime(ExpandConstant('{pf}\dotnet')) or
        DirHasDesktopRuntime(ExpandConstant('{localappdata}\Microsoft\dotnet'));
        RuntimeChecked := True;
    end;
    Result := RuntimePresent;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
    if not WizardSilent then
    begin
        if ProgressMax <> 0 then
        WizardForm.StatusLabel.Caption := Format('正在下载 .NET 10 Desktop Runtime……%d/%d MB', [Progress div 1048576, ProgressMax div 1048576])
        else
        WizardForm.StatusLabel.Caption := Format('正在下载 .NET 10 Desktop Runtime……%d MB', [Progress div 1048576]);
        WizardForm.Repaint;
    end;
    Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
    InstallerPath: String;
    ResultCode: Integer;
begin
    Result := '';
    if DesktopRuntimeInstalled then
        Exit;

    if not IsAdmin then
    begin
        Result := '本机缺少 .NET 10 Desktop Runtime，自动安装需要管理员权限。'#13#10'请以管理员身份重新运行安装程序，或从以下地址手动安装后再运行：'#13#10 + ManualDownloadUrl;
        Exit;
    end;

    InstallerPath := AddBackslash(ExpandConstant('{tmp}')) + DesktopRuntimeName;
    if not WizardSilent then
        WizardForm.StatusLabel.Caption := '正在下载 .NET 10 Desktop Runtime……';
        try
            // aka.ms 是常青链接，运行时每次补丁更新都会改变文件哈希，无法固定 RequiredSHA256；
            // 传输完整性由 TLS 与微软官方域名承担
            DownloadTemporaryFile(DesktopRuntimeUrl, DesktopRuntimeName, '', @OnDownloadProgress);
        except
            Result := '下载 .NET 10 Desktop Runtime 失败：' + GetExceptionMessage + #13#10 + '可从以下地址手动安装后再运行：' + ManualDownloadUrl;
            Exit;
    end;

    if not WizardSilent then
        WizardForm.StatusLabel.Caption := '正在安装 .NET 10 Desktop Runtime……';
    if not Exec(InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
        Result := '无法启动 .NET 10 Desktop Runtime 安装程序：' + SysErrorMessage(ResultCode) + #13#10 + '可从以下地址手动安装后再运行：' + ManualDownloadUrl;
        Exit;
    end;

    // 0 成功；1641/3010 成功但要求重启，运行时无需重启即可使用
    case ResultCode of
        0, 1641, 3010: ;
    else
    begin
        Result := '.NET 10 Desktop Runtime 安装失败（退出码 ' + IntToStr(ResultCode) + '）。'#13#10 + '可从以下地址手动安装后再运行：' + ManualDownloadUrl;
        Exit;
    end;
    end;

    RuntimeChecked := False;
    if not DesktopRuntimeInstalled then
        Result := '.NET 10 Desktop Runtime 安装后仍未检测到，请查看安装日志，或从以下地址手动安装后再运行：' + ManualDownloadUrl;
end;

#endif
