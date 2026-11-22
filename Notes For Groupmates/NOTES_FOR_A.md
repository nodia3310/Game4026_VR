# 給負責人 A：敵人系統對接說明

> 由敵人系統（B，分支 `feature/enemy-ai`）整理，更新日期 2026-11-18。
> **A 的程式一行都沒改。** 下面列出 B 依賴 A 的哪些地方、需要 A 配合的事，以及要和 A 確認的問題。

## 1. 目前的接法

```
A: MeleeWeapon.Hit(MeleeHit) ──┐
A: ArrowProjectile.Impact(Collision) ──┤→ B: Bridges（MeleeDamageBridge / ArrowDamageBridge）
                                        │     速度 → 傷害，hit.Collider.GetComponentInParent<IDamageable>()
                                        └→ B: EnemyHealth（身體） / WeakPoint（弱點，×2.5）
```

- 敵人照 `Game4026.Contracts.IDamageable.TakeDamage(float, Vector3, bool)` 實作，**合約簽名沒變**。
- 傷害換算寫在 B 的橋接組件（由場景裡的 `DamageBridgeInstaller` 自動掛上） `Game4026.Bridges`（`Assets/_Game/Scripts/Bridges/`）裡，A 的武器還是「只發事件、不管傷害」。
- 依賴方向：`Bridges → Interaction`、`Bridges → Contracts`、`Enemy → Contracts`。**請不要讓 `Game4026.Interaction` 反過來引用 Bridges 或 Enemy**，否則會循環引用。

## 2. 需要 A 配合的事

1. **場景裡放一個 `DamageBridgeInstaller`**（A 的 Prefab 和程式都不用改）：
   - 在要打敵人的場景（主場景、`InteractionTest`）建一個空物件，掛上 `Game4026.Bridges.DamageBridgeInstaller`。
   - 開場時，它會自動幫場景裡每把 `MeleeWeapon` 加上 `MeleeDamageBridge`；每次 `BowDrawAim.Shot` 觸發後，幫剛生出的箭加上 `ArrowDamageBridge`。
   - 開場之後才生成的武器或弓，要呼叫 `DamageBridgeInstaller.Refresh()`。
   - 沒有放安裝器的話，武器打中敵人不會扣血。
2. **改動下面這些公開 API 前請先告訴 B**（橋接直接用到它們）：
   - `Game4026.Interaction.Melee.MeleeWeapon.Hit`（`event Action<MeleeHit>`）
   - `MeleeHit.Collider`、`MeleeHit.Point`、`MeleeHit.Speed`
   - `Game4026.Interaction.Ranged.ArrowProjectile.Impact`（`event Action<Collision>`）
   - `Game4026.Interaction.Ranged.BowDrawAim.Shot`（`event Action<BowShot>`），以及「先 Instantiate + Launch 箭，再觸發 Shot」這個順序（安裝器靠它找到新的箭）
3. **以下行為請維持**，否則弱點會失效：
   - 命中目標的判定方式（`MeleeWeapon.Identity`）：有 `MeleeHitTarget` 就用它，沒有就用 attachedRigidbody 或 Collider 本身，而且一次揮擊、每個目標只算一次。B 的敵人**不會**掛 `MeleeHitTarget`，根物件也不放 Rigidbody，讓弱點 Collider 被當成獨立目標。如果 A 改了這個判定方式，請通知 B。
   - 武器端 `isWeakPoint` 一律傳 `false`，由 B 的 `WeakPoint` 元件判斷是不是弱點。

## 3. 目前的傷害數值（都能在 Inspector 調）

| 武器 | 公式 | 例子 |
| --- | --- | --- |
| 近戰 | 速度 × 8，限制在 5–40 | 3 m/s = 24；4 m/s = 32（≥25 會硬直）；打中弱點再 ×2.5 |
| 箭 | 碰撞相對速度 × 1，限制在 10–35 | 30 m/s = 30 |

- 敵人預設 100 HP。每次扣血後有 0.3 秒全域受擊冷卻（不分武器），和 A「每次揮擊每個目標只算一次」的過濾一起運作。
- A 的 `startSpeed`（1.2 m/s）以下的揮擊本來就不會發 `Hit` 事件，所以最低傷害 5 只是保底。

## 4. 之後會需要 A 的事（M2/M3）

- **玩家受傷**：敵人攻擊命中時會呼叫 `IPlayerDamageReceiver.ApplyPlayerDamage(float)`，但專案裡還沒有任何實作（玩家血量尚未指派）。在那之前，B 用測試用的 MockPlayer。
- **`PlayerTarget`（已新增）**：B 在 `Scripts/Contracts/` 新增了 `PlayerTarget.cs`（既有檔案沒動）。**請 A 把它掛在 XR Rig 根物件上**，並在 Inspector 指定 `head` = HMD 相機、`body` = 身體中心（可留空，會用 head）。**`head` 一定要指定**：留空的話會用 Rig 根物件的位置（地面高度），敵人的視線會被地板擋住，看不到玩家。沒有掛的話，敵人找不到玩家，只會待機或巡邏，不會報錯。
  - 玩家受傷：`PlayerTarget` 會在自己或父物件上找 `IPlayerDamageReceiver`，敵人命中時呼叫 `ApplyPlayerDamage(10)`（預設值）。
- **Layer**：敵人的 Collider 要放在武器 `targetMask` 包含的 Layer 上。如果之後新增 Enemy Layer，A 的武器 `targetMask` 和 `blockerMask` 可能要跟著調。

## 4b. 文物歸位（M3 已完成橋接）

- 場景裡放一個 `Game4026.Enemy.EncounterDirector`，同一個物件掛 `Game4026.Bridges.ArtifactEventBridge`。開場時，橋接會訂閱場景裡**所有** `ArtifactDisplayTrigger.OnArtifactRestored`，轉給 Director。**A 的程式不用改。**
- Director 以 `ArtifactId` 去重。收到某種文物 → 清除 `linkedArtifact` 相同的展廳遭遇戰；RosettaStone、CanopicJar、TutankhamunMask 三種都收到 → 開始 Boss 遭遇戰。
- 開場之後才生成的 `ArtifactDisplayTrigger`，要呼叫 `ArtifactEventBridge.Refresh()`。
- 如果 A 比較想直接呼叫：`EncounterDirector.NotifyArtifactRestored(ArtifactRestoredEvent)` 是公開方法（但這樣 Interaction 就要引用 Enemy，建議還是走橋接）。
- B 依賴的 A 公開 API 多了一個：`Game4026.Interaction.Artifacts.ArtifactDisplayTrigger.OnArtifactRestored`（`event Action<ArtifactRestoredEvent>`）。改動前請通知 B。

## 4c. Museum_Demo 用了 A 的玩家與道具（2026-11-22）

- B 把 A 在 `Museum_test` 的「West gallery play setup」（XR Origin、XR Interaction Manager、背包、`InteractionDesktopMode`）和 `MuseumGuardDesk` 搬進 `Assets/_Game/Scenes/Demo/Museum_Demo.unity`，加上 `Weapons/` 下的 Knife、Axe、Shovel、Bow。A 的場景和 Prefab 檔都沒改。
- 在 Museum_Demo 的 XR Origin 實例上加了 `PlayerTarget`、`PlayerHealth`、`BodyCenter`（B 的 `PlayerBodyAnchor`），場景另放 `DamageBridgeInstaller`。
- B 依賴的 A 行為多了兩個：
  - `InteractionDesktopMode` 的桌面相機掛 `PlayerTarget` + `DesktopPlayerDamageReceiver`（把傷害轉給 XR Rig 的 `PlayerHealth`）。B 的 DemoFlow 會改綁 `PlayerTarget.Current`，並從轉接元件的 `Health` 讀血量。
  - `PlayerHealth.CurrentHealth` / `MaxHealth`。因為沒有 `ResetHealth()`，B 重來時暫時用反射呼叫 `CurrentHealth` 的 private setter 補血。

## 4d. 正式 demo 場景（2026-11-22）

- 和組長確認：**正式 demo 是 `Assets/_Game/Scenes/Demo/Museum_Demo.unity`**。A 的 `Museum_Demo_VR` 當 A 的測試場景（場景檔沒辦法合併，兩邊各改一份會越差越遠）。
- B 已把 Museum_Demo_VR 的 `Museum VR Setup`、`MuseumGuardDesk`、`Museum Floor Confirmation`、`Guard Desk Weapon Stand`、`Boss Realm Weapon Stand` 和 DemoFlow 上的 `MuseumDemoRun` 搬進 Museum_Demo（A 的場景檔沒改）。A 之後的玩家/道具改動請盡量做在 Prefab 或程式裡，場景層的接線改動請告訴 B，由 B 改 Museum_Demo。
- DemoFlow 新增 **Wait For Player Ready**（預設開）：第一次開場等玩家拿起武器（B 的 `Bridges/DemoStartOnGrab`）或離起點 5 m 才倒數。Museum_Demo_VR 沒有 DemoStartOnGrab，所以那裡會等到玩家走開 5 m；要舊行為可取消勾選。
- 兩個 A 的 PlayMode 測試（`MuseumBackpackCarouselTests.ShoulderStow…`、`MuseumFloorPickupTests.PhysicalCrouch…`）在 B 的電腦失敗（模擬 Grip 抓不起手電筒），B 當時連著 Quest Link，可能是真手把干擾，請 A 在自己電腦確認。

## 5. 要問 A 的問題

1. 文物歸位事件：B 目前接的是 `ArtifactDisplayTrigger.OnArtifactRestored`。這是正式的來源嗎？還是應該用 `ArtifactRestorationTracker.OnArtifactRestored`？（Tracker 不是場景元件，由誰建立、要怎麼拿到它？）
2. 可以在 `PlayerHealth` 加一個 `public void ResetHealth()`（補滿）嗎？B 的 demo 重來時會優先用它，就不用反射改 private setter。
3. 不同武器（棍、彎刀、矛）要不要有不同的傷害係數？目前一律用同一組預設值，可以在每把武器的元件上分別調。
