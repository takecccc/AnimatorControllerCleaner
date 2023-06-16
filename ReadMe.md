# AnimatorControllerCleaner v1.0
UnityのAnimatorController内に参照されないのに残ってしまったデータを削除します。

## 動作確認環境
* Unity2019.4.31f1
* VRChat Package Resolver Tool 0.1.19<br>
  (YamlDotNetに依存していますが、`Packages/com.vrchat.core.vpm-resolver/Editor/Dependencies/YamlDotNet.dll`に含まれているのでこれを使います。)<br>
  VCCを使っていない場合はAsset StoreからYamlDotNet for UnityをImportしてください。

## 使い方
1. `Edit > Project Settings... > Editor > Asset Serialization > Mode`をForce Textに設定。
2. AnimatorControllerCleaner.unitypackageをimport
3. ゴミを削除したいAnimatorControllerをProject上で選択して右クリック `CleanAnimatorControllers`を実行。

## 何を行っているのか
AnimatorControllerのAssetをTextModeで保存した場合、Yaml形式となっています。

Yamlをパースし、Root要素であるAnimatorControllerから参照しているオブジェクトを辿って行きます。

AnimatorControllerから辿った参照ツリーに含まれていないオブジェクトを除外してYamlファイルを保存しなおしています。

## 注意事項
テストは行っていますが、完璧な動作は保証できません。一度AnimatorControllerを複製した上で実行し、問題ないことを確認することをおすすめします。
