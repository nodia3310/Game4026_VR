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
- EnemyHealthModel — Scripts/Enemy/Core/EnemyHealthModel.cs — 純 C# 受擊規則：弱點倍率、受擊冷卻（可略過：一波反彈飛刀）、硬直判定、死亡、Reset
- EnemyDamageResult — Scripts/Enemy/Core/EnemyDamageResult.cs — 一次有效受擊的結果（實際扣血、擊中點、弱點、硬直、擊殺）
- EnemyHealth — Scripts/Enemy/Core/EnemyHealth.cs — MonoBehaviour + IDamageable，包裝 Model，提供 Damaged/Died 事件、ResetState、IsInvulnerable；有 EnemyGuard 時先問它要不要擋
- WeakPoint — Scripts/Enemy/Core/WeakPoint.cs — 掛在弱點 Collider 子物件上，以 isWeakPoint=true 轉發給父物件的 EnemyHealth，附 Gizmo
- EnemyController — Scripts/Enemy/Core/EnemyController.cs — 敵人 AI 主元件：狀態機 + NavMeshAgent + 每 0.2 秒視線檢查，實作 IEnemyContext；可選 Animator；HitStop(秒, 倍率) 慢動作（AI 時鐘扣掉少走的時間、TimeScale 同步到 NavMesh 速度與 Animator、LocalTime、DeltaTime、HitStopChanged）；ForceAttack（反應招式，例如被打舉盾）；Alert / IsAlerted（遭遇戰敵人任何距離都追）；攻擊名額（MustWaitForTurn、CircleTarget 繞圈等待）
- EnemyAnimator — Scripts/Enemy/Core/EnemyAnimator.cs — 依 AI 狀態切換 Animator（Locomotion 混合樹／Hit／Death）；每個攻擊步驟交叉淡入招式的狀態（鏡像用 `_Mirror`），預警時起手停格（AttackSpeed=0），最後一下的收招等動畫播完（EnemyAttack.RecoveryOverride）
- EnemyAttack — Scripts/Enemy/Core/EnemyAttack.cs — 攻擊元件（規格的 EnemyAttackRunner）：用 AttackSelector 選招、AttackRunner 出招；BaseAttackSpeed（種類的基本攻速）× SpeedMultiplier = EffectiveSpeed；命中判定（扇形／AreaSlam 圓形）、突進用 NavMeshAgent.Move；事件 AttackStarted/Telegraph/StepStarted/Resolved/Finished/Cancelled；SpeedMultiplier；Queue（下一招指定）、IsGuarding / EndGuard（翼盾）
- EnemyWeapon — Scripts/Enemy/Core/EnemyWeapon.cs — 掛在敵人武器（刀身 Collider）上的 IDamageable：招架窗內被 A 的武器打到 → EnemyAttack.TryParry；窗外只擋住、不傷本體；leftHand 標記左右手；事件 Deflected/Blocked
- AttackIndicator — Scripts/Enemy/Core/AttackIndicator.cs — 刀尖光點提示：預警時小橘光、招架窗打開瞬間亮閃、招架成功白閃；跟著揮動那隻手的武器（鏡像招式 = 左手）
- StatueLocomotion — Scripts/Enemy/Core/StatueLocomotion.cs — 無骨架雕像的程式動作：彈跳走路、各招（Hop/Roll/Slam/Shoot）的預警與動作、生成升起、硬直搖晃、死亡倒下沉地；只動 Model 子物件；Impacted 事件（升起/滾動/落地/頭槌/倒地，給音效）
- HoverLocomotion — Scripts/Enemy/Core/HoverLocomotion.cs — 有翼、沒有走路動畫的敵人（翼人）：只動 Visual 子物件；懸浮上下晃、移動前傾、轉彎側傾、翼斬前後傾、俯衝（預警時升高再斜衝下來）、舉盾後仰、從天降下生成、硬直晃動、墜地沉入
- EnemyGuard — Scripts/Enemy/Core/EnemyGuard.cs — 翼盾：Guard 步驟期間擋下正面攻擊（Blocked 事件）、擋 2 下就提前反擊、單擊 ≥ 30 破盾（硬直 + 受傷 ×1.5，Broken 事件）、被打時機率舉盾；EnemyHealth 每次受擊先問它
- HitStopCurve — Scripts/Enemy/Feedback/HitStopCurve.cs — 慢動作曲線：前 60% 維持慢速，之後線性回到 1；算出某段真實時間內敵人時鐘走了多少（封閉解）
- EnemyHitFeedback — Scripts/Enemy/Feedback/EnemyHitFeedback.cs — 通用打擊感：受擊/招架/擋刀/擋盾/破盾/擊殺時讓敵人 HitStop 慢動作、模型沿受擊方向抖動、疊 M_HitFlash 閃白、噴火花、播音效、發 HitImpacts
- HitFeedbackProfile — Scripts/Enemy/Feedback/HitFeedbackProfile.cs — ScriptableObject：慢動作倍率；每種 HitKind（一般/弱點/硬直/擊殺/招架/擋刀/反彈/破盾）的慢動作秒數、震動、閃光、手把震動、火花；傷害縮放；閃光材質、火花 Prefab、音效
- EnemySfx — Scripts/Enemy/Feedback/EnemySfx.cs — 非受擊音效：生成時的召喚聲、雕像 StatueLocomotion.Impacted 的石頭聲；CreateSource 建 3D 一次性 AudioSource（擺錘也用）
- HitImpacts — Scripts/Enemy/Feedback/HitImpacts.cs — HitKind、HitImpact，全域同步事件 Played + Count/Last（橋接用來知道剛剛那一下的手感）
- HitSparkPool — Scripts/Enemy/Feedback/HitSparkPool.cs — 每種火花 Prefab 共用一個粒子系統，Emit 噴出
- EnemyGeometry — Scripts/Enemy/Core/EnemyGeometry.cs — 忽略高度的水平距離與扇形判定
- AssemblyInfo — Scripts/Enemy/AssemblyInfo.cs — InternalsVisibleTo Enemy EditMode / PlayMode 與 Bridges EditMode 測試（EnemyController.Tick、AttackDefinition.Create、EnemyData.SetAttacks）

## Enemy Combat（`Assets/_Game/Scripts/Enemy/Combat/`，`Game4026.Enemy`）
- AttackTokens — Scripts/Enemy/Combat/AttackTokens.cs — 一打多的攻擊名額（全域）：最多幾個同時出招、菁英佔較多、只有玩家視線內的敵人能拿；InView / AngleFromView
- AttackCoordinator — Scripts/Enemy/Combat/AttackCoordinator.cs — 場景元件：攻擊名額的開關、數量、視線角度（沒放就用預設 2 個 / 70°）
- AttackType — Scripts/Enemy/Combat/AttackType.cs — Melee / Lunge / Combo / Projectile / AreaSlam / Charge（未實作，不會被選到）/ Guard（舉盾，不傷人）
- AttackDefinition — Scripts/Enemy/Combat/AttackDefinition.cs — ScriptableObject，一招一份：動畫狀態、速度、鏡像、預警停格、擊中秒數、收招、距離、冷卻、權重、傷害、判定範圍、突進、連段步驟、砸地半徑
- AttackSelector — Scripts/Enemy/Combat/AttackSelector.cs — 純 C#：依距離、各招冷卻、全域冷卻、權重選招；HoldRange（沒招可出時停下的距離）
- EnemyProjectile — Scripts/Enemy/Combat/EnemyProjectile.cs — 有飛行時間的光彈：直線飛、打到玩家（身體到頭線段）或環境就結束；被近戰武器打到就反彈飛回發射者弱點（沒有弱點 → 最大的身體 Collider；微追蹤、×1.5 速）
- WeaponGrip — Scripts/Enemy/Combat/WeaponGrip.cs — 飛刀的握柄位置（本地座標，長度沿 Z），神之手握在這裡、刀刃朝上
- ProjectileSpin — Scripts/Enemy/Combat/ProjectileSpin.cs — 讓投射物的外觀子物件旋轉（擲出的武器翻轉）
- EnemyProjectilePool — Scripts/Enemy/Combat/EnemyProjectilePool.cs — 每種光彈 Prefab 一個 ObjectPool，同時上限 8 個
- ParryProbe / IParryProbe — Scripts/Enemy/Combat/ParryProbe.cs — 飛行武器（EnemyProjectile）問「這個放大形狀裡有沒有擋著的武器」；沒裝 probe 就什麼都不做（Bridges 實作）
- IParryTarget — Scripts/Enemy/Combat/IParryTarget.cs — 可被招架/反彈的東西（EnemyWeapon、EnemyProjectile）；箭改呼叫 TakeUnparryableHit
- AttackRunner — Scripts/Enemy/Combat/AttackRunner.cs — 純 C# 出招時間軸（起手 → 預警停格 → 揮下 → 判定 → 收招／連段下一下）；IAttackHost、AttackStepTiming；SkipToHit（翼盾提前結束）

## Enemy Flow（`Assets/_Game/Scripts/Enemy/Flow/`，`Game4026.Enemy`）
- DemoFlowModel — Scripts/Enemy/Flow/DemoFlowModel.cs — 純 C# demo 流程：Intro（可等 MarkReady 才倒數）→ Hall → BossIntro → Boss → Victory；玩家倒下 → Defeat → Restart；DemoPhase / DemoAction
- DemoFlow — Scripts/Enemy/Flow/DemoFlow.cs — 場景元件：第一次開場等玩家準備好（MarkPlayerReady 或離起點）；跟著 PlayerTarget.Current 換綁玩家；開展廳遭遇戰、清完開 Boss、倒下時清場並原地重來（重設遭遇戰、補血、移回 PlayerStart）；Boss 前淡出傳送到異空間（realmArrival），勝利後傳回；SkipToBoss / Start At Boss 測試捷徑；傳送保留頭盔偏移；OnGUI 提示文字
- PlayerRig — Scripts/Enemy/Flow/PlayerRig.cs — 移動玩家（CharacterController 所在物件，保留頭盔偏移）與淡黑閃現 Blink（不暈的推回/傳送）
- ScreenFader — Scripts/Enemy/Flow/ScreenFader.cs — 玩家頭前 0.2 m 的黑色面片淡出淡入（桌面與頭盔都能用）
- PlayerVitals — Scripts/Enemy/Flow/PlayerVitals.cs — 暫時的玩家血量轉接：反射讀 `CurrentHealth`/`MaxHealth`、`ResetHealth()` 或 private setter 補滿；轉接元件（A 的桌面模式）讀它持有的 receiver
- DemoMusic — Scripts/Enemy/Flow/DemoMusic.cs — 掛在 Museum_Demo 的 DemoFlow 物件：展廳戰鬥（Hall）播 `Audio & SFX/Normal battle.mp3`、BossIntro/Boss 播 `Boss.mp3`，其他階段靜音；兩個 2D 循環 AudioSource 交叉淡入（Volume 0.45、Fade Seconds 1.5）；兩首設 Streaming
- DemoWorldHud — Scripts/Enemy/Flow/DemoWorldHud.cs — 頭盔 HUD：DemoFlow 目標 + HP 的世界空間 TextMesh，浮在頭前、轉頭時慢慢跟上、穿牆可見；只在 XR 裝置啟用時顯示
- PlayerBodyAnchor — Scripts/Enemy/Flow/PlayerBodyAnchor.cs — XR 玩家的身體中心：跟著頭的水平位置、在眼睛下 0.45 m（給 PlayerTarget.Body；Museum_Demo 改用 A 的 TrackedPlayerHurtbox 後目前沒用到）

## Enemy Boss（`Assets/_Game/Scripts/Enemy/Boss/`，`Game4026.Enemy`）
- BossPhase — Scripts/Enemy/Boss/BossPhase.cs — 一個階段的 Inspector 設定：血量門檻、攻速倍率、無敵秒數、是否召喚小兵、要啟用的物件
- BossPhaseTracker — Scripts/Enemy/Boss/BossPhaseTracker.cs — 純 C#：依血量比例選階段，只往前，可一次跨多個階段
- PharaohBoss — Scripts/Enemy/Boss/PharaohBoss.cs — 法老階段層：PhaseChanged、換階段無敵、子物件 EncounterController 召喚小兵、啟用弱點、攻速；招式由 PharaohStand 依 CurrentPhase 出
- PharaohStand — Scripts/Enemy/Boss/PharaohStand.cs — 法老背後的半透明替身（左右兩隻攻擊手，出招輪流換手、閒置的手回休息位）：雙手合擊（前方左右等待 → 合掌，前後一步可躲）、掌拍（預警手掌立起 + 地上紅圈 → 拍下 → 卡地）、橫掃（蹲下或一步躲）、頭部橫斬（頭高光條預警，只能蹲下躲）、擲武器（掌中召喚匕首 → 翻轉丟出，可反彈，1/2/3 把）、連拍；換階段大招萬兵齊發（12 隻手拿武器、每波 3 把、打回 60% 硬直，之後開結界）、結界打開時暫停；QueueUltimate；側面攻擊路線斜向法老讓玩家看得到；跟著 Boss 的慢動作時鐘與階段攻速；Settings 調數值
- PharaohArena — Scripts/Enemy/Boss/PharaohArena.cs — 法老結界（半徑 5 m、VR 走進去會被推出）、Open（換階段大招後：結界下、從天花板吊下的實體擺錘鐮刀、受傷 ×2；走廊板已移除）、HangPendulums（找天花板、算臂長）、擺錘揮砍音效（Sound 欄位）、Close 衝擊波淡黑閃現推回；Current、IsOpen
- ArenaBarrier — Scripts/Enemy/Boss/ArenaBarrier.cs — 掛在結界與走廊牆上的 IDamageable：吸收武器/箭（只播擋住），不讓傷害傳到法老
- PendulumMath / BarragePlan — Scripts/Enemy/Boss/BossMechanics.cs — 純 C#：擺錘角度與刀刃位置、PassesBottom（揮砍音效時機）；萬兵齊發的扇形位置與每波的發射順序
- HandPose — Scripts/Enemy/Boss/HandPose.cs — 手指彎曲（0 張開 ~ 1 握拳），緩動、各手指稍有延遲；XR Hands 骨架
- PharaohHand — Scripts/Enemy/Boss/PharaohHand.cs — 替身的大手：卡地時才開 Collider、可被打，傷害 ×damageMultiplier 傳給法老 EnemyHealth；其他時候被打只播擋住；不推擠玩家
- StandMove / StandMoveSelector / StandGeometry — Scripts/Enemy/Boss/StandMoves.cs — 純 C#：依階段選招（第 2 階段起有連拍、同種側面攻擊不連續）、掌拍/橫掃/橫斬（LaneHits）命中判定、斜向橫掃路線

## Enemy Spawning（`Assets/_Game/Scripts/Enemy/Spawning/`，`Game4026.Enemy`）
- WaveDefinition — Scripts/Enemy/Spawning/WaveDefinition.cs — ScriptableObject：一波 = 多組「敵人 prefab + 數量」、spawnInterval、startDelay
- EncounterSchedule — Scripts/Enemy/Spawning/EncounterSchedule.cs — 純 C# 波次排程：上一波全滅後等 startDelay 才開下一波，波內每 spawnInterval 生一隻，遵守 maxAlive
- SpawnPoint — Scripts/Enemy/Spawning/SpawnPoint.cs — 生成點標記：確認在 NavMesh 上、離玩家 ≥3 公尺，附 Gizmo
- EnemyPoolManager — Scripts/Enemy/Spawning/EnemyPoolManager.cs — 每種 prefab 一個 ObjectPool，可預熱；敵人 Despawned 時自動回收；CreatedCount 用來確認有沒有重用
- EncounterController — Scripts/Enemy/Spawning/EncounterController.cs — 一個展廳的遭遇戰：StartEncounter、ForceClear、Started/Cleared、觸發區自動開始；alertOnSpawn（生成的敵人直接追）、TotalEnemies
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
- EnemyDebugLabel — Scripts/Enemy/Debug/EnemyDebugLabel.cs — 只在 Editor 顯示：敵人頭上的狀態、正在出的招式（連段第幾下）與血量
- DemoDebugKeys — Scripts/Enemy/Debug/DemoDebugKeys.cs — Demo 測試捷徑：B 跳到法老（DemoFlow.SkipToBoss）、U 法老立刻放大招（PharaohStand.QueueUltimate）
- DebugArtifactButton — Scripts/Enemy/Debug/DebugArtifactButton.cs — 按 1/2/3 模擬 Rosetta/Canopic/Mask 歸位，左上角顯示進度

## 資產與場景
- EnemyData_Default — Assets/_Game/Data/Enemies/EnemyData_Default.asset — 規格 4.1 預設數值
- Enemy_Placeholder — Assets/_Game/Prefabs/Enemies/Enemy_Placeholder.prefab — 佔位敵人基底（膠囊身體 + 方塊頭弱點，無 Animator）
- Enemy_Mummy / Enemy_AnubisGuard / Enemy_Sekhmet / Enemy_Pharaoh — Assets/_Game/Prefabs/Enemies/*.prefab — 以 Placeholder 為基底的 Prefab Variant（弱點：胸口 / 頭 / 背後 / 第三階段胸口）；Pharaoh 含 PharaohBoss、Model/Sphinx（人面獅身像）、子物件 Minions 遭遇戰、PharaohStand（Stand/Body/Mask 金面具替身、Hand、SlamMarker、SweepMarker）
- EnemyData_Mummy / _AnubisGuard / _Sekhmet / _Pharaoh — Assets/_Game/Data/Enemies/*.asset — 四種敵人的暫定數值（規格第 5 節）
- 招式資產 — Assets/_Game/Data/Enemies/Attacks/*.asset — 阿努比斯 5 招（Anubis_QuickSlash / Backhand / HeavySlash / LungeSlash / TripleSlash + TripleSlash_1–3 步驟）；其他敵人各一招（Default_Swing、Mummy_Swing、Sekhmet_HeavySwing、Pharaoh_Swing，數值與時間沿用舊的單招）
- Enemy_AnubisGuard 的武器 — 雙手 `mixamorig:Right/LeftHand/Weapon_Dagger`（dagger_low_poly.glb + CapsuleCollider + EnemyWeapon + Tip），根物件掛 AttackIndicator
- Enemy_ObsidianOracle — Assets/_Game/Prefabs/Enemies/Enemy_ObsidianOracle.prefab — 黑曜石神諭：Model（aztec_statue_sculpture.glb，1.5 m）、Body（BoxCollider + EnemyWeapon 傷害穿透）、Forehead_WeakPoint（金色紋章）、Eye（發射點）；StatueLocomotion、AttackIndicator
- EnemyData_ObsidianOracle / Oracle_* — Assets/_Game/Data/Enemies/ — 80 HP；5 招：Stomp、Headbutt、Roll、LeapSlam、LaserEye
- Projectile_Barrage{Sword,Khopesh,Axe,Sabre,HandAxe,Dagger} — Assets/_Game/Prefabs/Enemies/Projectiles/ — 萬兵齊發的半透明武器（egyptian_weapons.glb / dagger，球形 Collider、打回 12 傷害）
- M_SpectralBody — Assets/_Game/Prefabs/Enemies/Materials/ — 替身身體（同 SpectralGlow，render queue 3010：比手晚畫，手在身體後面時看得到）
- M_PendulumBronze — Assets/_Game/Prefabs/Enemies/Materials/ — 擺錘的青銅臂與吊座（URP Lit，實體）
- M_SpectralBarrier — Assets/_Game/Prefabs/Enemies/Materials/ — 結界與走廊牆（SpectralGlow，關掉 Solid Look）
- Projectile_SpectralDagger — Assets/_Game/Prefabs/Enemies/Projectiles/Projectile_SpectralDagger.prefab — 法老替身擲出的半透明大匕首（翻轉、可反彈 35 傷害、反彈後變藍）
- Projectile_LaserBolt — Assets/_Game/Prefabs/Enemies/Projectiles/Projectile_LaserBolt.prefab — 0.6 m 紅色光彈（M_LaserBolt + M_AttackGlint 光暈，CapsuleCollider 讓武器打得到）
- M_LaserBolt / M_WeakPointEmblem — Assets/_Game/Prefabs/Enemies/Materials/ — URP Unlit 紅 / 金
- HitFeedback_Default — Assets/_Game/Data/Enemies/HitFeedback_Default.asset — 所有敵人與光彈共用的打擊感設定
- M_HitFlash / M_HitSpark — Assets/_Game/Prefabs/Enemies/Materials/ — 閃白疊層（URP Unlit 透明加算）/ 火花（Particles Unlit 加算）
- FX_HitSpark — Assets/_Game/Prefabs/Enemies/FX/FX_HitSpark.prefab — 拉長的小火花粒子（不自動發射，由 HitSparkPool Emit）
- Enemy_Birdman — Assets/_Game/Prefabs/Enemies/Enemy_Birdman.prefab — 翼人（規格的有翼女神伊西斯，菁英，1.1 倍）：Visual（HoverLocomotion 移動）/ Model（birdman.fbx + AC_Birdman）、雙翼骨頭上的 EnemyWeapon + 膠囊（傷害穿透）、頸骨上的頭部弱點、Body、Eye；EnemyGuard、EnemyAnimator、AttackIndicator、EnemyHitFeedback
- EnemyData_Birdman / Birdman_* — Assets/_Game/Data/Enemies/ — 220 HP；翼斬（左右）、俯衝、翼盾（Birdman_WingGuard = Birdman_Guard + Birdman_GuardCounter）
- AC_Birdman / Birdman_Attack_Mirror — Assets/_Game/Animations/Enemy/Birdman/ — Locomotion（Idle）、Attack、Attack_Mirror（腳本翻轉的左翼版）、Block、Hit、Death；攻擊與 Block 速度綁 AttackSpeed
- M_AttackGlint — Assets/_Game/Prefabs/Enemies/Materials/M_AttackGlint.mat — 提示光點材質（URP Particles/Unlit 加算、Default-Particle 貼圖）
- AC_Anubis — Assets/_Game/Animations/Enemy/Anubis/AC_Anubis.controller — Locomotion、Attack（QuickerAttack）、Attack_Mirror（鏡像）、JumpAttack（突進斬）、Hit、Death；攻擊狀態速度綁 AttackSpeed
- Wave_Hall_1 / Wave_Hall_2 / Wave_Boss_Pharaoh / Wave_Boss_Minions — Assets/_Game/Data/Enemies/*.asset — 木乃伊×3 / 衛兵+女神像（延遲 2 秒）/ 法老×1 / 木乃伊×2（Boss 召喚）
- Museum_Demo — Assets/_Game/Scenes/Demo/Museum_Demo.unity — playable demo 場景（複製自 Museum_New）：建築與展示櫃加了 MeshCollider、NavMesh（Museum_Demo/NavMesh-Navigation.asset）、A 的 VR 玩家（Museum VR Setup：VR Player + TrackedPlayerHurtbox、背包、桌面模式、受傷紅框）、MuseumGuardDesk、Museum Floor Confirmation、Guard Desk Weapon Stand（斧、弓）、DemoFlow 上的 MuseumDemoRun、VR Services（DamageBridgeInstaller + DemoStartOnGrab）、MockPlayer（停用）、EnemyPool、SpawnPoints_Hall、SP_Throne、Encounter_Hall / Encounter_Boss、DemoFlow、AttackCoordinator；`BossRealm`（y=-200 的建築複製品、王座、即時光、RealmVolume、RealmArrival、SP_Throne）
- M_ScreenFade / M_Throne / RealmVolume — Assets/_Game/Scenes/Demo/Museum_Demo/ — 淡出材質、王座材質、異空間的局部後處理（紫色、暗角）
- M_SpectralGold / M_StandTelegraph — Assets/_Game/Prefabs/Enemies/Materials/ — 替身的神聖半透明金色（SpectralGlow shader）/ 預警紅光
- SpectralGlow — Assets/_Game/Prefabs/Enemies/Materials/Shaders/SpectralGlow.shader — URP 無光照 shader：深度預寫 + 半透明身體 + 菲涅耳邊光 + 光紋（不需要 HDR/Bloom，支援 VR single-pass instanced）
- Wave_Demo_1~3 / Wave_Demo_Boss — Assets/_Game/Data/Enemies/Demo/ — 阿努比斯×2 / 阿努比斯×2+神諭 / 翼人+阿努比斯 / 法老
- EnemyAI 測試場景 — Assets/_Game/Scenes/Test/EnemyAI.unity — AttackCoordinator、地板 + 兩面牆（已烘焙 NavMesh）、巡邏點、Enemy_PatrolDemo、MockPlayer、EnemyPool（預熱 4 隻）、3 個生成點、Encounter_Hall_Rosetta（觸發區）、Encounter_Boss、EncounterDirector（+ DebugArtifactButton、ArtifactEventBridge）

## Bridges（`Assets/_Game/Scripts/Bridges/`，`Game4026.Bridges`，引用 Contracts + Interaction + Enemy）
- ImpactDamageCurve — Scripts/Bridges/ImpactDamageCurve.cs — 速度 → 基礎傷害（速度 × 係數，限制在上下限之間），含近戰／箭預設值
- DamageDispatch — Scripts/Bridges/DamageDispatch.cs — 對命中 Collider 找 IDamageable 並呼叫 TakeDamage（isWeakPoint=false）；allowParry=false 時（箭）忽略 EnemyWeapon
- MeleeDamageBridge — Scripts/Bridges/MeleeDamageBridge.cs — 掛在 A 的 MeleeWeapon 旁，聽 Hit 事件扣血；依這一下的 HitImpact 震動握武器的手把（hapticScale）；Rumble(HitImpact) 給 ParryAssist 用
- ParryAssist — Scripts/Bridges/ParryAssist.cs — 掛在 EnemyWeapon 旁（阿努比斯雙匕首、翼人雙翼骨頭、神諭 Body）：招架窗內每個物理幀查比武器 Collider 大一圈的範圍（Radius Padding 0.13、Length Padding 0.15；骨頭上沒 Collider 就用屬於它的子 Collider），玩家握著的近戰武器在裡面就招架（不用揮）；雙持只看這一步揮的那隻手；只是查詢、不擋玩家攻擊；Gizmo 顯示範圍；幾何用 ParryZone
- ParryZone — Scripts/Bridges/ParryZone.cs — 把 Collider 形狀（Capsule/Box/Sphere，停用的也行）放大一圈做 Overlap 查詢、找符合條件的 Collider、畫 Gizmo
- HeldWeaponParryProbe — Scripts/Bridges/HeldWeaponParryProbe.cs — 實作 IParryProbe、裝成 ParryProbe.Current（DamageBridgeInstaller 開場自動加）：放大範圍內有玩家握著的近戰武器，且武器比玩家身體（Body–Head 線）更靠近飛來的武器（Front Margin 0.1）才算；成功後讓握武器的手把照反應震動
- ArrowDamageBridge — Scripts/Bridges/ArrowDamageBridge.cs — 掛在 A 的箭上（由安裝器自動加），聽 Impact 事件扣血
- ArtifactEventBridge — Scripts/Bridges/ArtifactEventBridge.cs — 訂閱場景裡所有 A 的 ArtifactDisplayTrigger.OnArtifactRestored，轉給 EncounterDirector
- DamageBridgeInstaller — Scripts/Bridges/DamageBridgeInstaller.cs — 場景元件：開場時幫所有 MeleeWeapon 加橋接，每次射擊後幫新的箭加橋接

## Tests（`Assets/_Game/Tests/EditMode/Enemy/`，`Game4026.Enemy.EditModeTests`）
- EnemyHealthModelTests — Tests/EditMode/Enemy/EnemyHealthModelTests.cs — 受擊規則單元測試（倍率、冷卻、硬直、死亡、Reset、無效傷害）
- EnemyHealthComponentTests — Tests/EditMode/Enemy/EnemyHealthComponentTests.cs — 模擬 A 的呼叫路徑：Collider → GetComponentInParent<IDamageable> → TakeDamage
- AttackTokensTests — Tests/EditMode/Enemy/AttackTokensTests.cs — 名額上限與釋放、菁英、關閉、視線範圍
- EnemyStateMachineTests — Tests/EditMode/Enemy/EnemyStateMachineTests.cs — 規格 4.4 的狀態轉換表（含 alerted 敵人）
- BossMechanicsTests — Tests/EditMode/Enemy/BossMechanicsTests.cs — 擺錘角度/刀刃位置、萬兵齊發每把只用一次且每波分散、扇形對稱
- PharaohStandTests — Tests/EditMode/Enemy/PharaohStandTests.cs — 替身選招（階段、不連續橫掃）、掌拍/橫掃命中（蹲下、跨步躲開）、橫掃路線
- DemoFlowModelTests — Tests/EditMode/Enemy/DemoFlowModelTests.cs — demo 流程：開場延遲、清場進 Boss、勝利、倒下重來
- FakeEnemyContext — Tests/EditMode/Enemy/FakeEnemyContext.cs — 測試用 IEnemyContext（不需要 NavMesh / Play Mode）
- EnemyAttackTests — Tests/EditMode/Enemy/EnemyAttackTests.cs — 出招時間、命中範圍與角度、AreaSlam、連段、突進距離、打斷、攻速倍率、預設招式
- EnemyProjectileTests — Tests/EditMode/Enemy/EnemyProjectileTests.cs — 飛行、打中玩家、壽命、反彈（一次、方向、速度）、箭不能反彈、線段距離
- DemoMusicTests — Tests/EditMode/Enemy/DemoMusicTests.cs — 每個 DemoPhase 對應的曲目
- ProjectileReflectAssistTests — Tests/EditMode/Enemy/ProjectileReflectAssistTests.cs — 飛行中問 ParryProbe：有擋著的武器就反彈、沒有就打中玩家、Reflect Assist 關掉不問、反彈後不再問
- AttackSelectorTests — Tests/EditMode/Enemy/AttackSelectorTests.cs — 距離篩選、權重、各招與全域冷卻、不支援的類型、HoldRange
- EnemyAnimatorTests — Tests/EditMode/Enemy/EnemyAnimatorTests.cs — 移動混合、預警停格速度、收招跟動畫長度
- EnemyGeometryTests — Tests/EditMode/Enemy/EnemyGeometryTests.cs — 水平距離與扇形
- HitFeedbackTests — Tests/EditMode/Enemy/HitFeedbackTests.cs — 受擊種類優先順序、慢動作隨傷害增加與上限、慢動作曲線、震動縮放與曲線、反彈光彈先慢動作、招架音效
- EnemyGuardTests — Tests/EditMode/Enemy/EnemyGuardTests.cs — Guard 步驟不傷人也不顯示預警、EndGuard 提前接反擊、正面範圍

## Tests（`Assets/_Game/Tests/EditMode/Bridges/`，`Game4026.Bridges.EditModeTests`）
- ImpactDamageCurveTests — Tests/EditMode/Bridges/ImpactDamageCurveTests.cs — 公式換算與上下限
- DamageBridgeInstallerTests — Tests/EditMode/Bridges/DamageBridgeInstallerTests.cs — 安裝器的 AttachMelee/AttachArrows（只用明確清單，不掃描開啟中的場景）
- EnemyWeaponBridgeTests — Tests/EditMode/Bridges/EnemyWeaponBridgeTests.cs — 近戰打刀身在窗內招架、窗外擋住、箭不能招架、本體不受傷
- ParryAssistTests — Tests/EditMode/Bridges/ParryAssistTests.cs — 放大範圍內握著的武器在窗內招架（含刀尖外）、窗前/範圍外/沒握著不招架、Padding 縮小、雙持只看揮的手（含鏡像）、骨頭武器用子 Collider 且不含弱點
- HeldWeaponParryProbeTests — Tests/EditMode/Bridges/HeldWeaponParryProbeTests.cs — 停用的 Box Collider 放大範圍內握著的武器算、範圍外/沒握著不算、大形狀已經壓到玩家時刀在旁邊或身後不算、刀伸向它才算
- DamageBridgeTests — Tests/EditMode/Bridges/DamageBridgeTests.cs — 自己組的 MeleeHit／箭命中 → 橋接 → 真的 EnemyHealth/WeakPoint
- EncounterScheduleTests / ArtifactProgressTests / EncounterDirectorTests — Tests/EditMode/Enemy/*.cs — 波次排程、文物去重、Director 路由
- BossPhaseTrackerTests — Tests/EditMode/Enemy/BossPhaseTrackerTests.cs — 階段門檻、只往前、跨階段、4 個階段

## Tests（`Assets/_Game/Tests/PlayMode/Enemy/`，`Game4026.Enemy.PlayModeTests`，引用 Unity.AI.Navigation）
- PlayModeWorld — Tests/PlayMode/Enemy/PlayModeWorld.cs — 共用 fixture：執行中烘焙 NavMesh、物件池、用程式建的敵人模板
- BossPlayModeTests — Tests/PlayMode/Enemy/BossPlayModeTests.cs — 法老三階段、無敵、小兵、弱點、跨階段、物件池重用
- EnemyGcTests — Tests/PlayMode/Enemy/EnemyGcTests.cs — 4 隻敵人追擊與攻擊 720 幀，敵人腳本 0 bytes GC
- ProjectilePlayModeTests — Tests/PlayMode/Enemy/ProjectilePlayModeTests.cs — 真的敵人開火：沒反彈打中玩家、反彈打中弱點（20 × 2.5）
- AttackSlotsPlayModeTests — Tests/PlayMode/Enemy/AttackSlotsPlayModeTests.cs — 三隻一個名額不會同時出招而且輪流、背後的敵人不出手、玩家轉身後才出手
- EnemyGuardPlayModeTests — Tests/PlayMode/Enemy/EnemyGuardPlayModeTests.cs — 真的敵人：擋正面不擋背後、擋 2 下立刻反擊、重擊破盾（硬直、×1.5）、被打後舉盾
- HitFeedbackPlayModeTests — Tests/PlayMode/Enemy/HitFeedbackPlayModeTests.cs — HitStop 讓 AI 時鐘與 NavMesh 變慢後緩回、出招順延少走的時間、受擊依種類觸發反應、震動不留位移
- ParryPlayModeTests — Tests/PlayMode/Enemy/ParryPlayModeTests.cs — 真的 EnemyController：招架窗內打武器 → 硬直、×1.5 傷害、結束後恢復、玩家沒被打到
- EncounterPlayModeTests — Tests/PlayMode/Enemy/EncounterPlayModeTests.cs — 執行中烘焙 NavMesh：遭遇戰開始到 Cleared、物件池重用、ForceClear、Boss 觸發與擊敗、生成點檢查
- M_TutankhamunMask — Assets/_Game/Character Models/Enemy/Meshy_AI_Mask_of_Tutankhamun_20260717_texture_fbx/ — 面具 URP Lit（底色、法線、合成的 _metallicSmoothness.png）；fbx 材質槽已 remap 到它
- DemoStartOnGrab — Scripts/Bridges/DemoStartOnGrab.cs — 玩家第一次抓起武器（MeleeWeapon/BowDrawAim 的 XRBaseInteractable）→ DemoFlow.MarkPlayerReady；等待中已握著武器也算；Refresh 給之後生成的武器
