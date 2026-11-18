# 敵人系統進度

> 每次任務結束時由 agent 更新。新的紀錄寫在最上面。

## 目前狀態
- 里程碑：**M1–M4 全部完成**；之後加上模型與動畫（EditMode 177/177；PlayMode 16/16）
- 傷害橋接完成，含場景安裝器（Bridges）
- 模型與動畫：所有敵人（基底 Enemy_Placeholder 與四個 Variant、場景的巡邏示範）都用 Anubis 模型 + AC_Anubis + EnemyAnimator；各自弱點掛在骨頭上（衛兵/基底：頭，木乃伊/法老：胸口，塞克邁特：背後）。各種敵人目前外觀相同，只有大小不同。
- Git（2026-11-18）：M1–M4 已 push 到 `origin/feature/enemy-ai`（`837f7bb`）。PR 頁面已在瀏覽器開好給使用者按建立，是否已建立未確認。**模型與動畫的改動還沒 commit。**
  - PR 不會刪除 main 上組員的 Docs 檔案（已從 main 恢復）。
  - A 要用到 `PlayerTarget`、橋接之前，需要先把這個分支合進 main。
  - `ProfilerCaptures/` 是 M4 的 Profiler 錄檔，沒進 git，可以刪。
- 下一步（規格之外，建議）：
  1. commit 模型與動畫（新增 `Character Models/`、`Animations/` 約 10 MB）
  2. 各種敵人換成自己的模型（木乃伊、塞克邁特、法老）；人形 Mixamo 模型可沿用 AC_Anubis 與 EnemyAnimator，只要在 Variant 裡換掉 Model
  3. 和 A 整合：在 A 的場景放 PlayerTarget（務必指定 head）、DamageBridgeInstaller、EncounterDirector + ArtifactEventBridge，戴頭盔實測；確認 A 的武器 targetMask 包含敵人 Collider 的 Layer
  4. 法老招式細節（PharaohBoss.OnPhaseEntered 的 TODO）、數值平衡（冷卻、前搖、血量）
  5. 可選：身體 Collider 改成掛在骨頭上（現在是固定膠囊，不會跟著彎腰或倒地）；生成/死亡特效、音效
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
- 2026-11-18（M4）：敵人數值暫定（木乃伊 60HP、衛兵 100HP 攻擊距離 2.4、女神像 250HP 硬直門檻 60 弱點 ×3、法老 600HP），都在 EnemyData 資產裡，Inspector 可調。
- 2026-11-18（M4）：四種敵人都做成 Enemy_Placeholder 的 Prefab Variant，基底改了會全部套用。
- 2026-11-18（模型）：Anubis 模型、AC_Anubis、EnemyAnimator 放在基底 prefab，各種敵人先共用；之後換專屬模型時在 Variant 裡替換 Model。動畫用 Humanoid，收招時間跟著攻擊動畫長度。
- 2026-11-18（M4）：Boss 階段做成 Inspector 列表（BossPhase），加階段不用改程式；一擊跨過多個門檻時，經過的每個階段效果都會執行（例如沒經過召喚階段也會召喚）。
- 2026-11-18（M4）：Boss 小兵用 Boss prefab 子物件上的 EncounterController（規格：「透過 Boss 自己的 EncounterController」），Boss 死亡時強制清除，重新生成時重置。
- 2026-11-18（M4）：EnemyAttack.SpeedMultiplier 同時縮短前搖與冷卻；ResetState 會回到 1。
- 2026-11-18（M4）：EnemyController.Update 改成呼叫 `internal Tick(now, dt)`，時間都從參數讀，PlayMode 測試可以直接驅動幀來量 GC。
- 2026-11-18（M4）：Docs 的三份敵人文件改成進 git（.gitignore 加例外）。
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
### 2026-11-18（所有敵人換成 Anubis 模型）
- 完成：模型、Animator、EnemyAnimator、頭部骨頭弱點 `HeadBone_WeakPoint` 從衛兵 Variant 移到基底 `Enemy_Placeholder`，所有 Variant 和場景的 `Enemy_PatrolDemo` 都繼承；衛兵 Variant 的舊覆寫還原
  - 木乃伊、塞克邁特、法老：停用頭部弱點；原本的胸口/背後弱點改掛到 `mixamorig:Spine2` / `Spine1`，跟著動畫移動（法老的胸口弱點仍是第三階段才啟用）
  - 所有 EnemyData 的 `despawnDuration` 改 2.5 秒（死亡動畫 2.4 秒）
- 問題：骨頭在綁定姿勢和動畫中的朝向不同（綁定姿勢轉了 180°），第一次用綁定姿勢算的頭部弱點跑到後腦。改成在 Play Mode 量動畫中的骨頭座標再寫回 prefab。
- 實測：四種敵人一起生成，都正常追擊；弱點位置在臉前/胸前/背後正確
- 測試：EditMode 177/177、PlayMode 16/16
- 備註：木乃伊、法老胸口與塞克邁特背後的弱點方塊仍然看得見（當作弱點標記）；不想看到的話，關掉該物件的 MeshRenderer 即可

### 2026-11-18（攻擊動畫換成 QuickerAttack、修正攻擊動畫被切斷）
- 原因：攻擊判定後的收招固定 0.5 秒（`EnemyAttack.recoveryDuration`），之後就切回追擊，Animator 跟著切回移動動畫，所以攻擊動畫一定會被切掉；玩家走遠時敵人馬上開始跑，所以特別明顯。跟距離判定無關（攻擊中途不檢查距離，只有硬直或死亡會中斷）。
- 修正：
  - `EnemyAttack.RecoveryOverride`：執行時可覆寫收招時間（負數 = 用 Inspector 的值）
  - `EnemyAnimator`：新增 `attackClip`、`recoveryFollowsClip`。每次出手時把收招時間設成「擊中點到動畫結束的剩餘時間 ÷ 播放速度 − 交叉淡出時間」，攻擊動畫會播完才去追擊
  - `anubis_2@QuickerAttack.fbx`（1.6 秒，單次右手揮擊）改 Humanoid、原地播放，取代 AC_Anubis 的 Attack；擊中點 0.77 秒
- 實測：前搖 0.7 秒時播放速度 1.10，攻擊狀態共約 1.3 秒；判定後玩家退開 3 公尺，動畫照樣播到結尾才淡回跑步
- 測試：EditMode 177/177、PlayMode 16/16
- 舊的 `anubis_2@Attack.fbx`（三連擊）留著沒用到

### 2026-11-18（阿努比斯衛兵換上模型與動畫）
- 完成：
  - `anubis_2@T-Pose WithSKin.fbx`（有網格）設為 Humanoid、沿用 T-Pose 的 Avatar、材質改用 M_Anubis
  - `Enemy_AnubisGuard`：隱藏膠囊外觀，身體 Collider 縮到脖子以下（0–1.38 m），舊的頭部弱點停用；新增 `Model` 子物件（Animator：AC_Anubis、不套用 Root Motion、看不到時不更新骨頭），新的 `Head_WeakPoint` 掛在 `mixamorig:Head` 底下，跟著動畫移動；根物件加 `EnemyAnimator`；Eye 改到 1.6 m
  - `EnemyData_AnubisGuard.despawnDuration` 1.5 → 2.5（死亡動畫 2.4 秒，避免倒到一半就消失）
- 實測（EnemyAI 場景 Play Mode）：生成 → 追擊（跑步動畫）→ 攻擊（AttackSpeed 0.86，擊中點對齊前搖 0.7 秒），玩家扣血 → 打頭部弱點 20 傷害扣 50 HP（×2.5）且播 Hit 動畫 → 擊殺播 Death 動畫、身體往後倒、2.5 秒後回物件池
- 測試：EditMode 全過；PlayMode 16/16
- 已知限制：身體 Collider 是固定膠囊，不會跟著動畫彎腰或倒地；之後若需要更準的命中，可以改成掛在骨頭上的多個 Collider
- 需要在 Editor 手動做：無

### 2026-11-18（阿努比斯模型與動畫，前置）
- 完成：
  - `anubis.fbx` 材質：新增 `materials/M_Anubis.mat`（URP Lit，Tiling Y = -1 修正 glTF 轉檔造成的 UV 顛倒），金屬度/粗糙度合成 `textures/anubis_MetallicSmoothness.png`，法線貼圖改成 Normal Map，不匯入 FBX 內的相機/燈光
  - 動畫：`anubis_2@T-Pose.fbx` 建立 Humanoid Avatar，6 個動畫改 Humanoid 並共用這個 Avatar；Idle/Walk/Run 循環；Walk/Run/Attack 原地播放（往前的位移不套用，移動交給 NavMesh），Death/Hit 保留身體位移
  - `Animations/Enemy/Anubis/AC_Anubis.controller`：Locomotion 混合樹（0 待機、1 走、2 跑）、Attack（播放速度由 `AttackSpeed` 參數控制）、Hit、Death
  - `EnemyAnimator`：依 AI 狀態切換動畫；攻擊動畫的擊中點（約 0.6 秒，第一下揮擊）對齊前搖結束；受擊重複時從頭播
  - EditMode 測試 `EnemyAnimatorTests`（全部通過）
- 當時卡住：`anubis.fbx` 是沒綁骨的靜態模型，`anubis_2@T-Pose.fbx` 和動畫檔只有骨架；使用者之後補上 With Skin 的模型
- 決定：用 Humanoid（之後木乃伊、法老可以共用動畫）；動畫由獨立元件 `EnemyAnimator` 驅動，`EnemyController` 的 Animator 欄位留空

### 2026-11-18（M4：敵人種類與 Boss 架構）
- 完成：BossPhase、BossPhaseTracker、PharaohBoss；EnemyAttack.SpeedMultiplier、IEnemyContext.AttackWindup、EncounterController.ResetEncounter、EnemyData.OnValidate、EnemyDebugLabel 顯示 Boss 階段；4 份 EnemyData、4 個 Prefab Variant、2 個新波次；EnemyAI 場景改用新敵人；14 個 EditMode + 4 個 PlayMode 測試
- 實測（EnemyAI 場景）：按 1/2/3 → 法老出場 → 50% 進入召喚階段（無敵 2 秒、2 隻木乃伊）→ 8% 進入狂暴階段（胸口弱點出現、攻速 ×1.5）→ 擊殺 → 小兵被清除、BossDefeated、敵人全部回到物件池（Instantiate 只有預熱的 8 次）
- 0 GC：PlayMode 測試 4 隻敵人 720 幀 = 0 bytes；Profiler 錄 300 幀（巡邏示範 + 3 隻木乃伊同時追擊/攻擊），第 10 幀之後 `EnemyController.Update` 1160 次呼叫 = 0 bytes（第 3 幀有一次性的 522 bytes 首次配置）。只有 Editor 專用的除錯 IMGUI（EnemyDebugLabel / MockPlayer / DebugArtifactButton 的 OnGUI）每幀有配置，它們不會進正式遊戲。
- 需要在 Editor 手動做：無
- 問題：GC 測試第一次沒過，因為測試裡 PlayerTarget 沒指定 head，head 落在地面高度，視線被地板擋住。已在測試修正，也寫進給 A 的說明（head 一定要指定）。Profiler capture 存在專案根目錄的 `ProfilerCaptures/`（沒進 git，可以刪）。

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
