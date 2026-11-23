# A 組交接｜博物館 VR 互動

團隊正式 Demo 的巡查板、整把武器的近戰判定與首波 30 秒倒數，請從 [Museum_Demo 交接報告（2026-11-23）](../../Docs/MuseumDemo-Handoff-2026-11-23.zh-TW.md) 開始。

**要在自己的場景加入這些功能，請從[跨場景設定指南](SceneSetup.zh-TW.md)開始。** 指南列出玩家受擊 Prefab、紅邊與雙手短震、地面確認、背包、武器和文物歸位的 Inspector 接線與驗收方式。

| 組員要驗收的內容 | 請開啟的場景 |
| --- | --- |
| 完整展廳遭遇戰、投斧自動接回、雙手弓、浮空背包及受傷回饋 | [Museum_Demo_VR](Scenes/Museum_Demo_VR.unity) |
| 開場地面確認、實際蹲下撿物、阿努比斯閃避練習與單手肩後兩格背包 | [Museum_test](Scenes/Museum_test.unity) |
| 個別武器命中與遮擋、弱點、三件文物歸位與法老生成 | [InteractionTest](Scenes/InteractionTest.unity) |

要在展廳遭遇戰中試武器，請開 A 的 [Museum_Demo_VR](Scenes/Museum_Demo_VR.unity)。出生點和朝向沿用原版 Museum_Demo 的 PlayerStart。戴上 Quest 後比對黃色地面圈，再按右手 **A** 開始。從出生點往前約 2.5 公尺，左邊是警衛交班桌，右邊的武器架放著投斧和弓。

- **投斧接回**。在交班桌旁握住斧柄，向敵人或場景中的實體表面揮出並鬆開 **Grip**。斧頭撞到後會自動回到投擲的空手。接住後可直接揮砍，要再丟或放下時，先按住 **Grip**，再鬆開。原手拿著其他物品時，斧頭會落地，可以走過去撿。
- **雙手弓**。一手用 **Grip** 握住弓柄，另一手抓住藍色弦珠，往後拉。持弓手保持握緊，只鬆開拉弦手的 **Grip** 就會射出一支箭。
- **受傷回饋**。玩家扣血時，視野邊緣泛紅，左右控制器各短震一次。每次扣血都會觸發，成功閃避則不會震動。

### Museum_Demo_VR 的浮空背包

![Demo 桌面試玩畫面。手電筒與撬棍浮在兩側，中央放大的斧頭可取出。](Editor/GuideImages/MuseumDemoBackpack.png)

| 動作 | Quest 控制器 | 桌面試玩 |
| --- | --- | --- |
| 收進背包 | 拿著道具，將**雙手**分別移到左右肩後並保持在那裡，持物的手鬆開 **Grip** 即可收納。兩隻手各拿一件時可以分別鬆手。 | 手上有道具時按 **B**，將手上拿著的道具收進背包。 |
| 開啟或關閉 | **雙手空著**，從身前沿頸部兩側移到頭部後方，像摸兩條背包肩帶。兩手可以先後到位。開包後把手移回面前，就能看到道具的縮小模型。要再觸發一次，先把雙手都移出肩後區。 | 兩手都空著時按 **B**。 |
| 左右滑選 | 用空手在模型前向左橫掃看下一件，向右看上一件。每滑一次，把手移出預覽區再滑。中央較大的模型是目前選取的道具。 | 滾動滑鼠滾輪，或按 **[**、**]**。 |
| 取出 | 任一隻空手伸到中央模型，按住 **Grip**。道具拿到手時，背包面板會自動關閉。 | 活動手移到中央，按 **Q** 取出，面板便會自動關閉。 |

可先收好交班桌的手電筒，再收武器架上的斧頭。開包後滑回手電筒，換另一隻手拿出並按 Trigger 開燈。走離選單會自動關閉，同一輪遊戲中再次開包仍可取回道具。重開這一輪會清空背包並重置場景裝備。

## 如何合併到 main

1. 在 GitHub 建立 Pull Request，選 **base = `main`**、**compare = `feature/vr-interaction`**。在 **Files changed** 確認新場景、Prefab、程式與同名的 `.meta` 都在，這些檔案的 GUID 讓 Unity 能找到對應資產。
2. 審查者先在 GitHub Desktop 切到 `feature/vr-interaction`，按 **Fetch origin**、**Pull origin**，再用 Unity **6000.6.7f1** 開啟專案。首次下載依[專案設定](../../README.md)安裝 Git LFS；已有副本但模型或貼圖缺失時，在儲存庫根目錄執行 `git lfs pull`。
3. 依下面步驟完成一次「拿手電筒 → 收進背包 → 換撬棍 → 取回並開燈」，再到 InteractionTest 試一次投斧接回或三件文物歸位。由另一位組員審查後合併 PR。若同一個場景或 Prefab 有衝突，先請兩邊作者確認要保留的版本。
4. 合併後在 GitHub Desktop 切到 `main`，按 **Fetch origin**、**Pull origin**，等 Unity 匯入完畢再開 Museum_test 確認。這兩個測試場景都可以直接從 Project 視窗開啟。

## 基本操作

用 Quest 時先進入 Quest Link，再在 Editor 按 **Play**。沒有頭顯時直接按 **Play**，點一下 **Game** 視窗讓它接收鍵盤輸入。

| 動作 | Quest 控制器 | 桌面 |
| --- | --- | --- |
| 走動、轉身 | 左搖桿移動，右搖桿分段轉向 | WASD 移動，按住滑鼠右鍵拖曳轉向 |
| 伸手、換手 | 左右手都能碰觸握柄 | Tab 切換活動手；方向鍵左右或上下移手，R 向前、F 向後；按住 Shift 可加快移手 |
| 拿起、放下 | 手靠近握柄，按住側面的 Grip；鬆手即放下 | 手移到握柄旁按 Q，再按 Q 放下 |
| 使用手電筒或射線工具 | 持有時按食指 Trigger | 持有時按 E |

## Museum_test｜開場地面確認

1. 在 Quest 的 Boundary 先設定實際地面。用 USB 連線進入 Quest Link，在 Unity 開啟 [Museum_test](Scenes/Museum_test.unity) 並按 **Play**。
2. 等待 **FLOOR CHECK** 取得頭盔與右手控制器追蹤。往下看黃色地面圈，比對它是否和雙腳站的實際地面同高。也可以把任一支控制器靠近實際地面，看虛擬控制器與黃圈的相對高度。
3. 確認同高後，放開再按右手 **A**。不用觸地。確認完成才開放搖桿、抓取、背包與閃避練習。遊戲沿用 Quest 的地面原點與身體高度。
4. 若兩個地面高度不同，或畫面顯示 **Floor tracking unavailable**，請在 Quest 設定重設 Boundary 的地面高度，再恢復或重開 Quest Link 與 Play Mode。桌面預覽使用固定地面，進場即可用鍵鼠。若 Play 後才戴上已恢復追蹤的 Quest，會切回 VR 地面確認。

道具掉到地面時，實際彎腰或蹲下，把控制器移到握把旁，再按 **Grip** 拿起來。收納和取出仍沿用同一個 Grip。

## Museum_test｜交班與背包

![玩家走近交班桌時，手電筒在巡查單旁，撬棍在右側維修車上](Editor/GuideImages/MuseumGuardDesk.png)

1. 走到左前方的 **SECURITY CHECK-IN** 桌前，在巡查單旁拿起手電筒。朝牆按 Trigger（桌面 E）切換光束。右側維修車上有一把撬棍，從走道側靠近握把即可拿起。
2. **開包**。任一隻手從身前伸向同側肩後，背包會在身前打開。桌面按 **B**。開包後把手移回身前操作。
3. **收納**。把拿著手電筒的手伸進標示 **EMPTY** 的格子，鬆開 Grip（桌面按 Q）。格子會顯示 **FLASHLIGHT**，光束會暫時消失。拿起撬棍，用另一個空格收納它，確認格子顯示 **CROWBAR**。
4. **切換**。把空手伸到想拿的道具格，按住 Grip（桌面按 Q），再把手移開。拿出的手電筒可以繼續開關，撬棍可以揮動，也可以放回桌面。背包有兩格，本場景可收納交班桌的手電筒與撬棍。
5. **關包**。手離開肩後區域後再伸回去，或在桌面再按 B。走離背包也會關閉；在同一輪 Play 重新打開，格內道具仍在。

![兩件工具收納後，背包左格顯示 FLASHLIGHT，右格顯示 CROWBAR](Editor/GuideImages/FieldBackpack.png)

### Quest 閃避練習

1. 完成開場地面確認後，從出生點往右前方找 **DODGE PRACTICE** 看板和阿努比斯。
2. 走到阿努比斯正前方約 1.5 公尺，保持站姿直到武器發光並揮擊。地面會標出這一擊的窄刀路，看板的 **Player HP** 應從 100 變成 92。
3. 再次發光時，原地把頭與肩側移約 40 公分，或實際下腰、蹲低。試後仰時先站在刀路外緣約 1.7 公尺處，讓頭往後約 30 公分。出招後看 HP，身體離開刀路時便會保留。
4. 站回中央，只移動雙手，再用左搖桿進出攻擊路徑。頭與軀幹留在刀路時會扣血，走出刀路時可以閃避。離開練習區可暫停下一次預告。重新進入 Play 即可重置 HP。

在 Unity Test Runner 的 **PlayMode** 執行 `MuseumFloorStartupTests`、`MuseumFloorPickupTests` 和 `TrackedPlayerHurtboxTests`，可用模擬 Quest 頭盔和控制器驗證開場、實際撿回掉落的手電筒、四種閃避姿勢、追蹤中斷與搖桿。Play Mode 選取 XR Origin 下的 **Tracked Player Combat → Enemy Target**，可用 Scene 視窗的 Gizmos 查看頭部和軀幹估算範圍。

## InteractionTest｜武器、文物與法老

| 想試的互動 | 操作與成功提示 |
| --- | --- |
| 近戰與弱點 | 在工具桌左側拿刀、斧、撬棍或鏟子，揮向左前方靶；命中時靶會閃黃，狀態板顯示速度。再從側後方打阿努比斯軀幹，觀察牠頭頂的血量。實體箭射中頭部會觸發弱點加成。 |
| 投斧、回彈與接取 | 握斧揮出並鬆開 Grip。斧頭碰到目標後自動回到投擲的空手。再按住 Grip，鬆開時可再投出或放下。桌面持斧按 E 投出，看到提示後按 Q 接回。漏接可到落點撿回。 |
| 射線與遮擋 | 在工具桌右側拿射線工具，瞄準可見的靶按 Trigger（桌面 E），再瞄牆後的靶。前者會閃，後者的射線停在牆面。 |
| 雙手拉弓 | 一手持弓，另一手抓住藍色弦珠往後拉，放開拉弦手發射實體箭。桌面用 Q 拿弓、Tab 換手、移到弦珠按 Q，再用 F 後拉、Q 放弦。 |
| 文物歸位與法老 | 卡諾卜罐在右側；羅塞塔石碑和圖坦卡門面具在左後方。照標籤放到對應托盤中央，鬆手稍候，文物會鎖定。三件都歸位後法老會在右後方出現一次。站在生成點旁時退開至少 3 公尺。 |

## 拖入單件 Prefab 佈置展廳

在 Unity 的 **Project** 視窗打開 `Assets/A_VRInteraction/Prefabs`，把藍色 Prefab 拖進 **Hierarchy**。讓握柄朝玩家接近的一側，物件下方要有能承托它的實體 Collider。在有 XR Origin 和 XR Interaction Manager 的場景按 **Play**，從物件落定的位置試抓。

| 目錄 | 可用的 Prefab | 佈置要點 |
| --- | --- | --- |
| [Weapons](Prefabs/Weapons/) | `Knife`、`Axe`、`Crowbar`、`Shovel`、`Bow`、`GrenadeProp` | Axe 附投擲回彈。Bow 已接好弦和[實體箭](InteractionTestAssets/Prefabs/TestArrow.prefab)，弓架旁要留雙手拉弦空間。GrenadeProp 可抓取和放下，用作佈景。 |
| [Tools](Prefabs/Tools/) | `Flashlight`、`RayTool`、`FieldBackpack` | 為手電筒留一面可照的牆；為射線留可見的靶。背包需要場景裡的玩家與可收納道具，接線方法見下節。 |
| [Artifacts](Prefabs/Artifacts/) | `CanopicJar`、`RosettaStone`、`TutankhamunMask`，各有同名加上 `Display` 的展示座 | 在展廳分別放一件文物和它的展示座。相同文物 ID 只計一次；要引出法老，放齊三種文物，並接上文物橋接及 Boss 遭遇戰。 |
| [Player](Prefabs/Player/) | `TrackedPlayerCombat` | 放到負責遊玩的 XR Origin 下，場景只保留這一組 `PlayerTarget` 與 `PlayerHealth`。XR Origin 請求 **Floor**，頭盔、控制器和抓取點共用校準後的地面原點。 |

可先開 [PrefabLibraryTest](Prefabs/Testing/PrefabLibraryTest.unity)，查看 15 個已接線的實例。自行佈置時，請一併檢查下列設定。

- **抓取**。左右手 Near Caster 的遮罩要包含 `InteractionTestProp`、`InteractionTestArtifact`；Trigger Interaction 選 `Collide`，讓手能選到弓弦和背包格。
- **近戰**。獨立武器可從握持它的 XR 手找到 Origin 與相機。使用桌面模擬手時，將武器 `MeleeWeapon` 的 `Rig Motion Reference`、`Ignored Player Root` 接到 XR Origin，`Obstruction Origin` 接到該 Origin 的 XR 相機；可直接對照驗收場景。要讓敵人扣血，場景內需有 `DamageBridgeInstaller`。
- **雙手弓與手電筒**。將弓指定給 `InteractionDesktopMode → Bow` 以啟用桌面拉弦。若箭會碰到玩家，將 Rig 的碰撞體加入 `BowDrawAim → Additional Ignored Colliders`。手電筒依賴 URP 的 `Additional Lights` 來照亮牆面。
- **玩家受擊**。受擊 Prefab 已接到 `Museum_test`、`InteractionTest` 和 `PrefabLibraryTest`。一般近戰使用敵人招式的扇形距離與角度，加上有限的揮擊高度，結算當下頭部和軀幹的重疊。範圍重擊仍可命中其水平範圍內的玩家。雷射使用同一組頭與胸位置，沿用敵人投射物原先的掃掠半徑。若用劇本直接生成敵人而未透過 `EnemyPoolManager`，生成時呼叫 `TrackedPlayerHurtbox.TrackEnemy` 註冊。

## 將交班桌與背包移入其他場景

把 [MuseumGuardDesk.prefab](Prefabs/Furniture/MuseumGuardDesk.prefab) 拖進目標場景，讓桌前與車側的握把面向走道。依 Museum_test 的場景實例，將撬棍 `MeleeWeapon` 的 `Rig Motion Reference` 與 `Ignored Player Root` 指向該場景 XR Origin，`Obstruction Origin` 指向 HMD 相機。

將 [FieldBackpack.prefab](Prefabs/Tools/FieldBackpack.prefab) 拖入場景後，先保留根物件的停用狀態。在 `Field Backpack` 指定該場景的 XR Origin、左右手 Near-Far Interactor，以及桌面試玩用的 `InteractionDesktopMode`。在 `Tools` 清單加入**場景實例**上的 `PhysicalGrabbable` 與顯示名稱，例如桌上的手電筒和車上的撬棍。兩個 `Slots` 與 `Display` 已在 Prefab 內接好。設定完成後啟用根物件，按 Play 逐件收納、再取回。

接敵人傷害、展櫃歸位或執行中生成的道具時，參考[互動接線與測試紀錄](../../Docs/InteractionTest.zh-TW.md)及 [B 的介面說明](../../Notes%20For%20Groupmates/NOTES_FOR_A.md)。

## 本次更新與驗收交接（2026-11-22）

- **自動驗收**。Unity 6000.6.7f1 隔離副本通過 **281/281 項 Edit Mode** 及 **64/64 項 Play Mode**。後者涵蓋敵人、武器、背包、地面追蹤、閃避與受傷回饋。另一次針對受傷回饋及追蹤的驗收通過 **19/19**，包含模擬 OpenXR 確認每次扣血會送出左右手各一次的震動命令。
- **展廳戰鬥**。VR Demo 已接上武器傷害橋接。用滿血木乃伊試過持斧揮擊、撬棍和實體箭，三者均會降低敵人 HP。關卡傳送時會帶著手上與背包裡的裝備，重開本輪會重置道具。
- **Quest 3 Link 實測**。在 `Museum_Demo_VR`，雙手空著摸到兩側肩後，浮空背包成功打開。也實測了肩後鬆開 Grip 收入一件道具，再次開包後以右手取回，背包件數依序為 0 → 1 → 0。
- **下一位試玩者**。先到 `Museum_test` 的閃避練習區承受一擊，再用實際側身、下腰、後仰和蹲下各躲一次。記錄每次的 HP、雙眼紅邊是否清楚，以及兩手在扣血瞬間的震感。回到 Demo 試左右手連投接斧、拉弓，以及收進兩件道具後橫掃滑選、換手取回。
- **美術交接**。原版博物館場景的 Global Volume 缺少 Volume Profile 引用，兩個 VR 博物館場景也繼承了這個狀態。要調整全域後製時，先與場景負責人確認預期使用的 Profile。
