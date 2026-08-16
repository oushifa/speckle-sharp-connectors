#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SourceRoot
  #define SourceRoot ""
#endif

[Setup]
AppId={{7503E9BC-4242-4EE5-A409-0EEBAFA1F5F0}
AppName=Speckle Revit 2020 Connector
AppVersion={#MyAppVersion}
DefaultDirName={userappdata}\Autodesk\Revit\Addins\2020
DisableDirPage=yes
DisableProgramGroupPage=yes
UninstallDisplayName=Speckle Revit 2020 Connector
OutputDir=out
OutputBaseFilename=Speckle-Revit2020-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#SourceRoot}\Speckle.Connectors.Revit2020.addin"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\Speckle.Connectors.Revit2020\*"; DestDir: "{app}\Speckle.Connectors.Revit2020"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
Type: files; Name: "{app}\Speckle.Connectors.Revit2020.addin"
Type: filesandordirs; Name: "{app}\Speckle.Connectors.Revit2020"
