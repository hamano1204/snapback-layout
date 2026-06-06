# snapback-layout

[English](#english) | [日本語](#日本語)

---

## English

`snapback-layout` is a lightweight, background utility for Windows that lets you capture and restore your window layouts instantly. It resides in the system tray, monitoring layouts, and restores them either with global hotkeys or from a tray context menu.

### Key Features

* 📸 **Instant Layout Backup & Restore**: Instantly save current desktop layouts and restore them precisely.
* ⌨️ **Global Hotkey Customization**: Custom hotkeys for saving and restoring layouts (supports modifier keys including the `Windows` key).
* ⏱️ **Silent Auto-Save**: Periodically backs up your layout in the background without disturbing your work.
* 📜 **Visual History Menu**: Left-click the system tray icon to display a dedicated popup menu showing the last 12 snapshots directly. Each item displays the active foreground window title at the time of the snapshot and the time (HH:mm). Clicking an item immediately restores that layout.
* 🖥️ **DPI Correction**: Automatically scales window bounds and relative coordinates when restoring layouts across monitors with different DPI factors.
* 🛟 **Off-screen Window Rescue**: Prevents "lost windows" by automatically centering coordinates onto the primary monitor if a saved window would restore off-screen (e.g., after disconnecting a monitor).
* 🥞 **Exact Z-Order Restoration**: Accurately restores the relative layering stack (front-to-back order) of all windows.
* ⚙️ **Modern Settings Form**: Grouped dialog layout containing accent purple visual styling, a recording state feedback indicator for hotkey inputs, and a "Reset Defaults" option.
* ⚡ **Ultra-lightweight**: Written in pure C# using Win32 APIs directly. No external dependencies, consuming less than 20MB of memory.

---

### Installation & Run

#### Prerequisites
* Windows 10 or Windows 11
* .NET 10.0 SDK

#### Running from Source
Run the following command in the project directory:
```powershell
dotnet run
```

#### Keyboard Shortcuts (Default)
* **Save Layout**: `Ctrl + Alt + S`
* **Restore Layout**: `Ctrl + Alt + R`

---

### Notes
- This application was developed using AI.
- Please be aware that code quality may require attention.
- While it should not contain any malicious code, please use it at your own risk.

---

## 日本語

`snapback-layout` は、Windows のウィンドウ配置（レイアウト）を瞬時に保存・復元できる軽量な常駐型ユーティリティです。システムトレイに常駐し、キーボードショートカットやメニュー操作によっていつでも元のレイアウトを復元します。

### 主な機能

* 📸 **レイアウトの即時保存・復元**: 現在のウィンドウ配置をキャプチャし、必要な時にいつでも元の位置・サイズに復帰させます。
* ⌨️ **カスタムホットキー**: 保存・復元のホットキーを自由に変更可能（`Windows` キーを含むショートカットキーに対応）。
* ⏱️ **静かな自動保存 (Auto-Save)**: 作業を邪魔しないサイレント仕様で、指定した間隔で自動的にバックアップを保存します。
* 📜 **履歴メニューへのクイックアクセス**: システムトレイアイコンを左クリックすると、直近12回の履歴だけを表示する専用のポップアップが展開されます。各項目にはスナップショット作成時にアクティブだったウィンドウタイトルと作成時刻（HH:mm）が分かりやすく表示され、クリックするだけで瞬時に復元を実行できます。
* 🖥️ **DPI自動補正機能**: 異なるDPI（ディスプレイ拡大率）のモニター間でレイアウトを復元する際、解像度に合わせてサイズや相対位置を自動的にスケーリング補正します。
* 🛟 **画面外ウィンドウ救出機能**: モニター接続解除などにより復元先座標が画面外に孤立してしまう場合、メインモニターの安全な領域（中央）へ自動的に引き戻します。
* 🥞 **正確な Z-Order 復元**: すべてのウィンドウの重なり順（前後関係）を保存時の順番通りに忠実に再現します。
* ⚙️ **モダンな設定画面**: 設定項目を綺麗にグループ化し、ブランドパープルを基調としたフラットデザイン。ホットキー録音状態のカラーフィードバックや、初期値リセットボタンを搭載しています。
* ⚡ **超軽量動作**: Win32 API を直接利用するピュアな C# 実装。外部 DLL を一切含まず、常駐時のメモリ消費量は 20MB 以下です。

---

### 使用方法

#### 動作環境
* Windows 10 または Windows 11
* .NET 10.0 SDK

#### ソースコードからの起動方法
プロジェクトディレクトリで以下のコマンドを実行します。
```powershell
dotnet run
```

#### デフォルトのショートカットキー
* **レイアウト保存**: `Ctrl + Alt + S`
* **レイアウト復元**: `Ctrl + Alt + R`

---

### 注意事項
- 本アプリはAIで作成されたアプリです。
- そのため、コードの品質には注意が必要です。
- 危険なコードは含まれていないはずですが、自己責任でご利用ください。

---

## License

This project is licensed under the MIT-0 License (MIT No Attribution). See the [LICENSE](file:///c:/Users/haman_9/dev/snapback-layout/LICENSE) file for details.
本プロジェクトは MIT-0 ライセンス (MIT No Attribution) の下で提供されています。詳細は [LICENSE](file:///c:/Users/haman_9/dev/snapback-layout/LICENSE) ファイルを参照してください。
