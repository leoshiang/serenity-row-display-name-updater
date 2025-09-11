#!/bin/bash

echo "正在建置 Serenity Row DisplayName Updater 單一執行檔..."
echo

# 清理之前的建置
if [ -d "dist" ]; then
    rm -rf "dist"
fi
mkdir -p "dist"

# 確保我們在專案根目錄
PROJECT_FILE="SerenityRowDisplayNameUpdater.csproj"

if [ ! -f "$PROJECT_FILE" ]; then
    echo "錯誤: 找不到專案檔案 $PROJECT_FILE"
    echo "請確保在專案根目錄下執行此腳本"
    exit 1
fi

# 檢查並確保 7za 可用
ensure_7za() {
    local SEVEN_ZIP_PATH="tools/7za"
    
    # 如果 7za 已存在且可執行，直接返回
    if [ -x "$SEVEN_ZIP_PATH" ]; then
        echo "找到 7za"
        return 0
    fi
    
    echo "未找到 7za，正在下載..."
    
    # 創建 tools 目錄
    mkdir -p "tools"
    
    # 檢測系統架構
    local OS_TYPE=""
    local ARCH=""
    
    case "$(uname -s)" in
        Linux*)
            OS_TYPE="linux"
            case "$(uname -m)" in
                x86_64) ARCH="x64" ;;
                i386|i686) ARCH="x86" ;;
                aarch64|arm64) ARCH="arm64" ;;
                *) echo "不支援的 Linux 架構: $(uname -m)"; return 1 ;;
            esac
            ;;
        Darwin*)
            OS_TYPE="mac"
            case "$(uname -m)" in
                x86_64) ARCH="x64" ;;
                arm64) ARCH="arm64" ;;
                *) echo "不支援的 macOS 架構: $(uname -m)"; return 1 ;;
            esac
            ;;
        *)
            echo "不支援的作業系統: $(uname -s)"
            return 1
            ;;
    esac
    
    # 下載對應平台的 7za
    local DOWNLOAD_URL=""
    local ARCHIVE_NAME=""
    
    if [ "$OS_TYPE" = "linux" ]; then
        DOWNLOAD_URL="https://www.7-zip.org/a/7z2201-linux-x64.tar.xz"
        ARCHIVE_NAME="7z2201-linux-x64.tar.xz"
    elif [ "$OS_TYPE" = "mac" ]; then
        DOWNLOAD_URL="https://www.7-zip.org/a/7z2201-mac.tar.xz"
        ARCHIVE_NAME="7z2201-mac.tar.xz"
    fi
    
    echo "正在下載 7za ($OS_TYPE-$ARCH)..."
    
    # 使用 curl 或 wget 下載
    if command -v curl >/dev/null 2>&1; then
        curl -L -o "tools/$ARCHIVE_NAME" "$DOWNLOAD_URL"
    elif command -v wget >/dev/null 2>&1; then
        wget -O "tools/$ARCHIVE_NAME" "$DOWNLOAD_URL"
    else
        echo "錯誤: 需要 curl 或 wget 才能下載 7za"
        return 1
    fi
    
    if [ $? -ne 0 ]; then
        echo "下載 7za 失敗"
        return 1
    fi
    
    echo "正在解壓 7za..."
    
    # 解壓檔案
    cd tools
    if command -v tar >/dev/null 2>&1; then
        tar -xf "$ARCHIVE_NAME"
    else
        echo "錯誤: 需要 tar 才能解壓 7za"
        cd ..
        return 1
    fi
    cd ..
    
    # 設定執行權限
    chmod +x "tools/7zz"
    ln -sf "7zz" "tools/7za"
    chmod +x "$SEVEN_ZIP_PATH"
    
    # 清理下載的壓縮檔
    rm -f "tools/$ARCHIVE_NAME"
    
    if [ -x "$SEVEN_ZIP_PATH" ]; then
        echo "✓ 7za 下載並安裝成功"
        return 0
    else
        echo "7za 安裝失敗"
        return 1
    fi
}

# 建置並打包函數
build_and_zip() {
    local platform=$1
    local platform_name=$2
    local zip_name=$3
    
    echo "建置 $platform_name 版本..."
    
    if dotnet publish "$PROJECT_FILE" -c Release -r "$platform" --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/$platform"; then
        echo "清理不需要的檔案..."
        
        # 刪除 .pdb 檔案
        find "dist/$platform" -name "*.pdb" -type f -delete 2>/dev/null || true
        # 刪除 .pdf 檔案（如果有的話）
        find "dist/$platform" -name "*.pdf" -type f -delete 2>/dev/null || true
        
        echo "使用 7za 打包 $platform_name 版本..."
        
        # 使用 7za 打包，最高壓縮等級
        tools/7za a -tzip "dist/serupd-$zip_name.zip" "dist/$platform/*" -mx9
        
        if [ $? -eq 0 ]; then
            # 清理建置目錄
            rm -rf "dist/$platform"
            echo "✓ $platform_name 建置並打包完成"
        else
            echo "✗ $platform_name 打包失敗"
            return 1
        fi
    else
        echo "✗ $platform_name 建置失敗"
        return 1
    fi
}

# 確保 7za 可用
ensure_7za
if [ $? -ne 0 ]; then
    echo "無法準備 7za，建置中止"
    exit 1
fi

# 建置各平台版本
build_and_zip "win-x64" "Windows x64" "win-x64"
build_and_zip "win-x86" "Windows x86" "win-x86"
build_and_zip "linux-x64" "Linux x64" "linux-x64"
build_and_zip "osx-x64" "macOS x64" "osx-x64"
build_and_zip "osx-arm64" "macOS ARM64" "osx-arm64"

echo
echo "建置完成！ZIP 檔案已生成："
echo "  Windows x64: dist/serupd-win-x64.zip"
echo "  Windows x86: dist/serupd-win-x86.zip"
echo "  Linux x64:   dist/serupd-linux-x64.zip"
echo "  macOS x64:   dist/serupd-osx-x64.zip"
echo "  macOS ARM64: dist/serupd-osx-arm64.zip"
echo

echo "ZIP 檔案大小："
for zipfile in dist/serupd-*.zip; do
    if [ -f "$zipfile" ]; then
        size=$(ls -lh "$zipfile" | awk '{print $5}')
        filename=$(basename "$zipfile")
        echo "  $filename: $size"
    fi
done

echo
echo "提示：在 Linux/macOS 上執行前，請先設定執行權限："
echo "  chmod +x serupd"