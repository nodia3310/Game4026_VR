# 敵人系統開發規格（給 AI Agent 閱讀）

> 專案：VR Project Anubis（第一人稱 VR 動作 + 文物歸位解謎，體驗 15–20 分鐘）
> 負責模組：敵人 AI、敵人生成、戰鬥受擊（原企劃中的「負責人 B」）
> 工作分支：`feature/enemy-ai`
> 標示「（待確認）」的項目尚未定案，遇到時先用預設值實作，並在回報中列出。

---

## 0. Agent 工作規則（必讀）

1. 只在 `feature/enemy-ai` 分支工作，不要 commit 到 `main`。
2. 只能新增或修改以下路徑：
   - `Assets/_Game/Scripts/Enemy/**`
   - `Assets/_Game/Scripts/Contracts/**`（共用合約，**修改既有檔案或簽名前必須先詢問我**，因為負責人 A 也在使用）
   - `Assets/_Game/Data/Enemies/**`
   - `Assets/_Game/Prefabs/Enemies/**`
   - `Assets/_Game/Animations/Enemy/**`（敵人動畫與 Animator Controller）
   - `Assets/_Game/Scenes/Test/EnemyAI.unity`
   - `Assets/_Game/Tests/EditMode/Enemy/**`、`Assets/_Game/Tests/PlayMode/Enemy/**`
3. **不要**修改主場景、負責人 A 的資料夾（`Scripts/Interaction/**`、`Scripts/Player/**`），也不要修改 `ProjectSettings/` 或 `Packages/manifest.json`。若需要新套件或新的 Layer／Tag，先列出需求問我。
4. 不要手寫 `.meta` 檔或 GUID；新腳本由 Unity 開啟時自動產生 `.meta`。
5. 語言：C#，Unity 6.6，URP。命名空間 `Game4026.Enemy`；共用合約為 `Game4026.Contracts`。（舊名稱 `_Project` / `Anubis.*` 已棄用）
6. 每完成一個里程碑，回報：新增／修改的檔案清單、需要我在 Editor 手動做的設定（掛元件、建 Layer、烘焙 NavMesh 等）、未解決的問題。
7. 不確定的設計決策先問，不要自行擴大範圍（例如不要自己做 UI、音效系統、玩家血量）。

---

## 1. 範圍

### 我負責（要做）
- 敵人血量、弱點、受擊硬直、死亡
- 敵人 AI 狀態機：生成 → 巡邏/待機 → 追擊 → 攻擊 → 受擊 → 死亡
- 敵人對玩家的攻擊與傷害判定（呼叫玩家的受擊介面）
- 敵人生成：生成點、波次、同時在場數量上限、物件池
- 遭遇戰（Encounter）控制：開始、清場、因文物歸位而強制清除
- 三件文物都歸位後觸發 Boss 戰（暫時由我這邊處理，日後可能移交給「關卡流程管理器」）
- 法老 Boss 的基本架構（分階段）
- 測試場景與假玩家／假武器，讓我不用 VR 頭盔、不依賴負責人 A 也能測試

### 不是我負責（不要做）
- VR 手部抓取、武器揮擊判定、拉弓（負責人 A）
- 玩家血量、死亡與重生、手錶 UI（尚未指派，只需要呼叫介面）
- 音效播放系統與 BGM 切換（我只需要提供事件）
- 文物歸位判定（A 負責，我只監聽事件）

---

## 2. 技術限制

- VR 必須維持高幀率：
  - `Update` 內不呼叫 `GetComponent`、`Find`、LINQ，不產生每幀 GC 配置。
  - 感知檢查（找玩家、視線）用固定間隔執行（預設每 0.2 秒），不要每幀做。
  - 同時在場敵人預設上限 4 隻（可在資料中調整）。
  - 敵人用物件池（`UnityEngine.Pool.ObjectPool<T>`），不要反覆 Instantiate / Destroy。
- 尋路使用 AI Navigation 套件（`com.unity.ai.navigation`，已安裝 2.0.16）的 `NavMeshSurface` + `NavMeshAgent`。
- 動畫：目前敵人模型可能**沒有骨架和動畫**。程式必須在沒有 Animator 的情況下也能運作（用計時器代替動畫事件）；有 Animator 時才設定參數。
- 用 Assembly Definition 分隔模組：
  - `Game4026.Contracts`（既有，不引用任何專案組件）
  - `Game4026.Enemy`（只引用 `Game4026.Contracts`，**不可**引用 A 的組件，例如 `Game4026.Interaction`）
  - `Game4026.Enemy.EditModeTests` / `Game4026.Enemy.PlayModeTests`

---

## 3. 共用合約（`Assets/_Game/Scripts/Contracts/`，`Game4026.Contracts`）

以下合約已經存在，A 也在使用，**照現有簽名實作，不要修改**：

```csharp
namespace Game4026.Contracts
{
    // 由 B（敵人）實作，A 的武器在被擊中的 Collider 上以 GetComponentInParent<IDamageable>() 取得後呼叫。
    // damage：套用弱點倍率前的基礎傷害；hitPoint：世界座標。
    // A 會先過濾同一次揮擊的重複接觸；血量、無敵、死亡規則由 B 負責。
    public interface IDamageable
    {
        void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint);
    }

    // 由 A（玩家）實作，B 的敵人攻擊命中時呼叫。B 負責避免重複命中。
    public interface IPlayerDamageReceiver
    {
        void ApplyPlayerDamage(float damage);
    }

    // A 在文物歸位並鎖定後發出的資料物件（不是全域事件匯流排）。
    // 以 ArtifactId 去重，每次新的一輪重設。
    public sealed class ArtifactRestoredEvent { string ArtifactId; ArtifactType Type; }

    public enum ArtifactType { Unknown, ... }   // 既有，數值固定，只能往後新增

    // （B 在 M2 新增）掛在玩家 XR Rig 上，讓敵人找到玩家：Head、Body、Damageable（IPlayerDamageReceiver）、PlayerTarget.Current
    public class PlayerTarget : MonoBehaviour { ... }
}
```

**提案（未採用，待確認）**：原規格曾提出 `DamageInfo` 結構（含 `Source`、`HitVelocity`、`WeaponType`）、`IPlayerDamageable.ApplyPlayerDamage(amount, sourcePosition)`、`GameEvents.ArtifactRestored`（全域事件）。目前都沿用既有簽名；下文提到這些的地方，以既有合約的對應方式實作（例如 `hitCooldown` 是全域短暫無敵，因為合約沒有 `Source`）。

實作時每個類別各自一個檔案（MonoBehaviour 的檔名必須與類別名稱相同）。

**傷害責任分工：**
- A 決定「這一下算不算命中」（最低揮擊速度）和基礎傷害 `Amount`。
- B（我）決定弱點倍率、受擊冷卻、硬直、死亡。

---

## 4. 要實作的元件

### 4.1 `EnemyData`（ScriptableObject）
每種敵人一份資料，數值都可調整。以下是初始值，之後會再平衡。

| 欄位 | 說明 | 初始值 |
| --- | --- | --- |
| `maxHealth` | 血量 | 100 |
| `weakPointMultiplier` | 弱點傷害倍率 | 2.5 |
| `hitCooldown` | 同一個 `Source` 再次造成傷害的最短間隔（秒） | 0.3 |
| `staggerThreshold` | 單次傷害 ≥ 此值或打中弱點時進入硬直 | 25 |
| `staggerDuration` | 硬直時間（秒） | 0.5 |
| `moveSpeed` / `chaseSpeed` | 巡邏速度 / 追擊速度 | 1.0 / 2.2 |
| `detectionRange` | 發現玩家的距離 | 12 |
| `loseTargetRange` | 失去目標的距離 | 18 |
| `attacks` | 這種敵人會用的攻擊清單（`AttackDefinition[]`，見 4.5）；空的話用一招預設近戰 | — |
| `globalAttackCooldown` | 任一招出完後，到下一招之前的最短間隔（秒） | 1.2 |
| `locomotion` | `Animated`（有骨架，用 Animator）或 `Statue`（無骨架，用程式移動，見 4.9）（M4） | Animated |
| `bodyDamageMultiplier` | 打中非弱點部位的傷害倍率（Statue of the Sphinx 用 0.2）（M4） | 1.0 |
| `keepDistance` | 玩家進入此距離內時往後退開（0 = 不退；Ka Statue 用 3）（M4） | 0 |
| `spawnDuration` | 生成出場時間（秒），期間無敵且不行動 | 1.5 |
| `despawnDuration` | 死亡或強制清除後消失的時間（秒） | 1.5 |

### 4.2 `EnemyHealth : MonoBehaviour, IDamageable`
- 處理 `TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint)`：
  - 生成中或已死亡 → 忽略。
  - 上次扣血後 `hitCooldown` 內 → 忽略（合約沒有 Source 參數，因此是全域短暫無敵）。
  - `isWeakPoint == true` → 傷害乘上 `weakPointMultiplier`（只乘一次）。
  - 達到硬直條件 → 通知狀態機進入 Hit 狀態。
  - 血量 ≤ 0 → 進入 Dead 狀態。
- 提供事件（給音效、特效、VR 震動用）：
  - `event Action<EnemyHealth, EnemyDamageResult> Damaged`（含實際扣血量、擊中點、是否弱點、是否硬直）
  - `event Action<EnemyHealth> Died`
- 物件池回收時要能 `ResetState()`。

### 4.3 `WeakPoint : MonoBehaviour, IDamageable`
- 掛在子物件的弱點 Collider 上（耳朵、頭部、權杖、額頭紋章等，見第 5 節）。
- 收到傷害時，以 `isWeakPoint = true` 轉發給父物件的 `EnemyHealth`。
- （M4）`bool IsExposed`（預設 true）：為 false 時代表弱點被保護住（例如伊西斯用翅膀包住自己），這時的傷害只有 10%，並觸發 `Blocked` 事件（給「鏗」的音效與火花）。由 4.10 的行為模組切換。
- 在 Scene 視圖用 Gizmo 標示弱點位置（方便我調整）。

### 4.4 `EnemyController` + 狀態機
- 狀態以 class 實作（每個狀態有 `Enter / Tick / Exit`），不要用一大串 `switch`。
- 狀態轉換：

| 目前狀態 | 條件 | 下一個狀態 |
| --- | --- | --- |
| Spawn | `spawnDuration` 結束 | Patrol（有巡邏點）或 Idle |
| Idle / Patrol | 玩家在 `detectionRange` 內且有視線 | Chase |
| Chase | `AttackSelector` 找到一招可用的攻擊（見 4.5） | Attack |
| Chase | 距離 > `loseTargetRange` 或超過 5 秒無視線 | Patrol / Idle |
| Attack | 前搖 → 判定 → 收招結束 | Chase |
| 任何（Dead 除外） | 達到硬直條件 | Hit |
| Hit | `staggerDuration` 結束 | Chase |
| 任何 | 血量 ≤ 0 | Dead |
| 任何 | 被 Encounter 強制清除 | Dead（不計入擊殺） |

- 視線檢查：從敵人眼睛位置 Raycast 到 `PlayerTarget.Current.Head`，只檢查環境 Layer。
- 追擊時 `NavMeshAgent.SetDestination` 不要每幀呼叫，每 0.2 秒或玩家移動超過 0.5 公尺才更新。
- 找不到 `PlayerTarget.Current` 時保持 Idle / Patrol，不要報錯。

### 4.5 攻擊系統（用少量動畫做出多種攻擊）

**目標：阿努比斯目前只有一個 Attack 動畫，要靠資料做出 4–5 種不同的攻擊。** 新增攻擊時只新增資料資產，不寫新程式。

> **實作狀態（M2.5 完成）**：`AttackDefinition`、`AttackSelector`、`AttackRunner`（純 C#）與 `EnemyAttack`（元件，承擔規格的 `EnemyAttackRunner` 職責）已完成，支援 `Melee`、`Lunge`、`Combo`、`AreaSlam`；`Projectile`、`Charge` 是 M4，目前不會被選到。招式資產放在 `Assets/_Game/Data/Enemies/Attacks/`。

#### `AttackDefinition`（ScriptableObject，一招一份）

| 欄位 | 說明 |
| --- | --- |
| `attackType` | `Melee`、`Lunge`、`Combo`、`Projectile`、`AreaSlam`、`Charge` |
| `animatorState` | 要交叉淡入的 Animator 狀態名稱（預設 `Attack`）。實作用狀態名稱而不是 Trigger，比較好控制起手停格與連段 |
| `clip` | （選填）該狀態的動畫，用它的長度讓最後一下的收招等動畫播完 |
| `animSpeed` | 動畫播放速度倍率（快速揮擊 1.5、重擊 0.6） |
| `mirror` | 是否左右鏡像（只有 Humanoid 骨架有效）。實作：Controller 有 `<狀態>_Mirror` 就改播它 |
| `clipStartTime` | 從動畫的第幾秒開始播（連段後面幾下跳過起手用） |
| `telegraphTime` | 額外預警時間（秒）。實作：動畫播到 `telegraphPoseTime` 的起手姿勢時**停格**這麼久，再繼續揮下 |
| `telegraphPoseTime` | 預警停格的姿勢在動畫第幾秒（速度 1 時；負數 = `hitTime` 的 70%） |
| `hitTime` | 動畫第幾秒（速度 1 時）擊中，判定時間 = 預警 + (`hitTime` − `clipStartTime`) ÷ `animSpeed`。要和動畫的擊中格一致 |
| `recoveryTime` | 出招後的硬直時間（秒），玩家反擊的空檔；連段中間的步驟則是到下一下的間隔。有 `clip` 時取它和「動畫剩餘時間」較長的那個 |
| `parryWindow` | 擊中前幾秒內可以被玩家武器招架（見 4.5b）；0 = 不能招架 |
| `minRange` / `maxRange` | 可以出這招的距離範圍（公尺） |
| `hitRange` / `hitAngle` | 命中判定的距離與扇形角度 |
| `damage` | 對玩家的傷害 |
| `cooldown` | 這招自己的冷卻時間（秒） |
| `weight` | 出招機率權重 |
| `lungeDistance` / `lungeDuration` | `Lunge` 用：出招時往前衝的距離與時間 |
| `comboSteps` | `Combo` 用：依序出的 `AttackDefinition[]` |
| `projectilePrefab` / `projectileSpeed` / `muzzle` 名稱 | `Projectile` 用（M4，尚未加入） |
| `slamRadius` | `AreaSlam` 用：落地衝擊的半徑 |
| `partPose` | （M4，尚未加入）切零件的雕像用：預警時要擺出的零件姿勢名稱（例如 `Raise`、`WingsOpen`），見 4.9；空白代表不動零件 |

#### `AttackSelector`
- 在 Chase 狀態每 0.2 秒檢查一次：從 `EnemyData.attacks` 挑出「距離在 `minRange`–`maxRange` 內、自己的冷卻已結束、`globalAttackCooldown` 已結束」的攻擊，依 `weight` 隨機選一招。
- 有選到 → 進入 Attack 狀態並帶入這招。
- 實作細節：自己的冷卻從出招開始算；`globalAttackCooldown` 從整招結束（或被打斷）開始算；攻擊速度倍率（Boss）同時縮短冷卻。
- 沒有招式可出時：玩家在「近身招式（`minRange` = 0）的最大 `maxRange`」內就停下面向玩家，否則繼續追（所以遠距離招式在冷卻時會走近改用近戰）。

#### `EnemyAttackRunner`（執行一招）
1. **預警**：觸發 `AttackTelegraph` 事件（音效、發光）；動畫播到起手姿勢停格 `telegraphTime` 秒。停格結束前持續面向玩家，揮下後方向鎖定（玩家側閃可以躲）。
2. **播動畫**：`EnemyAnimator` 在 `StepStarted` 時交叉淡入 `animatorState`（鏡像用 `_Mirror` 狀態），`AttackSpeed = animSpeed × 攻速倍率`，停格時設 0。
3. **命中時機**：用計時（`hitTime`），不使用動畫事件；有 Animator 與沒有 Animator 時間完全一樣。
4. **依類型執行**：
   - `Melee`：距離 + 角度判定（玩家 `Body` 在 `hitRange` 內且在 `hitAngle` 扇形內），命中呼叫 `ApplyPlayerDamage`。
   - `Lunge`：預警結束時鎖定方向，到命中前用 `NavMeshAgent.Move` 往玩家推進（最多 `lungeDistance`，停在玩家前 0.9 公尺），不能穿牆；推進結束時做 Melee 判定。
   - `Combo`：依序執行 `comboSteps`，中間不回 Chase。整招的預警、距離、冷卻、權重、最後收招用 Combo 資產本身；每一下的動畫、傷害、判定範圍用步驟資產。
   - `Projectile`：從 `muzzle` 子物件位置發射 `EnemyProjectile`（見下方）。（M4）
   - `AreaSlam`：以敵人為中心、`slamRadius` 內的玩家受傷（玩家高度不限）。
   - `Charge`：見 4.10 的衝撞行為。（M4）
5. **收招**：等 `recoveryTime`，回到 Chase。
- 被硬直打斷時，整招取消。
- 沒有 Animator 的敵人跳過第 2 步，其餘照常。

#### Animator 設定（AC_Anubis，已由 agent 設定完成）
- 參數：`Locomotion`（Float）、`AttackSpeed`（Float，預設 1）。
- 攻擊狀態都勾 Speed Multiplier 綁定 `AttackSpeed`：
  - `Attack`：QuickerAttack（擊中點 0.77 秒、起手姿勢 0.55 秒）
  - `Attack_Mirror`：QuickerAttack，Mirror 勾選（反手揮擊、連段第二下）
  - `JumpAttack`：Standing Melee Run Jump Attack（擊中點 1.58 秒；Root 旋轉與高度烘進姿勢，水平位移不套用，前衝交給 NavMeshAgent）
- 不需要動畫事件 `OnAttackHit`（命中用計時）。
- 阿努比斯模型的 Rig 已確認為 **Humanoid**，`mirror` 可以使用。之後加入的 Mixamo 動畫也會以 Humanoid 套用到它身上。新動畫只要在 Controller 加一個狀態、在 `AttackDefinition` 填狀態名稱和擊中秒數。

#### 阿努比斯的初始攻擊組合（用 QuickerAttack + Jump Attack 兩個動畫）

| 招式 | 類型 | animSpeed | mirror | telegraph | 距離 | 傷害 | 說明 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 快速揮擊 | Melee | 1.5 | 否 | 0.2 | 0–1.8 | 8 | 基本招，最常出 |
| 反手揮擊 | Melee | 1.3 | 是 | 0.3 | 0–1.8 | 8 | 方向相反 |
| 重擊 | Melee | 0.6 | 否 | 0.8 | 0–2.0 | 20 | 預警時武器發光，收招硬直長 |
| 突進斬 | Lunge（JumpAttack 動畫） | 1.2 | 否 | 0.5 | 3–5 | 12 | 從遠處衝過來 |
| 三連斬 | Combo | 1.3 / 1.3 / 0.8 | 否/是/否 | 0.4 | 0–1.8 | 6 / 6 / 12 | 最後一下最重 |

實際資產的其他數值（`Assets/_Game/Data/Enemies/Attacks/Anubis_*.asset`）：冷卻 1.5 / 2 / 6 / 6 / 7 秒，權重 3 / 2 / 1.5 / 2 / 1，收招 0.3 / 0.3 / 1.0 / 0.4 / 0.6 秒（有動畫時會等動畫播完），`globalAttackCooldown` 0.8。三連斬第二、三下從動畫 0.25 秒開始。

### 4.5b 招架（Parry）與攻擊提示（已完成）

- **武器**：阿努比斯（`Enemy_AnubisGuard`）雙手各拿一把匕首（`Assets/Models/Weapons/dagger_low_poly.glb`），掛在 `mixamorig:RightHand` / `LeftHand` 底下的 `Weapon_Dagger`。鏡像招式（Humanoid mirror）是左手揮，所以雙持才不會出現空手揮砍。
- **`EnemyWeapon : MonoBehaviour, IDamageable`**：掛在匕首上（刀身有非 Trigger 的 CapsuleCollider）。A 的近戰武器照合約呼叫 `TakeDamage` → 在招架窗內就招架；窗外只是被刀擋住，不會傷到本體。**不需要改共用合約。**
- **招架窗**：每一下在擊中前 `parryWindow` 秒內（阿努比斯預設 0.35；突進斬 0.4；三連斬每下 0.3），隨攻速倍率縮短。所有招式都能招架（使用者決定）。
- **招架成功**：整招取消（連段剩下的也取消），不會打到玩家；敵人硬直 `parryStaggerDuration`（1.5 秒），期間受到的傷害 ×`parriedDamageMultiplier`（1.5）。硬直中再被打出一般硬直，不會縮短剩餘的招架硬直。事件：`EnemyAttack.Parried`、`EnemyController.Parried`、`EnemyWeapon.Deflected` / `Blocked`（給音效、火花、震動）。
- **箭不能招架**：`ArrowDamageBridge` 打到 `EnemyWeapon` 直接忽略（決定，可改）。
- **攻擊提示 `AttackIndicator`**：刀尖上的面向鏡頭光點（Unlit 加算材質 `M_AttackGlint`，**不用真光源**）。預警停格時是小的橘色光；**招架窗打開的瞬間閃一下亮黃白光**（＝「攻擊來了，現在擋」），到擊中前縮小；招架成功時白色爆閃。光點跟著正在揮的那隻手的匕首。
- **桌面測試**：`MockWeapon` 按 **F** 對 3 公尺內最近的敵人武器做一次招架（滑鼠點不到移動中的匕首）。

#### `EnemyProjectile : MonoBehaviour, IDamageable`（M4）
- 用物件池管理；直線飛行，碰到環境就消失，碰到玩家就呼叫 `ApplyPlayerDamage`。
- **可被反彈**：它本身實作 `IDamageable`，所以玩家的武器打到它時，A 的武器會照合約呼叫 `TakeDamage` → 投射物改為飛回發射者，並改成「玩家方」：打到敵人時呼叫敵人的 `IDamageable.TakeDamage`（打到 `WeakPoint` 就算弱點傷害）。**不需要改共用合約。**
- 用自發光或 Unlit 材質，**不要掛真正的光源**；同時存在的投射物上限 8 個。

### 4.6 生成系統
- **`SpawnPoint`**：場景中的標記，帶 Gizmo。生成前檢查位置是否在 NavMesh 上（`NavMesh.SamplePosition`），而且距離玩家至少 3 公尺，避免怪物生在玩家臉上。
- **`WaveDefinition`**（ScriptableObject）：
  - 一個波次 = 多組「敵人資料 + 數量」
  - `spawnInterval`：同一波內每隻敵人的生成間隔（預設 1.5 秒）
  - `startDelay`：這一波開始前的延遲
- **`EnemyPoolManager`**：依敵人 Prefab 各自建立 `ObjectPool`，並可預熱（先產生指定數量）。
- **`EncounterController`**：每個展廳一個，負責一場遭遇戰。
  - 欄位：`WaveDefinition[] waves`、`SpawnPoint[] spawnPoints`、`int maxAlive`、`ArtifactType linkedArtifact`、`bool startOnPlayerEnter` + 觸發用 Collider。
  - 公開 API：
    - `void StartEncounter()`
    - `void ForceClear()`：所有在場敵人播放消失效果後回收，並停止後續波次
    - `bool IsActive { get; }`、`bool IsCleared { get; }`
    - `event Action<EncounterController> Started`、`event Action<EncounterController> Cleared`
  - 同時在場數量達到 `maxAlive` 時，暫停生成，等有敵人死亡再繼續。
  - 全部波次的敵人都死亡 → 觸發 `Cleared`。
- **`EncounterDirector`**（場景中唯一）：
  - 接收 A 發出的 `ArtifactRestoredEvent`（目前經由 `ArtifactEventBridge` 接 A 的 `ArtifactDisplayTrigger.OnArtifactRestored`），以 `ArtifactId` 去重。
  - 收到某件文物歸位 → 對 `linkedArtifact` 相同的 Encounter 呼叫 `ForceClear()`。
  - 三件文物都歸位 → 啟動 Boss 遭遇戰。
  - 提供 `event Action BossEncounterStarted`、`event Action BossDefeated`，給之後的關卡流程與音樂切換使用。

### 4.7 法老 Boss（先做架構）
- 沿用 `EnemyHealth`、狀態機，另外加上階段系統：
  - 第 1 階段（100%–60% 血量）：近戰。
  - 第 2 階段（60%–25%）：召喚小兵（透過 Boss 自己的 `EncounterController` 生成）。
  - 第 3 階段（25%–0%）：攻擊加快、弱點出現。
- 切換階段時有 2 秒無敵與 `PhaseChanged` 事件。
- 招式細節（待確認），先用空殼方法與 TODO 註解。

### 4.8 測試工具（讓我不用頭盔也能測試）
- `MockPlayer`：在 `EnemyAI` 場景中用 WASD 移動、滑鼠轉向，掛 `PlayerTarget` 並實作 `IPlayerDamageReceiver`（受傷時印 Log 並顯示剩餘血量）。
- `MockWeapon`：按滑鼠左鍵對準星指到的物件呼叫 `TakeDamage`，傷害值可在 Inspector 調整，用來測試弱點與硬直。
- `DebugArtifactButton`：按數字鍵 1/2/3 送出對應的 `ArtifactRestoredEvent`，用來測試清場與 Boss 觸發。
- 敵人頭上的 Debug 文字（只在 Editor 顯示）：目前狀態、正在出的招式（連段第幾下）與血量。
- （M4）`MockWeapon` 也要能打到 `EnemyProjectile`，用來測試反彈。

### 4.9 雕像移動（`StatueLocomotion`，無骨架敵人用）
圖鑑裡大部分敵人是被詛咒的**雕像**，沒有骨架，也不需要走路動畫。用程式移動整個模型，效能最好，也更詭異：
- **飄浮滑行**：用 `NavMeshAgent` 移動，模型離地 0.1–0.2 公尺，加上緩慢上下浮動（正弦波，幅度 0.05 公尺）。
- **出招前抖動**：預警期間模型快速小幅抖動（位置 ±0.02 公尺），取代起手動畫。
- **砸地（AreaSlam）**：0.4 秒內升高 1 公尺，停 0.3 秒，0.15 秒砸下。
- **衝撞（Charge）**：見 4.10。
- 全部改的是模型子物件的 local 位置與旋轉，不改根物件（避免和 NavMeshAgent 衝突）。
- 不使用 Animator；如果之後雕像有關鍵影格動畫，再用 Animation 元件播放。

#### `StatuePart`（切零件的雕像用）
部分雕像由組員在 Blender 切出可動零件（翅膀、手臂、頭），零件是身體的子物件，軸心已在關節。
- 掛在零件上，設定幾個**姿勢**：姿勢名稱 + 目標 local 旋轉（例如 `Rest` 0°、`Raise` 手臂抬起 -90°、`WingsClosed` 翅膀收合）。
- `void SetPose(string pose, float duration)`：在 `duration` 秒內轉到該姿勢（用程式插值，不用 Animator）。
- 可選 `aimAtPlayer`：擺出姿勢後，零件持續朝向玩家（例如手臂舉起權杖瞄準）。
- `EnemyAttackRunner` 在預警開始時呼叫 `SetPose(partPose, telegraphTime)`，收招後回到 `Rest`。
- 零件的名稱與軸心見第 5.2 節。

### 4.10 敵人行為模組（依圖鑑弱點設計）
每個模組是一個獨立元件，掛在需要的敵人身上，透過狀態機的事件運作。**MVP 只做標示「MVP」的模組。**

| 模組 | 給誰用 | 行為 | 優先度 |
| --- | --- | --- | --- |
| `WingShield` | 有翼女神伊西斯 | 玩家靠近 3 公尺內或剛被打時，收起翅膀 2.5 秒：翅膀上的 `WeakPoint.IsExposed = false`，且本體受傷減半；之後展翅攻擊，這時弱點暴露 | MVP |
| `ChargeBehavior` | 人面獅身 | 停下對準玩家 1 秒（預警）→ 高速直線衝撞（不用 NavMesh 路徑，用 Raycast 檢查前方）→ 撞到牆就暈眩 3 秒，臉上的弱點只在暈眩時暴露；沒撞牆就減速停下 | MVP |
| `StunOnFlashlight` | 巴斯特雕像（感官敏銳） | 被手電筒照到累積 1 秒 → 暈眩 2 秒。**需要和 A 新增合約**（手電筒要通知被照到的物件），先只做暈眩邏輯與一個 `Stun(float seconds)` 方法，觸發方式待確認 | 之後 |
| `HealAllies` | 鍍金巴斯特 | 每 4 秒替 6 公尺內血量最低的敵人回 15 血，期間項圈發光（弱點暴露加倍傷害） | 之後 |
| `RhythmAttacker` | 巴斯特雕像（叉鈴） | 依固定節拍出招，並觸發 `Beat` 事件給音效；叉鈴被打壞後節奏亂掉，攻擊間隔變長 | 之後 |
| `TopplePlinth` | 還願頭像 | 底座被打到 3 次就倒下，進入長時間暈眩 | 之後 |

---

## 5. 敵人種類

敵人來自遊戲內的展品圖鑑：**圖鑑介紹裡的粗體字就是弱點**，玩家讀圖鑑就能知道該打哪裡。寫著「remains a silent display」的展品不是敵人。

### 5.1 MVP（先做這 4 種）

> 目前專案裡的 Prefab 還是舊名單（木乃伊、阿努比斯衛兵、塞克邁特、法老，都用 Anubis 模型）。`Enemy_AnubisGuard` 就是 Statue of Anubis，已掛 5 招；換成下表的名單（卡雕像、伊西斯）屬於 M4。

| 敵人 | 弱點（圖鑑） | 移動 | 攻擊 | 玩法重點 |
| --- | --- | --- | --- | --- |
| 木乃伊（Mummy of Kha） | 暴露的頭部 | Animated（之後再找模型；先用膠囊體） | 1 招慢速 Melee | 成群出現，教玩家打弱點 |
| 阿努比斯雕像（Statue of Anubis） | 耳朵 | Animated（**已有骨架與 Idle / Walk / Run / Attack / Death 動畫**） | 4.5 節的 5 招 | 招式多變，弱點在高處，要舉手瞄準 |
| 卡雕像（Ka Statue） | 手上的權杖 | Statue + 切零件（手臂） | Projectile（權杖發射魔法彈） | 把魔法彈**打回去**，或打掉權杖 |
| 有翼女神伊西斯（Winged Goddess Isis） | 翅膀 | Statue + 切零件（雙翅） | Melee + `WingShield` | 等她展翅的空檔才打得動 |

### 5.1b 之後擴充（或當 Boss 戰的召喚小兵）

| 敵人 | 弱點（圖鑑） | 建議玩法 |
| --- | --- | --- |
| 人面獅身（Statue of the Sphinx） | 人臉（身體幾乎打不壞） | `ChargeBehavior`：閃開讓它撞牆，暈眩時打臉 |
| 黑曜石神諭（The Obsidian Oracle） | 額頭紋章 | Projectile 光束，用弓箭射額頭 |
| 覺醒卡雕像（The Awakened Ka Statue） | 額頭的眼鏡蛇（uraeus） | Projectile 噴吐 |
| 巴斯特雕像（Statue of Bastet） | 叉鈴 | `RhythmAttacker`、`StunOnFlashlight` |
| 鍍金巴斯特（The Gilded Bastet） | 項圈上的荷魯斯之眼 | `HealAllies`，玩家要優先處理 |
| 年輕木乃伊（Younger Mummy） | 被詛咒的身體（全身都是弱點） | 弱小、數量多的雜兵 |
| 還願頭像（The Votive Head） | 金色蓮花底座 | `TopplePlinth` |
| 法老 Boss | 第三階段出現 | 見 4.7 |

### 5.2 移動方式與零件

| 移動方式 | 敵人 | 說明 |
| --- | --- | --- |
| Animated（骨架動畫） | Statue of Anubis | Humanoid，已有 Idle / Walk / Run / Attack / Death |
| Statue（整座一起動） | Mummy of Kha、Younger Mummy、The Obsidian Oracle、The Awakened Ka Statue、Statue of the Sphinx、The Gilded Bastet | 只用 `StatueLocomotion`，不切零件 |
| Statue + 切零件 | 見下表 | `StatueLocomotion` + `StatuePart` |

| 雕像 | 零件（物件名稱） | 軸心 | 姿勢 |
| --- | --- | --- | --- |
| Winged Goddess Isis | `WingedGoddessIsis_Wing_L`、`WingedGoddessIsis_Wing_R` | 翅膀根部 | `Rest`（半開）、`WingsOpen`（全開）、`WingsClosed`（收合，防禦） |
| Ka Statue | `KaStatue_Arm`（含權杖） | 肩膀 | `Rest`、`Raise`（舉起並瞄準玩家）、`Swing`（橫掃） |
| The Votive Head | `VotiveHead_Head` | 脖子底部 | `Rest`、`TiltBack`（後仰蓄力）、`Headbutt`（前傾） |
| Statue of Bastet | `StatueOfBastet_Arm`（含叉鈴） | 肩膀 | `Rest`、`Shake`（搖叉鈴）、`Swing`（揮擊） |

零件模型還沒做好前，先用身體上的空物件代替零件，程式照常運作。

### 5.3 各敵人招式表（數值為初始值，之後會再平衡）

每一行是一份 `AttackDefinition`。距離單位為公尺，時間單位為秒。

**Mummy of Kha**（血量 60，慢速，成群出現）

| 招式 | 類型 | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- |
| 撲抓 | Melee | 0.6（抖動） | 0–1.5 | 8 | 2.5 | 基本招 |
| 撲身 | Lunge（2 公尺） | 0.8 | 2–4 | 10 | 6 | 突然撲過來 |

**Younger Mummy**（血量 30，雜兵；整個身體都是弱點，`WeakPoint` 碰撞體包住全身）

| 招式 | 類型 | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- |
| 撞擊 | Melee | 0.4 | 0–1.3 | 5 | 2 | 數量多、單隻弱 |

**Statue of Anubis**（血量 120）：見 4.5 節的 5 招。

**Ka Statue**（血量 90；保持距離，玩家靠近 3 公尺內會往後滑開）

| 招式 | 類型 | partPose | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 權杖魔法彈 | Projectile（6 m/s） | `Raise` | 0.8（權杖發光） | 4–12 | 10 | 3 | 可被打回去 |
| 三連發 | Combo（3 發 Projectile，間隔 0.3） | `Raise` | 1.0 | 4–12 | 6 ×3 | 8 | 要連續揮擊反彈 |
| 權杖橫掃 | Melee | `Swing` | 0.5 | 0–1.8 | 10 | 4 | 被近身時才用 |

**Winged Goddess Isis**（血量 100；`WingShield`）

| 招式 | 類型 | partPose | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 翅膀橫掃 | Melee | `WingsOpen` | 0.6 | 0–2.2 | 12 | 3 | 展翅時弱點暴露 |
| 展翅衝擊 | AreaSlam（半徑 3） | `WingsOpen` | 1.0 | 0–3 | 10 | 7 | 整座升起後拍翅落下 |

**The Awakened Ka Statue**（血量 90）

| 招式 | 類型 | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- |
| 眼鏡蛇噴吐 | Projectile（8 m/s） | 0.6（額頭的眼鏡蛇發光） | 3–10 | 8 | 2.5 | 比卡雕像快、傷害低 |
| 詛咒震波 | AreaSlam（半徑 2.5） | 1.0 | 0–2.5 | 12 | 6 | 被近身時使用 |

**The Obsidian Oracle**（血量 80；飄浮的半身像）

| 招式 | 類型 | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- |
| 紋章光束 | Projectile（12 m/s） | 1.2（額頭紋章蓄力發光） | 5–15 | 15 | 5 | 很快，要看預警提早閃 |
| 墜落 | AreaSlam（半徑 3） | 0.8 | 0–3 | 15 | 6 | 升高後整座砸下 |

**Statue of the Sphinx**（血量 200，小 Boss；身體受到的傷害只有 20%，只有暈眩時臉部弱點暴露；`ChargeBehavior`）

| 招式 | 類型 | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- |
| 衝撞 | Charge | 1.0（停下、對準） | 4–15 | 20 | 5 | 撞牆後暈眩 3 秒 |
| 前掌壓 | AreaSlam（半徑 2.5） | 0.8 | 0–2.5 | 15 | 4 | 被近身時使用 |

**Statue of Bastet**（血量 80；`RhythmAttacker`，節拍 0.8 秒一拍）

| 招式 | 類型 | partPose | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 節奏音波 | Projectile（7 m/s） | `Shake` | 1 拍 | 3–10 | 6 | 每 2 拍 | 跟著節拍發射 |
| 叉鈴揮擊 | Melee | `Swing` | 0.5 | 0–1.8 | 8 | 3 | 被近身時使用 |

**The Gilded Bastet**（血量 70；`HealAllies`，盡量留在其他敵人後方）

| 招式 | 類型 | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- |
| 撲擊 | Lunge（2.5 公尺） | 0.6 | 2–4 | 8 | 5 | 只在附近沒有其他敵人時主動攻擊 |

**The Votive Head**（血量 70；`TopplePlinth`）

| 招式 | 類型 | partPose | 預警 | 距離 | 傷害 | 冷卻 | 說明 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 頭槌 | Melee | `TiltBack` → `Headbutt` | 0.7 | 0–1.8 | 10 | 3 | 後仰蓄力再撞 |
| 跳砸 | AreaSlam（半徑 2） | — | 0.9 | 0–2 | 10 | 6 | 整座跳起砸下 |

- 沒有模型或骨架的敵人，先用膠囊體 + 方塊頭作為佔位模型，弱點用一個小球標示。
- 無毛貓模型（CC BY-NC-ND）**不要使用**；貓類敵人之後另找模型。
- 阿努比斯模型來源若為 Sketchfab（CC BY），需在 Credits 標註作者。

---

## 6. 資料夾結構

```
Assets/_Game/
├─ Scripts/
│  ├─ Contracts/         Game4026.Contracts.asmdef（既有，共用）
│  └─ Enemy/             Game4026.Enemy.asmdef
│     ├─ Core/           EnemyController, EnemyHealth, WeakPoint, EnemyAttack, EnemyAnimator（之後：StatueLocomotion）
│     ├─ Combat/         AttackType, AttackDefinition, AttackSelector, AttackRunner（之後：EnemyProjectile）
│     ├─ Behaviours/     （M4）WingShield, ChargeBehavior（之後：StunOnFlashlight, HealAllies, RhythmAttacker, TopplePlinth）
│     ├─ States/         SpawnState, IdleState, PatrolState, ChaseState, AttackState, HitState, DeadState（之後：StunnedState）
│     ├─ Spawning/       SpawnPoint, WaveDefinition, EnemyPoolManager, EncounterController, EncounterDirector
│     ├─ Boss/           PharaohBoss, BossPhase
│     ├─ Data/           EnemyData
│     └─ Debug/          MockPlayer, MockWeapon, DebugArtifactButton
├─ Data/Enemies/         各敵人的 EnemyData、WaveDefinition 資產
│  └─ Attacks/           AttackDefinition 資產
├─ Animations/Enemy/     敵人動畫與 Animator Controller
├─ Prefabs/Enemies/
├─ Scenes/Test/EnemyAI.unity
└─ Tests/
   ├─ EditMode/Enemy/    Game4026.Enemy.EditModeTests.asmdef
   └─ PlayMode/Enemy/    Game4026.Enemy.PlayModeTests.asmdef
```

---

## 7. 里程碑與驗收標準

### M1：共用介面與受擊
- [x] `Game4026.Enemy` asmdef 建立，正確引用 `Game4026.Contracts` 並可編譯
- [x] `EnemyHealth`、`WeakPoint`、`EnemyData` 完成
- [x] EditMode 測試：弱點倍率、受擊冷卻、死亡只觸發一次、`ResetState` 後血量恢復

### M2：AI 與基本攻擊
- [x] 狀態機完成，所有狀態轉換符合第 4.4 節的表格
- [x] `AttackDefinition`、`AttackSelector`、`EnemyAttackRunner`（實作為 `EnemyAttack` + `AttackRunner`）完成，先支援 `Melee`
- [x] 在 `EnemyAI` 用 `MockPlayer` 測試：會發現、追擊、預警後攻擊、被打會硬直、死亡
- [x] 沒有 Animator 的佔位模型也能完整運作

### M2.5：阿努比斯的多種攻擊
- [x] 支援 `Lunge`、`Combo`（另外也支援 `AreaSlam`）；Animator 的 `AttackSpeed` 與鏡像生效（鏡像用 `Attack_Mirror` 狀態）
- [x] 建立 4.5 節阿努比斯的 5 招 `AttackDefinition` 資產，掛到阿努比斯（`EnemyData_AnubisGuard`）
- [x] 測試：近距離會隨機出快速揮擊、反手揮擊、重擊、三連斬，遠距離會出突進斬；重擊有明顯預警（起手停格 0.8 秒）
- [x] Animator 設定由 agent 完成（見 4.5 節），不需要手動

### M2.6：招架與攻擊提示（規格外，使用者追加）
- [x] 阿努比斯雙手拿匕首；`EnemyWeapon` 招架、`AttackIndicator` 提示
- [x] 招架窗、整招取消、1.5 秒硬直、×1.5 傷害；箭不能招架
- [x] 測試：EditMode（招架窗時間、連段、攻速、橋接）、PlayMode（真的 EnemyController 招架流程）

### M3：生成與遭遇戰
- [x] 物件池運作正常，反覆生成和回收時不產生新的 Instantiate
- [x] 波次、同時在場上限、生成點安全距離正常
- [x] 按 1/2/3 模擬文物歸位：對應展廳的敵人被清除；三件都歸位後 Boss 戰開始
- [x] PlayMode 測試：一場遭遇戰從開始到 `Cleared` 的完整流程

### M4：MVP 敵人與 Boss 架構
> 原 M4（三種佔位敵人、Boss 三階段、0 GC）已完成；下面未勾的是新規格加入的項目。
- [ ] `StatueLocomotion`、`StatuePart`、`EnemyProjectile`（含反彈）、`WingShield` 完成
- [ ] 依 5.3 節建立 MVP 敵人的 `AttackDefinition` 資產
- [ ] 第 5 節 MVP 4 種敵人的 `EnemyData` 與 Prefab（沒有模型的用佔位模型）
- [ ] 測試：用 `MockWeapon` 打中卡雕像的魔法彈，魔法彈會飛回去並傷害卡雕像
- [ ] 測試：伊西斯收翅膀時打弱點只有 10% 傷害並觸發 `Blocked` 事件
- [x] Boss 三階段切換與 `PhaseChanged` 事件
- [x] Profiler 確認：4 隻敵人同時追擊時，敵人腳本每幀沒有 GC 配置（M2.5 後 PlayMode GC 測試仍為 0 bytes）

---

## 8. 待確認事項（遇到時用預設值，並在回報中提醒我）

1. 是否要和 A 協調擴充合約（`DamageInfo`、`sourcePosition`、文物事件的發送方式）；目前沿用既有簽名。`hitCooldown` 暫時當全域無敵。
2. 目標平台（Quest 獨立運行 / PCVR）會影響同時在場敵人上限，目前預設 4 隻。
3. 玩家可否格擋敵人攻擊？目前不做格擋。
4. 法老 Boss 的招式細節。
5. 手電筒照射敵人的合約（`StunOnFlashlight` 需要），要和 A 討論。
6. 圖鑑中哪些展品確定是敵人（目前假設：有粗體弱點的都是）。
7. 日後是否有「關卡流程管理器」接手 `EncounterDirector` 的 Boss 觸發職責。
8. 箭打到敵人武器是否也算招架（目前不算）；招架要不要給 VR 震動（A 那邊的手把震動）。
