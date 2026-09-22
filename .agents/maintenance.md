# 保守メモ

## 入口

- 本体は `Packages/com.kyubuns.trackpad-navigation`。`Editor/` が入力配送と各ビューの操作、`NativePlugin~/` がAppKit入力とビルド・検証スクリプト。Unityプロジェクトはリポジトリ直下。
- READMEは英語を基本に日本語版を相互リンクし、両言語ともトップとパッケージ内を同一内容に保つ。利用者に自然な挙動の保証や修正経緯を並べず、実装上の対策はこのメモへ記録する。
- 開発環境はProjectVersionとpackages-lockを参照する。READMEの動作確認環境には実行検証したmacOSとUnityの版だけを記載する。進捗・実行中のプロセス・一時ファイル・CLIの一般的な手順は記録しない。
- 補助コードはBashかC#。配布はUPM。アーカイブや作成スクリプトは置かない。
- `Assets/TrackpadExamples` と参照される描画設定・metaはGit管理する。各Editorを開く入口は `Assets/Editor/TrackpadValidation.cs`。Unityテンプレート由来のグラフには元のライセンスを同梱する。

## 実装境界

- 本体とテストのasmdefはEditor限定＋ `UNITY_EDITOR_OSX`。Native ImporterはOSX／ARM64のEditorのみ。PlayerとWindows／Linux Editorを有効化しない。
- AppKit local monitor → 固定長キュー → C ABI → Editor update。メインスレッド内で完結し、逆P/InvokeやApple private APIは使用しない。
- Native有効中は対象未検出・期限切れ・対象消失・キューoverflowでもMagnifyを破棄し、対応キャンバスで捕捉した分だけ配送する。Modal／Sheetは標準処理を維持する。
- AppKit monitorで止まらないUnity内部のピンチもある。UnityGestureGuardは内部globalEventHandlerに接続し、WindowLayout.MaximizeGestureHandlerの直前でMagnifyだけを消費する。公開APIに遮断口がないためReflectionをここへ隔離し、イベント型はUnityから取得する。Nativeと同時に登録・解除し、APIが失われた場合は起動を中止して診断へ表示する。マウス・キー入力は通す。
- 非アクティブなUnityへ届く精密Scroll／Magnify／Smart Zoomは破棄し、標準ズームへの漏れを防ぐ。通常のマウスホイールは標準処理を維持する。
- 操作開始は期限付きのヒットテストと同じNative Windowに限定。開始した操作はカーソル移動やEditor遅延でも入力先を維持し、次の操作で再判定する。キューoverflow・フォーカス喪失・対象変更では配送を解除する。別キャンバスへの途中取得や孤立Momentumは受け付けない。
- ノード内の入力欄もPan／Pinchの対象。ノードやドロップダウンがカーソル下へ動いたことを理由に標準Scrollへ戻さない。
- Smart ZoomはSceneのカーソル下のObject／GraphViewのNodeを公開APIでフレーミングし、選択状態を変えない。空白では何もしない。
- Domain Reload前と終了時に解除し、再ロード後に再登録。ABIサイズを起動時に検証する。
- SceneのPanはNSEventの論理pointとcameraViewportの高さから換算する。Perspectiveはpivot深度と垂直FOV、Orthographicは表示サイズを使う。感度は最後の倍率。
- SceneのPinchは開始時に中心を固定し、サイズ比に合わせてpivotも移動する。Perspectiveは表面の交点、空白とOrthographicはpivot深度面を使い、終了・取消で解放する。
- OrbitのPOIも開始時に固定する。公開Picking＋Mesh読み取りでColliderやRead/Write設定への依存を避ける。Mesh取得不可ならCollider、交点なしならpivotへ戻る。PlaceObjectはグリッドにもヒットするため採用しない。
- 画面端のPOIを中央へ寄せず、視点と構図を維持する。終了・Momentum入力は指を離す前の操作種別を維持し、OrbitにはMomentumを適用しない。2D／回転ロック中はPan、Command操作はカメラ位置を保って見回す。
- Zoomは指数カーブ。GraphViewの丸め端数とShader Graphのゼロ座標復元回避を維持する。Animator／Timeline／UI BuilderのReflectionはビュー状態に限定する。

## 検証

手順はREADMEの開発節を参照。Nativeを変更したらarm64・export・再ビルド・署名の検証後にEditorを再起動し、ロード済みの旧バイナリと混同しない。
自動テストは操作計算・設定復元・ABI・環境分離・Native入力の安全性とUnityのジェスチャー配送に絞る。画面配置やサンプル依存の検証コードは一時的に使用し、常設テストへ増やさない。
PlatformIsolationTestsはasmdef・Playerのコンパイル対象・Native Importerを検査する。Windows実機実行やPlayer buildの代用とはしない。
物理的な指操作・遅延・方向・複数ディスプレイの操作感は自動テストでは判定しない。

## 設計資料

- [Apple NSEvent](https://developer.apple.com/library/archive/documentation/Cocoa/Conceptual/EventOverview/HandlingTouchEvents/HandlingTouchEvents.html) / [local monitor](https://developer.apple.com/library/archive/documentation/Cocoa/Conceptual/EventOverview/MonitoringEvents/MonitoringEvents.html)
- [GraphView](https://docs.unity3d.com/ScriptReference/Experimental.GraphView.GraphView.UpdateViewTransform.html) / [SceneView](https://docs.unity3d.com/ScriptReference/SceneView.html) / [asmdef条件](https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html)
- [Rhino](https://docs.mcneel.com/rhino/mac/help/en-us/macpreferencesandsettings/trackpad.htm)、[Fusion](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/3D-orbit-not-working-when-an-Apple-touchpad-is-used-for-3D-navigation-in-Fusion-360.html)、[Shapr3D](https://support.shapr3d.com/hc/en-us/articles/7873881091356-Navigation-Presets)の連続Pan／Pinchを参考に、Option Orbit・Command Lookの固定操作を採用。
