# 敵人系統進度

> 每次任務結束時由 agent 更新。新的紀錄寫在最上面。

## 目前狀態
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

## 已做的決定
> 2026-11-18（M1–M4）的決定在 `PROGRESS_ARCHIVE.md`。
- 2026-11-22（一打多）：使用者覺得一打多很亂（同時出招、動畫看不出蓄力）。討論後先做攻擊名額 + 背後不准出手；錯開出場、蓄力音效（使用者之後找）、波次重排之後再做。
- 2026-11-21（翼人）：使用者在 Blender 做了 birdman（跪姿有翼女神，Idle / Attack / Block 三段動畫），要做成菁英敵人，只要 3 招：翼斬、俯衝、翼盾（太多招很難玩）。左翼攻擊用腳本鏡像。翼盾照使用者確認的設計：完全擋住正面，取代原規格的 10% / 減半；換成可以破盾，也可以招架盾反擊。
- 2026-11-21（慢動作）：使用者覺得完全定格看起來不自然，hit stop 改成極慢動作（0.08 倍速，前 60% 維持，之後緩回 1），秒數加倍。Profile 新增 `Hit Stop Time Scale`，設 0 就回到定格。
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

## 待確認問題
- 文物事件以哪個為準（A 回覆前先接 `ArtifactDisplayTrigger`）；`ArtifactRestorationTracker` 由誰建立、要不要也接。
- `MeleeWeapon` 每次揮擊、每個目標只算一次（目標以 `MeleeHitTarget` 根物件或 attachedRigidbody 為準）。敵人若掛了 `MeleeHitTarget` 或 Rigidbody，弱點和身體會被當成同一個目標，一次揮擊只算先碰到的 Collider；敵人 Collider 也要放在武器的 `targetMask` Layer 上。
- `IPlayerDamageReceiver` 目前沒有實作（玩家血量還沒指派）；M2 測試用 MockPlayer。

## 紀錄
> 較早的紀錄在 `PROGRESS_ARCHIVE.md`（平常不用讀）。

### 2026-11-22（攻擊名額）
- 新增 `AttackTokens`、`AttackCoordinator`；`EnemyData` 加 `Attack Slot Cost`、`Wait Distance`；`IEnemyContext` 加 `MustWaitForTurn`、`WaitDistance`、`CircleTarget`。
- `ChaseState`：沒名額又在等待距離內時，繞著玩家走而不是出招。
- `EnemyController`：選招前先檢查名額與視線，開始出招時拿名額，離開 Attack 狀態、停用或重生時還回去；繞圈時關掉 agent 的自動轉向，面向玩家。
- 翼人：佔 2 個名額，等待距離 4.5 m。EnemyAI 場景新增 `AttackCoordinator` 物件。
- 測試 fixture（`PlayModeWorld`）把視線角度設成 180°，因為既有測試常把玩家擺成背對敵人。
- EditMode 251/251、PlayMode 29/29。實際場景的繞圈樣子還沒親眼看過。
