@echo off
echo ���b�ظm��e���x����@������...
echo.

:: �T�O�ڭ̦b�M�׮ڥؿ�
set PROJECT_FILE=SerenityRowDisplayNameUpdater.csproj

if not exist "%PROJECT_FILE%" (
    echo ���~: �䤣��M���ɮ� %PROJECT_FILE%
    echo �нT�O�b�M�׮ڥؿ��U���榹�}��
    pause
    exit /b 1
)

:: �M�z���e���ظm
if exist "serupd.exe" del "serupd.exe"
if exist "serupd.pdb" del "serupd.pdb"

:: �ظm��e���x���� (Windows x64)
echo �ظm Windows x64 ����...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "."

echo.
if exist "serupd.exe" (
    echo �ظm�����I�����ɡGserupd.exe
    echo.
    for %%f in (serupd.exe) do echo �ɮפj�p�G%%~zf bytes
    echo.
    echo �{�b�i�H����G
    echo serupd.exe ^<�ؿ����|^> [appsettings.json���|]
) else (
    echo �ظm���ѡI
)

pause