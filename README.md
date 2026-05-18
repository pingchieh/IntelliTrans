# IntelliTrans

IntelliTrans 是一个用于批量翻译 .NET IntelliSense XML 文档的命令行工具。它会扫描 .NET SDK packs 或指定 NuGet 包中的 XML 文档，提取待翻译内容写入数据库，再调用 OpenAI 兼容接口生成译文，最后把译文回写为本地化的 IntelliSense XML 文件。

## 功能特性

- **扫描 (scan)**：遍历配置的 IntelliSense XML 文件，提取符合条件的文档节点并去重入库。
- **翻译 (translate)**：调用 OpenAI Chat Completions 兼容接口翻译未处理的原文。
- **优化 (optimize)**：基于原文和初始译文再次调用模型，润色已有译文并标记为已优化。
- **补丁 (patch)**：将数据库中的译文写入本地化目录，生成可供 IDE 使用的 XML 文档。
- **迁移 (migrate)**：根据当前数据库类型执行 Entity Framework Core 数据库迁移。
- **多数据库支持**：支持 SQLite（默认）和 PostgreSQL。
- **可观测性**：支持 OpenTelemetry 日志、指标和链路追踪；设置 `OTEL_EXPORTER_OTLP_ENDPOINT` 后启用 OTLP 导出。

## 系统要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 或与项目 `TargetFramework` 兼容的 SDK。
- OpenAI API Key，或兼容 OpenAI Chat Completions 的服务密钥与端点。
- SQLite（默认）或 PostgreSQL（可选）。

## 安装与构建

```bash
# 克隆项目
git clone <repository-url>
cd IntelliTrans

# 恢复依赖项
dotnet restore

# 构建项目
dotnet build
```

## 配置

命令行程序会读取 `src/IntelliTrans.Cli/appsettings.json`。运行 `dotnet run --project src/IntelliTrans.Cli -- ...` 时，该文件会被复制到输出目录并作为默认配置使用。

```jsonc
{
  // 数据库类型："Sqlite" 或 "Postgres"
  "DBType": "Sqlite",
  "ConnectionStrings": {
    "Sqlite": "Data Source=IntelliTrans.db",
    "Postgres": "Host=127.0.0.1;Port=5432;Database=IntelliTrans;User Id=postgres;Password=postgres;"
  },
  "IntelliSense": {
    // .NET SDK packs 目录
    "PacksDir": "%ProgramFiles%\\dotnet\\packs",

    // 扫描/补丁时排除的 XML 文件名
    "ExcludeFiles": [
      "Microsoft.VisualBasic.Core.xml",
      "Microsoft.VisualBasic.Forms.xml",
      "System.Runtime.Intrinsics.xml",
      "netstandard.xml"
    ],

    // NuGet 包缓存目录
    "NugetDir": "%USERPROFILE%\\.nuget\\packages",

    // 额外扫描的 NuGet 包名；会与 NugetDir 拼接为扫描目录
    "IncludePackages": [
      "consoleappframework"
    ],

    // 预留的包排除列表
    "ExcludePackages": []
  },
  "Openai": {
    "ApiKey": "",
    "Model": "gpt-4o-mini",
    "Endpoint": "https://api.openai.com/v1"
  }
}
```

> 建议不要把真实 API Key 提交到仓库。可使用本地未提交的配置文件、用户机密或环境变量覆盖配置。

## 基本工作流

首次使用通常按以下顺序执行：

```bash
# 1. 创建/升级数据库结构
dotnet run --project src/IntelliTrans.Cli -- migrate

# 2. 扫描 IntelliSense XML，提取待翻译文本
dotnet run --project src/IntelliTrans.Cli -- scan

# 3. 调用模型生成译文
dotnet run --project src/IntelliTrans.Cli -- translate --api-key "<your-api-key>"

# 4. 可选：润色已有译文
dotnet run --project src/IntelliTrans.Cli -- optimize --api-key "<your-api-key>"

# 5. 写入本地化 XML 文件
dotnet run --project src/IntelliTrans.Cli -- patch
```

## 命令说明

### `migrate`

执行当前数据库类型对应的 EF Core 迁移。

```bash
dotnet run --project src/IntelliTrans.Cli -- migrate
```

数据库类型由 `DBType` 决定：

- `Sqlite`：使用 `ConnectionStrings:Sqlite` 和 `IntelliTrans.Migrations.Sqlite`。
- `Postgres`：使用 `ConnectionStrings:Postgres` 和 `IntelliTrans.Migrations.Postgres`。

### `scan`

扫描配置中的目录，提取尚未翻译且匹配过滤规则的 XML 文档内容。

```bash
dotnet run --project src/IntelliTrans.Cli -- scan [options]
```

参数：

- `--skip-no-dll <true|false>`：是否跳过没有同名 `.dll` 文件的 XML 文件，默认 `true`。
- `--content-filter <regex>`：内容过滤正则，默认 `[\u4e00-\u9fa5]`，即跳过已经包含中文字符的内容。

说明：

- 已扫描文件会记录到 `scaned.list`，再次运行时会跳过这些文件。
- 扫描目录来自 `IntelliSense:PacksDir` 和 `IntelliSense:NugetDir` + `IntelliSense:IncludePackages`。
- 排除文件来自 `IntelliSense:ExcludeFiles`。

### `translate`

翻译数据库中尚无指定语言译文的原文。

```bash
dotnet run --project src/IntelliTrans.Cli -- translate [options]
```

参数：

- `--api-url <url>`：OpenAI 兼容接口地址；默认读取 `Openai:Endpoint`。
- `--api-key <key>`：API Key；默认读取 `Openai:ApiKey`。
- `--model <model>`：模型名称；默认读取 `Openai:Model`。
- `--temperature <number>`：采样温度，默认 `0`。
- `--language <language>`：目标语言，默认 `简体中文`。
- `--parallelism <number>`：并发翻译数量，默认 `8`。

### `optimize`

优化指定语言下尚未标记为 `IsOptimized` 的译文。

```bash
dotnet run --project src/IntelliTrans.Cli -- optimize [options]
```

参数与 `translate` 相同：

- `--api-url <url>`
- `--api-key <key>`
- `--model <model>`
- `--temperature <number>`，默认 `0`
- `--language <language>`，默认 `简体中文`
- `--parallelism <number>`，默认 `8`

### `patch`

把数据库中的译文写入 XML 文件。默认会在原 XML 所在目录下创建 `zh-Hans` 子目录，并保存同名 XML 文件。

```bash
dotnet run --project src/IntelliTrans.Cli -- patch [options]
```

参数：

- `--skip-no-dll <true|false>`：是否跳过没有同名 `.dll` 文件的 XML 文件，默认 `true`。
- `--save-path <path>`：译文 XML 保存目录名或路径片段，默认 `zh-Hans`。
- `--content-filter <regex>`：内容过滤正则，默认 `[\u4e00-\u9fa5]`。

注意：

- 如果目标文件已存在且不是原文件本身，程序会跳过，避免覆盖已有本地化文件。
- 如果 XML 位于 `Program Files` 下且当前进程没有管理员权限，程序会跳过该文件。

## 数据库与迁移

项目包含两套迁移工程：

- `src/IntelliTrans.Migrations.Sqlite`
- `src/IntelliTrans.Migrations.Postgres`

切换数据库时需要同时修改：

1. `DBType`
2. 对应的 `ConnectionStrings:<DBType>`
3. 运行 `migrate` 命令应用迁移

如果需要新增迁移，可参考仓库中的 `add-migration.ps1`。

## 项目结构

```text
IntelliTrans/
├── src/
│   ├── IntelliTrans.Cli/                  # 命令行入口、配置、命令与提示词
│   ├── IntelliTrans.Core/                 # IntelliSense XML 解析与字符串工具
│   ├── IntelliTrans.Database/             # EF Core DbContext 与实体模型
│   ├── IntelliTrans.Migrations.Postgres/  # PostgreSQL 迁移
│   └── IntelliTrans.Migrations.Sqlite/    # SQLite 迁移
├── Directory.Packages.props               # Central Package Management 版本定义
├── IntelliTrans.slnx                      # 解决方案文件
└── README.md
```

## 常见问题

### 为什么扫描不到内容？

请检查：

- `IntelliSense:PacksDir` 或 `IncludePackages` 拼接后的目录是否存在。
- XML 旁边是否存在同名 DLL；如果没有，可使用 `--skip-no-dll false`。
- 内容是否被 `--content-filter` 过滤掉。
- 文件是否已记录在 `scaned.list` 中。

### 如何使用兼容 OpenAI 的第三方端点？

把 `Openai:Endpoint` 改为第三方服务的 OpenAI 兼容基础地址，或在命令中传入：

```bash
dotnet run --project src/IntelliTrans.Cli -- translate \
  --api-url "https://example.com/v1" \
  --api-key "<your-api-key>" \
  --model "<model-name>"
```

### 译文 XML 格式错误怎么办？

`translate` 和 `optimize` 会校验模型返回内容是否为合法 XML 片段。若模型输出格式错误，可降低 `--temperature`、更换模型，或稍后重试未成功的条目。
