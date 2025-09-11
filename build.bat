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

:: �ˬd�äU�� 7za
call :ensure_7za
if errorlevel 1 goto :error

:: �ظm�å��]���
call :build_and_zip "win-x64" "Windows x64" "win-x64"
if errorlevel 1 goto :error

call :build_and_zip "win-x86" "Windows x86" "win-x86"
if errorlevel 1 goto :error

call :build_and_zip "linux-x64" "Linux x64" "linux-x64"
if errorlevel 1 goto :error

call :build_and_zip "osx-x64" "macOS x64" "osx-x64"
if errorlevel 1 goto :error

call :build_and_zip "osx-arm64" "macOS ARM64" "osx-arm64"
if errorlevel 1 goto :error

echo.
echo �ظm�����IZIP �ɮפw�ͦ��G
echo   Windows x64: dist/serupd-win-x64.zip
echo   Windows x86: dist/serupd-win-x86.zip
echo   Linux x64:   dist/serupd-linux-x64.zip
echo   macOS x64:   dist/serupd-osx-x64.zip
echo   macOS ARM64: dist/serupd-osx-arm64.zip
echo.

echo ZIP �ɮפj�p�G
for %%f in (dist\serupd-*.zip) do (
    echo   %%~nxf: %%~zf bytes
)

echo.
echo ���ܡG�b Linux/macOS �W����e�A�Х��]�w�����v���G
echo   chmod +x serupd
goto :end

:: �ˬd�ýT�O 7za �i��
:ensure_7za
set "SEVEN_ZIP_PATH=tools\7za.exe"

:: �p�G 7za �w�s�b�A������^
if exist "%SEVEN_ZIP_PATH%" (
    echo ��� 7za.exe
    exit /b 0
)

echo ����� 7za.exe�A���b�U��...

:: �Ы� tools �ؿ�
if not exist "tools" mkdir "tools"

:: �U�� 7za
echo ���b�q 7-Zip �x���U�� 7za...
powershell -Command "try { Invoke-WebRequest -Uri 'https://www.7-zip.org/a/7za920.zip' -OutFile 'tools/7za.zip' -UseBasicParsing } catch { Write-Host '�U�����ѡA���ˬd�����s�u'; exit 1 }"
if errorlevel 1 (
    echo �U�� 7za ����
    exit /b 1
)

:: ���� 7za
echo ���b���� 7za...
powershell -Command "try { Expand-Archive -Path 'tools/7za.zip' -DestinationPath 'tools/' -Force; Remove-Item 'tools/7za.zip' } catch { Write-Host '��������'; exit 1 }"
if errorlevel 1 (
    echo ���� 7za ����
    exit /b 1
)

:: �T�{ 7za.exe �s�b
if exist "%SEVEN_ZIP_PATH%" (
    echo ? 7za.exe �U���æw�˦��\
    exit /b 0
) else (
    echo 7za.exe �w�˥���
    exit /b 1
)

:: �ظm�å��]���
:build_and_zip
set "runtime=%~1"
set "display_name=%~2"
set "zip_name=%~3"

echo �ظm %display_name% ����...
dotnet publish "%PROJECT_FILE%" -c Release -r %runtime% --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/%runtime%"
if errorlevel 1 (
    echo %display_name% �ظm����
    exit /b 1
)

echo �M�z���ݭn���ɮ�...
:: �R�� .pdb �ɮ�
if exist "dist\%runtime%\*.pdb" del /q "dist\%runtime%\*.pdb"
:: �R�� .pdf �ɮס]�p�G�����ܡ^
if exist "dist\%runtime%\*.pdf" del /q "dist\%runtime%\*.pdf"

echo �ϥ� 7za ���] %display_name% ����...
"%SEVEN_ZIP_PATH%" a -tzip "dist/serupd-%zip_name%.zip" "dist/%runtime%/*" -mx9
if errorlevel 1 (
    echo %display_name% ���]����
    exit /b 1
)

:: �M�z�ظm�ؿ�
rmdir /s /q "dist/%runtime%"
echo ? %display_name% �ظm�å��]����

exit /b 0

:error
echo.
echo �ظm�L�{���o�Ϳ��~�A���ˬd��X�T��

:end
pause