# 四項 VR 交互測試場景

打開 `Assets/A_VRInteraction/Scenes/InteractionTest.unity`，按 Play 即可測試。此場景使用既有 Starter Assets XR Origin 和左右手 Input Actions，包含七件可抓工具、三件可歸位文物、三個多碰撞體靶、遮擋牆及手電筒照明牆。房間後方偏右有一隻固定站位的阿努比斯，可測近戰、弓箭和斧頭投擲的傷害。Play Mode 可從頭頂的文字讀血量，重新進入 Play 會回滿。

## Quest／XR 控制器

| 操作 | 左手 | 右手 |
| --- | --- | --- |
| 抓取／放開 | 按住／鬆開 Grip 側握鍵 | 按住／鬆開 Grip 側握鍵 |
| 使用所持物 | Trigger 食指扳機 | Trigger 食指扳機 |
| 移動／轉向 | 搖桿平移 | 搖桿分段轉向 |
| 持弓／拉弦 | 可持弓或拉弦 | 可持弓或拉弦，需與持弓手不同 |

四件近戰武器在前方桌面向左延伸的部分，附英文名稱；手電筒、射線工具與弓在右半邊，文物在右側獨立矮台。

1. **物理抓取**：手靠近道具按住 Grip，移動手並放開，確認物品跟隨、下落和碰撞。手電筒持有時按 Trigger 切換，對準右後方白色牆面觀察光斑。
2. **近戰**：抓起刀、斧頭、撬棍或鏟子，揮向左前方靶。打擊端有短軌跡，命中靶閃黃並發聲，後方狀態板顯示速度 m/s、能量 J 與估計停止力 N。相同一次揮擊的同一多碰撞體靶只計一次；左後方牆後另有遮擋測試靶。力道為遊戲估算，並非手柄感測器量到的真實力。
3. **射線／雙手弓**：射線工具的 +Z 槍口朝靶，持有後按 Trigger 發射事件，射線被牆遮擋。弓用一手 Grip 持住，另一手靠近藍色弦珠，再按住 Grip 向後拉；最大拉距 0.6 m。鬆開拉弦手 Grip 生成實體箭，放下弓會取消拉弦。弧線是帶重力的預覽，箭真正碰撞時短暫顯示標記和音效，約 10 秒後消失。
4. **文物歸位**：把右側 jar 放進後方展櫃藍色托盤中心再鬆手。ID 為 `interaction-jar-01`，類型 `CanopicJar`；距 Snap Point 0.23 m 內且鬆手停留 0.35 秒後鎖定。成功後狀態板變綠，通知數為 1，文物不可再抓。停止／重新 Play 可重做所有測試。

狀態板的 Ray/Bow 數字是發射事件數；只有真命中才閃靶或顯示箭碰撞標記，射空不代表命中。

## 無頭顯桌面測試

Editor 未檢測到頭顯時自動建立桌面相機與兩隻模擬手；先點 Game 視窗使鍵盤輸入生效。綠球為活動手，另一手保持姿態和抓取。此模式透過 XRI 真實 Select/Activate 與 Rigidbody 運作，但不是控制器追蹤或真機手感驗證。

| 按鍵 | 用途 |
| --- | --- |
| WASD／滑鼠右鍵拖動 | 移動／轉動視角 |
| Tab | 切換左右活動手 |
| 方向鍵 | 移動活動手左右／上下 |
| R／F | 活動手向前／向後 |
| Shift + 手部移動鍵 | 快速移手，測近戰 |
| Q | 抓取附近道具或弦、放開、接回斧頭 |
| E | 投擲手上的斧頭、切換手電筒或發射射線 |

拉弓時先以一手 Q 拿弓，Tab 換手，用方向鍵與 R/F 把手移到弦珠附近，Q 抓弦，再按 F 逐步後拉，Q 放弦。兩手都跟隨桌面視角，因此拉弓期間先固定視角。桌面模式只在 Editor 啟用；Android 不會啟用它。桌面失去焦點會取消拉弓。

測阿努比斯扣血時，從側後方瞄準軀幹。正面可能先撞上它的短刀，短刀擋下攻擊時血量維持原值。桌面模式可讓活動手朝向軀幹，按 E 投擲斧頭，等回彈接取提示出現後按 Q。錯過時機時，斧頭會落地，可以再撿起。

## 三件文物與法老

原有的卡諾卜罐在右側。羅塞塔石碑與圖坦卡門面具的測試道具放在左後方的底座，對應的藍色托盤在後壁，旁邊有英文標籤。把三件文物各自放到正確托盤中心再鬆手，順序可互換。第三件歸位後，法老會從房間右後方出現。生成點至少要離玩家 3 公尺，站得太近時可退開等待。三件歸位與法老生成只在本測試場景配置。

## 接線與變更範圍

### 敵人系統

- `InteractionTest` 和 `Museum` 使用 `DamageBridgeInstaller` 接近戰與弓箭傷害。執行中生成武器或弓後，呼叫安裝器的 `Refresh()`。
- 兩個場景的 XR Rig 都把 `PlayerTarget.head` 指向 HMD 相機，`PlayerHealth` 從 100 HP 開始並接收敵人的傷害。桌面模式沿用同一份血量。
- 文物歸位由 `ArtifactDisplayTrigger.OnArtifactRestored` 通知 `ArtifactEventBridge`，再由 `EncounterDirector` 按 ID 去重及推進遭遇戰。執行中生成展示台後，呼叫橋接的 `Refresh()`。
- `ArtifactBossTest.prefab` 包含兩個補充測試台、Boss 生成點和小型導覽網格。法老使用 B 的 `Wave_Boss_Pharaoh` 與 `Enemy_Pharaoh`。斧頭投擲和接取在 `InteractionTest` 驗收。

### 匯入模型更新

已套用 `Assets/A_VRInteraction/Models/ImportedTools/Weapon/` 的 Knife、Axe、Crowbar、Shovel、Flashlight FBX；手榴彈依本次選擇保留為素材。原始 FBX 不變，模型以場景子物件引用。匯出父節點 `T_ORPH_ToolsAll` 的約 0.32 m 位移在實例上歸零，使握點、刀刃標記與燈頭對齊。

四件近戰物件各自有 Rigidbody、PhysicalGrabbable、MeleeWeapon、簡化 BoxCollider、握點和打擊段，質量初值分別為 0.35／1.15／0.9／1.1 kg；它們共同更新命中板。模型包未附貼圖，目前使用新增的簡單 URP 金屬／握柄／木柄材質。原弓、射線工具與文物保留灰模。

本輪建置工具為 `Docs/Tools/ReplaceInteractionModels.cs`，會拒絕重複覆寫已替換的模型。模型替換後重新編譯通過、8/8 PlayMode 測試通過，其中近戰測試逐一抓取並揮擊四件武器。相關結果存於 `Docs/QA/ModelReplacement-*`；測試亦確認打擊點貼近實際幾何、燈頭位於鏡片前，以及物品在物理模擬後仍留在桌台上。原場景備份在 `Docs/QA/InteractionTest-BeforeModels.unity`（不會當作遊戲場景匯入）。

- 新場景由 Editor API 建立，沒有手寫 Unity YAML；建場工具保存在 `Docs/Tools/BuildInteractionTest.cs`，遇到已存在場景會拒絕覆寫。
- 實體箭：`Assets/A_VRInteraction/InteractionTestAssets/Prefabs/TestArrow.prefab`，含 Rigidbody、非 Trigger 碰撞體、ArrowProjectile 和實際碰撞回饋。
- 所有工具為單手、Velocity Tracking 物理抓取；弓根 Collider 清單排除獨立 XRSimpleInteractable 弦。String Rest 與 String Visual 分離，Arrow Origin.forward 為射出方向。
- 近戰接 Blade Base/Tip、XR Origin、頭部遮擋原點及自體忽略。新增物理圖層 8/9/10：InteractionTestTarget／InteractionTestProp／InteractionTestArtifact；既有圖層保留。新場景的 Sphere/Curve caster mask 包含道具層，近距離 Sphere caster 使用 Collide 偵測弦 Trigger。
- 原 Android Low 品質的 Performance URP 關閉額外燈光。新增獨立 `InteractionTest URP.asset`，只開啟 PerPixel 額外燈光、每物件上限 1、不加額外燈光陰影。SceneLightingOverride 僅在此場景運行時切換，停止後恢復；原品質資產不變。Android build 的 shader stripping、效能及真機輸出尚未驗證。
- `SampleScene`、`BasicScene` 及原 `Performance URP Config.asset` 的前後 SHA-256 完全相同，紀錄在 `Docs/QA/InteractionTest-PreservedAssets.json`。未把測試場景設成正式 build 啟動場景。

## 敵人整合驗證（2026-11-22）

- Unity 6000.6.7f1 完整回歸通過 251 項 Edit Mode 與 39 項 Play Mode 測試。
- Unity 6000.6.7f1 Play Mode 載入儲存的 `InteractionTest`，用實際 XRI 抓取與物理武器命中阿努比斯。刀、實體箭和投擲斧各使身體扣血一次。斧頭回彈時按接取鍵可重新握住。實體箭命中頭部時採用 2.5 倍弱點傷害，冷卻期間的下一擊不重複扣血。
- 阿努比斯以原有的 Quick Slash 命中 XR Rig 的 `PlayerTarget`，玩家血量由 100 降至 92 HP。另載入 `Museum` 驗證近戰橋接、同一份玩家血量與既有卡諾卜罐歸位。
- 逐一抓放三件實體文物，完成後只生成一隻法老。重複歸位會經過展示台與橋接，Director 仍只計一次同 ID。

## 本次驗證（2026-11-12，Asia/Singapore）

- 最終編譯通過。
- **64/64 自動測試通過**：56 項 EditMode、原有 2 項 PlayMode 物理測試、新增 6 項真場景整合測試。新測試直接載入儲存的 InteractionTest，驗證引用、碰撞體、真 XRI 抓放、啟動事件、射線牆體遮擋、兩手發一支實體箭並碰撞、文物鬆手鎖定／不重複、近戰單揮去重／力道。
- Play Mode 幀計數已觀察到持續改變，非只進入 Play 後拍靜止圖。
- 報告：`Docs/QA/InteractionTest-AllTests.json`、`InteractionTest-Compile.json`、`InteractionTest-Frames.json`。
- 畫面：`Docs/QA/InteractionTest-Play.png`；手電筒開／關對照另存於同一 QA 目錄。
- 尚未連接 Quest 3；追蹤恢復、握持姿態、快揮／碰撞手感、真機音效與 Android build 仍需實機驗收。測試過程可見無頭顯 XR 音效驅動回退至預設裝置，以及 Unity 帳號連線提示；這些與交互元件執行錯誤分開記錄。
