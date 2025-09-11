@echo off
echo 正在建置 Serenity Row DisplayName Updater 單一執行檔...
echo.

:: 清理之前的建置
if exist "dist" rmdir /s /q "dist"
mkdir "dist"

:: 確保我們在專案根目錄
set PROJECT_FILE=SerenityRowDisplayNameUpdater.csproj

if not exist "%PROJECT_FILE%" (
    echo 錯誤: 找不到專案檔案 %PROJECT_FILE%
    echo 請確保在專案根目錄下執行此腳本
    pause
    exit /b 1
)

:: 檢查並下載 7za
call :ensure_7za
if errorlevel 1 goto :error

:: 建置並打包函數
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
echo 建置完成！ZIP 檔案已生成：
echo   Windows x64: dist/serupd-win-x64.zip
echo   Windows x86: dist/serupd-win-x86.zip
echo   Linux x64:   dist/serupd-linux-x64.zip
echo   macOS x64:   dist/serupd-osx-x64.zip
echo   macOS ARM64: dist/serupd-osx-arm64.zip
echo.

echo ZIP 檔案大小：
for %%f in (dist\serupd-*.zip) do (
    echo   %%~nxf: %%~zf bytes
)

echo.
echo 提示：在 Linux/macOS 上執行前，請先設定執行權限：
echo   chmod +x serupd
goto :end

:: 檢查並確保 7za 可用
:ensure_7za
set "SEVEN_ZIP_PATH=tools\7za.exe"

:: 如果 7za 已存在，直接返回
if exist "%SEVEN_ZIP_PATH%" (
    echo 找到 7za.exe
    exit /b 0
)

echo 未找到 7za.exe，正在下載...

:: 創建 tools 目錄
if not exist "tools" mkdir "tools"

:: 下載 7za
echo 正在從 7-Zip 官網下載 7za...
powershell -Command "try { Invoke-WebRequest -Uri 'https://www.7-zip.org/a/7za920.zip' -OutFile 'tools/7za.zip' -UseBasicParsing } catch { Write-Host '下載失敗，請檢查網路連線'; exit 1 }"
if errorlevel 1 (
    echo 下載 7za 失敗
    exit /b 1
)

:: 解壓 7za
echo 正在解壓 7za...
powershell -Command "try { Expand-Archive -Path 'tools/7za.zip' -DestinationPath 'tools/' -Force; Remove-Item 'tools/7za.zip' } catch { Write-Host '解壓失敗'; exit 1 }"
if errorlevel 1 (
    echo 解壓 7za 失敗
    exit /b 1
)

:: 確認 7za.exe 存在
if exist "%SEVEN_ZIP_PATH%" (
    echo ? 7za.exe 下載並安裝成功
    exit /b 0
) else (
    echo 7za.exe 安裝失敗
    exit /b 1
)

:: 建置並打包函數
:build_and_zip
set "runtime=%~1"
set "display_name=%~2"
set "zip_name=%~3"

echo 建置 %display_name% 版本...
dotnet publish "%PROJECT_FILE%" -c Release -r %runtime% --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/%runtime%"
if errorlevel 1 (
    echo %display_name% 建置失敗
    exit /b 1
)

echo 清理不需要的檔案...
:: 刪除 .pdb 檔案
if exist "dist\%runtime%\*.pdb" del /q "dist\%runtime%\*.pdb"
:: 刪除 .pdf 檔案（如果有的話）
if exist "dist\%runtime%\*.pdf" del /q "dist\%runtime%\*.pdf"

echo 使用 7za 打包 %display_name% 版本...
"%SEVEN_ZIP_PATH%" a -tzip "dist/serupd-%zip_name%.zip" "dist/%runtime%/*" -mx9
if errorlevel 1 (
    echo %display_name% 打包失敗
    exit /b 1
)

:: 清理建置目錄
rmdir /s /q "dist/%runtime%"
echo ? %display_name% 建置並打包完成

exit /b 0

:error
echo.
echo 建置過程中發生錯誤，請檢查輸出訊息

:end
pause