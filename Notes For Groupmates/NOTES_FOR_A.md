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
- **`PlayerTarget`（M2）**：B 會在 `Scripts/Contracts/` **新增** `PlayerTarget.cs`（讓敵人找到玩家的頭和身體位置，既有檔案不動），之後要請 A 把它掛在 XR Rig 上，並指定 head（HMD 相機）和 body。
- **Layer**：敵人的 Collider 要放在武器 `targetMask` 包含的 Layer 上。如果之後新增 Enemy Layer，A 的武器 `targetMask` 和 `blockerMask` 可能要跟著調。

## 5. 要問 A 的問題

1. 文物歸位事件（M3）：場景中「正式」的來源是哪一個，`ArtifactRestorationTracker.OnArtifactRestored` 還是 `ArtifactDisplayTrigger.OnArtifactRestored`？B 會寫橋接去訂閱它，A 那邊不用改。
2. 不同武器（棍、彎刀、矛）要不要有不同的傷害係數？目前一律用同一組預設值，可以在每把武器的元件上分別調。
