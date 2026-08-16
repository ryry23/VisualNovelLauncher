# Visual Novel Launcher

ビジュアルノベルごとに解像度とリフレッシュレートを切り替えて起動する、Windows向けポータブルランチャーです。

高リフレッシュレート環境で古いゲームのアニメーション速度が変わる場合や、ゲームごとに表示設定を切り替えたい場合に利用できます。ゲーム終了後は、起動前の表示モードへ自動的に戻します。

## 主な機能

- アプリごとに解像度、リフレッシュレート、起動引数を保存
- `+`ボタンまたはEXEのドラッグ＆ドロップで登録
- EXEのアイコンと、拡張子を除いたファイル名を自動取得
- 一覧のダブルクリックまたは起動ボタンで実行
- アプリ終了後に元の表示モードへ自動復帰
- ポータブル運用：設定は実行ファイルと同じ場所の`profiles.json`へ保存
- レジストリ不使用

## 動作環境

- Windows 10 / 11 x64
- .NET 8 Desktop Runtime

## 使い方

1. [Releases](../../releases/latest)から最新版ZIPをダウンロードします。
2. ZIPを任意のフォルダへ展開します。
3. `VisualNovelLauncher.exe`を起動します。
4. `+`ボタン、またはEXEのドラッグ＆ドロップでゲームを登録します。
5. 解像度とリフレッシュレートを設定して保存します。
6. 一覧からゲームを選び、`起動`を押します。

マルチモニター環境では「解像度も変更する」をオフにし、リフレッシュレートだけ変更する設定を推奨します。デスクトップ解像度を変更すると、Windowsが他のディスプレイの配置を調整する場合があります。

## ビルド

.NET 8 SDKを使用します。

```powershell
dotnet build VisualNovelLauncher.csproj -c Release
```

フレームワーク依存版の発行：

```powershell
dotnet publish VisualNovelLauncher.csproj -c Release -r win-x64 --self-contained false -o publish
```

自己完結版の発行：

```powershell
dotnet publish VisualNovelLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## 注意事項

- 本ツールはゲーム本体、認証機能、DRMを変更しません。
- 未保存の作業がある状態での使用には注意してください。
- 異常終了やOSの強制終了時には表示モードを復帰できない場合があります。
- 本ソフトウェアは無保証で提供されます。

## ライセンス

[MIT License](LICENSE)

