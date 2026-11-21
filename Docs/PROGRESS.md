# 敵人系統進度

> 每次任務結束時由 agent 更新。新的紀錄寫在最上面。

## 目前狀態
- 規格：2026-11-21 起以合併後的 `Docs/ENEMY_SYSTEM_SPEC.md` 為準（來自使用者的 `ENEMY_SYSTEM_SPEC_Edited.md`，名稱與合約改回現行的 `_Game` / `Game4026.*`）
- 里程碑：M1–M3、**M2.5（阿努比斯多招式）完成**；新規格的 M4（雕像移動、投射物反彈、WingShield、新敵人名單）還沒做（EditMode 201/201；PlayMode 16/16）
- 黑曜石神諭（M2.7，使用者追加）：`Enemy_ObsidianOracle`（頭像雕像，無骨架彈跳），5 招：震地、頭槌、滾動、跳砸、鐳射眼；全部可反彈（EditMode 233/233；PlayMode 19/19）
- 招架（M2.6，使用者追加）：阿努比斯雙手持匕首，所有招式都能在擊中前 0.35 秒內用武器招架 → 整招取消、硬直 1.5 秒、受傷 ×1.5；刀尖光點提示招架時機（EditMode 214/214；PlayMode 17/17）
- 阿努比斯（`Enemy_AnubisGuard`）有 5 招：快速揮擊、反手揮擊（鏡像）、重擊（起手停格 0.8 秒）、突進斬（Jump Attack 動畫）、三連斬；其他敵人各一招，時間沿用舊數值
- 傷害橋接完成，含場景安裝器（Bridges）
- 模型與動畫：所有敵人（基底 Enemy_Placeholder 與四個 Variant、場景的巡邏示範）都用 Anubis 模型 + AC_Anubis + EnemyAnimator；各自弱點掛在骨頭上（衛兵/基底：頭，木乃伊/法老：胸口，塞克邁特：背後）。各種敵人目前外觀相同，只有大小不同。
- Git（2026-11-18 收工時）：
  - PR #2（M1–M4）**已合進 main**（`29246a2`）
  - 模型與動畫已 commit 並 push（`6111b70`，模型/貼圖走 Git LFS）。PR #3 頁面已在瀏覽器開好（main ← feature/enemy-ai，不刪任何檔案），**使用者還要按 Create pull request**；下次先確認 PR #3 是否建立/合併
  - 沒有 gh CLI：開 PR 用預先填好的 GitHub compare 網址；查 PR 狀態可用 `https://api.github.com/repos/nodia3310/Game4026_VR/pulls?head=nodia3310:feature/enemy-ai&state=all`
  - `ProfilerCaptures/` 是 M4 的 Profiler 錄檔，沒進 git，可以刪
- 下一步：
  0. 使用者在 EnemyAI 場景實際看阿努比斯 5 招的手感，調招式資產數值（冷卻、權重、預警、傷害）
  0b. 新規格 M4：StatueLocomotion / StatuePart、EnemyProjectile（反彈）、WingShield、卡雕像與伊西斯（需要模型）
  1. 確認 PR #3 建立並合併
  2. 各種敵人換成自己的模型（木乃伊、塞克邁特、法老）；人形 Mixamo 模型可沿用 AC_Anubis 與 EnemyAnimator，只要在 Variant 裡換掉 Model
  3. 和 A 整合：在 A 的場景放 PlayerTarget（務必指定 head）、DamageBridgeInstaller、EncounterDirector + ArtifactEventBridge，戴頭盔實測；確認 A 的武器 targetMask 包含敵人 Collider 的 Layer
  4. 法老招式細節（PharaohBoss.OnPhaseEntered 的 TODO）、數值平衡（冷卻、前搖、血量）
  5. 可選：身體 Collider 改成掛在骨頭上（現在是固定膠囊，不會跟著彎腰或倒地）；生成/死亡特效、音效
- 給 A 的對接說明：`Notes For Groupmates/NOTES_FOR_A.md`

## 已做的決定
- 2026-11-21（神諭）：`aztec_statue_sculpture.glb`（深色石雕半身像）對應圖鑑的 The Obsidian Oracle，弱點 = 額頭紋章（圖鑑：紋章是魔法焦點）。招式依使用者要求改成彈跳、撞擊、滾動、鐳射眼，加近身震地。
- 2026-11-21（神諭）：雕像身體本身就是武器：Body 上的 `EnemyWeapon` 開 `passDamageWhenNotParrying`，招架窗內打身體 = 招架，窗外照常扣血。
- 2026-11-21（神諭）：鐳射是一顆 0.6 m 長、7 m/s 的光彈（`EnemyProjectile`），不是整條光束；沒有招架窗，飛行中隨時可以用近戰武器打回去，飛回去打發射者的弱點（20 × 2.5 = 50）。箭不能反彈。
- 2026-11-21（神諭）：`AttackDefinition` 新增 `leap`（AreaSlam 先跳過去）、`lungeStopDistance`（每招停在玩家前多遠）、`lungeArcHeight`、`statueMotion`、`projectilePrefab`/`projectileSpeed`。
- 2026-11-21（招架）：攻擊前的「頓一拍」是預警停格，保留；另加 `AttackIndicator` 光點提示。
- 2026-11-21（招架）：所有招式都能招架；成功 = 整招取消（含連段剩下的）、硬直 1.5 秒、期間受傷 ×1.5（使用者選的）。
- 2026-11-21（招架）：招架用既有合約：匕首上的 `EnemyWeapon` 實作 `IDamageable`，A 的武器打到刀身就會呼叫它。窗外打到刀身只是被擋，不傷本體。
- 2026-11-21（招架）：招架窗 = 擊中前 `parryWindow` 秒（阿努比斯 0.35，突進斬 0.4，三連斬每下 0.3）；光點在窗打開時亮閃。
- 2026-11-21（招架）：箭不能招架（`DamageDispatch` 的 allowParry=false），之後可改。
- 2026-11-21（招架）：阿努比斯改成**雙持**匕首：反手揮擊和三連斬第二下用 Humanoid 鏡像，實際是左手在揮，單手拿刀會變成空手揮砍。只要單手的話，刪掉左手的 `Weapon_Dagger` 並把那兩招的 mirror 關掉。
- 2026-11-21（招架）：一般硬直打在招架硬直中途時，保留較長的剩餘時間（`EnemyController.Stagger`）。
- 2026-11-21：Edited 規格的設計內容採用，但路徑／命名空間／合約維持 `_Game`、`Game4026.*`、既有簽名（使用者已確認）。
- 2026-11-21（M2.5）：規格的 `EnemyAttackRunner` 由既有的 `EnemyAttack` 元件承擔（不改元件名稱，Prefab 不用重掛），時間軸邏輯在純 C# 的 `AttackRunner`，選招在 `AttackSelector`。
- 2026-11-21（M2.5）：命中用計時，不用動畫事件 `OnAttackHit`；`hitTime` 填動畫的擊中秒數，有沒有 Animator 時間都一樣。
- 2026-11-21（M2.5）：預警 = 動畫播到起手姿勢（`telegraphPoseTime`）後停格 `telegraphTime` 秒（規格寫「停在起手」）；停格結束前面向玩家，揮下後方向鎖定，玩家側閃可躲。
- 2026-11-21（M2.5）：Animator 用狀態名稱（`animatorState`）而不是 Trigger；鏡像用獨立的 `Attack_Mirror` 狀態，避免同一狀態連段時鏡像參數瞬間翻轉。
- 2026-11-21（M2.5）：冷卻：各招從出招開始算，`globalAttackCooldown` 從整招結束（含被打斷）開始算。舊的單招敵人轉成資產時，冷卻 = 舊冷卻 + 舊前搖、全域冷卻 0，節奏和以前一樣。
- 2026-11-21（M2.5）：沒招可出時，在「近身招式的最大距離」內停下面向玩家，否則繼續追（突進斬冷卻中會走近改用近戰）。
- 2026-11-21（M2.5）：突進斬停在玩家前 0.9 公尺（`EnemyAttack.lungeStopDistance`），方向在預警結束時鎖定。
- 2026-11-21（M2.5）：EnemyData 移除舊的 attackRange/Windup/Damage/Cooldown/Angle，改成 `attacks[]` + `globalAttackCooldown`；`attacks` 空的時候用一招預設近戰（舊預設值）。
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
### 2026-11-21（黑曜石神諭）
- 完成：StatueLocomotion、EnemyProjectile + Pool、IParryTarget、EnemyWeapon 傷害穿透、EnemyAttack 發射（muzzle）與每招停止距離、AttackRunner 支援跳躍 AreaSlam；MockWeapon 按 F 也會反彈最近的光彈；Enemy_ObsidianOracle、5 招資產、光彈 Prefab、2 個材質
- 模型：face 在 glb 的 +X，Prefab 裡轉 -90° 朝 +Z、放大 1.25 倍（1.5 m）；額頭/眼睛位置用網格輪廓取樣決定（眼 y 1.0、額頭 y 1.12）
- 實測（EnemyAI 場景）：9 m 外出滾動、滾到 1.0 m 命中；近身震地；5 m 跳砸（截圖確認在空中）；震地被 F 招架 → 硬直；鐳射光彈飛行（截圖），F 反彈 → 命中額頭 50 傷害、硬直
- 測試：EditMode 233/233、PlayMode 19/19
- 問題：
  - 第一次測鐳射，光彈 0.16 秒就消失：是 MockPlayer 被我傳送到地板外掉下去，光彈朝下打到地板，不是程式問題。
  - 鐳射測試時我在 Play Mode 暫時把 EnemyData 的招式改成只剩鐳射（資產不會被 Play Mode 還原），測完已改回 5 招。
  - 近身只會一直震地 → 頭槌最小距離 1.5 改 0.9。
- 需要在 Editor 手動做：無。要在場景測試，把 `Enemy_ObsidianOracle` 拖進 EnemyAI 場景，或加到波次資產。

### 2026-11-21（招架、攻擊提示、阿努比斯拿匕首）
- 完成：
  - `AttackDefinition.parryWindow`；`AttackRunner` 招架窗（`IsParryWindowOpen`、`OnParryWindowOpened`）；`EnemyAttack.TryParry` + 事件 `ParryWindowOpened`/`Parried`
  - `EnemyData.parryStaggerDuration`/`parriedDamageMultiplier`；`EnemyHealth(Model).DamageMultiplier`；`IEnemyContext.CurrentStaggerDuration`，HitState 改用它；`EnemyController.Stagger(seconds)`、`Parried` 事件
  - 新增 `EnemyWeapon`、`AttackIndicator`、材質 `M_AttackGlint`；`DamageDispatch` allowParry，箭傳 false
  - `Enemy_AnubisGuard` 雙手加 `Weapon_Dagger`（長 0.45 公尺，刀身膠囊碰撞）；根物件加 AttackIndicator
  - `MockWeapon` 按 F 招架最近的敵人武器
  - 測試：EnemyAttackTests +7、EnemyHealthModelTests +2、EnemyStateMachineTests +1、EnemyWeaponBridgeTests 3、ParryPlayModeTests 1
- 握法：用手的骨頭算方向（只有食指與拇指骨頭）。刀身從拳頭拇指側伸出，揮砍時看起來像舉刀往下刺。
- 實測（EnemyAI 場景，截圖確認）：右手揮時匕首與光點在右手刀尖；鏡像招式（三連斬第二下）在左手；招架窗在擊中前 0.35/0.4/0.3 秒打開。腳本測試：預警時按 F → 失敗、玩家被打；窗內按 F → 招架成功、硬直 1.5 秒、打身體 10 → 扣 15、硬直結束倍率回到 1。
- 問題：第一次實測，硬直開始的同一刻打到只扣 10，因為倍率在進 Hit 之後才設；改成先設倍率再進硬直，並加 PlayMode 測試防止回歸。
- 測試：EditMode 214/214、PlayMode 17/17（GC 測試仍 0 bytes）
- 需要在 Editor 手動做：無。和 A 整合時確認 A 武器的 `targetMask` 包含匕首 Collider 的 Layer（目前是 Default）。

### 2026-11-21（M2.5：資料驅動攻擊、阿努比斯 5 招）
- 完成：
  - 新增 `Combat/`：AttackType、AttackDefinition、AttackSelector、AttackRunner；EnemyAttack 改寫成選招 + 出招元件；IEnemyContext 攻擊介面改成 TrySelectAttack / AttackHoldRange / BeginAttack / TickAttack / CancelAttack；AttackState、ChaseState 跟著改；EnemyAnimator 改成依每個步驟播動畫並在預警時停格；EnemyDebugLabel 顯示招式
  - AC_Anubis 加 `Attack_Mirror`、`JumpAttack` 狀態；`anubis_2@Standing Melee Run Jump Attack.fbx` 的動畫改名 JumpAttack，旋轉與高度烘進姿勢（原地播放，前衝交給 NavMeshAgent）
  - 招式資產 13 個（`Data/Enemies/Attacks/`），掛到 5 份 EnemyData
  - 測試：EnemyAttackTests 重寫（20 個）、新增 AttackSelectorTests（9 個），狀態機與 EnemyAnimator 測試更新
- 擊中格：用 Editor 取樣右手軌跡找出 QuickerAttack 起手姿勢 0.55 秒、擊中 0.77 秒；Jump Attack 起跳 0.75 秒、擊中 1.58 秒、根部前進約 4.6 公尺
- 實測（EnemyAI 場景，單獨一隻阿努比斯）：5 招都會出；近身隨機出快速揮擊／反手／重擊／三連斬，玩家退到 4 公尺會出突進斬（4.7 → 0.9 公尺命中）；每招判定時動畫都在擊中格（normalized 0.48 = 0.77/1.6；突進斬 0.46–0.48）；重擊停格 0.8 秒；三連斬第三下玩家已離開 → 揮空
- 測試：EditMode 201/201、PlayMode 16/16（GC 測試仍 0 bytes）
- 問題：我取樣動畫的 eval 腳本第一次丟例外，在 EnemyAI 場景留下 3 個 `anubis_2@T-Pose WithSKin(Clone)`，進 Play Mode 時場景被自動存檔。已刪掉並重存，場景與 git 版本一致。之後 eval 裡 Instantiate 要用 try/finally 清掉。
- 需要在 Editor 手動做：無
- 備註：`ENEMY_SYSTEM_SPEC_Edited.md` 沒動；內容已併進 `ENEMY_SYSTEM_SPEC.md`，使用者確認後可刪

### 2026-11-19（GLB 的 PARSING JSON Failed）
- 原因：`Assets/Models` 底下 44 個 `.glb` 在工作目錄裡是 Git LFS 指標文字，glTFast 把指標當 JSON 解析。本體已在本地 LFS 物件庫，沒有重新下載。
- 完成：`git lfs pull --include="*.glb"` 把本體 checkout 出來，並在開啟中的 Editor 重新匯入。44 個都匯成 GameObject。Git 狀態沒有新增修改。
- 還在：`egyptian_styled_trim_set.glb` 的 JSON 正常、mesh 有匯入，但法線貼圖用第三組 UV（`TEXCOORD_2`）。glTFast 在 URP 只支援前兩組，Console 仍有一筆 `Failed to import`（`UVMulti`）。外觀上法線可能不對。要修得改模型 UV，這次沒改檔案。

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
