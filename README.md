# Stage-Sync-System

## 概要
イベント等でパフォーマンスをする人向けVRChat向けのワールドギミックです。
音源、演出、動画を各Player間で同期し、演者と観客のタイムラグを考慮したオフセットも設定できます。

## 機能紹介
 - 観客遅延制御機能
  演者の動きの遅延を考慮したタイミングずらしが可能となっています。「Performer Space」のCollider内に入った時に「Core」で設定した時間(ms)タイミングが速くなるように調整されています。 
 - 自動同期
  動画が読み込まれてから演出が開始されるようになっており、全員が同期された状態で上映できます。 
 - 演出管理機能
  演出に「DirectionObject」U#Scriptを付けることでシステムが自動的に演出を認識してリストに組み込んでくれます。演出名と再生する動画のURLを各演出に登録できます。
 - 専用のUI
  Editor(Unityの編集画面)とVRChatでの操作盤を独自のUIで用意しました。現在、自動読み込みされている演出とiwaSyncとの連携状況がEditorから確認できます。
 - 外部操作防止用Collider
  VRChatの仕様でUIへRaycastが遠方から届いてしまい誤操作を招く問題があるため、UIの周囲をColliderで囲ってあります。


## 仕様
 - Event driven型システム
  Event driven型システムで必要な時に同期処理が走るため上映中は低負荷での運用が可能です。
 - LateJoiner対応
  Joinしてから読み込まれるまで同期を取るためJoin直後でも高精度な同期を実現します。