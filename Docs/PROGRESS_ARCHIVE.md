# 敵人系統進度（封存）

> 從 `PROGRESS.md` 搬來的舊內容，平常不用讀；要查以前為什麼這樣做時再看。

## 舊的 Git 狀態（2026-11-18）
- Git（2026-11-18 收工時）：
  - PR #2（M1–M4）**已合進 main**（`29246a2`）
  - 模型與動畫已 commit 並 push（`6111b70`，模型/貼圖走 Git LFS）。PR #3 頁面已在瀏覽器開好（main ← feature/enemy-ai，不刪任何檔案），**使用者還要按 Create pull request**；下次先確認 PR #3 是否建立/合併
  - 沒有 gh CLI：開 PR 用預先填好的 GitHub compare 網址；查 PR 狀態可用 `https://api.github.com/repos/nodia3310/Game4026_VR/pulls?head=nodia3310:feature/enemy-ai&state=all`
  - `ProfilerCaptures/` 是 M4 的 Profiler 錄檔，沒進 git，可以刪

## 已做的決定（2026-11-18，M1–M4）
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

## 紀錄
### 2026-11-21（阿努比斯跳斬空中慢動作）
- 分析 JumpAttack 動畫：0.7 s 蹲、0.8 s 起跳、約 1.1–1.2 s 最高點、1.5 s 落地、1.58 s 命中。原本的預警姿勢（0.2 s）還在地上，所以看起來不是空中。
- 招式資產新增 `Slow motion`：`Slow Motion At`（動畫秒數，負數 = 關）、`Duration`、`Scale`。動畫播到那一刻時，整隻敵人進入慢動作（動畫、突進位移、出招時間軸），之後緩回。
- `Anubis_LungeSlash`：最高點 1.12 s、0.6 s、0.12 倍速；招架窗剛好在最高點打開。
- 紅光 indicator 依使用者偏好由使用者自己調（AttackIndicator → Flash Size）。
- EditMode 245/245、PlayMode 27/27。

### 2026-11-21（預警改成慢動作）
- 使用者覺得 Anubis 出招「還是時停」：原本是預警時動畫停在舉刀姿勢（播放速度 0），不是打中時的慢動作。
- 改成 `EnemyAnimator.telegraphCreep`（預設 0.15）：舉刀動作提早變慢，以約 0.15 倍速慢慢舉到底，揮刀時再回正常速度；命中時間不變。所有有 Animator 的敵人都套用，設 0 就回到停住。EditMode 245/245、PlayMode 26/26。

### 2026-11-21（翼人 + 慢動作打擊感）
- 慢動作：
  - `EnemyController.HitStop(秒, 倍率)` 改成慢動作：`HitStopCurve` 前 60% 維持慢速，之後線性回到 1
  - NavMeshAgent 不再暫停寫位置，改成速度乘上倍率（突進在慢動作中也不會丟失距離）
  - `EnemyAnimator` 每幀跟著 `TimeScale`；光彈反彈改成慢動作
  - `HitFeedback_Default` 的秒數全部加倍，上限 0.4 s
- 翼人：
  - 分析動畫：Attack 是右翼 0.15 s 後收、0.25 s 打中；Block 是靜態雙翼合攏；沒有走路動畫
  - 腳本翻轉出左翼攻擊，左右骨頭依靜止位置自動配對；驗證翼尖軌跡和原版左右對稱
  - Idle 改成循環（改了 birdman.fbx 的匯入設定）
  - 新元件 `HoverLocomotion`、`EnemyGuard`；新攻擊類型 `AttackType.Guard`；`EnemyController.ForceAttack`、`EnemyAttack.Queue / EndGuard`、`AttackRunner.SkipToHit`；`EnemyHealth` 受擊前先問 `EnemyGuard`；新 `HitKind.GuardBreak`
  - 資產：`AC_Birdman`、6 個 `Birdman_*` 招式、`EnemyData_Birdman`、`Enemy_Birdman`
- 測試：EditMode 244/244、PlayMode 26/26
- 驗證：EnemyAI 場景實際生成，先俯衝（停在 1.31 m 打中），之後左右翼斬輪流出，全部命中。翼盾用測試驗證（eval 指令往返太慢，抓不準時間）
- 注意：
  - 跑 EditMode 測試時 Unity 跳出「儲存場景」視窗，存進了我分析模型時留下的 `birdman(Clone)`。已把 EnemyAI.unity 還原成 git 版本並重新載入
  - 翼人還沒放進任何波次或場景，要測就把 Prefab 拖進 EnemyAI 場景

### 2026-11-21（招架音效只在成功時播放）
- 問題：沒擋成功也會響 Parry 音效。原因是 Blocked Clips 為空時，程式退回去用 Parry Clips；刀身在窗外被打到（招架失敗）就播了 Parry 音效。
- 修正：Blocked 只用 Blocked Clips，空的就不出聲；Parry Clips 只在成功招架與反彈光彈時播放。EditMode 240/240。

### 2026-11-21（打擊感 / 卡肉）
- 完成：
  - `EnemyController.HitStop`：只停這隻敵人。AI 時鐘扣掉停格時間，出招、硬直、冷卻都順延；NavMeshAgent 暫停寫位置；Animator speed 0；StatueLocomotion 和 EnemyAnimator 改用 `controller.DeltaTime`
  - `EnemyHitFeedback`、`HitFeedbackProfile`、`HitImpacts`、`HitSparkPool`
  - 光彈被反彈時先定格 0.07 秒
  - `MeleeDamageBridge` 依反應強度震動手把
  - `EnemyWeapon` 和 `MockWeapon` 的招架改用 `EnemyAttack.Now`
- 資產：
  - `HitFeedback_Default.asset`
  - `M_HitFlash`、`M_HitSpark`
  - `FX/FX_HitSpark.prefab`
  - 6 個敵人 Prefab 都掛上了 EnemyHitFeedback，光彈 Prefab 也指定了 Profile
- 決定：
  - 不改 `Time.timeScale`。VR 改全域時間會讓玩家手上的物理武器卡住，而且容易暈，所以只停敵人；玩家那一側用手把震動補。
  - 音效欄位先留空，等有音檔再放進 Profile。
- 實測（EnemyAI 場景）：
  - 打神諭和巡邏阿努比斯，兩隻都觸發了停格、閃白、抖動，停格 0.083 秒（15 傷害）
  - 用 A/B 截圖確認閃白有顯示
- 問題：
  - 閃白材質原本複製自 M_AttackGlint（Particles Unlit），結果完全看不到：Particles shader 會乘頂點色，而模型沒有頂點色，乘出來是 0。改成 URP Unlit 透明加算後正常。
  - 透明度原本也乘進亮度，強度被平方變得太淡；改成 alpha 固定為 1，Profile 的 flash 數值就是線性亮度。
- 測試：EditMode 239/239、PlayMode 22/22
- 需要在 Editor 手動做：無。手感數值在 `Assets/_Game/Data/Enemies/HitFeedback_Default.asset`。

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

## 2026-11-22 目前狀態快照（從 PROGRESS.md 搬來）

- **雙手替身 + 走廊去板 + 手消失修正（2026-11-22）**：左手（`HandL`）也會攻擊：掌拍/連拍左右輪流、橫掃/頭部橫斬用起點那側的手、擲武器左右手交替；新招**雙手合擊**（兩手在玩家前方左右等待、掌心相對、地上紅色帶狀預警 1.4 秒 → 斜向合掌，玩家前後 0.8 m 內受 22 傷害，往前/後一步可躲；合掌後兩手卡 1.4 秒都能打）。鐮刀走廊的兩片半透明板拿掉。手移動時短暫消失：原因是替身身體的深度預寫擋住身後的手 → 繪製順序改成手/武器 3000 → 身體（新材質 M_SpectralBody）3010 → 結界 3020。合擊的「後退一步可躲」只有單元測試，Play Mode 沒驗到（使用者說先跳過）（EditMode 281/281；PlayMode 29/29）
- **實體擺錘 + 自然的神之手（2026-11-22）**：擺錘鐮刀改成實體（青銅臂 + 原始材質的 4.5 m 大 Kopesh），生成時往上打射線找天花板、從天花板（17–19 m）吊下來，刀刃最低點離地 1.2 m，±35°、週期 3.2 秒，刀面朝走廊（玩家看得到整把彎刀）。神之手：新增 `HandPose` 讓手指真的握起來；萬兵齊發的每隻手有自己的傾斜、大小、漂浮節奏、依序出現（先張開手、武器成形後握住刀柄，刀刃朝上）、出手前後拉蓄力、丟出後跟進並張開手淡出；主手也會依招式變手勢（掌拍張平、手刀伸直、拿匕首握拳、放大招時半握）（EditMode 279/279；PlayMode 29/29）
- **法老結界 + 鐮刀走廊 + 萬兵齊發（2026-11-22）**：法老周圍半徑 5 m 的金色結界（實心，打到只播擋住；VR 走進去 0.6 秒後被推出）。換階段（60%、25%）時：大招「萬兵齊發」— 背後扇形浮現 12 隻手各拿一把半透明武器（劍、Kopesh、斧、彎刀、單手斧、匕首，來自 `egyptian_weapons.glb`），每 0.75 秒一波 3 把飛向玩家（6 傷害），每把都能打回去（12 傷害，不受受擊冷卻限制），打回 60% 以上法老硬直 3 秒 → 結界打開 12 秒：出現 6 m 寬走廊 + 3 把擺錘 Kopesh（12 傷害、不擊退），本體受傷 ×2，替身暫停出招 → 結束時衝擊波：結界附近的玩家淡黑閃現到 12 m 外（8 傷害）。桌面測試：B 跳 Boss、U 立刻放大招。小兵召喚先關掉（在 PharaohBoss → Phases 的 Start Minions 可開回）。全部用 CLI 驗證過（EditMode 279/279；PlayMode 29/29）
- **VR 操作備忘（讀 A 的 MeleeWeapon）**：沒有按鈕招架/反彈，抓著武器真的揮；刀刃速度 ≥ 1.2 m/s 才算一次揮擊，一次揮擊最長 0.6 秒、要慢到 0.45 m/s 才能再揮，同一揮擊對同一目標只算一次（不同飛刀算不同目標）→ 大招用一波一波的節奏；刀刃判定半徑 3.5 cm，所以飛行物 Collider 做大（0.35–0.6 m）
- **法老擲武器（2026-11-22）**：新招 WeaponThrow：手飛到法老前方側上方、掌中召喚半透明大匕首（1.1 秒變大）→ 後拉 → 翻轉飛向玩家胸口（9 m/s，15 傷害）；第 1/2/3 階段連丟 1/2/3 把、左右交替。用近戰武器打中 → 變藍彈回法老（35 傷害）。手放大到 13 倍。`EnemyProjectile` 反彈時若沒有啟用中的弱點，改瞄最大的身體 Collider（之前瞄 Eye 會從頭頂擦過）。驗證過：打中玩家、兩次反彈各扣 35（EditMode 274/274；PlayMode 29/29）
- **法老頭部橫斬 + 替身材質（2026-11-22）**：新招 HeadSlash：手刀在頭的高度橫斬，路線寬 4 m（跨一步躲不掉），要蹲下（頭低於 1.3 m）；預警是一條頭高的紅色光條。橫掃和橫斬的路線往法老那側斜 50°，手從玩家前方斜側出現（之前從正側面來，玩家看不到）。替身材質改用自寫的 `Game4026/SpectralGlow` shader（實心半透明 + 邊緣菲涅耳亮光 + 往上流動的光紋），原本的漸變是因為複製來的材質帶著 Default-Particle 圓形貼圖。驗證過：站著被斬 18、蹲下沒事（EditMode 270/270；PlayMode 29/29）
- **測試捷徑 + 英文 UI（2026-11-22）**：Play 時按 **B** 直接跳到法老（`DemoDebugKeys`，掛在場景的 DemoFlow 物件上）；或勾 DemoFlow 的 `Start At Boss`，每次開場（含重來）都直接進 Boss。Demo 的提示文字全部改英文。A 的 PR 由使用者自己 merge（遠端還沒有 A 的 PR）（EditMode 268/268）
- **Playable demo（2026-11-22 開始）**：`Assets/_Game/Scenes/Demo/Museum_Demo.unity`（複製自環境組的 `Museum_New`，原檔不動）。流程 `DemoFlow`：開場 3 秒 → 展廳 3 波（阿努比斯×2 → 阿努比斯×2+神諭 → 翼人+阿努比斯，同時最多 3 隻）→ 4 秒 → 法老 → 勝利；玩家倒下 → 4 秒後原地重來。遭遇戰生成的敵人一出場就追玩家（`Alert`）。桌面用 MockPlayer 測過整個流程（EditMode 259/259）。
- **法老替身 + 異空間（2026-11-22）**：展廳清完 → 淡出 → 傳送到 `BossRealm`（同場景 y=-200 的博物館建築複製品，沒有展示櫃，紫色局部 Volume、即時光、王座在 x≈24）。法老坐著不動（EnemyData_Pharaoh 移動速度 0、450 HP），背後的半透明金色替身（`PharaohStand`，暫用基本形狀 + XR Hands 的手放大 10 倍）出招：掌拍（手掌先立起面向玩家、地上紅圈預警 → 翻平拍下，拍完卡地 1.8 秒，打手 = 傷害 ×1.5 傳給法老）、橫掃（胸口高度掃過，往前/後一步或蹲下躲）、連拍（第 2 階段起，3 下）。打贏 5 秒後傳回展廳。桌面測過：傳送、出招、打手扣血、倒下重來（EditMode 266/266；PlayMode 29/29）
- **攻擊名額（M2.10，使用者追加）**：一打多時同時最多 2 個名額（翼人佔 2），只有玩家視線前方 ±70° 的敵人能出招，其他在 3.5 m 外繞圈等；設定在場景的 `AttackCoordinator`（EditMode 251/251；PlayMode 29/29）
- **翼人（M2.9，使用者追加）**：`Enemy_Birdman`（使用者的 birdman.fbx，就是規格裡的有翼女神伊西斯，菁英 220 HP），懸浮移動（`HoverLocomotion`），3 招：翼斬（左右輪流，左翼是腳本翻轉的動畫）、俯衝、翼盾（`EnemyGuard`：擋正面、擋 2 下反擊、重擊破盾、被打會舉盾）。打擊感的 hit stop 改成極慢動作並緩出（EditMode 244/244；PlayMode 26/26）
- 規格：2026-11-21 起以合併後的 `Docs/ENEMY_SYSTEM_SPEC.md` 為準（來自使用者的 `ENEMY_SYSTEM_SPEC_Edited.md`，名稱與合約改回現行的 `_Game` / `Game4026.*`）
- 里程碑：M1–M3、**M2.5（阿努比斯多招式）完成**；新規格的 M4（雕像移動、投射物反彈、WingShield、新敵人名單）還沒做（EditMode 201/201；PlayMode 16/16）
- 打擊感（M2.8，使用者追加）：所有敵人通用的 `EnemyHitFeedback`，打中時只讓敵人定格（卡肉）、抖動、閃白、噴火花、手把震動；招架/擊殺最重；數值在 `HitFeedback_Default` 資產調（EditMode 239/239；PlayMode 22/22）
- 黑曜石神諭（M2.7，使用者追加）：`Enemy_ObsidianOracle`（頭像雕像，無骨架彈跳），5 招：震地、頭槌、滾動、跳砸、鐳射眼；全部可反彈（EditMode 233/233；PlayMode 19/19）
- 招架（M2.6，使用者追加）：阿努比斯雙手持匕首，所有招式都能在擊中前 0.35 秒內用武器招架 → 整招取消、硬直 1.5 秒、受傷 ×1.5；刀尖光點提示招架時機（EditMode 214/214；PlayMode 17/17）
- 阿努比斯（`Enemy_AnubisGuard`）有 5 招：快速揮擊、反手揮擊（鏡像）、重擊（起手停格 0.8 秒）、突進斬（Jump Attack 動畫）、三連斬；其他敵人各一招，時間沿用舊數值
- 傷害橋接完成，含場景安裝器（Bridges）
- 模型與動畫：所有敵人（基底 Enemy_Placeholder 與四個 Variant、場景的巡邏示範）都用 Anubis 模型 + AC_Anubis + EnemyAnimator；各自弱點掛在骨頭上（衛兵/基底：頭，木乃伊/法老：胸口，塞克邁特：背後）。各種敵人目前外觀相同，只有大小不同。
- Git（2026-11-22）：攻擊名額已 commit 並 push 到 feature/enemy-ai，PR 頁面已開，**使用者還要按 Create pull request**
- 下一步：
  0. 使用者在 Museum_Demo 實際玩法老戰，調 `Enemy_Pharaoh` 上 `PharaohStand` 的 Settings（預警秒數、範圍、傷害、卡地秒數）與 `PharaohHand` 的 Damage Multiplier；VR 閃避方式使用者還要再想
  0c. 使用者會放法老替身模型進來：換掉 `Enemy_Pharaoh/Stand/Body`（佔位形狀）；攻擊的手是 `Enemy_Pharaoh/Hand/Model`
  0d. **文物流程（使用者 2026-11-22 定的，等 A 合併後做）**：每波一隻精英，死掉時原地留下它的文物（A 的 RestorableArtifact），玩家放回展台（ArtifactDisplayTrigger → ArtifactEventBridge）後下一波才開始；三件都歸位 → 法老。生成點改成從對應展品出現（阿努比斯像 x=-24、Case_wMummy x=23、SphinxPharaoh x=11.5）。A 的 `MuseumEncounterPlan.zh-TW.md`（在 A 的分支）也是同樣的「三件文物各對應一隻被詛咒展品」
  0a. Demo 收尾：HUD 是 OnGUI（只有桌面看得到），VR 要改成世界空間文字；等 A 合併 `feature/vr-interaction` 後在 Museum_Demo 換成 VR 玩家（PlayerTarget、DamageBridgeInstaller），A 的 `PlayerHealth` 沒有 ResetHealth，重來時不會補血（要跟 A 談一個玩家血量合約）
  00. 使用者在 EnemyAI 場景放 3 隻阿努比斯看繞圈/輪流出招（繞圈還沒親眼確認過）
  00b. 一打多後續：錯開出場 + 出場後一段時間不攻擊、蓄力音效（使用者找素材）、波次重排
  0. 使用者在 EnemyAI 場景實際看阿努比斯 5 招的手感，調招式資產數值（冷卻、權重、預警、傷害）
  0b. 新規格 M4：StatueLocomotion / StatuePart、EnemyProjectile（反彈）、WingShield、卡雕像與伊西斯（需要模型）
  1. 確認 PR #3 建立並合併
  2. 各種敵人換成自己的模型（木乃伊、塞克邁特、法老）；人形 Mixamo 模型可沿用 AC_Anubis 與 EnemyAnimator，只要在 Variant 裡換掉 Model
  3. 和 A 整合：在 A 的場景放 PlayerTarget（務必指定 head）、DamageBridgeInstaller、EncounterDirector + ArtifactEventBridge，戴頭盔實測；確認 A 的武器 targetMask 包含敵人 Collider 的 Layer
  4. 法老招式細節（PharaohBoss.OnPhaseEntered 的 TODO）、數值平衡（冷卻、前搖、血量）
  5. 可選：身體 Collider 改成掛在骨頭上（現在是固定膠囊，不會跟著彎腰或倒地）；生成/死亡特效、音效
- 給 A 的對接說明：`Notes For Groupmates/NOTES_FOR_A.md`
