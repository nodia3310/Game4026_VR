# 在自己的場景接上 Quest 3 互動

本指南適用於團隊專案的 Unity **6000.6.7f1 / URP**。先取得 `feature/vr-interaction` 的更新，儲存自己的測試場景。要加閃避和受傷提示，完成第 1、2 節就可以用現有敵人驗收。要加背包或武器，可以直接從對應章節開始。

若從空場景開始，可從 Project 的 `Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs` 拖入 **XR Origin (XR Rig)**，在場景加一個 **XR Interaction Manager**，並在出生點鋪上帶 Collider 的地板。已有可用的 Quest 玩家時，沿用原本的 Rig 和 Manager。若場景已有會扣血的 **Player Health**，只想加紅邊與雙手震動，可直接做第 2 節。

## 1. 讓敵人瞄準可閃避的玩家

1. 在 Hierarchy 找到遊玩中的 **XR Origin**。在 Inspector 將 **Requested Tracking Origin Mode** 設為 **Floor**，確認 **Camera** 與 **Camera Floor Offset Object** 指向本場景的 HMD 相機與 Camera Offset。相機和兩手控制器應各有啟用的 **Tracked Pose Driver**，左、右 **Near-Far Interactor** 的 **Handedness** 應分別為 Left、Right。
2. 從 Project 把 [TrackedPlayerCombat.prefab](Prefabs/Player/TrackedPlayerCombat.prefab) 拖到 XR Origin 下，將實例的 local position 和 rotation 歸零、scale 設為 1。Prefab 根物件已有 **Player Health**，子物件 **Enemy Target** 上已有 **Player Target** 與 **Tracked Player Hurtbox**。保留 Prefab 的 **Head → Tracked Eyes** 和 **Body → Estimated Chest**，程式會依追蹤姿勢更新這兩個節點。
3. 在新場景啟用這組玩家目標，停用原先的 MockPlayer 或重複的 Player Target，將既有血量顯示接到同一個 Player Health。一般父子配置會自動找到 XR Origin 與血量。若關卡傳送的是共同玩家根物件，可對照 [Museum_Demo_VR](Scenes/Museum_Demo_VR.unity) 的 **VR Player** 配置，在 Tracked Player Hurtbox 明確指定 **Rig Override**、**Health Override**，桌面測試另接 **Desktop**。
4. 使用 B 的 **Enemy Attack**。場景內的敵人與 Enemy Pool Manager 建立的敵人會被接上。若自己的生成腳本直接 Instantiate 敵人，在取得新敵人的 EnemyAttack 後呼叫 `hurtbox.TrackEnemy(attack)`。一般近戰依招式角度、距離和揮擊高度判定，範圍重擊覆蓋整個水平範圍。

若自行寫敵人招式，先核對傷害來源。直接呼叫 `PlayerHealth.ApplyPlayerDamage` 會立即扣血，無法用這條呼叫驗證閃避。要重用現成的身體判定，請沿用 B 的 EnemyAttack 結算流程及 PlayerTarget 的傷害接收器。可以先放一隻現有的阿努比斯確認玩家接線，再接自己的敵人。

**驗收**　用 Quest Link 按 Play，選取 **Enemy Target** 並開啟 Scene 視窗的 Gizmos。實際蹲下或側身應帶動頭、胸範圍。讓同一招窄幅攻擊打中一次，再將頭和肩移出刀路一次。第一次應扣血，第二次應維持原 HP。只移動控制器、頭胸仍在刀路時，應照常扣血。

## 2. 扣血時顯示紅邊，並讓雙手短震

| 在哪裡設定 | Inspector 要接什麼 |
| --- | --- |
| 場景中常駐的空物件 | 加 **Interaction Desktop Mode** 和 **Player Damage Vignette** 到同一物件。可對照 Demo 裡的 **Museum VR Setup**。將物件放在 XR Rig 外，切換桌面模式時它仍會運作。 |
| Interaction Desktop Mode | **Xr Rig** 指向本場景的 XR Rig GameObject，**Xr Camera** 指向 Rig 的 HMD Camera。只在頭顯遊玩時可取消 **Enable In Editor Without Headset**。 |
| Player Damage Vignette | **Health** 拖入實際扣血的 Player Health，**Material** 使用 [PlayerDamageVignette.mat](Prefabs/Player/PlayerDamageVignette.mat)。 |
| XR Rig 下的左右控制器 | 每個 Near-Far Interactor 本身或其父物件需有啟用的 **Haptic Impulse Player**。在 XRI 的 **Haptic Output** 選取對應手的 **Haptic Device** Action，確認輸入動作已啟用，**Amplitude Multiplier** 大於 0。 |

**驗收**　用頭顯承受一次攻擊。實際扣血時雙眼邊緣應泛紅、中央清楚，左右手各短震一次，約半秒後紅邊淡出。閃過同一招應保留 HP，且沒有受傷提示。若有紅邊但只有一手震，先查該手的 Handedness、Haptic Impulse Player 和 Haptic Device。若 HP 下降卻兩者都沒有，先查 Vignette 的 Health 是否就是正在扣血的實例。

## 3. 加入開場地面確認

在 [Museum_test](Scenes/Museum_test.unity) 對照 **Museum Floor Confirmation**（含子物件 **Virtual Floor Marker**）與 **Floor Check Prompt**（含文字 **Instructions**）。將兩組物件複製到自己的場景。黃圈放在出生點地面，Prompt 放到 HMD 相機下，保留原場景的局部位置與縮放，把其 World Space Canvas 的 **Event Camera** 改指向新場景的 HMD 相機。在 **Museum Floor Confirmation** 重新指定下列欄位。

| 欄位 | 目標 |
| --- | --- |
| Origin、Headset、Right Controller | 新場景的 XR Origin、HMD 的 Tracked Pose Driver、右控制器的 Tracked Pose Driver。 |
| Prompt、Instructions、Floor Marker | 新場景的提示物件、TMP 文字、黃色地面圈。黃圈與 Camera Offset 的世界座標 Y 在開場時須相差 5 公分以內。 |
| Desktop、Backpack | 使用桌面預覽或背包時，分別接到這個場景的 Interaction Desktop Mode、Field Backpack。 |

**驗收**　戴上 Quest 看向腳邊，比對黃圈與實體地面，等畫面提示後放開再按右手 **A**，此時才可移動與抓取。若要讓新關卡的刷怪也等待確認，在關卡啟動處檢查 `MuseumFloorConfirmation.IsGameplayReady`。對照 Demo 的 [MuseumDemoRun](Scripts/Combat/MuseumDemoRun.cs) 可看到啟動和跨區傳送裝備的接法。

## 4. 加入浮空背包、投斧與雙手弓

1. **背包模式**。把 [FieldBackpack.prefab](Prefabs/Tools/FieldBackpack.prefab) 拖入 Hierarchy，先在停用狀態設定。**Rig** 接 XR Origin，**Left Hand / Right Hand** 接對應的 Near-Far Interactor。勾選 **Use Floating Carousel** 可啟用 Demo 的雙手肩後手勢與橫掃選物。沿用 `Museum_test` 的兩格背包時保持未勾選。
2. **道具與格子**。保留 Prefab 的 **Display** 與兩個 **Slots**，選擇器會重用第一格的 Grip 和 Label。**Tools** 填入場景中可抓道具的 **Physical Grabbable** 實例與名稱。要用鍵鼠時，**Desktop** 接本場景的 Interaction Desktop Mode。完成後啟用背包根物件。
3. **武器與命中**。拖入 [Axe](Prefabs/Weapons/Axe.prefab) 與 [Bow](Prefabs/Weapons/Bow.prefab)，保留弦珠、箭與碰撞設定。左右手 Near Caster 的物理遮罩需包含 **InteractionTestProp**，Trigger Interaction 選 **Collide**。在場景啟用一個 **Damage Bridge Installer**，將揮擊和箭的命中接到敵人。遊戲開始後才生成武器或弓時，生成完呼叫 `installer.Refresh()`。

**驗收**　雙手摸肩後，鬆開持物手收納。手回身前，再以雙手空手摸肩後開包。左右橫掃換選後，用另一手 Grip 抓中央模型，應拿回可用的原件。在有實體 Collider 的表面試投斧，原投擲手空著時會自動接回。再用一手持弓、一手抓藍色弦珠，鬆開弦手射箭，確認敵人 HP 下降。

## 5. 用三件文物觸發 Boss

從 [Artifacts](Prefabs/Artifacts/) 放入卡諾卜罐、羅塞塔石碑、圖坦卡門面具與各自同名的 **Display**。左右手 Near Caster 的遮罩包含 **InteractionTestArtifact**。依 [InteractionTest](Scenes/InteractionTest.unity) 的配置，將 **Artifact Event Bridge → Director** 指向本場景的 **Encounter Director**，並將其 **Boss Encounter** 接到已配置法老生成的 Encounter Controller。若開場後才生成展示座，建立後呼叫 `ArtifactEventBridge.Refresh()`。

**驗收**　拿錯展櫃時文物應可取回。在對應托座中央鬆手後文物應固定。三種文物都歸位時法老出場一次。若展品能鎖定但 Boss 沒出現，先查 Director 是否收到三種不同的文物，以及 Boss Encounter 的生成配置。
