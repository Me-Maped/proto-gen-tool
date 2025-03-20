@echo off
set TOOL_DIR=.\Tools\ProtoGenTool.dll
set PROTOC=.\Tools\protoc\win\protoc.exe
set PROTO_FILES=.\Proto
set PROTO_GEN=.\CSharp\ProtoGen

set SETTING_FILE=.\appsettings.json

dotnet %TOOL_DIR% %SETTING_FILE%

if exist %PROTO_GEN% (
    rd /s /q %PROTO_GEN%
)
md %PROTO_GEN%

:process_protos
setlocal enabledelayedexpansion
for /r "%PROTO_FILES%" %%f in (*.proto) do (
    "%PROTOC%" --csharp_out="%PROTO_GEN%" "%%f"
    if !errorlevel! neq 0 (
        echo Error compiling "%%f"
        exit /b 1
    )
)
endlocal

echo Generate protobuf finish!