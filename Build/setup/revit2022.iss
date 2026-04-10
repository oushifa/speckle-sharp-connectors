#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SourceRoot
  #define SourceRoot ""
#endif

[Setup]
AppId={{A7E2C34F-0A51-4F3B-9A3B-7F8B5B8E3B2D}
AppName=Speckle Revit 2022 Connector
AppVersion={#MyAppVersion}
DefaultDirName={userappdata}\Autodesk\Revit\Addins\2022
DisableDirPage=yes
DisableProgramGroupPage=yes
UninstallDisplayName=Speckle Revit 2022 Connector
OutputDir=out
OutputBaseFilename=Speckle-Revit2022-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#SourceRoot}\Speckle.Connectors.Revit2022.addin"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\Speckle.Connectors.Revit2022\*"; DestDir: "{app}\Speckle.Connectors.Revit2022"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
Type: files; Name: "{app}\Speckle.Connectors.Revit2022.addin"
Type: filesandordirs; Name: "{app}\Speckle.Connectors.Revit2022"
