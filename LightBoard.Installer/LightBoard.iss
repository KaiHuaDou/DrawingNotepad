; 轻白板（LightBoard）安装包脚本
; 需要 Inno Setup 7.0.2 或更高版本编译
;
; 编译命令（AppVersion 默认为 0.0.0，CI 从 csproj 的 Version 提取后传入）：
;   ISCC -DAppVersion=1.2.3 LightBoard.iss                        框架依赖版，含 .NET 运行时检测
;   ISCC -DAppVersion=1.2.3 -DSelfContained LightBoard.iss        自包含版，无运行时检测
;   上述命令均可加 -DArm64 构建 Windows Arm64 版本，或加 -DNoOpenCv 构建不含 OpenCV 的变体
;
; 产物来源（各变体读取各自的发布目录，与发布、构建的先后顺序无关）：
;   框架依赖版 ← LightBoard/bin/publish[-nocv]-<架构>
;   自包含版 ← LightBoard/bin/publish[-nocv][-runtime]-<架构>
;   架构取 x64 或 arm64，发布命令见 README 的「构建」。
;
; 框架依赖版在运行时缺失时，在 PrepareToInstall 阶段从 aka.ms 下载最新 .NET 10 Desktop Runtime
; 并静默安装，普通模式与静默模式（/SILENT、/VERYSILENT）行为一致。

#ifndef AppVersion
    #define AppVersion "0.0.0"
#endif
; VersionInfoVersion 只接受点分数字；预发布版本需另传 -DAppVersionNumeric=1.2.3
#ifndef AppVersionNumeric
    #define AppVersionNumeric AppVersion
#endif

#ifdef SelfContained
    #define RuntimeSuffix "-runtime"
#else
    #define RuntimeSuffix ""
#endif

#ifdef NoOpenCv
    #define OpenCvSuffix "-nocv"
#else
    #define OpenCvSuffix ""
#endif

#ifdef Arm64
    #define ArchSuffix "-arm64"
    #define ArchitecturesValue "arm64"
#else
    #define ArchSuffix "-x64"
    #define ArchitecturesValue "x64compatible"
#endif

; 目录名与产物名共用同一后缀顺序：publish[-nocv][-runtime]-<架构>，LightBoard-installer[-nocv][-runtime]-<架构>
#define VariantSuffix OpenCvSuffix + RuntimeSuffix + ArchSuffix
#define PublishDir "..\LightBoard\bin\publish" + VariantSuffix

#define AppName "LightBoard"
#define AppDisplayName "轻白板"
#ifndef SelfContained
    #ifdef Arm64
        #define DesktopRuntimeUrl "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-arm64.exe"
        #define DesktopRuntimeName "windowsdesktop-runtime-win-arm64.exe"
    #else
        #define DesktopRuntimeUrl "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
        #define DesktopRuntimeName "windowsdesktop-runtime-win-x64.exe"
    #endif
#endif

[Setup]
AppId={{57F024C3-01AE-49FB-935E-F82E13059603}}
AppVersion={#AppVersion}
AppName={#AppDisplayName}
AppVerName={#AppDisplayName} {#AppVersion}
VersionInfoVersion={#AppVersionNumeric}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppDisplayName}
UninstallDisplayIcon={app}\{#AppName}.exe
OutputDir=Output
OutputBaseFilename=LightBoard-installer{#VariantSuffix}-{#AppVersion}
WizardStyle=modern
; 引导程序为 x64（Inno Setup 的 SetupArchitecture 只有 x86、x64），Arm64 版本在 Arm64 Windows 上以 x64 模拟运行
SetupArchitecture=x64
ArchitecturesAllowed={#ArchitecturesValue}
ArchitecturesInstallIn64BitMode={#ArchitecturesValue}
PrivilegesRequired=admin
SetupLogging=yes

Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=6

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
; 发布目录可能残留应用运行时生成的数据（本地从发布目录直接启动过）：不得打入安装包
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "\settings.json,\error.log,\XpsCache,\recover,\fastsave"

[Icons]
Name: "{group}\{#AppDisplayName}"; Filename: "{app}\{#AppName}.exe"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#AppName}.exe"

[Run]
Filename: "{app}\{#AppName}.exe"; Description: "{cm:LaunchProgram,{#AppDisplayName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
    DeleteUserDataOnUninstall: Boolean;

// 数据文件由卸载程序在卸载阶段按用户选择处理，不写入 [UninstallDelete]：
// 该节的 Check 在安装阶段评估，首次安装时数据尚不存在，条目会被永久跳过。
function HasUserData: Boolean;
begin
    Result :=
        FileExists(ExpandConstant('{app}\settings.json')) or
        FileExists(ExpandConstant('{app}\error.log')) or
        DirExists(ExpandConstant('{app}\XpsCache')) or
        DirExists(ExpandConstant('{app}\recover')) or
        DirExists(ExpandConstant('{app}\fastsave'));
end;

function InitializeUninstall: Boolean;
begin
    Result := True;
    DeleteUserDataOnUninstall := HasUserData;
    if not DeleteUserDataOnUninstall then
        Exit;

    // 静默卸载不显示消息框，按默认值保留数据
    if UninstallSilent then
    begin
        DeleteUserDataOnUninstall := False;
        Exit;
    end;

    DeleteUserDataOnUninstall := MsgBox('是否删除设置、日志、缓存与自动备份？', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
    if (CurUninstallStep = usPostUninstall) and DeleteUserDataOnUninstall then
    begin
        DelTree(ExpandConstant('{app}\XpsCache'), True, True, True);
        DelTree(ExpandConstant('{app}\recover'), True, True, True);
        DelTree(ExpandConstant('{app}\fastsave'), True, True, True);
        DeleteFile(ExpandConstant('{app}\settings.json'));
        DeleteFile(ExpandConstant('{app}\error.log'));
    end;
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
            if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) = 0 then
                Continue;
            if Copy(FindRec.Name, 1, Pos('.', FindRec.Name + '.') - 1) = IntToStr(DesktopRuntimeMajor) then
            begin
                Result := True;
                Break;
            end;
        until not FindNext(FindRec);
    finally
        FindClose(FindRec);
    end;
end;

// Arm64 的运行时装在 {pf}\dotnet；Arm64 上的 x64 运行时装在 {pf}\dotnet\x64。
// 两套可以同时存在，检测时只看目标架构的位置。
function DesktopRuntimeInstalled: Boolean;
var
    EnvRoot: String;
begin
    if not RuntimeChecked then
    begin
#ifdef Arm64
        EnvRoot := ExpandConstant('{%DOTNET_ROOT|}');
        RuntimePresent :=
        DirHasDesktopRuntime(ExpandConstant('{pf}\dotnet')) or
        ((EnvRoot <> '') and DirHasDesktopRuntime(EnvRoot));
#else
        EnvRoot := ExpandConstant('{%DOTNET_ROOT_X64|}');
        if IsArm64 then
            RuntimePresent :=
            DirHasDesktopRuntime(ExpandConstant('{pf}\dotnet\x64')) or
            ((EnvRoot <> '') and DirHasDesktopRuntime(EnvRoot))
        else
            RuntimePresent :=
            DirHasDesktopRuntime(ExpandConstant('{pf}\dotnet')) or
            ((EnvRoot <> '') and DirHasDesktopRuntime(EnvRoot));
#endif
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
        Result := '无法启动 .NET 10 Desktop Runtime 安装程序。'#13#10 + '可从以下地址手动安装后再运行：' + ManualDownloadUrl;
        Exit;
    end;

    // 0 成功；1641/3010 表示安装器报告成功并要求重启
    case ResultCode of
        0: ;
        1641, 3010: NeedsRestart := True;
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
