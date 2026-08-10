# 安全审查报告：MemoDock

> 本报告由 Codex Security 对 MemoDock 仓库执行标准扫描后生成，内容为原始英文报告的中文版；代码示例与哈希等元数据保持原样。

> **修复状态（2026-08-07）**：本报告唯一发现（[#1](#finding-1)）已修复：`ci.yml` 与 `release.yml` 中全部 5 处第三方 action 已固定到完整提交 SHA（`actions/checkout@11d5960a… # v4.4.0`、`actions/setup-dotnet@67a3573c… # v4.3.1`、`softprops/action-gh-release@3bb12739… # v2.6.2`）。下文保留扫描时的原始内容供追溯。

## 扫描范围

对 MemoDock 仓库（Windows 11 WPF 本地优先笔记应用，当前工作树）的仓库级标准 Codex Security 扫描。

- 扫描模式：repository（仓库）
- 目标类型：git_worktree（Git 工作树）
- 目标 ID：target_memodock_git_worktree
- 版本（revision）：3aad97c2fde9eb19088d0e31f6a97fda5bbce12d
- 快照摘要：codex-security-snapshot/v1:sha256:11faf192b7b29c88456d721563fc8273c94caf4e8529aa6a938dff4d6df42c96
- 清单策略：repository（仓库）
- 包含路径：.
- 排除路径：无
- 运行时/测试状态：未记录
- 扫描背景：用户要求"用 Codex Security 扫描这个项目"，未提供其他安全背景。

限制与排除：

- 独立基线子代理未能启动（三次 spawn 均收到空任务），基线审计与定向调查由父代理顺序完成；这不改变审查范围，报告中如实披露。
- 排除了 bin/、obj/、artifacts/、.dotnet-cli/、.dotnet-sdk/、tmp/、output/imagegen/*（除已跟踪的 PNG 外）：构建产物和本地 SDK/CLI 缓存不属于仓库跟踪内容。

### 扫描摘要

| 字段 | 值 |
| --- | --- |
| 可报告发现数 | 1 |
| 严重度分布 | 低：1 |
| 置信度分布 | 高：1 |
| 覆盖率 | 完整 |
| 验证模式 | 未记录 |

规范产物：`scan-manifest.json`、`findings.json`、`coverage.json`。本报告是这些文件的确定性投影。

## 威胁模型

MemoDock 是一款单用户、本地优先的 Windows 11 WPF 笔记应用。它通过 Win32 API 识别前台应用，将每个应用的笔记以明文 JSON 保存在 %LOCALAPPDATA%\MemoDock 下，支持导出用户选择的 JSON 备份，并通过 GitHub Actions 发布自包含二进制文件。安全相关的边界包括：可被同用户进程写入的本地 JSON 文件、Win32/P-Invoke 交互面，以及 GitHub Actions 供应链；应用不暴露网络服务，也不导入不受信任的文档。

### 资产

- 本地笔记数据库（%LOCALAPPDATA%\MemoDock\memos.json 及 .bak 备份）
- 窗口状态文件（%LOCALAPPDATA%\MemoDock\window.json）
- 发布产物与 GitHub Actions 工作流完整性
- 本地磁盘上用户笔记的机密性

### 信任边界

- 可在应用进程之外被修改的本地 JSON 文件
- Windows 前台进程元数据与 shell 图标 API
- GitHub Actions runner 与第三方 action 代码
- 用户选择的导出路径

### 攻击者能力

- 以同一用户身份运行的本地进程或脚本可以读取或修改明文 JSON 数据文件
- 攻破或重定向第三方 GitHub Action 的攻击者可以在 CI/发布 runner 上执行代码
- 应用本身没有暴露远程网络攻击面

### 安全目标

- 防止本地笔记意外丢失或损坏
- 保持笔记本地化，避免网络外泄
- 保护发布产物的完整性
- 避免执行来自不受信任输入文件的代码

### 假设

- 单用户本地优先应用，无账号、无网络服务、无文档导入
- 笔记按设计以明文 JSON 存储；SECURITY.md 已声明这不是加密存储
- 发布二进制未做代码签名（项目已知限制）
- 仓库本身及其维护者可信；威胁主要针对使用者和 CI 供应链

## 发现

| 发现 | 严重度 | 置信度 | 详细说明 |
| --- | --- | --- | --- |
| [GitHub Actions 工作流使用可变的第三方 action 版本标签](#finding-1) | 低 | 高 | 见下文 |

### 置信度说明

| 标签 | 含义 |
| --- | --- |
| 高 | 有直接证据支持该发现，且不存在实质性未解决的阻碍。 |
| 中 | 证据支持可能存在问题的合理推测，但仍缺少实质性运行时或可达性证明。 |
| 低 | 证据不完整，仅保留以便明确跟进。 |

<a id="finding-1"></a>

### [1] GitHub Actions 工作流使用可变的第三方 action 版本标签

| 字段 | 值 |
| --- | --- |
| 严重度 | 低 |
| 置信度 | 高 |
| 置信度依据 | 两个工作流文件中都有直接源码证据；没有任何 SHA 固定（pinning）缓解措施。 |
| 类别 | supply-chain（供应链） |
| CWE | CWE-829 |
| 影响行 | .github/workflows/release.yml:42-45、.github/workflows/ci.yml:15-18、.github/workflows/release.yml:20-23 |

#### 摘要

CI 和发布工作流都引用了浮动的大版本标签（actions/checkout@v4、actions/setup-dotnet@v4、softprops/action-gh-release@v2）。标签可以被重定向，或上游仓库被攻破，从而把攻击者控制的代码注入到发布二进制的 CI 运行中。

#### 根本原因

工作流步骤对会在 runner 中执行第三方代码的 action 使用了可变标签引用，而不是不可变的 SHA。

**CI checkout 使用可变标签** — `.github/workflows/ci.yml:15`

通过可变的大版本标签拉取第三方 action 代码。

```yaml
        uses: actions/checkout@v4
```

**CI SDK 安装使用可变标签** — `.github/workflows/ci.yml:18`

通过可变标签执行第三方 action 代码。

```yaml
        uses: actions/setup-dotnet@v4
```

**发布 checkout 使用可变标签** — `.github/workflows/release.yml:20`

在发布流水线中通过可变的大版本标签拉取第三方 action 代码。

```yaml
        uses: actions/checkout@v4
```

**发布 SDK 安装使用可变标签** — `.github/workflows/release.yml:23`

在发布流水线中通过可变标签执行第三方 action 代码。

```yaml
        uses: actions/setup-dotnet@v4
```

**发布上传使用可变第三方标签** — `.github/workflows/release.yml:42-44`

发布步骤通过可变标签执行第三方代码，是所有第三方 action 中影响最大的一个。

```yaml
        uses: softprops/action-gh-release@v2
        with:
          files: |
```

#### 验证

通过阅读 .github/workflows/ci.yml 和 release.yml 确认：所有 uses: 行都引用大版本标签，两个文件中均无 SHA 固定，且发布 job 持有发布产物所需的 contents:write 令牌。

#### 修复状态

已修复（2026-08-07）。两个工作流中的全部 5 处 `uses:` 已改为完整提交 SHA，并保留版本注释：

- `actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4.4.0`（ci.yml:15、release.yml:20）
- `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4.3.1`（ci.yml:18、release.yml:23）
- `softprops/action-gh-release@3bb12739c298aeb8a4eeaf626c5b8d85266b0e65 # v2.6.2`（release.yml:42）

后续升级固定版本时，建议继续通过 Dependabot/Renovate 更新并校验上游 SHA。

#### 数据流

攻击者攻破或重定向被引用的 action 仓库或标签 → 工作流的 checkout 步骤在 runner 上拉取并执行攻击者控制的 action 代码 → 该代码以仓库的 GITHUB_TOKEN 权限运行（发布工作流中为 contents:write），从而可以篡改发布产物或访问仓库密钥。

**发布上传使用可变第三方标签** — `.github/workflows/release.yml:42-44`

发布步骤通过可变标签执行第三方代码，是所有第三方 action 中影响最大的一个。

```yaml
        uses: softprops/action-gh-release@v2
        with:
          files: |
```

**发布 job 的令牌范围** — `.github/workflows/release.yml:8-9`

发布 job 授予 contents:write 权限，被攻破的 action 可能滥用该权限篡改发布产物。

```yaml
permissions:
  contents: write
```

#### 可达性

每次 push 或 pull request 都会触发（ci.yml），每次推送版本标签都会触发（release.yml）；除触发工作流外无需任何用户交互。

#### 严重度

**低** — 影响高（可在具备 contents:write 权限的 CI 中执行任意代码），但可能性低：利用需要上游被攻破或标签重定向，且该本地优先应用没有远程运行时攻击面。额外的运行时或部署证据可能提高或降低该严重度。

#### 修复建议

将所有第三方 action 固定到完整提交 SHA（例如 `uses: actions/checkout@<40位SHA> # v4.x.y`），并通过 Dependabot 或 Renovate 流程更新固定版本，同时校验上游 SHA。优先使用 GitHub 官方 action，保持工作流权限最小化，并增加发布产物签名与校验作为纵深防御。

## 已审查面

| 面 | 风险领域 | 结果 | 说明 |
| --- | --- | --- | --- |
| 本地 JSON 持久化与恢复（memos.json、.bak、window.json） | local-data | 未发现问题 | 审查了 MemoRepository、AtomicFile、MemoMigrator、AppPaths、WindowStateService。System.Text.Json 类型化反序列化不提供代码执行原语；损坏数据会被保留并尝试备份恢复。明文存储是文档化的设计决策。 |
| 数据模型、查询、迁移与导出 | data-integrity | 未发现问题 | 审查了 models、MemoQuery、MemoMigrator、EditorWindow 以及 MainWindow 的修改/回滚流程。没有注入或路径穿越面；导出路径由用户通过保存对话框选择。 |
| Win32 前台窗口识别、热键、单实例与窗口特效 | os-integration | 未发现问题 | 审查了 ForegroundAppService、HotKeyService、SingleInstanceService、WindowEffects。P/Invoke 使用受限；同会话命名 mutex/event 交互不跨越权限边界。 |
| 构建、发布脚本与 GitHub Actions 工作流 | supply-chain | 已报告 | 1 个低严重度发现：第三方 action 固定到可变标签。NuGet.Config 有意清空远程源；global.json 固定 SDK feature band。 |
| 仓库配置、安全策略与文档 | policy | 未发现问题 | 审查了 SECURITY.md、README、CONTRIBUTING、CLAUDE.md、.gitignore、解决方案/项目文件、issue/PR 模板。未发现硬编码凭据或密钥；漏洞披露策略已定义。 |

## 待确认问题与后续跟进

- 发布二进制未做代码签名；是否要增加签名发布，防止产物在传输中被篡改？
  - 跟进：评估在发布工作流中增加 Authenticode 签名，并记录信任模型。
- MemoDock 按设计以明文 JSON 存储笔记；是否需要对敏感笔记提供可选的静态加密（例如 DPAPI）？
  - 跟进：在保持明文默认的前提下，评估增加可选的数据库级加密。
- 第一方 GitHub actions 是否也要固定 SHA，还是只固定第三方 action？
  - 跟进：确定固定策略，并在 CONTRIBUTING.md 中记录。
