# 敵人系統進度

> 每次任務結束時由 agent 更新。新的紀錄寫在最上面。

## 目前狀態
> 2026-11-22 整理。舊的詳細條目在 `PROGRESS_ARCHIVE.md`（「2026-11-22 目前狀態快照」）。

- **Playable demo**：`Assets/_Game/Scenes/Demo/Museum_Demo.unity`（複製自環境組的 `Museum_New`，原檔不動）。`DemoFlow`：開場 → 展廳 3 波（阿努比斯×2 → 阿努比斯×2+神諭 → 翼人+阿努比斯）→ 淡黑傳送到異空間 `BossRealm`（同場景 y=-200 的建築複製品）→ 法老 → 勝利後傳回；倒下 → 原地重來。玩家目前是 MockPlayer（桌面）。HUD 英文（OnGUI，只有桌面看得到）。
- **桌面測試捷徑**：Play 時 **B** 跳到法老、**U** 法老立刻放大招；DemoFlow 勾 `Start At Boss` 每次直接進 Boss。MockPlayer：WASD、右鍵轉視角、左鍵攻擊、F 招架、C 蹲下。
- **法老 Boss**（`Enemy_Pharaoh`，450 HP，坐在王座不動）：背後半透明金色替身（`PharaohStand`，身體是佔位形狀）用**左右兩隻大手**出招，閒置的手回休息位：
  - 掌拍（拍完卡地，打手 ×1.5 傳給法老）、連拍（第 2 階段起，左右輪流）、橫掃（往前後一步或蹲下）、頭部橫斬（只能蹲下）、擲武器（掌中召喚匕首，可打回去 35）、**雙手合擊**（前後一步可躲，合掌後兩手都能打）
  - 換階段（60%、25%）大招**萬兵齊發**：12 隻手各握一把武器（`egyptian_weapons.glb`）每 0.75 秒丟 3 把，每把都能打回去（12），打回 60% 法老硬直 → **結界**（半徑 5 m，平常進不去）打開 12 秒：天花板吊下的**實體擺錘大鐮刀**（3 把）、本體受傷 ×2 → 結束時衝擊波把附近玩家淡黑閃現推回 12 m。小兵召喚關閉中。
  - 材質：自寫 shader `Game4026/SpectralGlow`；繪製順序 手/武器 3000 → 替身身體 3010 → 結界 3020（避免手在身體後面消失）。
- **其他敵人**：阿努比斯（5 招、雙持匕首可招架）、黑曜石神諭（5 招、鐳射可反彈）、翼人菁英（翼斬/俯衝/翼盾）；打擊感（慢動作 hit stop）、攻擊名額（一打多最多 2 個、只有視線內能出手）、遭遇戰敵人生成就追玩家（`Alert`）。
- **VR 操作備忘（A 的 MeleeWeapon）**：沒有按鈕，抓著武器真的揮；刀刃 ≥ 1.2 m/s 算一次揮擊、最長 0.6 秒、慢到 0.45 m/s 才能再揮；同一揮擊對同一目標只算一次；刀刃判定半徑 3.5 cm → 飛行物 Collider 做大（0.35–0.6 m），大招一波一波來。
- **暫時做法**：玩家血量用 `PlayerVitals` 反射讀 `CurrentHealth` / 呼叫 `ResetHealth()`（合約沒有血量 API）；專案 URP（Performance URP Config）HDR 關、相機沒開後處理 → 沒有 Bloom，RealmVolume 實際沒生效（共用設定，沒改）。
- **測試**：EditMode 281/281、PlayMode 29/29。
- **Git**：feature/enemy-ai，demo 與法老相關改動已 commit + push（2026-11-22），PR 頁面已開，**使用者要按 Create pull request**；A 的 `feature/vr-interaction` 由使用者自己 merge。
- 下一步：
  1. 使用者在 Museum_Demo 實玩法老戰並調數值（`Enemy_Pharaoh` → `PharaohStand` / `PharaohArena` 的 Settings、`PharaohHand` 的 Damage Multiplier）。合擊的「前後一步可躲」只有單元測試，Play Mode 沒驗到。
  2. 使用者放法老替身模型：換 `Enemy_Pharaoh/Stand/Body`（佔位形狀）；手是 `Hand/Model`、`HandL/Model`（XR Hands 範例的手）。
  3. A 合併後接 VR：Museum_Demo 換成 A 的 XR 玩家（PlayerTarget 指定 head、DamageBridgeInstaller）；A 的武器 targetMask 要包含 Default（飛刀、結界的 Layer）；實測淡黑傳送/閃現推回；A 的 `PlayerHealth` 沒有 ResetHealth → 重來不會補血，要和 A 定玩家血量合約；HUD 改世界空間文字。
  4. 文物流程（使用者定的，等 A 合併後做）：每波一隻精英，死掉時原地留下它的文物（A 的 RestorableArtifact），放回展台（ArtifactDisplayTrigger → ArtifactEventBridge）後才開下一波；三件都歸位 → 法老。生成點改成從對應展品出現（阿努比斯像 x=-24、Case_wMummy x=23、SphinxPharaoh x=11.5）。A 的 `MuseumEncounterPlan.zh-TW.md` 也是這個方向。
  5. 打磨：替身招式/合擊/擺錘/結界音效、異空間燈光（現在紫燈讓地板看起來紫黑）、Bloom 要跟組員討論 Quest 效能。
  6. 舊項目：一打多錯開出場與蓄力音效、阿努比斯 5 招手感調整、規格 M4（StatuePart、WingShield、卡雕像）、其他敵人換模型、身體 Collider 掛骨頭。
- 給 A 的對接說明：`Notes For Groupmates/NOTES_FOR_A.md`

## 已做的決定
> 2026-11-18（M1–M4）的決定在 `PROGRESS_ARCHIVE.md`。
- 2026-11-22（法老）：使用者覺得展廳太擠，Boss 很難發揮 → 傳送到異空間（同一個博物館，但展示櫃全部消失）。我評估後用同場景遠處（y=-200）的建築複製品，不另開場景（不用改 Build Settings），傳送用淡黑避免暈。
- 2026-11-22（法老）：法老本體不動，靠背後的半透明替身攻擊（使用者的想法，像替身那樣）；使用者選「拍地後卡住時打手」當主要輸出方式，招式選掌拍、橫掃、連拍。VR 舒適：預警範圍小（掌拍半徑 1.1 m、橫掃寬 1.6 m），一步或蹲下就能躲；預警時手掌立起面向玩家，不用抬頭也看得到。
- 2026-11-22（法老）：使用者說第二隻手沒用 → 兩手都攻擊（StandMove.Clap 新增，同招不連續兩次）；使用者說走廊的半透明板不要。
- 2026-11-22（法老）：使用者要擺錘是實體、更大、掛在天花板甩 → 吊點在執行時往上找天花板（博物館模型改了也會跟著），刀刃最低點固定 1.2 m（胸口高，要抓時機，不是蹲下）。
- 2026-11-22（法老）：使用者覺得神之手拿武器動作一樣很僵硬 → 手指骨架握拳（XR Hands 骨架，關節繞本地 X 彎曲）、每隻手隨機化、依序出現、蓄力與跟進動作。武器握柄位置記在各飛刀 prefab 的 `WeaponGrip`（劍/Kopesh/彎刀的柄在 +Z，斧/單手斧/匕首在 −Z）。
- 2026-11-22（法老）：使用者希望玩家不要太容易接近本體 → 結界 + 換階段才打開；推開用淡黑閃現（使用者選，避免暈）；擺錘鐮刀走廊只在換階段出現；換階段大招「萬兵齊發」可反彈（使用者想要「超爽反彈」）。武器模型用 `Assets/Models/Weapons/egyptian_weapons.glb`（使用者同意用武器資產；這個檔案 A 的分支沒搬動，引用安全）。
- 2026-11-22（法老）：飛行物 Launch 時對玩家的 CharacterController IgnoreCollision（大武器的 Collider 會把玩家推走）；反彈命中走 `EnemyHealth.TakeProjectileDamage`（不受 0.3 秒受擊冷卻，一波 3 把都算）；反彈目標：弱點 → 最大的身體 Collider（排除結界/走廊牆）→ Eye。
- 2026-11-22（法老）：Boss 房間地板的紫黑格子不是材質錯誤，是博物館的黑白磁磚被紫色燈照的（白燈下就是黑白格）。
- 2026-11-22（法老）：使用者要「真的半透明、亮亮的、神聖」→ 自寫 URP shader（深度預寫讓身體像一個實心半透明體、菲涅耳邊光、光紋）。專案的 URP 資產（Performance URP Config）HDR 關、相機沒開後處理，所以沒有 Bloom，RealmVolume 其實也沒生效；這是共用設定，沒改。
- 2026-11-22（法老）：手的 Collider 只在卡地時打開，並對玩家的 CharacterController IgnoreCollision（之前會把玩家頂高）；A 的近戰武器忽略 Trigger，所以用一般 Collider。
- 2026-11-22（demo）：場景用 `Museum_New` 的複製品 `Museum_Demo`，避免和環境組衝突；只有一個展廳，分波次出怪，最後法老。玩家端先用 MockPlayer，等 A 合併後再接 VR。
- 2026-11-22（demo）：博物館模型本身沒有 Collider，在 Museum_Demo 裡幫建築與展示櫃加 MeshCollider（玻璃碎片沒加），NavMesh 用 Physics Colliders 烘焙。可用展廳約 x∈[-31,31]、z∈[-12,12]，兩端 x≈±32 以外是牆和封閉壁龕。
- 2026-11-22（demo）：玩家血量讀取：合約沒有血量 API，`PlayerVitals` 暫時用反射讀 `CurrentHealth`、呼叫 `ResetHealth()`（MockPlayer 都有，A 的 PlayerHealth 只有前者）。之後要和 A 定合約。
- 2026-11-22（demo）：`EncounterController.alertOnSpawn`（預設開）：生成的敵人任何距離都追、不會丟失目標，因為生成點常在偵測距離 12 m 外。
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

### 2026-11-22（雙手替身 + 合擊）
- `PharaohStand` 改成「目前出招的手」（SetActiveHand / UseHand、FollowIdleHand、UsesBothHands）、左手欄位 leftHand / leftHandRest / leftHeldWeapon、合擊 ClapTelegraph/Strike/Stuck 與 Clap 設定（含 clapForward 讓兩手在玩家前方等待）、LeftHand / ActiveHand 屬性；`StandMoveSelector` 權重加 Clap；`StandGeometry.ClapHits`。
- 資產：`Enemy_Pharaoh/HandL`（LeftHand.fbx ×13、HandPose、PharaohHand、BoxCollider、HeldWeapon）、`Stand/HandRestL`；移除 Stand/Body/LeftHand 裝飾手與 Arena/Corridor；新增 `M_SpectralBody`（queue 3010），M_SpectralBarrier queue 3020。
- EditMode 281/281（新增合擊 2 個）、PlayMode 29/29。尚未 commit。

### 2026-11-22（實體擺錘 + 神之手動作）
- 新增 `HandPose`（Boss/）、`WeaponGrip`（Combat/）；`PharaohArena.HangPendulums`（找天花板、算臂長）、擺錘設定改成 bladeBottomHeight / ceilingMargin / fallbackArm、命中沿刀刃擺動方向；`PharaohStand`：PoseBarrageSlot（隨機化、出現順序、握持、蓄力 WindUpFor、丟出後跟進淡出）、MainHandCurl、新設定 barrageWindUp / barrageHandSize。
- 資產：`M_PendulumBronze`；Pendulum_0~2 重建（Mount、Arm、Blade/Hub/Orient + 實體 Kopesh 4.5 m）；HandPose 加到 Hand/Model、Stand/Body/LeftHand、Stand/BarrageHand；6 個 Projectile_Barrage* 加 WeaponGrip。
- EditMode 279/279、PlayMode 29/29。尚未 commit。

### 2026-11-22（結界 + 鐮刀走廊 + 萬兵齊發）
- 新增 `PharaohArena`（結界、Open/Close、走廊與擺錘、衝擊波閃現推回）、`ArenaBarrier`（吸收打在結界/走廊牆的攻擊）、`BossMechanics.cs`（PendulumMath、BarragePlan）、`PlayerRig`（MoveTo / Blink，DemoFlow 也改用它）；`ScreenFader.Current`。
- `PharaohStand`：Barrage 步驟（BarrageSummon/Waves/After）、換階段排入大招、結界打開時暫停出招、`QueueUltimate()`；`EnemyProjectile`：`ShowHeld()`、Launch 忽略玩家碰撞、反彈目標排除 ArenaBarrier；`EnemyHealth.TakeProjectileDamage`、`EnemyHealthModel.TryApplyDamage(..., ignoreHitCooldown)`；`EnemyProjectilePool.MaxActive` 8 → 24；`ProjectileSpin` 只在飛行中旋轉；`SpectralGlow` 加 Solid Look（深度預寫）開關；`DemoDebugKeys` 加 U；DemoFlow 結界打開時提示 "The barrier is down! Strike the Pharaoh!"。
- 資產：`Projectile_Barrage{Sword,Khopesh,Axe,Sabre,HandAxe,Dagger}`、`M_SpectralBarrier`；`Enemy_Pharaoh` 加 Barrier、Arena（Corridor、Pendulum_0~2、ShockRing）、Stand/BarrageHand 樣板、PharaohArena；Phases 的 StartMinions 全關。
- EditMode 279/279（新增 BossMechanics 4 個、受擊冷卻 1 個）、PlayMode 29/29。尚未 commit。

### 2026-11-22（擲武器）
- `StandMove.WeaponThrow`、`StandMoveSelector.ThrowCount`；權重改成 Slam/Sweep/HeadSlash/WeaponThrow/Combo；`PharaohStand` 加 Summon/WindUp 步驟、Weapon throw 設定、`heldWeapon` / `weaponPrefab`。
- 新增 `ProjectileSpin`（Combat/）、`Projectile_SpectralDagger` prefab（dagger_low_poly.glb ×0.45、M_SpectralGold、膠囊 Collider 讓武器打得到）；`Enemy_Pharaoh/Hand/HeldWeapon`；手與左手 ×13。
- `EnemyProjectile.FindReflectTarget`：弱點 → 最大的啟用中實心 Collider → Eye。
- EditMode 274/274（新增 4 個）、PlayMode 29/29。尚未 commit。

### 2026-11-22（頭部橫斬 + SpectralGlow）
- `StandMove.HeadSlash`、`StandGeometry.LaneHits`、`SweepLane` 加 `angleTowardBoss`；選招權重改成 Slam/Sweep/HeadSlash/Combo，同一種側面攻擊不連續兩次；`PharaohStand` Settings 加 Head slash 區塊與 `sideLaneAngle`（50°）。
- 新增 `Prefabs/Enemies/Materials/Shaders/SpectralGlow.shader`（支援 single-pass instanced），`M_SpectralGold` 改用它。
- EditMode 270/270（新增 2 個）、PlayMode 29/29。尚未 commit。

### 2026-11-22（測試捷徑 + 英文 UI）
- `DemoFlowModel.SkipToBoss`、`DemoFlow.SkipToBoss` / `startAtBoss`（清掉展廳但不算擊殺，直接 BossIntro → 傳送）；新增 `DemoDebugKeys`（Debug/，B 鍵，左下角提示）；DemoFlow 的 HUD 文字改英文。
- EditMode 268/268（新增 SkipToBoss 2 個）。

### 2026-11-22（法老替身 + 異空間）
- 新增 `StandMoves.cs`（StandMove、StandMoveSelector、StandGeometry）、`PharaohHand`、`PharaohStand`（Scripts/Enemy/Boss/）；`ScreenFader`（Scripts/Enemy/Flow/）；`DemoFlow` 加異空間傳送（realmArrival、淡出淡入、勝利後傳回）、重來時淡黑；`MockPlayer` 加 C 蹲下。`PharaohBoss.OnPhaseEntered` 的 TODO 改成說明招式在 PharaohStand。
- `Enemy_Pharaoh` 加 Stand（Body 佔位形狀、LeftHand、HandRest）、Hand（RightHand.fbx ×10 + BoxCollider + PharaohHand）、SlamMarker、SweepMarker、PharaohStand；`EnemyData_Pharaoh` 移動 0、血量 450。
- 材質：`M_SpectralGold`、`M_StandTelegraph`（Prefabs/Enemies/Materials/，複製自 M_HitFlash 的加算材質）；`M_ScreenFade`、`M_Throne`、`RealmVolume`（Scenes/Demo/Museum_Demo/）。
- Museum_Demo：`BossRealm`（建築複製品、王座、3 盞即時光、RealmVolume、RealmArrival x=12、SP_Throne 移進來 x=24.4），NavMesh 重新烘焙（兩區一起）。
- EditMode 266/266（新增 PharaohStandTests 7 個）、PlayMode 29/29。

### 2026-11-22（playable demo 第一步）
- main 合進 feature/enemy-ai（本地 merge commit，未 push），拿到 `Museum_New`。
- 新增 `DemoFlowModel`、`DemoFlow`、`PlayerVitals`（Scripts/Enemy/Flow/）；`EncounterController.TotalEnemies`、`alertOnSpawn`；`EnemyController.Alert/IsAlerted`、`IEnemyContext.IsAlerted`，`EnemyStateRules` 對 alerted 敵人不看距離/視線。
- 資產：`Data/Enemies/Demo/Wave_Demo_1~3`、`Wave_Demo_Boss`；`Scenes/Demo/Museum_Demo/NavMesh-Navigation.asset`。
- 場景物件：MockPlayer（x=-28）、PlayerStart、AttackCoordinator、EnemyPool（預熱阿努比斯 3、神諭 1、翼人 1）、SpawnPoints_Hall（6 個）、SP_Throne、Encounter_Hall、Encounter_Boss、DemoFlow、Navigation；原本的 Main Camera 關掉。
- 用 CLI 驗證：3 波 → 法老 → 勝利、玩家倒下 → 補血回起點重來。EditMode 259/259（新增 DemoFlowModel 6 個、alerted 2 個）；PlayMode 29/29。

### 2026-11-22（攻擊名額）
- 新增 `AttackTokens`、`AttackCoordinator`；`EnemyData` 加 `Attack Slot Cost`、`Wait Distance`；`IEnemyContext` 加 `MustWaitForTurn`、`WaitDistance`、`CircleTarget`。
- `ChaseState`：沒名額又在等待距離內時，繞著玩家走而不是出招。
- `EnemyController`：選招前先檢查名額與視線，開始出招時拿名額，離開 Attack 狀態、停用或重生時還回去；繞圈時關掉 agent 的自動轉向，面向玩家。
- 翼人：佔 2 個名額，等待距離 4.5 m。EnemyAI 場景新增 `AttackCoordinator` 物件。
- 測試 fixture（`PlayModeWorld`）把視線角度設成 180°，因為既有測試常把玩家擺成背對敵人。
- EditMode 251/251、PlayMode 29/29。實際場景的繞圈樣子還沒親眼看過。
