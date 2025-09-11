# Serenity Row DisplayName Updater

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md)

一個自動化工具，用於更新 Serenity Framework 中 Row 類別的 DisplayName 屬性，從資料庫欄位註解中自動同步顯示名稱。

## 功能特點

- **自動掃描**: 遞迴掃描指定目錄下的所有 `*Row.cs` 檔案
- **多資料庫支援**: 支援 SQL Server、PostgreSQL 資料庫
- **智慧更新**: 從資料庫欄位註解自動更新或新增 `DisplayName` 屬性
- **註解文字提取**: 支援正規表達式自訂提取註解中的特定部分，避免多行註解造成排版問題
- **高效處理**: 使用 Roslyn 編譯器 API 進行精確的程式碼解析和修改
- **安全操作**: 僅更新必要的內容，保持原始程式碼格式
- **單一執行檔**: 可建置為獨立執行檔，無需安裝 .NET Runtime

## 快速開始

### 系統需求

- .NET 9.0 或更高版本（開發時）
- 支援的資料庫：SQL Server、PostgreSQL、SQLite

### 安裝方式

#### 方式一：使用預建的單一執行檔（推薦）

1. 下載對應平台的 ZIP 檔案
2. 解壓縮後直接執行，無需安裝 .NET Runtime

#### 方式二：從原始碼建置

1. 複製專案到本機：
```bash
git clone https://github.com/leoshiang/serenity-row-display-name-updater.git
cd serenity-row-display-name-updater
```

2. 使用建置腳本（推薦）：

**Windows:**

```bat
# 建置所有平台的單一執行檔並打包成 ZIP
build.bat
```

**Linux/macOS:**

```bash
# 設定執行權限（首次執行）
chmod +x build.sh

# 建置所有平台的單一執行檔並打包成 ZIP
./build.sh
```

3. 手動建置：
```bash
# 一般建置
dotnet build

# 建置單一執行檔（Windows x64）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o dist
```

## 建置腳本說明

專案提供了自動化建置腳本，會自動建置所有平台版本並打包成 ZIP 檔案：

### 腳本清單

| 腳本檔案 | 平台 | 說明 |
|---------|------|------|
| `build.bat` | Windows | Windows 批次檔，建置所有平台版本並打包 |
| `build.sh` | Linux/macOS | Shell 腳本，建置所有平台版本並打包 |

### 自動化功能

- **自動下載 7za**: 如果系統沒有 7za 壓縮工具，會自動下載
- **清理檔案**: 自動移除 `.pdb` 和 `.pdf` 檔案以減少檔案大小
- **最高壓縮**: 使用 `-mx9` 參數達到最佳壓縮效果
- **跨平台支援**: 根據系統自動選擇對應的 7za 版本

### 建置輸出

執行建置腳本後，會在 `dist/` 目錄下生成以下 ZIP 檔案：
```
dist/
├── serupd-win-x64.zip    # Windows 64位元
├── serupd-win-x86.zip    # Windows 32位元
├── serupd-linux-x64.zip  # Linux 64位元
├── serupd-osx-x64.zip    # macOS Intel
└── serupd-osx-arm64.zip  # macOS Apple Silicon
```
每個 ZIP 檔案內包含：
- 對應平台的 `serupd` 或 `serupd.exe` 執行檔
- 所有必要的執行時期檔案（自包含部署）

### 使用範例

**Windows:**

```bat
# 執行建置腳本
build.bat

# 解壓並使用建置的執行檔
# 解壓 dist/serupd-win-x64.zip 後
serupd.exe C:\MyProject\Modules
```

**Linux/macOS:**
```bash
# 首次使用需設定權限
chmod +x build.sh

# 執行建置腳本
./build.sh

# 解壓並設定執行權限後使用
# 解壓 dist/serupd-linux-x64.zip 後
chmod +x serupd
./serupd /path/to/project/modules
```

## 使用方法

### 基本用法
```
serupd <目錄路徑> [appsettings.json路徑] [--comment-regex <正規表達式>]
```

### 範例

**基本用法:**
```bash
# Windows
serupd.exe C:\MyProject\Modules
serupd.exe C:\MyProject\Modules C:\MyProject\appsettings.json

# Linux/macOS
./serupd /path/to/project/modules
./serupd /path/to/project/modules /path/to/appsettings.json
```

**使用正規表達式提取註解:**
```bash
# 提取註解的第一行（預設行為，無需指定）
serupd.exe C:\MyProject\Modules --comment-regex "^([^\n\r]+)"

# 提取括號內的文字
serupd.exe C:\MyProject\Modules --comment-regex "\(([^)]+)\)"

# 提取冒號前的文字
serupd.exe C:\MyProject\Modules --comment-regex "^([^:]+)"

# 提取前20個字符
serupd.exe C:\MyProject\Modules --comment-regex "^(.{1,20})"

# 提取特定格式的標籤
serupd.exe C:\MyProject\Modules --comment-regex "標籤:(.+?)(?:\n|$)"
```

## 參數說明

| 參數 | 說明 | 必填 |
| --- | --- | --- |
| `<目錄路徑>` | 要掃描的 Serenity 專案目錄路徑 | 是 |
| `[appsettings.json路徑]` | 資料庫連接設定檔路徑，預設為目前目錄下的 appsettings.json | 否 |
| `--comment-regex <正規表達式>` | 用於從資料庫註解中提取特定文字的正規表達式 | 否 |

### 註解文字提取功能

預設情況下，工具會自動取資料庫註解的第一行作為 DisplayName。如果您的註解格式較為複雜或包含多行，可以使用 `--comment-regex` 參數指定正規表達式來提取所需的部分。

**正規表達式使用注意事項：**
- 如果正規表達式包含捕獲群組 `()`，會使用第一個群組的內容
- 如果沒有捕獲群組，會使用整個匹配結果
- 如果正規表達式沒有匹配成功，會回退到使用註解的第一行
- 如果正規表達式語法錯誤，會顯示警告並回退到使用註解的第一行

**常用正規表達式範例：**

| 用途 | 正規表達式 | 說明 |
|------|------------|------|
| 第一行 | `^([^\n\r]+)` | 提取第一行文字（預設行為） |
| 括號內容 | `\(([^)]+)\)` | 提取括號內的文字 |
| 冒號前內容 | `^([^:]+)` | 提取冒號前的文字 |
| 限制字數 | `^(.{1,20})` | 提取前20個字符 |
| 特定標籤 | `名稱:(.+?)(?:\n\|$)` | 提取「名稱:」後的內容 |

## 設定檔格式

`appsettings.json` 檔案應包含以下結構的資料庫連接設定：

```json
{
    "Data": {
        "Default": {
            "ConnectionString": "Server=localhost;Database=MyDB;Trusted_Connection=true;",
            "ProviderName": "Microsoft.Data.SqlClient"
        },
        "Postgres": {
            "ConnectionString": "Host=localhost;Database=mydb;Username=user;Password=pass;",
            "ProviderName": "Npgsql"
        }
    }
}
```

### 支援的 ProviderName

- `Microsoft.Data.SqlClient` - SQL Server
- `Npgsql` - PostgreSQL

## 工作原理

1. **掃描檔案**: 遞迴搜尋目錄中的所有 `*Row.cs` 檔案
2. **解析程式碼**: 使用 Roslyn 解析 C# 程式碼，提取類別資訊
3. **讀取屬性**: 從 Row 類別中讀取 `ConnectionKey` 和 `TableName` 屬性
4. **查詢資料庫**: 根據連接資訊查詢對應資料表的欄位註解
5. **提取註解文字**: 使用指定的正規表達式（或預設取第一行）提取合適的顯示文字
6. **更新程式碼**: 自動更新或新增 `DisplayName` 屬性

## 使用範例

### 基本範例

假設您有以下的 Row 類別：
```c#
[ConnectionKey("Default")]
[TableName("Users")]
public class UserRow : Row {
    [Column("user_id")]
    public int? UserId { get; set; }
    
    [Column("user_name")]
    public string UserName { get; set; }

    [Column("email")]
    public string Email { get; set; }
}
```

如果資料庫中的欄位註解為：
- `user_id`: "使用者編號"
- `user_name`: "使用者名稱"
- `email`: "電子郵件地址"

執行工具後，程式碼會自動更新為：
```c#
[ConnectionKey("Default")]
[TableName("Users")]
public class UserRow : Row {
    [DisplayName("使用者編號")]
    [Column("user_id")]
    public int? UserId { get; set; }
    
    [DisplayName("使用者名稱")]
    [Column("user_name")]
    public string UserName { get; set; }

    [DisplayName("電子郵件地址")]
    [Column("email")]
    public string Email { get; set; }
}
```

### 複雜註解範例

如果資料庫註解較為複雜：
- `user_id`: "使用者編號\n系統自動產生的唯一識別碼\n不可為空"
- `user_name`: "使用者名稱\n長度限制：3-50字元\n不可重複"
- `email`: "電子郵件地址 (用於登入)\n必須符合email格式"

使用不同的正規表達式會得到不同結果：

```bash
# 預設（取第一行）
serupd.exe C:\MyProject\Modules
# 結果: "使用者編號", "使用者名稱", "電子郵件地址 (用於登入)"

# 取括號內容
serupd.exe C:\MyProject\Modules --comment-regex "\(([^)]+)\)"
# 結果: "用於登入" (只有email欄位有括號內容)

# 取前10個字元
serupd.exe C:\MyProject\Modules --comment-regex "^(.{1,10})"
# 結果: "使用者編號", "使用者名稱", "電子郵件地址"
```

## 開發環境設定

### 必要套件

- `Microsoft.CodeAnalysis.CSharp` - Roslyn 編譯器 API
- `Microsoft.Data.SqlClient` - SQL Server 連接
- `Npgsql` - PostgreSQL 連接

### 除錯

在 Visual Studio 或 VS Code 中設定命令列參數：
```
C:\MyProject\Modules C:\MyProject\appsettings.json --comment-regex "^([^\n\r]+)"
```

## 常見問題

### Q: 建置腳本執行失敗？

A: 請確認：
1. 已安裝 .NET 9.0 SDK
2. Linux/macOS 使用者需先執行 `chmod +x build.sh`
3. 網路連線正常（需要下載 NuGet 套件和 7za 工具）

### Q: 執行檔無法執行？

A: 請確認：
1. Linux/macOS 使用者需執行 `chmod +x serupd`
2. 選擇正確的平台版本
3. 檢查系統是否缺少相依性（通常單一執行檔會包含所有相依性）

### Q: 正規表達式不起作用？

A: 請檢查：
1. 正規表達式語法是否正確
2. 是否需要使用捕獲群組 `()` 來擷取特定部分
3. 注意轉義字符，特別是在命令列中使用時
4. 查看程式執行時的警告訊息

### Q: 7za 下載失敗？

A: 請確認：
1. 網路連線正常
2. 防火牆或防毒軟體沒有阻擋下載
3. 手動下載 7za 並放置在 `tools/` 目錄下

### Q: ZIP 檔案損壞或無法開啟？

A: 可能原因：
1. 建置過程中斷導致檔案不完整
2. 重新執行建置腳本
3. 檢查 `dist/` 目錄的寫入權限

### Q: DisplayName 沒有更新？

A: 請檢查：
1. 資料庫欄位是否真的有註解
2. Row 類別的 ConnectionKey 和 TableName 是否正確
3. appsettings.json 中的連接字串是否正確
4. 如果使用了正規表達式，是否有匹配到內容

## 授權條款

本專案採用 MIT 授權條款 - 詳見 [LICENSE](LICENSE) 檔案