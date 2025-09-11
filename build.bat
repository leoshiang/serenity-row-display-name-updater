@echo off
echo ���b�ظm Serenity Row DisplayName Updater ��@������...
echo.

:: �M�z���e���ظm
if exist "dist" rmdir /s /q "dist"
mkdir "dist"

:: �T�O�ڭ̦b�M�׮ڥؿ�
set PROJECT_FILE=SerenityRowDisplayNameUpdater.csproj

if not exist "%PROJECT_FILE%" (
    echo ���~: �䤣��M���ɮ� %PROJECT_FILE%
    echo �нT�O�b�M�׮ڥؿ��U���榹�}��
    pause
    exit /b 1
)

:: Windows x64 ����
echo �ظm Windows x64 ����...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/win-x64"
if errorlevel 1 (
    echo Windows x64 �ظm����
    goto :error
)

:: Windows x86 ����
echo �ظm Windows x86 ����...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/win-x86"
if errorlevel 1 (
    echo Windows x86 �ظm����
    goto :error
)

:: Linux x64 ����
echo �ظm Linux x64 ����...
dotnet publish "%PROJECT_FILE%" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/linux-x64"
if errorlevel 1 (
    echo Linux x64 �ظm����
    goto :error
)

:: macOS x64 ����
echo �ظm macOS x64 ����...
dotnet publish "%PROJECT_FILE%" -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/osx-x64"
if errorlevel 1 (
    echo macOS x64 �ظm����
    goto :error
)

:: macOS ARM64 ����
echo �ظm macOS ARM64 ����...
dotnet publish "%PROJECT_FILE%" -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/osx-arm64"
if errorlevel 1 (
    echo macOS ARM64 �ظm����
    goto :error
)

echo.
echo �ظm�����I�����ɦ��H�U�ؿ��G
echo   Windows x64: dist/win-x64/serupd.exe
echo   Windows x86: dist/win-x86/serupd.exe
echo   Linux x64:   dist/linux-x64/serupd
echo   macOS x64:   dist/osx-x64/serupd
echo   macOS ARM64: dist/osx-arm64/serupd
echo.

echo �ɮפj�p�G
for /d %%d in (dist\*) do (
    echo   %%~nd:
    for %%f in (%%d\serupd*) do (
        if exist "%%f" (
            echo     %%~nxf: %%~zf bytes
        )
    )
)

echo.
echo ���ܡG�b Linux/macOS �W����e�A�Х��]�w�����v���G
echo   chmod +x serupd
goto :end

:error
echo.
echo �ظm�L�{���o�Ϳ��~�A���ˬd��X�T��

:end
pause