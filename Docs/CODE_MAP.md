# 程式地圖

> 一行一個類別：類別 — 路徑 — 一句用途。新增或改名類別時由 agent 更新。
> 目的：讓 agent 查這份就知道東西在哪，不必掃描整個專案。
> 根目錄是 `Assets/_Game/`（舊的 `_Project` / `Anubis.*` 已棄用）。

## Contracts（`Assets/_Game/Scripts/Contracts/`，`Game4026.Contracts`，與 A 共用，既有簽名不可擅改）
- IDamageable — Scripts/Contracts/IDamageable.cs — 敵人實作；A 的武器呼叫 `TakeDamage(float, Vector3, bool)`
- IPlayerDamageReceiver — Scripts/Contracts/IPlayerDamageReceiver.cs — A 的玩家實作；敵人攻擊呼叫 `ApplyPlayerDamage(float)`
- ArtifactRestoredEvent — Scripts/Contracts/ArtifactRestoredEvent.cs — A 發出的文物歸位資料（ArtifactId + Type）
- ArtifactType — Scripts/Contracts/ArtifactType.cs — 文物種類 enum（數值固定，只能往後加）
- PlayerTarget — Scripts/Contracts/PlayerTarget.cs — （B 新增）掛在玩家身上，提供 Head/Body 和 IPlayerDamageReceiver，`PlayerTarget.Current` 給敵人找玩家

## Enemy（`Assets/_Game/Scripts/Enemy/`，`Game4026.Enemy`，只引用 Contracts）
- EnemyData — Scripts/Enemy/Data/EnemyData.cs — ScriptableObject，單一敵人種類的所有數值（規格 4.1）
- EnemyHealthModel — Scripts/Enemy/Core/EnemyHealthModel.cs — 純 C# 受擊規則：弱點倍率、受擊冷卻、硬直判定、死亡、Reset
- EnemyDamageResult — Scripts/Enemy/Core/EnemyDamageResult.cs — 一次有效受擊的結果（實際扣血、擊中點、弱點、硬直、擊殺）
- EnemyHealth — Scripts/Enemy/Core/EnemyHealth.cs — MonoBehaviour + IDamageable，包裝 Model，提供 Damaged/Died 事件、ResetState、IsInvulnerable
- WeakPoint — Scripts/Enemy/Core/WeakPoint.cs — 掛在弱點 Collider 子物件上，以 isWeakPoint=true 轉發給父物件的 EnemyHealth，附 Gizmo
- EnemyController — Scripts/Enemy/Core/EnemyController.cs — 敵人 AI 主元件：狀態機 + NavMeshAgent + 每 0.2 秒視線檢查，實作 IEnemyContext；可選 Animator
- EnemyAnimator — Scripts/Enemy/Core/EnemyAnimator.cs — 依 AI 狀態切換 Animator（Locomotion 混合樹／Attack／Hit／Death），攻擊動畫擊中點對齊前搖，收招時間跟著攻擊動畫長度（EnemyAttack.RecoveryOverride）
- EnemyAttack — Scripts/Enemy/Core/EnemyAttack.cs — 前搖事件、距離 + 角度命中判定、冷卻；命中時呼叫 ApplyPlayerDamage
- EnemyGeometry — Scripts/Enemy/Core/EnemyGeometry.cs — 忽略高度的水平距離與扇形判定
- AssemblyInfo — Scripts/Enemy/AssemblyInfo.cs — InternalsVisibleTo PlayMode 測試（讓測試直接呼叫 EnemyController.Tick）

## Enemy Boss（`Assets/_Game/Scripts/Enemy/Boss/`，`Game4026.Enemy`）
- BossPhase — Scripts/Enemy/Boss/BossPhase.cs — 一個階段的 Inspector 設定：血量門檻、攻速倍率、無敵秒數、是否召喚小兵、要啟用的物件
- BossPhaseTracker — Scripts/Enemy/Boss/BossPhaseTracker.cs — 純 C#：依血量比例選階段，只往前，可一次跨多個階段
- PharaohBoss — Scripts/Enemy/Boss/PharaohBoss.cs — 法老階段層：PhaseChanged、換階段無敵、子物件 EncounterController 召喚小兵、啟用弱點、攻速；招式先寫成 TODO

## Enemy Spawning（`Assets/_Game/Scripts/Enemy/Spawning/`，`Game4026.Enemy`）
- WaveDefinition — Scripts/Enemy/Spawning/WaveDefinition.cs — ScriptableObject：一波 = 多組「敵人 prefab + 數量」、spawnInterval、startDelay
- EncounterSchedule — Scripts/Enemy/Spawning/EncounterSchedule.cs — 純 C# 波次排程：上一波全滅後等 startDelay 才開下一波，波內每 spawnInterval 生一隻，遵守 maxAlive
- SpawnPoint — Scripts/Enemy/Spawning/SpawnPoint.cs — 生成點標記：確認在 NavMesh 上、離玩家 ≥3 公尺，附 Gizmo
- EnemyPoolManager — Scripts/Enemy/Spawning/EnemyPoolManager.cs — 每種 prefab 一個 ObjectPool，可預熱；敵人 Despawned 時自動回收；CreatedCount 用來確認有沒有重用
- EncounterController — Scripts/Enemy/Spawning/EncounterController.cs — 一個展廳的遭遇戰：StartEncounter、ForceClear、Started/Cleared、觸發區自動開始
- EncounterDirector — Scripts/Enemy/Spawning/EncounterDirector.cs — 場景唯一：NotifyArtifactRestored → 清除對應展廳；三件都歸位 → 開始 Boss 遭遇戰；BossEncounterStarted/BossDefeated
- ArtifactProgress — Scripts/Enemy/Spawning/ArtifactProgress.cs — 純 C#：以 ArtifactId 去重、判斷三種文物是否都已歸位

## Enemy States（`Assets/_Game/Scripts/Enemy/States/`，`Game4026.Enemy`）
- IEnemyState / EnemyStateId — States/IEnemyState.cs、EnemyStateId.cs — 狀態介面（Enter/Tick/Exit）與狀態編號
- IEnemyContext — States/IEnemyContext.cs — 狀態能讀寫的資料與動作（EnemyController 實作；測試用假的）
- EnemyStateMachine — States/EnemyStateMachine.cs — 持有所有狀態、切換；Dead 只能切到 Spawn
- EnemyStateRules — States/EnemyStateRules.cs — 共用轉換條件（開始追擊、失去目標、可攻擊）
- SpawnState / IdleState / PatrolState / ChaseState / AttackState / HitState / DeadState — States/*.cs — 規格 4.4 的七個狀態

## Enemy DebugTools（`Assets/_Game/Scripts/Enemy/Debug/`，`Game4026.Enemy.DebugTools`，引用 Enemy + Contracts + Input System）
- MockPlayer — Scripts/Enemy/Debug/MockPlayer.cs — 桌面假玩家：WASD、右鍵轉視角，實作 IPlayerDamageReceiver，顯示 HP；Teleport() 給 CLI 測試用
- MockWeapon — Scripts/Enemy/Debug/MockWeapon.cs — 左鍵從滑鼠位置 Raycast，對 IDamageable 呼叫 TakeDamage（同 A 的呼叫方式）
- EnemyDebugLabel — Scripts/Enemy/Debug/EnemyDebugLabel.cs — 只在 Editor 顯示：敵人頭上的狀態與血量
- DebugArtifactButton — Scripts/Enemy/Debug/DebugArtifactButton.cs — 按 1/2/3 模擬 Rosetta/Canopic/Mask 歸位，左上角顯示進度

## 資產與場景
- EnemyData_Default — Assets/_Game/Data/Enemies/EnemyData_Default.asset — 規格 4.1 預設數值
- Enemy_Placeholder — Assets/_Game/Prefabs/Enemies/Enemy_Placeholder.prefab — 佔位敵人基底（膠囊身體 + 方塊頭弱點，無 Animator）
- Enemy_Mummy / Enemy_AnubisGuard / Enemy_Sekhmet / Enemy_Pharaoh — Assets/_Game/Prefabs/Enemies/*.prefab — 以 Placeholder 為基底的 Prefab Variant（弱點：胸口 / 頭 / 背後 / 第三階段胸口）；Pharaoh 含 PharaohBoss 與子物件 Minions 遭遇戰
- EnemyData_Mummy / _AnubisGuard / _Sekhmet / _Pharaoh — Assets/_Game/Data/Enemies/*.asset — 四種敵人的暫定數值（規格第 5 節）
- Wave_Hall_1 / Wave_Hall_2 / Wave_Boss_Pharaoh / Wave_Boss_Minions — Assets/_Game/Data/Enemies/*.asset — 木乃伊×3 / 衛兵+女神像（延遲 2 秒）/ 法老×1 / 木乃伊×2（Boss 召喚）
- EnemyAI 測試場景 — Assets/_Game/Scenes/Test/EnemyAI.unity — 地板 + 兩面牆（已烘焙 NavMesh）、巡邏點、Enemy_PatrolDemo、MockPlayer、EnemyPool（預熱 4 隻）、3 個生成點、Encounter_Hall_Rosetta（觸發區）、Encounter_Boss、EncounterDirector（+ DebugArtifactButton、ArtifactEventBridge）

## Bridges（`Assets/_Game/Scripts/Bridges/`，`Game4026.Bridges`，引用 Contracts + Interaction + Enemy）
- ImpactDamageCurve — Scripts/Bridges/ImpactDamageCurve.cs — 速度 → 基礎傷害（速度 × 係數，限制在上下限之間），含近戰／箭預設值
- DamageDispatch — Scripts/Bridges/DamageDispatch.cs — 對命中 Collider 找 IDamageable 並呼叫 TakeDamage（isWeakPoint=false）
- MeleeDamageBridge — Scripts/Bridges/MeleeDamageBridge.cs — 掛在 A 的 MeleeWeapon 旁，聽 Hit 事件扣血
- ArrowDamageBridge — Scripts/Bridges/ArrowDamageBridge.cs — 掛在 A 的箭上（由安裝器自動加），聽 Impact 事件扣血
- ArtifactEventBridge — Scripts/Bridges/ArtifactEventBridge.cs — 訂閱場景裡所有 A 的 ArtifactDisplayTrigger.OnArtifactRestored，轉給 EncounterDirector
- DamageBridgeInstaller — Scripts/Bridges/DamageBridgeInstaller.cs — 場景元件：開場時幫所有 MeleeWeapon 加橋接，每次射擊後幫新的箭加橋接

## Tests（`Assets/_Game/Tests/EditMode/Enemy/`，`Game4026.Enemy.EditModeTests`）
- EnemyHealthModelTests — Tests/EditMode/Enemy/EnemyHealthModelTests.cs — 受擊規則單元測試（倍率、冷卻、硬直、死亡、Reset、無效傷害）
- EnemyHealthComponentTests — Tests/EditMode/Enemy/EnemyHealthComponentTests.cs — 模擬 A 的呼叫路徑：Collider → GetComponentInParent<IDamageable> → TakeDamage
- EnemyStateMachineTests — Tests/EditMode/Enemy/EnemyStateMachineTests.cs — 規格 4.4 的狀態轉換表
- FakeEnemyContext — Tests/EditMode/Enemy/FakeEnemyContext.cs — 測試用 IEnemyContext（不需要 NavMesh / Play Mode）
- EnemyAttackTests — Tests/EditMode/Enemy/EnemyAttackTests.cs — 命中範圍、角度、冷卻
- EnemyGeometryTests — Tests/EditMode/Enemy/EnemyGeometryTests.cs — 水平距離與扇形

## Tests（`Assets/_Game/Tests/EditMode/Bridges/`，`Game4026.Bridges.EditModeTests`）
- ImpactDamageCurveTests — Tests/EditMode/Bridges/ImpactDamageCurveTests.cs — 公式換算與上下限
- DamageBridgeInstallerTests — Tests/EditMode/Bridges/DamageBridgeInstallerTests.cs — 安裝器的 AttachMelee/AttachArrows（只用明確清單，不掃描開啟中的場景）
- DamageBridgeTests — Tests/EditMode/Bridges/DamageBridgeTests.cs — 自己組的 MeleeHit／箭命中 → 橋接 → 真的 EnemyHealth/WeakPoint
- EncounterScheduleTests / ArtifactProgressTests / EncounterDirectorTests — Tests/EditMode/Enemy/*.cs — 波次排程、文物去重、Director 路由
- BossPhaseTrackerTests — Tests/EditMode/Enemy/BossPhaseTrackerTests.cs — 階段門檻、只往前、跨階段、4 個階段

## Tests（`Assets/_Game/Tests/PlayMode/Enemy/`，`Game4026.Enemy.PlayModeTests`，引用 Unity.AI.Navigation）
- PlayModeWorld — Tests/PlayMode/Enemy/PlayModeWorld.cs — 共用 fixture：執行中烘焙 NavMesh、物件池、用程式建的敵人模板
- BossPlayModeTests — Tests/PlayMode/Enemy/BossPlayModeTests.cs — 法老三階段、無敵、小兵、弱點、跨階段、物件池重用
- EnemyGcTests — Tests/PlayMode/Enemy/EnemyGcTests.cs — 4 隻敵人追擊與攻擊 720 幀，敵人腳本 0 bytes GC
- EncounterPlayModeTests — Tests/PlayMode/Enemy/EncounterPlayModeTests.cs — 執行中烘焙 NavMesh：遭遇戰開始到 Cleared、物件池重用、ForceClear、Boss 觸發與擊敗、生成點檢查
