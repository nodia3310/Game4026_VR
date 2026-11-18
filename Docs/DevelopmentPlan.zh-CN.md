# Quest 3 博物馆 VR 游戏开发计划

> 当前范围已由负责人 A 收窄：仅实现物理抓取、挥击计算、射线/拉弓、文物 Trigger 归位。不创建场景，不跟踪 B 或其他人的进度。以下四周全游戏排期保留作历史参考，不是当前执行任务；实际组件使用见 InteractionSetup.zh-CN.md。

负责人：A。制定日期：2026-11-11。依据：[团队游戏计划](https://docs.google.com/document/d/1r559BfKifzrhUYyDhf0Jqr1TCL3FVpmU5TZMf0Kq84k/edit?tab=t.0)。

目标是完成约 15–20 分钟的第一人称 VR 体验：巡逻探索、与活化文物战斗、把文物放回展柜、引出并击败法老 Boss。A 对 VR 操控与互动负责，B 对敌人 AI 与战斗负责。

当前排期假设两条开发线可持续投入、4 周开发、Quest 3 独立运行 Android APK、Touch 控制器优先。期限、每日投入和 B 的现有进度尚待确认；以下是估算，不是交付承诺。裸手和双手弓不作为首个可玩版本的前置条件。

## 先交付什么

先完成 3–5 分钟的纵向切片：一个灰模展厅、一把警棍、一只木乃伊、一个文物、一个正确展柜、一次阶段推进。它必须在 Quest 3 上可运行，才能扩充内容。

完整版本建议保留三件文物、两类普通敌人、一场简化 Boss 战和失败重试。Khopesh 可复用近战逻辑；长矛、双手弓、权杖复杂技能、第三种普通敌人列为后续扩展。最终数量由团队确认，原计划中的素材清单不等同于全部必做。

## 项目现状与优先风险

| 项目 | 已查明的现状 | 下一步 |
| --- | --- | --- |
| 引擎 | Unity 6000.6.7f1 | 固定版本，不在首个切片期间升级 |
| 包 | URP 17.6.0、XRI 3.6.1、OpenXR 1.18.0、Meta OpenXR 2.6.1、Input System 1.20.1 | 保留 manifest 和 packages-lock，按本地 API 开发 |
| Android | 已配置 ARM64、IL2CPP、新 Input System；Min/Target SDK 为 34；Editor API 报告 Android 构建支持不可用，活动平台为 StandaloneWindows64 | 先核查并补齐当前 Editor 的 Android Build Support、SDK/NDK/OpenJDK，再切换平台和运行 Project Validation；SDK 现状不代表已满足商店发布规则 |
| XR | 新版 Meta Quest Support、Touch / Touch Plus 已启用；也有 Android XR 设置 | 在 Editor 检查实际目标 Feature 与验证结果，不依据旧 OculusQuestFeature 的禁用状态误判缺失支持 |
| 场景 | Build Settings 仅启用 SampleScene，另有 BasicScene | 新建独立交互验收场景；集成者维护最终关卡 |
| 手电筒 | Android Low 对应 Performance URP Config，额外灯光关闭 | 最优先验证 Spot Light；决定开启受限额外光还是使用低成本方案 |
| 示例 | XRI Samples 3.5.1、Hands Samples 1.8.1，低于已安装包 | 测试已有输入和 prefab 引用，迁移前保留 GUID；不直接覆盖样例 |
| 物理 | Fixed Timestep 为 0.02 秒 | 挥击使用扫掠与攻击窗口去重，测高速漏判和多 collider 重复命中 |
| 版本控制 | 当前目录不是 Git 仓库 | 首次集成前建立本地基线，再建 A/B 分支或独立 worktree；尚未初始化/提交 |

依据文件：ProjectSettings/ProjectVersion.txt、Packages/manifest.json、ProjectSettings/ProjectSettings.asset、ProjectSettings/QualitySettings.asset、Assets/Settings/Project Configuration/Performance URP Config.asset、Assets/XR/Settings/OpenXRPackageSettings.asset。

## 四周里程碑

按工作日 D1–D20 排列，等期限确认后再映射到日历；美术和音效可并行，但不能阻断灰模验收。

| 阶段 | A 和 A 侧 agents | B 和 B 侧 agents | 放行条件 |
| --- | --- | --- | --- |
| 第 1 周 D1–D5 | Quest APK 基线；XR Rig、抓放、手电筒；单手警棍；文物归位 | 假敌人接收接口；木乃伊最小追击、受击、死亡；玩家伤害 | 真机完成一次抓取→战斗→归位；没有卡流程缺陷 |
| 第 2 周 D6–D10 | 挥击轨迹、速度阈值、多 collider 去重、弱点和震动；道具回收；移动舒适设置 | 巡逻/追击/攻击/受击/死亡完整状态；攻击前摇；关卡阶段与一次性触发 | 3–5 分钟切片可重复游玩；正确/错误/重复归位及重试通过 |
| 第 3 周 D11–D15 | 射线或简单投射物；第二近战武器复用；交互反馈与提示 | 第二敌人变体；简化 Boss；文物完成触发 Boss；胜败流程 | 15–20 分钟全流程成立；达到选定真机帧率基线 |
| 第 4 周 D16–D20 | 真机回归、抓取/瞄准/舒适度调整、暂停和追踪恢复 | 难度与节奏调整、Boss 回归、性能修复 | 冻结功能；连续热机完整通关；留出最后 2 天修复缓冲 |

若只有两周：交付一个展厅、一把武器、一类敌人、一个文物与简化 Boss，削减内容数量；不压缩真机验证。双手弓只在切片、帧率和全流程稳定后开始。

## A 的任务清单

| 编号 | 任务与交付 | 依赖 | 验收 |
| --- | --- | --- | --- |
| A0 | 构建基线、Android XR 验证、设备连接、版本记录 | 无 | APK 冷启动，双眼画面与左右控制器正常，暂停恢复正常 |
| A1 | XR Rig、近抓/远抓、释放、手电筒开关、道具回收 | A0 | 每类道具左右手各抓放 20 次，无无法释放或永久丢失 |
| A2 | 警棍抓点、刃/棒端轨迹采样、有效挥击窗口、伤害桥接 | A1、共享接口 | 静止贴住敌人不连续扣血；高速挥击不明显漏判；同次攻击同目标只扣一次 |
| A3 | 文物身份、正确插槽、松手后锁定、归位事件、重复保护 | A1、共享接口 | 错类型/错身份拒绝；同类型不同文物分别计数；重复 Trigger 不重复推进 |
| A4 | 玩家血量接收适配、震动/声音反馈、死亡后重试 | B 的攻击接口 | 攻击窗口只结算一次；重开清除旧订阅和旧进度 |
| A5 | 射线/简单弹道瞄准；后续才做双手弓 | A2、切片已通过 | 正确命中弱点/普通区域；不绕过伤害统一接口 |
| A6 | 传送、分段转向、可选连续移动、暂停/摘头显/追踪丢失恢复 | A1 | 真人在 Quest 3 上验证舒适度；恢复后不产生异常巨额挥击速度 |

近战速度要去掉玩家 Rig 平移/传送造成的假速度，追踪恢复时重置采样。首版用可配置速度到伤害映射，不把控制器位移直接宣称为真实物理力。伤害、阈值和冷却由配置驱动。

归位 adapter 只在正确物品已释放且满足位置/朝向容差后提交归位；先完成成功判定，再锁定物理、播放反馈和推进。每件物品使用稳定 ArtifactId，枚举 Type 表示类别，不能用类别代替实例身份。

## A 与 B 的接口约定

共享程序集为 Game4026.Contracts，双方不得引用对方具体 Enemy/Player 类。

```csharp
IDamageable.TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint);
IPlayerDamageReceiver.ApplyPlayerDamage(float damage);
ArtifactRestoredEvent(string artifactId, ArtifactType type);
```

- A 传递基础伤害与是否命中弱点，B 接收者应用一次弱点倍率和护甲；禁止双方重复乘算。
- A 按一次攻击和目标根对象去重，不能把每个碰撞体都当独立敌人；B 的一次攻击也需对玩家去重。
- A 发出归位事件，B/关卡系统按 ArtifactId 消费并决定消怪/阶段/Boss。Boss 生成必须有一次性保护。
- ArtifactType 当前固定为 Unknown、RosettaStone、CanopicJar、TutankhamunMask；数值后续只追加，不能重排已序列化枚举。
- 双方各维护 mock：A 的假伤害接收者、B 的模拟武器/归位触发器，避免等对方才测试。
- 对场景使用可显式注入或序列化的引用；避免全局静态事件残留导致重开多次订阅。

## AI agent 指挥方式

A 决定范围、体验与最终验收；主 agent 管理依赖、统一接口、代码审查、Editor 操作与集成。每次只派发可验收的小任务。当前最多同时有主 agent 加三个子 agent，按需轮换。

| 角色 | 拥有路径 | 交付重点 | 使用的 skills |
| --- | --- | --- | --- |
| 主 agent / 集成 | Docs、共享契约审批、ProjectSettings、Packages、集成场景 | 分派、编译、集成、构建、回归 | unity-cli、unity-mcp-orchestrator |
| A 交互 agent | Scripts/Interaction、Scripts/Combat、Prefabs/Interaction | XR 抓取、挥击和瞄准 | unity-cli、physics-3d-collision |
| 文物 agent | Scripts/Artifacts、对应测试和独立 prefab | 身份、插槽、去重、归位反馈 | unity-cli、unity-mcp-orchestrator |
| B 战斗 agent | Scripts/Enemies、Prefabs/Enemies、独立测试场景 | 状态机、弱点、玩家伤害、Boss | initialize-ai-navigation、physics-3d-collision |
| QA agent | Tests、Docs/QA | 契约、状态转换、归位、构建和真机检查表 | unity-cli |

上表 Scripts/Prefabs/Tests 均位于 Assets/_Game 下。B 是否使用我们派发的 agent 尚待团队确认；已创建的 A/B 共享接口不表示 B 的完整实现已完成。

共享同一目录时不让多个 agent 切换 Git 分支。跨分支开发使用独立 worktree 和独立 Editor 实例；当前目录内按路径分工。场景、prefab、包和项目设置由主 agent 单写；子 agent 的 Editor 操作先排队。任何 agent 都不得手写或覆盖 Unity scene/prefab YAML。

派单格式见 AgentWorkflow.zh-CN.md。每次完成必须报告修改文件、接口、验证结果和尚未验证的真机项目。测试通过后再交下一任务，不把“已生成脚本”当成功能完成。

## CLI 与 MCP 开发循环

1. 读任务和适用 Unity skill，查看 Git/文件状态、Editor 状态和实际 API。
2. 使用 CLI 或当前实际挂载的 MCP 工具读取场景/组件；不能照搬另一种 MCP server 的工具名。
3. 按路径所有权改脚本；Editor 负责资产导入、场景和 prefab 操作。
4. 等编译完成，检查 Console，再跑相关 EditMode/PlayMode 测试。
5. 有画面改动时获取截图；Play Mode 验收需证明确实在推进，不能只截一帧。
6. 构建并在 Quest 3 上检查 APK；记录版本、结果和设备数据。Editor 或 Quest Link 不能代替独立 APK 验收。

本机 CLI 已实际连接 Editor；项目有 com.unity.pipeline 1.0.0-pre.1。当前会话未挂载 Unity 专用 MCP 工具，因此已执行的 Editor 工作使用 CLI。可用的 MCP 启动入口为 `unity mcp --project-path "E:\unity project\game4026"`，供客户端 stdio 配置使用。

已检查 `unity mcp configure codex --local --dry-run`，该命令除 MCP 注册外还会放宽沙箱网络设置，因此未自动写入配置。优先使用现有授权的本机 CLI 通路；后续接入 MCP 时仅注册必要 server，并核对实际工具 schema，不改变沙箱策略。第三方 MCP for Unity 的 resource/tool 名称与 Unity Pipeline MCP 不同。

CLI 当前版本 beta.15，更新检查报告 beta.17；保留现有可用版本，本次未升级。

## 验收与性能预算

团队先选 72 Hz，对应 13.89 ms 帧预算；90 Hz 对应 11.11 ms，属于后续提升目标。Meta 的性能指南要求交互应用至少 72 FPS；本项目仍必须用真机数据证明达到目标。[Meta 性能指南](https://developers.meta.com/vr/documentation/unity/unity-perf/?device=QUEST)

每个里程碑至少验证：

- Quest 3 独立 APK 冷启动、左右手输入、移动、暂停/恢复和追踪恢复。
- 手电筒真实照明效果；战斗最密集位置的 CPU/GPU 时间、掉帧、内存和 GC 峰值。
- 普通命中/弱点/高速挥击/贴住目标/多碰撞体，不重复扣血。
- 错误文物、同类型不同文物、重复 Trigger、丢落场景外、死亡重开，不重复计数或卡死。
- 最终连续热机至少 20 分钟，测试整段流程；记录 APK 版本、头显系统版本、刷新率及测试场景。
- A 戴头显判断抓取手感、瞄准、文字可读性与舒适度。真人体验不能由自动测试替代。

烘焙静态环境光、简化碰撞体、按需 LOD 和遮挡剔除；纹理 2K 是预算建议，不是性能保证。先测量最贵的光照、透明物、物理和脚本，再优化。高精扫描文物需移动端减面与贴图处理，美术导入时记录授权来源。

沿用 OpenXR + Meta OpenXR 路线，依据 [Unity Meta Quest 开发说明](https://docs.unity.com/en-us/engine/6000.0/manual/xr/configuring-project-for/meta-quest-develop)。Samples 迁移遵循 [XRI 安装说明](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit%403.6/manual/installation.html)，迁移后验证引用。

## 当前执行记录

- 已完成：源计划阅读、项目只读审查、CLI 连通性核验、两个子 agent 派单、共享接口初版。
- 归位核心和自动测试由文物 agent 实现，最终结果见 ExecutionStatus.zh-CN.md。
- 下一张功能任务是 A0：Quest APK 基线及手电筒验证；之后 A1 抓取测试场景，与 B 的假敌人接口并行。
- 尚未交付：可玩关卡、APK、Quest 3 真机性能结果、完整敌人/Boss 和双手弓。
