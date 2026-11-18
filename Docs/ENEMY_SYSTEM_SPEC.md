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
}
```

**提案（未採用，待確認）**：原規格曾提出 `DamageInfo` 結構、`WeaponType`、`IPlayerDamageable.ApplyPlayerDamage(amount, sourcePosition)`、`PlayerTarget`（讓敵人找到玩家）、`GameEvents.ArtifactRestored`（全域事件）。其中 `PlayerTarget` 已在 M2 新增到 Contracts（`Head`、`Body`、`Damageable` 為 `IPlayerDamageReceiver`、`PlayerTarget.Current`），其餘仍未採用。第 4 節提到 `PlayerTarget`、文物事件的地方，實際要放在 Contracts 還是 Enemy、怎麼接收，在 M2/M3 開工前再確認。

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
| `attackRange` | 開始攻擊的距離 | 1.6 |
| `attackWindup` | 攻擊前搖時間，讓玩家能閃避（秒） | 0.7 |
| `attackDamage` | 對玩家造成的傷害 | 10 |
| `attackCooldown` | 攻擊間隔（秒） | 2.0 |
| `attackAngle` | 攻擊判定扇形角度（度） | 70 |
| `spawnDuration` | 生成出場時間（秒），期間無敵且不行動 | 1.5 |
| `despawnDuration` | 死亡或強制清除後消失的時間（秒） | 1.5 |

### 4.2 `EnemyHealth : MonoBehaviour, IDamageable`
- 處理 `TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint)`：
  - 生成中或已死亡 → 忽略。
  - 上次扣血後 `hitCooldown` 內 → 忽略（合約沒有 Source 參數，因此是全域短暫無敵；是否要依來源區分，待確認）。
  - `isWeakPoint == true` → 傷害乘上 `weakPointMultiplier`（只乘一次）。
  - 達到硬直條件 → 通知狀態機進入 Hit 狀態。
  - 血量 ≤ 0 → 進入 Dead 狀態。
- 提供事件（給音效、特效、VR 震動用）：
  - `event Action<EnemyHealth, EnemyDamageResult> Damaged`（含實際扣血量、擊中點、是否弱點、是否硬直）
  - `event Action<EnemyHealth> Died`
- 物件池回收時要能 `ResetState()`。

### 4.3 `WeakPoint : MonoBehaviour, IDamageable`
- 掛在子物件的弱點 Collider 上（頭部、胸口發光處等）。
- 收到傷害時，以 `isWeakPoint = true` 轉發給父物件的 `EnemyHealth`。
- 在 Scene 視圖用 Gizmo 標示弱點位置（方便我調整）。

### 4.4 `EnemyController` + 狀態機
- 狀態以 class 實作（每個狀態有 `Enter / Tick / Exit`），不要用一大串 `switch`。
- 狀態轉換：

| 目前狀態 | 條件 | 下一個狀態 |
| --- | --- | --- |
| Spawn | `spawnDuration` 結束 | Patrol（有巡邏點）或 Idle |
| Idle / Patrol | 玩家在 `detectionRange` 內且有視線 | Chase |
| Chase | 距離 ≤ `attackRange` 且冷卻結束 | Attack |
| Chase | 距離 > `loseTargetRange` 或超過 5 秒無視線 | Patrol / Idle |
| Attack | 前搖 → 判定 → 收招結束 | Chase |
| 任何（Dead 除外） | 達到硬直條件 | Hit |
| Hit | `staggerDuration` 結束 | Chase |
| 任何 | 血量 ≤ 0 | Dead |
| 任何 | 被 Encounter 強制清除 | Dead（不計入擊殺） |

- 視線檢查：從敵人眼睛位置 Raycast 到 `PlayerTarget.Current.Head`，只檢查環境 Layer。
- 追擊時 `NavMeshAgent.SetDestination` 不要每幀呼叫，每 0.2 秒或玩家移動超過 0.5 公尺才更新。
- 找不到 `PlayerTarget.Current` 時保持 Idle / Patrol，不要報錯。

### 4.5 `EnemyAttack`
- 前搖（`attackWindup`）期間面向玩家，並觸發 `AttackWindup` 事件（給音效、特效用）。
- 判定時不使用物理碰撞，改用距離 + 角度判斷：玩家 `Body` 位置在 `attackRange` + 0.3 公尺內，且在 `attackAngle` 扇形內 → 命中。
- 命中時呼叫 玩家的 `IPlayerDamageReceiver.ApplyPlayerDamage(attackDamage)`。
- 玩家在前搖期間離開範圍 → 揮空。

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
  - 接收 A 發出的 `ArtifactRestoredEvent`（接收方式待確認，見第 3 節），以 `ArtifactId` 去重。
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
- `MockPlayer`：在 `EnemyAI` 場景中用 WASD 移動、滑鼠轉向，掛玩家目標元件並實作 `IPlayerDamageReceiver`（受傷時印 Log 並顯示剩餘血量）。
- `MockWeapon`：按滑鼠左鍵對準星指到的物件呼叫 `TakeDamage`，傷害值可在 Inspector 調整，用來測試弱點與硬直。
- `DebugArtifactButton`：按數字鍵 1/2/3 送出對應的 `ArtifactRestoredEvent`，用來測試清場與 Boss 觸發。
- 敵人頭上的 Debug 文字（只在 Editor 顯示）：目前狀態與血量。

---

## 5. 敵人種類（數值與弱點為暫定，可修改）

| 敵人 | 定位 | 弱點（暫定） | 備註 |
| --- | --- | --- | --- |
| 活化木乃伊 | 慢速近戰，數量多 | 胸口護符 | 第一場遭遇戰，教玩家打弱點 |
| 阿努比斯衛兵 | 中速，長武器，攻擊距離較長 | 頭部 | 模型：Sketchfab Anubis（CC BY，需標註作者） |
| 塞克邁特女神像 | 慢速、高血量、重擊 | 背後裂縫 | 需要玩家繞背，招式待確認 |
| 法老 Boss | 三階段 | 第三階段出現 | 見 4.7 |
| 無毛貓 | **角色待確認** | — | 模型授權 CC BY-NC-ND，暫時不要使用 |

模型沒有骨架前，一律用膠囊體 + 方塊頭作為佔位模型。

---

## 6. 資料夾結構

```
Assets/_Game/
├─ Scripts/
│  ├─ Contracts/         Game4026.Contracts.asmdef（既有，共用）
│  └─ Enemy/             Game4026.Enemy.asmdef
│     ├─ Core/           EnemyController, EnemyHealth, WeakPoint, EnemyAttack
│     ├─ States/         SpawnState, IdleState, PatrolState, ChaseState, AttackState, HitState, DeadState
│     ├─ Spawning/       SpawnPoint, WaveDefinition, EnemyPoolManager, EncounterController, EncounterDirector
│     ├─ Boss/           PharaohBoss, BossPhase
│     ├─ Data/           EnemyData
│     └─ Debug/          MockPlayer, MockWeapon, DebugArtifactButton
├─ Data/Enemies/         各敵人的 EnemyData、WaveDefinition 資產
├─ Prefabs/Enemies/
├─ Scenes/Test/EnemyAI.unity
└─ Tests/
   ├─ EditMode/Enemy/    Game4026.Enemy.EditModeTests.asmdef
   └─ PlayMode/Enemy/    Game4026.Enemy.PlayModeTests.asmdef
```

---

## 7. 里程碑與驗收標準

### M1：共用介面與受擊
- [ ] `Game4026.Enemy` asmdef 建立，正確引用 `Game4026.Contracts` 並可編譯
- [ ] `EnemyHealth`、`WeakPoint`、`EnemyData` 完成
- [ ] EditMode 測試：弱點倍率、受擊冷卻、死亡只觸發一次、`ResetState` 後血量恢復

### M2：AI 與攻擊
- [ ] 狀態機完成，所有狀態轉換符合第 4.4 節的表格
- [ ] 在 `EnemyAI` 場景用 `MockPlayer` 測試：會發現、追擊、前搖後攻擊、被打會硬直、死亡
- [ ] 沒有 Animator 的佔位模型也能完整運作

### M3：生成與遭遇戰
- [ ] 物件池運作正常，反覆生成和回收時不產生新的 Instantiate
- [ ] 波次、同時在場上限、生成點安全距離正常
- [ ] 按 1/2/3 模擬文物歸位：對應展廳的敵人被清除；三件都歸位後 Boss 戰開始
- [ ] PlayMode 測試：一場遭遇戰從開始到 `Cleared` 的完整流程

### M4：敵人種類與 Boss 架構
- [ ] 三種敵人的 `EnemyData` 與 Prefab（佔位模型）
- [ ] Boss 三階段切換與 `PhaseChanged` 事件
- [ ] Profiler 確認：4 隻敵人同時追擊時，敵人腳本每幀沒有 GC 配置

---

## 8. 待確認事項（遇到時用預設值，並在回報中提醒我）

1. 是否要和 A 協調擴充合約（`DamageInfo`、`sourcePosition`、`PlayerTarget`、文物事件的發送方式）；目前沿用既有簽名。`hitCooldown` 暫時當全域無敵。
2. 目標平台（Quest 獨立運行 / PCVR）會影響同時在場敵人上限，目前預設 4 隻。
3. 玩家可否格擋敵人攻擊？目前不做格擋。
4. 塞克邁特女神像與法老 Boss 的招式細節。
5. 無毛貓模型的用途與授權。
6. 日後是否有「關卡流程管理器」接手 `EncounterDirector` 的 Boss 觸發職責。
