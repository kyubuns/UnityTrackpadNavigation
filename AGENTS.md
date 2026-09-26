# Trackpad Navigation の保守方針

## 構成と変更範囲

- 配布対象は `Packages/com.kyubuns.trackpad-navigation`。`Editor/` は入力配送・ビュー操作、`NativePlugin~/` はAppKit入力・ビルド・検証、`Tests/Editor/` はEditorテスト。リポジトリ直下は検証用Unityプロジェクト。
- Unity・パッケージの版は `ProjectSettings/ProjectVersion.txt` と `Packages/packages-lock.json` を確認する。内部APIは対象Editorのソース・Reflection・実動作で照合し、別バージョンでも同じとは仮定しない。
- `Assets/TrackpadExamples` は検証用サンプル、`Assets/Editor/TrackpadValidation.cs` は各ビューを開く入口。参照する描画設定・meta・元アセットのライセンスも維持する。
- `Assets/Editor/TrackpadLiveView` は録画用の独立した観測ツールで、UPMに含めない。NSTouchを観測するだけで入力を消費せず、変更したNSViewのタッチ設定は終了・Domain Reload時に戻す。
- 配布はUPM。補助コードはBashかC#とし、配布アーカイブやその作成スクリプトは追加しない。
- READMEは英語・日本語それぞれで、リポジトリ直下とパッケージ内を同一内容に保つ。利用者向けの説明と、ここに記す実装上の注意点を分ける。進捗、修正履歴、一時ファイル、共通のCLI操作手順はこの文書に蓄積しない。

## 座標系

- 画面、ホストウィンドウ、Panel、要素ローカル、IMGUIの座標を区別する。変換には `NavigationCoordinates` を使う。
- Native入力は画面左上基準の論理point。`screenPoint - window.position.position` はホスト基準であり、EditorWindowのコンテンツや`OnGUI`の座標ではない。
- ホスト基準からPanelへの変換には `window.rootVisualElement.panel.visualTree` を使う。`rootVisualElement` や `baseRootVisualElement` で代用すると、タブ・枠の余白を二重に加算する。余白の値は固定しない。
- 通常のEditorWindowの`OnGUI`で得た`drawRect`は、そのウィンドウの`rootVisualElement`のローカル座標と比較する。ネストしたIMGUIContainerの領域はそのContainerのローカルへ変換する。Profilerの詳細欄・Sprite Editorなどを通常のウィンドウ原点で扱わない。
- 境界判定には、比較する座標系に対応した`worldBound`・ローカル領域を使う。ホスト基準の点を `Rect(Vector2.zero, window.position.size)` と比較すると、タブ等の余白によって下端の操作領域が欠ける。
- SceneのPickingはカメラとGUIの文脈が必要。`ScenePicking`のように対象SceneViewのGUI内で画面座標を変換し、Picking・Ray生成を行う。
- Retina倍率を論理pointへ重ねて掛けない。SceneのPanは入力deltaと`cameraViewport`の論理高さから投影に応じて換算する。

## 入力配送と寿命

- 本体・テストのasmdefはEditor限定かつ `UNITY_EDITOR_OSX`。Native ImporterはOSX／ARM64 Editorのみ。Playerや他OSのEditorへ依存を漏らさない。
- AppKitのlocal monitor → 固定長キュー → C ABI → Editor updateという経路を維持する。メインスレッドで完結させ、逆P/InvokeやApple private APIを使わない。起動時にABIを検証し、Domain Reload前・終了時に解除する。
- 操作開始は期限付きのヒット判定と同じNative Windowに限定する。開始後は入力先を保持し、途中のカーソル移動で別キャンバスへ取得し直さない。孤立したMomentumは受け付けず、フォーカス喪失・対象変更・キューoverflow時の配送解除を保つ。
- `NavigationTarget`の寿命判定とヒット判定を分ける。所属Panel・編集対象・表示モードが変わった入力先には、キューに残った入力も適用しない。カーソル下に操作UIが移動しただけで、開始済みのジェスチャーを失効させない。
- Native有効中は、未対応領域・期限切れ・overflowでもMagnifyをUnityの最大化ジェスチャーへ漏らさない。`UnityGestureGuard`はAppKitで遮断できないUnity内部のMagnifyを消費する。二重にナビゲーションを適用せず、必要な内部フックが取得できない場合は起動を中止して診断へ出す。
- Modal／Sheetは標準処理を維持する。非アクティブなUnityに届く精密Scroll／Magnify／Smart Zoomは破棄し、通常のマウスホイールやクリック・キー入力は標準処理を維持する。
- `NavigationTargets`で入力先を解決する。独自UI Toolkitのキャンバスは、viewport直下のcontentを`TrackpadCanvas`へ登録して終了時にDisposeする。利用側の参照もmacOS Editorに限定する。GraphViewは自動検出する。

## ビュー操作で保つ挙動

- 操作対象はビュー状態と選択状態。ナビゲーションのためにアセット、キーフレーム、ノード配置、Spriteのimport設定等を変更しない。内部メンバーへのアクセスは`EditorMember`を使い、必要な型・setter・メソッドを確認してから操作する。任意パッケージへの必須依存を追加しない。
- Smart Zoomはカーソル下の対象を選択し、各ビューの標準のFrame Selected／Fキー相当の処理を使う。独自Bounds計算や合成ダブルクリックで代用しない。空白では選択と表示を変えない。
- Zoomは指数カーブとカーソル基準の平行移動補正を使う。Unityが倍率・表示範囲を制限する場合は、適用後の値で補正し、限界で繰り返してもカーソル下の対象をずらさない。負のYスケールも保持する。
- `ZoomAreaNavigation`へ共通処理を集約し、アダプターには領域・寿命・軸・ビュー固有の制約を残す。独自の範囲setterを持つビューはUnity自身のsetterを使う。ルーラー・スクロールバー・操作UIをキャンバスに含めない。
- Game Viewは停止中・一時停止中だけ操作し、再生中はゲーム入力を優先する。AnimationのDope Sheetは横軸のズームと階層一覧の共有縦スクロール、Curvesは両軸の変換を使う。Profiler CPU Timelineでは時間軸だけを拡大する。
- GraphViewの位置の丸め端数は次の入力へ持ち越す。Shader Graphが位置ゼロで保存済み表示を復元する分岐も考慮する。ノード内の入力欄でもPan／Pinchはキャンバス操作として扱う。
- SceneのPinchとOrbitは開始時の対象点を保持し、終了・取消で解放する。Perspectiveでは対象表面、Pinchの空白・Orthographicではpivot深度面を使う。Pickingした対象のMeshを公開Editor APIで読み、Collider・Read/Write設定を必須にしない。取得不能時のfallbackを維持する。
- Sceneでは画面端の対象を中央へ寄せず、構図を維持する。終了・Momentum時の操作種別を保持し、OrbitにはMomentumを適用しない。2D／回転ロック時はPan、Lookはカメラ位置を維持する。NSEventのスクロール方向はmacOSの設定反映済みなので、再度反転しない。
- VFX Graph・UI Builderの標準Scroll Zoomへの一時上書きは元値を保持し、無効化・Detach・Domain Reload・終了時に復元する。Shader Graphの設定はUnity自身のPreferencesを変更するため、プラグインのRestore defaultsと混同しない。

## 検証とコミット前の確認

- **スクリプトを変更したら、コミット前にリビルドして警告・エラーを確認し、修正してからコミットする。** 非推奨API警告も放置しない。コンパイル要求の受付だけで完了扱いにせず、完了と実際のコンパイラ診断を確認する。必要に応じてクリーンリビルドで全スクリプトを確認する。
- UI Toolkitの非推奨な`ITransform.position / scale`は使わない。書き込みには`style`、解決後の読み取りには`resolvedStyle`を使う。レイアウト更新前の同期的なテストでは、設定した`style`値を検証する。
- 座標テストの入力を製品側と同じ変換式だけで作らない。対象の`OnGUI`内の`GUIUtility.GUIToScreenPoint`など、独立した基準と照合する。キャンバス中心だけでなく、タブ・ツールバー・下端・スクロールバー、パン／ズーム後の位置も確認する。
- `Apply`直呼び出しだけで対応完了としない。対象解決・ヒット判定・Native捕捉の種類・対象の選択までを確認する。閉じたビュー・再所属・モード変更・編集対象変更・ズーム限界・空白の挙動も変更に応じて検証する。
- 回帰テストは一時ウィンドウや一時データで再現し、既存タブの状態・設定・選択を復元する。PanelのAttach／Detachを検証する場合は対象タブを表示・Focusしてから行う。特定のサンプルや保存済みレイアウトに依存する調査プローブは常設しない。
- Native変更時は `NativePlugin~/test.sh` でCore／AppKitテスト、arm64・ABI export・再ビルド・署名を検証する。配布バイナリ更新には`build.sh`を使い、ロード済みMach-Oを直接上書きしない。更新後はEditorを再起動し、旧バイナリの検証と混同しない。
- `PlatformIsolationTests`はasmdef・Playerコンパイル対象・Native Importerの検査であり、他OSでの実行検証の代用ではない。物理的な指操作・遅延・方向・複数ディスプレイの操作感は別途確認し、未確認なら明示する。
- README両言語のトップ／パッケージ間の一致と`git diff --check`を確認する。検証環境の版や成功件数など、その作業だけの証跡はコミット・報告へ記し、この文書には追加しない。
