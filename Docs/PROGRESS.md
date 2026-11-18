# 敵人系統進度

> 每次任務結束時由 agent 更新。新的紀錄寫在最上面。

## 目前狀態
- 里程碑：M3（生成與遭遇戰）— **完成**（EditMode 151/151；PlayMode 12/12；EnemyAI 場景實測通過）
- M2（AI 與攻擊）、M1（共用介面與受擊）— 完成
- 傷害橋接完成，含場景安裝器（Bridges，EditMode 測試 19/19；全專案 EditMode 98/98）
- 下一步：M4（三種敵人的 EnemyData 與 Prefab、法老 Boss 三階段架構與 PhaseChanged、Profiler 確認 4 隻同時追擊時每幀 0 GC）
- 給 A 的對接說明：`Notes For Groupmates/NOTES_FOR_A.md`

## 已做的決定
- 2026-11-18：棄用 `Assets/_Project/`、`Anubis.*`，改用現有的 `Assets/_Game/`、`Game4026.*`（Enemy 程式在 `Game4026.Enemy`）。
- 2026-11-18：共用合約沿用 `Game4026.Contracts` 的既有簽名（`TakeDamage(float, Vector3, bool)`、`ApplyPlayerDamage(float)`、`ArtifactRestoredEvent`）；規格原本的 `DamageInfo`、`PlayerTarget`、`GameEvents`、`WeaponType` 列為提案，尚未採用。
- 2026-11-18：規格以 `Docs/ENEMY_SYSTEM_SPEC.md` 為準，`設計文檔.md` 不參考。
- 2026-11-18：測試場景是 `Assets/_Game/Scenes/Test/EnemyAI.unity`。AI Navigation 2.0.16 已安裝。
- 2026-11-18：`hitCooldown` 暫時當作**全域**短暫無敵（合約沒有 Source 參數，且 A 會先過濾同一次揮擊的重複接觸）。使用者已確認，不需要擴充合約。
- 2026-11-18：硬直判定用套用弱點倍率**之後**的傷害；致命一擊不觸發硬直；傷害 ≤0、NaN、Infinity 一律忽略，也不消耗冷卻。（使用者已確認）
- 2026-11-18：M2 的 `PlayerTarget` 放在 `Game4026.Contracts`（新增檔案，不改既有簽名）；A 負責把它掛在 XR Rig 上。
- 2026-11-18：不改 A 的程式；武器 → 敵人的傷害換算放在 B 的 `Game4026.Bridges`（引用 Interaction）。公式：近戰為速度 ×8，限制在 5–40；箭為相對速度 ×1，限制在 10–35。
- 2026-11-18：武器不改 Prefab，改用場景裡的 `DamageBridgeInstaller` 在執行時自動掛上橋接（A 的 Prefab 和場景都不動）。
- 2026-11-18（M3）：下一波要等上一波**全滅**後再等 startDelay 才開始（規格沒寫）；波內每 spawnInterval 生一隻，在場數達 maxAlive 就暫停。
- 2026-11-18（M3）：「三件文物都歸位」= RosettaStone、CanopicJar、TutankhamunMask 三種各收到一次，以 ArtifactId 去重。
- 2026-11-18（M3）：文物事件先接 A 的 `ArtifactDisplayTrigger.OnArtifactRestored`（`ArtifactEventBridge`）；`ArtifactRestorationTracker` 不是場景元件，等 A 回覆再接。
- 2026-11-18（M3）：ForceClear 也會觸發 `Cleared`（`WasForceCleared = true`），被清除的敵人不算擊殺；遭遇戰在開始前就被清除的話，之後不會再開始。
- 2026-11-18（M3）：Boss 遭遇戰正常打完（不是被清除）才觸發 `BossDefeated`；Boss 本體是 M4，目前用 1 隻佔位敵人。
- 2026-11-18（M3）：物件池把實例放在停用中的容器底下 Instantiate，Awake/OnEnable 會等到真正生成時才跑；生成時先放好位置再啟用，NavMeshAgent 才會落在正確位置。
- 2026-11-18（M3）：Bridges 多引用 Enemy（Enemy 不引用 Bridges，沒有循環）。
- 2026-11-18（M2）：狀態用 class + `IEnemyContext` 介面，狀態本身不碰 NavMesh/物理，EditMode 就能測轉換表。
- 2026-11-18（M2）：距離與攻擊扇形都用水平距離（忽略高度），避免 VR 玩家站著時身高讓敵人打不到。
- 2026-11-18（M2）：攻擊冷卻從「判定」時開始算；收招時間 0.5 秒（`EnemyAttack.recoveryDuration`，規格沒寫，先用預設值）。
- 2026-11-18（M2）：在攻擊範圍內但還在冷卻時，敵人停下並面向玩家，不會一直往玩家身上擠。
- 2026-11-18（M2）：視線檢查用 `RaycastNonAlloc`，忽略自己和玩家的 Collider；遮擋 Layer 先用 Default（使用者已確認）。
- 2026-11-18（M2）：測試工具放在獨立組件 `Game4026.Enemy.DebugTools`，敵人本體不依賴 Input System。
- 2026-11-18（M2）：死亡後 `despawnDuration` 秒觸發 `Despawned` 並停用物件；M3 的物件池會接手回收。
- 2026-11-18：受擊規則抽成純 C# 的 `EnemyHealthModel`（時間由外部傳入），測試不需要 VR 或 Play Mode。

## 待確認問題
- 文物事件以哪個為準（A 回覆前先接 `ArtifactDisplayTrigger`）；`ArtifactRestorationTracker` 由誰建立、要不要也接。
- `MeleeWeapon` 每次揮擊、每個目標只算一次（目標以 `MeleeHitTarget` 根物件或 attachedRigidbody 為準）。敵人若掛了 `MeleeHitTarget` 或 Rigidbody，弱點和身體會被當成同一個目標，一次揮擊只算先碰到的 Collider；敵人 Collider 也要放在武器的 `targetMask` Layer 上。
- `IPlayerDamageReceiver` 目前沒有實作（玩家血量還沒指派）；M2 測試用 MockPlayer。

## 紀錄
### 2026-11-18（M3：生成與遭遇戰）
- 完成：WaveDefinition、EncounterSchedule、SpawnPoint、EnemyPoolManager、EncounterController、EncounterDirector、ArtifactProgress、ArtifactEventBridge、DebugArtifactButton；EnemyController 加上 ForceClear / Killed / SetPatrolPoints；Enemy_Placeholder prefab、3 個波次資產；EnemyAI 場景加上遭遇戰；18 個 EditMode + 4 個 PlayMode 測試
- 實測（EnemyAI 場景）：玩家走進觸發區 → 遭遇戰開始，從預熱的物件池生 2 隻 → 按 1 → 清場（不算擊殺），敵人回到物件池 → 重複按 1 被忽略 → 按 2、3 → Boss 遭遇戰開始 → 打倒 → BossDefeated。從頭到尾 Instantiate 只有預熱的 4 次。Console 0 錯誤。
- 需要在 Editor 手動做：無
- 問題：`unity command script eval --file` 的檔案必須是 .cs 副檔名。

### 2026-11-18（M2：AI 與攻擊）
- 完成：PlayerTarget（Contracts）、EnemyController、EnemyAttack、EnemyGeometry、狀態機與 7 個狀態、MockPlayer / MockWeapon / EnemyDebugLabel、EnemyAI 場景布置（NavMesh 已烘焙）、34 個新 EditMode 測試
- Play Mode 實測（EnemyAI 場景，無 Animator 的佔位模型）：Spawn 1.5 秒 → Patrol → 看到玩家 → Chase → Attack（前搖 0.7 秒、命中扣 10、收招 0.5 秒、冷卻 2 秒）→ 打頭 50 傷害並硬直 0.5 秒 → 身體 20 傷害不硬直 → Dead → 1.5 秒後停用。Console 0 錯誤。
- 需要在 Editor 手動做：無（場景已用 CLI 布置並存檔）
- 問題：無。Meta XR 在沒接頭盔時會印 5 個警告，與敵人系統無關。

### 2026-11-18（傷害橋接安裝器）
- 完成：`DamageBridgeInstaller` 和 3 個 EditMode 測試；給 A 的說明搬到 `Notes For Groupmates/`（這個資料夾會被 git 追蹤）
- 需要在 Editor 手動做：用真武器測試時，在該場景放一個 `DamageBridgeInstaller`
- 問題：Unity 6.6 的 `FindObjectsSortMode` 已棄用，改用只帶 `FindObjectsInactive` 的呼叫方式

### 2026-11-18（傷害橋接）
- 完成：Bridges 組件（ImpactDamageCurve、DamageDispatch、MeleeDamageBridge、ArrowDamageBridge）與 16 個 EditMode 測試；寫了 `Notes For Groupmates/NOTES_FOR_A.md`
- 需要在 Editor 手動做：（後來改用安裝器，不必改 Prefab）
- 問題：無（先建 asmdef、再加 .cs，這次沒有發生漏檔）

### 2026-11-18
- 完成：
  - 文件改成實際名稱（CLAUDE.md、ENEMY_SYSTEM_SPEC.md 第 0/2/3/4/6/7/8 節、CODE_MAP.md）
  - M1：EnemyData、EnemyHealthModel、EnemyDamageResult、EnemyHealth、WeakPoint 及 EditMode 測試
- 修改檔案：見 CODE_MAP.md 的 Enemy / Tests 段落，另有 `Game4026.Enemy.asmdef`、`Game4026.Enemy.EditModeTests.asmdef`
- 需要在 Editor 手動做：無
- 問題：
  - Unity 在建立 asmdef 的同時匯入新腳本，有 3 個 .cs 沒有被加進組件的編譯清單（明明已匯入卻出現 CS0246）。Refresh、ForceUpdate 都沒用，用 `AssetDatabase.RenameAsset` 改名再改回才恢復。之後新增腳本時，建議先建 asmdef 並等編譯完，再加 .cs。
  - 測試的冷卻邊界不要用剛好等於的時間點（`1f + 0.3f` 會有浮點誤差）。
