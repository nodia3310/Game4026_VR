# A 的四项 VR 交互组件

现已另行建立可直接运行的 `Assets/_Game/Scenes/InteractionTest.unity`，操作方式与本次场景验证见 [测试场景说明](InteractionTest.zh-TW.md)。以下正文保留原组件接口说明；末尾 56+2 的结果是组件阶段历史记录，新场景的最终结果为 64/64，见 `Docs/QA/InteractionTest-AllTests.json`。

当前范围只有物理抓取、武器挥击、射线/拉弓和文物展柜归位。实现为 Assets/_Game/Scripts/Interaction 下的独立组件，不创建场景、不依赖敌人、Boss 或其他开发者的进度。既有全游戏开发计划不再作为当前工作队列。

基于项目已有 Unity 6000.6.7f1、XRI 3.6.1。脚本编译和自动测试通过情况见本文末尾。没有头显实测，下面的数值是初始调参值。

## 物理抓取与手电筒

武器、手电筒、文物的根物体挂 `PhysicalGrabbable`，它会要求 `Rigidbody` 和 `XRGrabInteractable`。物体至少有一个非 Trigger 的 Box/Sphere/Capsule Collider 或 convex MeshCollider；组合碰撞体放子物体，避免再加独立 Rigidbody。

`PhysicalGrabbable` 在 Awake 配置 Velocity Tracking、动态 Rigidbody、重力、ContinuousDynamic、插值、松手抛掷和 Single Select。按物品调 `Mass Kg`；`Grip Pose` 指定手柄握点，文物可勾 `Use Dynamic Grip`。默认质量 1 kg，抛掷速度倍率 1；`ApplyPhysicsSettings()` 可在未持有时显式重配，已持有时拒绝修改。

已有玩家控制器需提供 XR Interaction Manager、左右手跟踪以及 XRI Direct / NearFar Interactor，Select 对应抓握键、Activate 对应使用键。物体和手的 Interaction Layer 必须匹配。该实现是由跟踪手驱动物品刚体，不模拟每根手指的物理关节；输入可来自控制器或已配置的 XRI 手追踪 interactor。本次不创建或改动 XR Rig。

手电筒再挂 `GrabbableFlashlight`，将子节点的 Spot Light 拖给 `Beam`，可选 `Switch Audio`。持有者 Activate 切换开关，其他悬停手不会触发。也可调用 `Toggle()` / `SetPower(bool)`，监听 `On Power Changed`。实际照亮场景需要所用 URP 画质允许额外灯光；本次没有修改项目的灯光设置。

抓取事件为 `PhysicalGrabbable.Grabbed` / `Released`，Inspector 也有对应 UnityEvent。XRI 已负责跟随与抛掷，不再额外写 transform 跟随手，避免两个系统争用姿态。详见 [XRI 抓取说明](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit%403.6/manual/xr-grab-interactable.html)。

## 武器挥击轨迹与力道

武器根再挂 `MeleeWeapon`，接线如下：

| 字段 | 指向 |
| --- | --- |
| Blade Base / Blade Tip | 有效打击部分两端的子 Transform |
| Rig Motion Reference | 被移动系统驱动的 XR Origin 根，不是手或头 |
| Obstruction Origin | 头部 Camera，用于阻挡检查 |
| Ignored Player Root | 自己玩家的根，避免击中自己的碰撞体 |
| Target Mask | 希望产生命中事件的层 |
| Blocker Mask | 墙和实心阻挡物所在层 |

只在持有时采样，抵消 Rig 平移/旋转，使用刀刃上多个球体从上帧扫到当前帧。默认起挥速度 1.2 m/s、停止阈值 0.45 m/s、单挥最大 0.6 秒。一次挥击同一目标仅通知一次；多碰撞体目标在共同根上挂 `MeleeHitTarget`，未挂时按 Rigidbody 或 Collider 对象归组。

Inspector `On Hit` 或 C# `Hit` 返回 `MeleeHit`：Collider、Target、Point、Direction、Speed、KineticEnergy、EstimatedForce、SwingId。组件只报告命中，不调用任何敌人或扣血逻辑。

速度单位 m/s；动能 `0.5 × effectiveMass × speed²`，单位 J；估计停止力 `effectiveMass × speed / contactSeconds`，单位 N。默认有效质量 0.8 kg、接触时间 0.04 秒。这是游戏用途估算，不是手柄实测力，也不能用物品质量直接代表玩家真实力量。

传送、重新定位和追踪丢失时调用 `ResetSampling()`。组件还会拒绝过长帧、异常位移/速度和明显的 Rig 瞬移。目标若是 Trigger，明确选择 `Trigger Interaction = Collide` 并分配适当层。查询缓冲满时保守放弃命中，`QueryOverflowCount` 可诊断；不会因截断结果忽略墙。

刀刃长度必须不大于 `(Maximum Blade Samples - 1) × Sweep Radius`；默认 32 与 0.035 m 适合约 1 m 内的打击段，较长武器需增大采样数。轨迹是帧间线性近似，大角度快挥仍需真机调参。对象和 Rig 使用固定均匀缩放。

## 射线瞄准

可抓工具根挂 `RayAim`，指定 `Ray Origin`，其本地 +Z/forward 是瞄准方向。配置距离、Hit Mask、Trigger Interaction；Mask 应包含会遮挡射线的墙，避免只包含目标层而穿墙。

`Ignore Hierarchy` 默认忽略该工具自身，也可指向玩家根。可选 LineRenderer 显示射线，组件设置 world space 和两个端点；线条材质/粗细在资源上配置。

`QueryAim()` 返回最近有效命中或最远点；`Fire()` 输出 `Fired(RayAimResult)`，有命中时触发 Inspector `On Hit Point`。默认只有持有时能 Fire，持有者 Activate 可触发，空射也会通过 Fired 返回 `HasHit=false`。不依赖敌人接口。

射线起点应在发射口、离开墙体内部；射线查询本身不会报告包含起点的实心 collider。

## 双手拉弓

弓根挂 `PhysicalGrabbable` 与 `BowDrawAim`，弓体用 Single Select。准备以下节点/引用：

1. `String Interactable`：独立弦节点上的 XRSimpleInteractable（或 XRBaseInteractable），一个小 Trigger Collider，Single Select。弓根 XRGrabInteractable 的 Colliders 列表必须显式排除这个弦碰撞体。
2. `String Rest`：弓上的固定弦静止标记。
3. `Arrow Origin`：箭的发射口，forward/+Z 指向箭飞行方向。
4. `String Visual`：可选的弦拉点视觉节点，必须与 String Rest 分开。它只移动拉点，不自动生成整条弓弦网格。
5. `Trajectory Line`：可选 LineRenderer，显示带重力的飞行曲线。只是轨迹预览，不预测碰撞落点。

第一手持弓，另一手选中弦并向箭方向的反方向拉。按投影距离计算，横向移动不当拉距，反向推不蓄力；默认满拉 0.6 m、最小释放 0.08 m，箭速随力度从 4 到 25 m/s。物体使用 1:1 缩放。

松开弦仅暂存发射，LateUpdate 确认弓仍由原手持有且跟踪有效后才输出一次 `Shot(BowShot)` 和 `On Shot Power(float)`。同帧放下弓、取消选择、暂停或跟踪丢失会清掉待发状态，不误发箭。

默认 `Require Tracked Controllers=true`，左右手 interactor 的 Handedness 要分别设 Left/Right。模拟器或其他跟踪输入可关闭该开关，显式用 `SetTrackingValid(bool)` 接入追踪状态；失效时会取消，需要重新抓弦。`CancelDraw()` 可主动取消。

`Arrow Prefab` 可为空，此时只输出 Origin、Velocity、NormalizedDraw，足够独立测试瞄准/拉力。需要实体箭时，提供挂有 `ArrowProjectile`、Rigidbody 和非 Trigger Collider 的 prefab，箭本地 +Z 指前。生成后按重力飞行，忽略弓自身碰撞；Additional Ignored Colliders 应加入玩家/手的碰撞体。首次碰撞输出 `Impact(Collision)` / `On Impact Point`，可冻结，默认 10 秒销毁。箭在飞行后不开放抓取回收。

## 文物放入展柜 Trigger

文物根添加 `PhysicalGrabbable`、`ArtifactIdentity`、`RestorableArtifact`。Identity 设置稳定的 ID（例如 `jar-01`）与 Type；两件同类型文物必须使用不同 ID，不能用运行时对象 ID。

展柜接收区同一物体添加 BoxCollider（Is Trigger）和 `ArtifactDisplayTrigger`，指定 Expected Artifact Id、Expected Type、Snap Point。Snap Point 位于希望锁定的最终姿态；它不能是文物自身或文物子节点。

默认要求 ID/type 匹配、文物已松手、根坐标距 Snap Point 不超过 0.2 m、持续满足条件 0.35 秒。可关闭位置检查、启用角度检查或修改停留时间。接收区应覆盖文物，位置/旋转容差围绕文物根而非模型几何中心计算。

组件每个物理步检测 BoxCollider 区域实际重叠，避免只依赖 Enter/Exit 回调漏掉睡眠刚体、多个子碰撞体和“持有时进入、里面松手”的情况。Artifact Layers 指向文物物理碰撞体层；文物至少有非 Trigger Collider，展柜 Transform 避免旋转父级配合非均匀缩放造成斜切。

归位后清零速度、设为 kinematic、禁用该文物抓取、对齐并挂到 Snap Point，触发一次 `OnArtifactRestored(ArtifactRestoredEvent)` 和 Inspector `On Restored`。事件已经包含实例 ID 和类型，无需关卡管理器。重复重叠不会再次触发。

调用展柜 `ResetRestoration()` 恢复文物归位前的姿态、物理和抓取设置，允许重新放置。可选 `SetTracker()` 接入已有计数核心；不需要统计时完全不用提供。若提供 tracker，重开需换新 tracker，展柜重置不撤销统计。

## 自动验证

EditMode 测试涵盖挥击运动补偿/窗口/去重/墙体阻挡、射线过滤、拉距和取消发射、归位身份/容差/停留/复位等。PlayMode 测试用临时对象验证 XRI 抓取跟随、释放后重力/碰撞，以及睡眠多碰撞体文物的自动归位；没有保存场景资产。

最终执行结果：Unity 编译通过，0 编译错误、0 编译警告；56 项 EditMode 测试全部通过（包括原有 14 项归位核心测试）；2 项 PlayMode 测试全部通过，包括真实禁用/启用后的归位停留时间重置。结果保存在 `Docs/QA/Interaction-EditMode.json` 与 `Docs/QA/Interaction-PlayMode.json`。

验证中修复了 Unity 6.6 禁用的 GetInstanceID API，以及 EditMode 生命周期模拟调用方式。Editor 的历史窗口缩放/联网日志不属于组件编译错误。头显上的抓取手感、追踪恢复、瞄准姿态与高速挥击仍需要 Quest 3 实测。
