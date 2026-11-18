# Game4026_VR



| 項目 | 內容 |
| --- | --- |
| 引擎 | Unity `6000.6.7f1`
| 渲染管線 | URP |


---

## 目錄

1. [第一次設定（只做一次）](#1-第一次設定只做一次)
2. [每天的工作流程](#2-每天的工作流程)
3. [合併到 main：Pull Request](#3-合併到-mainpull-request)
4. [團隊規則](#4-團隊規則)
5. [分支與資料夾分工](#5-分支與資料夾分工)
6. [常見問題](#6-常見問題)

---

## 1. 第一次設定（只做一次）

### 1-1 安裝工具

1. 安裝 [Git for Windows](https://git-scm.com/download/win)（安裝時全部用預設選項即可）。
2. 安裝 [GitHub Desktop](https://desktop.github.com)，用你的 GitHub 帳號登入。
3. 開啟「命令提示字元」，執行下面這行（**一定要做**，否則圖片和模型下載下來會是壞檔）：

   ```
   git lfs install
   ```

4. 用 Unity Hub 安裝上表的 Unity 版本。

### 1-2 下載專案

1. 到信箱或 GitHub 右上角通知，接受 Repo 的邀請/ Send請求給我
2. 開啟 GitHub Desktop → **File → Clone repository** → 選 `Game4026_VR`。
3. **Local path** 選短路徑，例如 `D:\Projects`。
   - 不要有中文、空格
   - 不要放在 OneDrive / Google Drive 同步資料夾
4. 按 **Clone**，等待下載完成（第一次較久）。

### 1-3 切換到自己的分支

GitHub Desktop 上方 **Current branch** → 選你負責的分支（見[第 5 節](#5-分支與資料夾分工)）。

### 1-4 用 Unity 開啟

1. Unity Hub → **Add → Add project from disk** → 選剛才的資料夾。
2. 確認版本與上表相同後開啟。第一次開啟需要幾分鐘建立 Library。
3. 打開測試場景按 Play，沒有錯誤就完成了。

---

## 2. 每天的工作流程

```
開工：Pull  →  在 Unity 工作  →  Commit  →  Push
```

| 步驟 | 在 GitHub Desktop 怎麼做 | 什麼時候做 |
| --- | --- | --- |
| **Pull** | 按上方 **Fetch origin**，有更新時會變成 **Pull origin**，再按一次 | 每次開工前 |
| **Commit** | 左側勾選要提交的檔案 → 左下 Summary 寫一句說明 → 按 **Commit to 分支名** | 完成一小段功能時 |
| **Push** | 按上方 **Push origin** | Commit 之後，至少每天下班前一次 |

**Commit 說明怎麼寫：** 一句話講做了什麼，例如：

- `新增木乃伊追擊狀態`
- `修正弱點傷害倍率`
- `調整展廳 A 燈光`

**Commit 前先檢查：**

- 存檔了嗎？Unity 要按 **Ctrl + S** 存場景。
- 每個新檔案旁邊都有對應的 `.meta` 檔嗎？兩個要一起提交。
- 有沒有不小心改到別人的檔案？看到不是自己的就取消勾選，按右鍵 **Discard changes**。

---

## 3. 合併到 main：Pull Request

功能完成、測試過後，把你的分支合併進 `main`：

1. 確認已經 Push。
2. GitHub Desktop → **Branch → Create Pull Request**（會開啟網頁）。
3. 標題寫這次完成了什麼，說明欄寫怎麼測試。
4. 按 **Create pull request**，通知另一位組員來看。
5. 另一位組員確認沒問題後，按 **Merge pull request**。

合併後，讓自己的分支跟上最新的 main：

GitHub Desktop → **Branch → Update from main**

---

## 4. 團隊規則

1. **不要直接 Commit 到 `main`**，一律在自己的分支工作，再用 Pull Request 合併。
2. **不要兩個人同時改同一個場景。** 各自在自己的測試場景工作，做好的東西存成 **Prefab**。正式場景只由一個人編輯。
3. **只改自己負責的資料夾。** 要改別人的程式或共用檔案（`Scripts/Shared/`），先在群組講。
4. **`.meta` 檔一定要跟著檔案一起提交。** 少了它，別人那邊的參照會斷掉。
5. **搬移或改名檔案要在 Unity 裡面做**，不要用檔案總管，否則 `.meta` 會對不上。
6. **不要提交超過 100 MB 的檔案。** 掃描模型請先減面。
7. **所有人使用同一個 Unity 版本**，不要按「升級專案」。

---

## 5. 分支與資料夾分工

| 負責人 | 分支 | 負責資料夾 |
| --- | --- | --- |
| A：VR 操控與互動 | `feature/vr-interaction` | `自己起` |
| B：敵人 AI 與戰鬥 | `feature/enemy-ai` | `自己起` |
| C：環境/UI | `feature/Envrionment_and_UI` | `自己起` |
| 共用（改之前先討論） | — | `自己起` |

---

## 6. 常見問題

**Q：圖片或模型打開是壞的，材質變粉紅色。**
沒有啟用 Git LFS。在專案資料夾開啟命令列執行：

```
git lfs install
git lfs pull
```

**Q：Pull 時出現 Conflict（衝突）。**
代表你和別人改到同一個檔案。

- 如果是**程式檔**：GitHub Desktop 會列出衝突檔案，用 VS Code 或 Visual Studio 打開，保留正確的內容後存檔，再 Commit。
- 如果是**場景或 Prefab**：不要自己硬改，先在群組問，決定保留誰的版本。
- 不確定時**先不要按任何東西**，截圖問人。

**Q：Unity 開啟時跳出要升級版本。**
按取消，用 Unity Hub 安裝正確版本再開。

**Q：GitHub Desktop 出現一大堆我沒改過的檔案。**
可能是 Unity 自動更新的設定檔。不確定的話不要提交，先問人；確定不需要的話按右鍵 **Discard changes**。

**Q：不小心 Commit 了錯的東西，但還沒 Push。**
GitHub Desktop → **History** → 在那次 Commit 上按右鍵 → **Undo commit**。

**Q：已經 Push 了錯的東西。**
不要自己刪除或強制推送，在群組講，一起處理。
