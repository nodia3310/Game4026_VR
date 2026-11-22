# A 組交接｜博物館 VR 互動

第一次試玩請開 [Museum_test](Scenes/Museum_test.unity)。這是從團隊的 [Museum_New](../Scenes/Museum_New.unity) 複製的獨立場景，出生點面向西側警衛交班桌。要試投斧、弓箭、文物歸位與法老，請開 [InteractionTest](Scenes/InteractionTest.unity)。

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

## Museum_test｜交班與背包

![玩家走近交班桌時，手電筒在巡查單旁，撬棍在右側維修車上](Editor/GuideImages/MuseumGuardDesk.png)

1. 走到左前方的 **SECURITY CHECK-IN** 桌前，在巡查單旁拿起手電筒。朝牆按 Trigger（桌面 E）切換光束。右側維修車上有一把撬棍，從走道側靠近握把即可拿起。
2. **開包**。任一隻手從身前伸向同側肩後，背包會在身前打開。桌面按 **B**。開包後把手移回身前操作。
3. **收納**。把拿著手電筒的手伸進標示 **EMPTY** 的格子，鬆開 Grip（桌面按 Q）。格子會顯示 **FLASHLIGHT**，光束會暫時消失。拿起撬棍，用另一個空格收納它，確認格子顯示 **CROWBAR**。
4. **切換**。把空手伸到想拿的道具格，按住 Grip（桌面按 Q），再把手移開。拿出的手電筒可以繼續開關，撬棍可以揮動，也可以放回桌面。背包有兩格，本場景可收納交班桌的手電筒與撬棍。
5. **關包**。手離開肩後區域後再伸回去，或在桌面再按 B。走離背包也會關閉；在同一輪 Play 重新打開，格內道具仍在。

![兩件工具收納後，背包左格顯示 FLASHLIGHT，右格顯示 CROWBAR](Editor/GuideImages/FieldBackpack.png)

## InteractionTest｜武器、文物與法老

| 想試的互動 | 操作與成功提示 |
| --- | --- |
| 近戰與弱點 | 在工具桌左側拿刀、斧、撬棍或鏟子，揮向左前方靶；命中時靶會閃黃，狀態板顯示速度。再從側後方打阿努比斯軀幹，觀察牠頭頂的血量。實體箭射中頭部會觸發弱點加成。 |
| 投斧、回彈與接取 | 握斧揮出並鬆開 Grip（桌面持斧按 E）。斧頭碰到目標後飛回，聽到提示音或感到短震，再以原手按 Grip（桌面 Q）。漏接可到落點撿回。 |
| 射線與遮擋 | 在工具桌右側拿射線工具，瞄準可見的靶按 Trigger（桌面 E），再瞄牆後的靶。前者會閃，後者的射線停在牆面。 |
| 雙手拉弓 | 一手持弓，另一手抓住藍色弦珠往後拉，放開拉弦手發射實體箭。桌面用 Q 拿弓、Tab 換手、移到弦珠按 Q，再用 F 後拉、Q 放弦。 |
| 玩家受傷 | XR Rig 以 100 HP 開始，敵人命中會扣血。要重現一次已知攻擊，在 Unity Test Runner 的 PlayMode 分頁執行 `Anubis_AuthoredAttackDamagesTheXRPlayersHealth`；快斬命中一次後玩家應剩 92 HP。 |
| 文物歸位與法老 | 卡諾卜罐在右側；羅塞塔石碑和圖坦卡門面具在左後方。照標籤放到對應托盤中央，鬆手稍候，文物會鎖定。三件都歸位後法老會在右後方出現一次。站在生成點旁時退開至少 3 公尺。 |

要試蹲下撿物，開 [Museum](../Scenes/Museum.unity)，按 Quest 左手 **X** 蹲下，再按一次站起。停止後重新進入 Play 可重置該場景的道具與進度。

## 拖入單件 Prefab 佈置展廳

在 Unity 的 **Project** 視窗打開 `Assets/A_VRInteraction/Prefabs`，把藍色 Prefab 拖進 **Hierarchy**。讓握柄朝玩家接近的一側，物件下方要有能承托它的實體 Collider。在有 XR Origin 和 XR Interaction Manager 的場景按 **Play**，從物件落定的位置試抓。

| 目錄 | 可用的 Prefab | 佈置要點 |
| --- | --- | --- |
| [Weapons](Prefabs/Weapons/) | `Knife`、`Axe`、`Crowbar`、`Shovel`、`Bow`、`GrenadeProp` | Axe 附投擲回彈。Bow 已接好弦和[實體箭](InteractionTestAssets/Prefabs/TestArrow.prefab)，弓架旁要留雙手拉弦空間。GrenadeProp 可抓取和放下，用作佈景。 |
| [Tools](Prefabs/Tools/) | `Flashlight`、`RayTool`、`FieldBackpack` | 為手電筒留一面可照的牆；為射線留可見的靶。背包需要場景裡的玩家與可收納道具，接線方法見下節。 |
| [Artifacts](Prefabs/Artifacts/) | `CanopicJar`、`RosettaStone`、`TutankhamunMask`，各有同名加上 `Display` 的展示座 | 在展廳分別放一件文物和它的展示座。相同文物 ID 只計一次；要引出法老，放齊三種文物，並接上文物橋接及 Boss 遭遇戰。 |

可先開 [PrefabLibraryTest](Prefabs/Testing/PrefabLibraryTest.unity)，查看 15 個已接線的實例。自行佈置時，請一併檢查下列設定。

- **抓取**。左右手 Near Caster 的遮罩要包含 `InteractionTestProp`、`InteractionTestArtifact`；Trigger Interaction 選 `Collide`，讓手能選到弓弦和背包格。
- **近戰**。獨立武器可從握持它的 XR 手找到 Origin 與相機。使用桌面模擬手時，將武器 `MeleeWeapon` 的 `Rig Motion Reference`、`Ignored Player Root` 接到 XR Origin，`Obstruction Origin` 接到該 Origin 的 XR 相機；可直接對照驗收場景。要讓敵人扣血，場景內需有 `DamageBridgeInstaller`。
- **雙手弓與手電筒**。將弓指定給 `InteractionDesktopMode → Bow` 以啟用桌面拉弦。若箭會碰到玩家，將 Rig 的碰撞體加入 `BowDrawAim → Additional Ignored Colliders`。手電筒依賴 URP 的 `Additional Lights` 來照亮牆面。

## 將交班桌與背包移入其他場景

把 [MuseumGuardDesk.prefab](Prefabs/Furniture/MuseumGuardDesk.prefab) 拖進目標場景，讓桌前與車側的握把面向走道。依 Museum_test 的場景實例，將撬棍 `MeleeWeapon` 的 `Rig Motion Reference` 與 `Ignored Player Root` 指向該場景 XR Origin，`Obstruction Origin` 指向 HMD 相機。

將 [FieldBackpack.prefab](Prefabs/Tools/FieldBackpack.prefab) 拖入場景後，先保留根物件的停用狀態。在 `Field Backpack` 指定該場景的 XR Origin、左右手 Near-Far Interactor，以及桌面試玩用的 `InteractionDesktopMode`。在 `Tools` 清單加入**場景實例**上的 `PhysicalGrabbable` 與顯示名稱，例如桌上的手電筒和車上的撬棍。兩個 `Slots` 與 `Display` 已在 Prefab 內接好。設定完成後啟用根物件，按 Play 逐件收納、再取回。

接敵人傷害、展櫃歸位或執行中生成的道具時，參考[互動接線與測試紀錄](../../Docs/InteractionTest.zh-TW.md)及 [B 的介面說明](../../Notes%20For%20Groupmates/NOTES_FOR_A.md)。

## 這次驗收的範圍

使用 Unity 6000.6.7f1，在 Museum_test 以桌面輸入試過從出生點走到新桌前、拾取兩件工具、開關光束、分格收納、取回並揮擊。模擬 HMD 與兩支追蹤控制器時，左右後肩皆能開關背包，另一隻手能取回原本那支手電筒。Quest 3 的肩後追蹤範圍與實際握持手感仍待戴頭顯確認。

隔離測試專案通過 251 項 Edit Mode、49 項 Play Mode，其中 10 項專門驗證獨立 Prefab。合併後可用上面的三個場景分別複測博物館動線、戰鬥流程及個別道具。
