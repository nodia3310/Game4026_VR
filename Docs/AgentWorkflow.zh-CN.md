# AI agent 派单与集成规则

> 当前仅开发 A 的四项独立交互组件，不创建场景，不等待或安排 B 的开发。下方全游戏队列属于旧计划；当前交付与接线见 InteractionSetup.zh-CN.md。

本文件用于负责人 A 和主 agent 在该 Unity 项目持续开发。完整范围与阶段见 DevelopmentPlan.zh-CN.md。

## 每张任务卡

```text
任务：A2 单手警棍命中
目标：有效挥击可命中假敌人，同次攻击对同一目标只结算一次。
依赖：共享 Contracts、A1 抓取已通过。
允许修改：Assets/_Game/Scripts/Combat/ 及其专属测试。
禁止修改：Packages、ProjectSettings、共享接口、其他人的场景/prefab。
先读：本计划、现有契约、相关 Unity skills 与实际包 API。
验收：静止接触无伤害；有效挥击一次伤害；多 collider 不重复；传送不算挥击。
交付：文件列表、接线说明、测试命令和实际结果、真机待测项。
Editor：需要操作时提交主 agent 排队，不并发争用 Editor。
```

## 推荐任务队列

| 顺序 | 任务 | Owner | 依赖 | 状态 |
| --- | --- | --- | --- | --- |
| 1 | 项目和 Quest 配置审查 | 审查 agent | 无 | 已完成只读审查 |
| 2 | A/B Contracts | 契约 agent | 原计划 | 已编写，验证见执行记录 |
| 3 | 文物归位计数核心及测试 | 文物 agent | Contracts | 验证见执行记录 |
| 4 | Quest 构建与手电筒基线 | 主 agent + A | 设备、Android 模块、XR Validation | 待执行 |
| 5 | XR Rig 和抓放实验场景 | 交互 agent | 任务 4 | 待执行 |
| 6 | MockDamageable 与敌人接口联调 | B / B agent | Contracts | 待 B 进度确认 |
| 7 | 文物 Trigger / XRI Socket adapter | 文物 agent | 任务 3、5 | 待执行 |
| 8 | 警棍扫掠命中 | 交互 agent | 任务 5、6 | 待执行 |
| 9 | 一间展厅纵向切片 | 主 agent + B | 任务 7、8、最小敌人 | 待执行 |

## 开工与交付

1. 主 agent 在任务卡注明路径所有权和接口版本。不要让多个 agent 同时改同一 scene、prefab、asmdef 或设置文件。
2. 子 agent 先读文件再写，保留无关用户改动。输出的 .meta 与资源一起管理，不生成引用到不存在资产的 prefab。
3. 子 agent 完成后，主 agent 审查代码和测试，统一触发导入/编译；编译失败先修复，再继续派单。
4. 测试选择真实风险：命中去重、错误归位、阶段转换、重开状态；不为简单字段或文字变更堆测试。
5. 状态使用待执行、进行中、已编译、自动测试通过、真机通过；不能把这些状态混用。
6. 涉及体验的任务，A 真机验收后才记为完成；agent 报告必须保留未验证事项。

## 已核验的本机命令

以下命令基于当前 CLI 的实际 help 与 Editor 命令列表。命令中的项目路径需在其他机器替换；调用实际 MCP 时先列工具并读取 schema。

```powershell
unity status --format json
unity command --caller plugin --skill unity-cli --project-path 'E:\unity project\game4026' --format json
unity recompile --project-path 'E:\unity project\game4026' --timeout 120 --format json
unity command console status --caller plugin --skill unity-cli --project-path 'E:\unity project\game4026' --format json
unity command console get --level error --tail 20 --caller plugin --skill unity-cli --project-path 'E:\unity project\game4026' --format json
unity command test run --mode editor --filter Game4026 --async-tests --caller plugin --skill unity-cli --project-path 'E:\unity project\game4026' --format json
unity command test status --detail failures --caller plugin --skill unity-cli --project-path 'E:\unity project\game4026' --format json
```

构建前先查询 `build` 命令的当前 schema，选择实际 Build Profile；不要直接对仍打开的同一项目启动第二个 batch Editor。测试返回已启动不表示通过，要查询最终状态并核对测试数非零。

CLI 在受限环境可能把可用 Editor 报为 unreachable。本次用经授权的命令范围访问本机 Editor 后已连接；不能把沙箱内的单次失败认定为 Unity 未启动，也不修改沙箱策略来绕过限制。

## Git 协作准备

当前没有 Git 仓库。建立仓库时排除 Library、Temp、Logs、Obj、Builds、UserSettings 和机器本地配置，纳入 Assets 及 .meta、Packages、ProjectSettings、Docs。检查无密钥和大体积生成文件后做基线提交。

之后使用 feature/vr-interaction 与 feature/enemy-ai。两条线使用独立 worktree，分别打开 Editor；集成者合并到集成分支并处理 Unity 资产冲突。没有远端地址时先做本地协作，不虚构远端仓库或宣称已创建分支。
