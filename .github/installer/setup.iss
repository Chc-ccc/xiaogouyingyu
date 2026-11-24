; ============================================================================
; 小狗英语 · Inno Setup 安装脚本
; 硬性要求：per-user 免 UAC、AppId 跨版本固定、双击即覆盖升级、卸载只删程序目录。
; ============================================================================

; AppId：首次生成工程时确定的固定 GUID，后续所有版本沿用，不得更换或按版本/时间动态生成。
; 双击新版安装包即识别为升级、直接覆盖程序文件，不要求先卸载。
; 注意：AppId 是普通字符串，不含花括号；{#MyAppId} 由 Inno 预处理器展开为字面值。
#define MyAppId '3E702BA5-0A7E-49E0-B613-F3D2C56675C0'
#define MyAppName '小狗英语'
#define MyAppPublisher '小狗英语'

; 版本号由工作流通过 /DMyAppVersion=<版本号>（取自 csproj 的 <Version>）传入；
; 本地直接编译时若未传，则回退为 1.0.0。
#ifndef MyAppVersion
  #define MyAppVersion '1.0.0'
#endif

[Setup]
; 用预处理器指令 {#MyAppId} 展开为固定 GUID 字符串，AppId 跨版本固定
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userappdata}\Programs\小狗英语
DefaultGroupName={#MyAppName}
; 免 UAC：最低权限，不弹管理员提示
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
; 允许覆盖安装（升级）、静默升级
DisableProgramGroupPage=yes
OutputDir=..\..\artifacts
OutputBaseFilename=小狗英语Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; 卸载只删程序目录与快捷方式：不写 UninstallDelete 去碰用户数据
UninstallDisplayName={#MyAppName} {#MyAppVersion}
AllowNoIcons=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："

[Files]
; 载荷是 self-contained 单文件 exe（CMU 词典、内置音节、ARPAbet 映射均已嵌入其中）
Source: "..\..\artifacts\publish\XiaoGouYingYu.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\小狗英语"; Filename: "{app}\XiaoGouYingYu.exe"
Name: "{autodesktop}\小狗英语"; Filename: "{app}\XiaoGouYingYu.exe"; Tasks: desktopicon

[Run]
; 安装完成可选启动程序；安装过程不写任何用户数据——首次播种由程序首次运行完成
Filename: "{app}\XiaoGouYingYu.exe"; Description: "立即运行小狗英语"; Flags: nowait postinstall skipifsilent

; 注意：本脚本没有任何 [Dirs] 在 {userappdata}\小狗英语 下预写词库/进度/设置，
; 也没有 UninstallDelete 删除用户数据。升级覆盖与卸载都只影响 {app} 程序目录与快捷方式。
; 用户数据根目录为 %APPDATA%\小狗英语\，与程序目录刻意分离，升级/卸载全程不触碰。
