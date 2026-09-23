# Trackpad Navigation

[English](README.md) | 日本語

Apple SiliconのmacOS Unity Editorを、トラックパッドでパン・ズーム・回転できます。

## 導入

Package Managerの **Install package from git URL** に次のURLを指定します。

```text
https://github.com/kyubuns/UnityTrackpadNavigation.git?path=Packages/com.kyubuns.trackpad-navigation
```

## 操作

| 入力 | 動作 |
|---|---|
| 2本指スライド | パン（平行移動） |
| ピンチ | カーソル位置を中心にズーム |
| 2本指ダブルタップ | カーソル下の対象にフォーカス |
| Option + 2本指スライド | 対象の周囲を回転 |
| Command + 2本指スライド | カメラ位置を保って見回す |

`Preferences > Trackpad Navigation` で有効化・感度・反転・慣性スクロールを調整できます。
`Window > Trackpad Navigation > Diagnostics` で入力を確認し、`Copy diagnostic report` で報告用情報をコピーできます。

## 対応と制約

動作確認環境：macOS 26.6.2／Unity 6000.3.23f1（Apple Silicon）。

- Scene View
- Timeline
- Animator
- Animation（ドープシート／カーブ）
- Shader Graph
- VFX Graph
- UI Builder

回転・見回しはScene View、ダブルタップのフォーカスはScene View／GraphViewに対応します。

- Animator／Animation／Timeline／UI BuilderはUnity内部APIを使うため、Unityの更新に伴い対応が必要になる場合があります。
- Sceneの表面取得には公開の[PickGameObject](https://docs.unity3d.com/ScriptReference/HandleUtility.PickGameObject.html)と[MeshUtility.AcquireReadOnlyMeshData](https://docs.unity3d.com/ScriptReference/MeshUtility.AcquireReadOnlyMeshData.html)を使用します。Meshを取得できない対象はCollider、空白では回転に現在のpivot、ズームにpivotの深度面を使います。GPUのみで描画・変形するGeometryや三角形以外のMeshは表面取得の対象外です。
- その他のGraphViewは自動検出します。独自UI Toolkitでは、viewport直下のcontentを登録してください。型への参照と登録コードは `#if UNITY_EDITOR_OSX` で囲みます。

```csharp
canvas = new TrackpadNavigation.TrackpadCanvas(viewport, content, new Vector2(0.1f, 4f));
// ウィンドウ終了時
canvas?.Dispose();
```

## 開発・検証

サンプルは `Assets/TrackpadExamples` にあります。`Tools > Trackpad Navigation > Open ...` から各Editorで開けます。Shader GraphとVFX GraphのサンプルはUnityのテンプレートを使用し、同じフォルダーにライセンスを同梱しています。
録画用には、このプロジェクトの `Window > Trackpad Navigation > Live View` で指位置と修飾キーを表示できます。`Assets/Editor/TrackpadLiveView` に置いた開発用ツールで、UPMパッケージには含みません。専用Nativeのビルドは `Assets/Editor/TrackpadLiveView/Native~/build.sh` で行います。
Test RunnerのEditModeで `TrackpadNavigation.Tests` を実行できます。外部プロジェクトからテストする場合は、manifestの `testables` にパッケージ名を追加してください。

Nativeを変更する場合は、Apple Silicon MacとXcode Command Line Toolsで、リポジトリ直下から次を実行し、Unityを再起動します。

```sh
./Packages/com.kyubuns.trackpad-navigation/NativePlugin~/build.sh
./Packages/com.kyubuns.trackpad-navigation/NativePlugin~/test.sh
```

## FAQ

### ピンチでUnityのウィンドウを最大化できなくなった

プラグインが有効な間は、対応外ウィンドウや個別のintegrationをOFFにしたビューも含め、Unity Editor全体で標準のピンチ操作を無効にします。標準の操作へ戻すには、`Preferences > Trackpad Navigation` の **Enable** をOFFにしてください。

### ピンチでズームが効かない

macOSの **システム設定 > トラックパッド > スクロールとズーム > 拡大／縮小** がONになっているか確認してください。

### 2本指ダブルタップでフォーカスできない

macOSの **システム設定 > トラックパッド > スクロールとズーム > スマートズーム** がONになっているか確認してください。

[MIT License](LICENSE)
