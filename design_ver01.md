
# **snapback-layout 設計書 v0.1**

---

## 1. **目的（Purpose）**
Windows のウィンドウ配置は、割り込み作業が入ると簡単に崩壊する。  
本アプリは、**「1時間以内のレイアウトを保存し、ワンクリックで復元する」**ことを目的とする。

### 解決する問題
- 割り込み作業でウィンドウが散らかる  
- 元のレイアウトに戻すのが面倒  
- タイル WM では割り込みで崩壊する  
- Windows 標準はレイアウトの文脈を保存しない  

### 本アプリの価値
- **レイアウトの文脈を保存する唯一のツール**  
- **割り込み後の復帰を高速化**  
- **集中力の維持に寄与する**

---

## 2. **機能要件（Requirements）**

### ✔ 必須（MVP）
- **レイアウト保存（Snapshot Save）**
  - 現在のウィンドウ配置を JSON に保存
  - 保存タイミング：手動 or 自動（任意）

- **レイアウト復元（Snapshot Restore）**
  - 保存されたレイアウトを可能な限り忠実に復元
  - 復元できないウィンドウはスキップ

- **ホットキー操作**
  - Ctrl+Alt+S → 保存  
  - Ctrl+Alt+R → 復元  

- **スナップショット履歴（最大 60 分）**
  - 直近のスナップショットを複数保持  
  - 例：5分ごとに自動保存 → 最大12個

---

### ✔ 任意（将来）
- モニタ構成変化への自動補正  
- DPI 変化への補正  
- アプリごとの復元優先度  
- レイアウトの名前付け  
- タスクバーの位置復元（上/下/左右）  

---

## 3. **保存すべきデータ構造（Snapshot Schema）**

```json
{
  "timestamp": "2026-06-06T03:40:00",
  "monitors": [
    {
      "id": 1,
      "bounds": { "x": 0, "y": 0, "w": 1920, "h": 1080 },
      "dpi": 125
    }
  ],
  "windows": [
    {
      "hwnd": 123456,
      "processId": 9999,
      "title": "Edge - ChatGPT",
      "state": "normal", // normal, maximized, minimized
      "bounds": { "x": 100, "y": 50, "w": 1200, "h": 900 },
      "zIndex": 3,
      "monitorId": 1
    }
  ]
}
```

---

## 4. **アーキテクチャ（Architecture）**

### モジュール構成
- **WindowEnumerator**  
  - 現在のウィンドウ一覧を取得  
  - Win32 API: `EnumWindows`, `GetWindowPlacement`, `GetWindowRect`

- **SnapshotManager**  
  - 保存・読み込み  
  - JSON 形式で管理

- **RestoreEngine**  
  - ウィンドウ位置・サイズ・状態を復元  
  - Win32 API: `SetWindowPos`, `ShowWindow`, `MoveWindow`

- **MonitorManager**  
  - モニタ情報の取得  
  - DPI 補正

- **HotkeyManager**  
  - グローバルホットキー登録

- **Logger**  
  - デバッグログ

---

## 5. **処理フロー（Flow）**

### 5.1 スナップショット保存
1. WindowEnumerator がウィンドウ一覧を取得  
2. MonitorManager がモニタ情報を取得  
3. SnapshotManager が JSON に保存  
4. 履歴に追加（最大60分）

---

### 5.2 スナップショット復元
1. 最新のスナップショットを読み込み  
2. モニタ構成が変わっていたら補正  
3. 各ウィンドウに対して  
   - プロセスが生きているか確認  
   - 位置・サイズ・状態を復元  
4. 復元できないウィンドウはログに記録

---

## 6. **UI（最小構成）**

### トレイアイコンメニュー
- [Save Layout]  
- [Restore Layout]  
- [History]  
- [Settings]  
- [Exit]

### 設定画面
- 自動保存：ON/OFF  
- 保存間隔：5/10/15分  
- 履歴保持時間：30/60/120分  
- 復元時の DPI 補正：ON/OFF  

---

## 7. **非機能要件（NFR）**
- 常駐メモリ：20MB 以下  
- 保存/復元：1秒以内  
- クラッシュしても OS に影響しない  
- Win32 API のみで実装（外部 DLL 不要）  
- MIT-0 ライセンス  

---

## 8. **リポジトリ構成案**

```
layout-snapshot-manager/
 ├─ src/
 │   ├─ enumerator/
 │   ├─ snapshot/
 │   ├─ restore/
 │   ├─ monitor/
 │   ├─ hotkey/
 │   └─ logger/
 ├─ config/
 │   └─ settings.json
 ├─ snapshots/
 │   └─ *.json
 ├─ docs/
 │   └─ design.md
 ├─ LICENSE
 └─ README.md
```
