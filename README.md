# Stage-Sync-System
![Stage Sync System Logo](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Texture/Stage%20Sync%20System%20Logo%20v1.5.png)

> [!NOTE]
> システム全体の詳細な内部設計・遅延計算アルゴリズムの仕様については、**[DOCUMENTATION.md](DOCUMENTATION.md)** を参照してください。

## 概要
イベント等でパフォーマンスをする人向けのVRChatワールドギミックです。
演出と動画再生を各プレイヤー間で同期し、演者と観客間の通信・IK補間遅延を吸収するオフセットを自動計算・適用できるシステムとなっています。

## 主要機能
- **動的遅延計算 & 観客遅延制御**
  - インスタンス内のプレイヤー間でNetworkCallableによる双方向Ping送受信を行い、端末間の時計ズレ（クロックスキュー $\delta$）を吸収
  - 3人以上では**双方向三角測量法（Bidirectional Triangulation）**、2人では**双方向ペア遅延折半方式**により動的に片道遅延を算出
  - 演者エリア（`Performer Space`）に入ったプレイヤーは先読み再生（`+Offset`）、観客は遅延再生（`-Offset`）することで、観客視点で演者のダンスと音源・映像を数十ミリ秒単位で調整
  - **ビーコン3名への負荷集中仕様**: 全員での総当たりPingによる帯域逼迫を防ぐため、演者を最優先とした代表3名を「ビーコン」として選出し、遅延計測・集計処理の通信負荷をその3名へ意図的に集中（押し付け）させることで、大多数の観客端末の通信負荷を大幅に削減
- **全プレイヤー動画事前ロード待機同期 & エラーハンドリング**
  - 動画が全員（または許容閾値以上）に読み込まれ、0秒地点で準備が完了したことを確認してから一斉に再生開始
  - 演者の読み込み失敗、または全体の失敗率50%(デフォルト設定)以上で再生をキャンセルし、曲が聴けない状態での開始事故を防止
  - 誰かが動画を読み込めないことによる進行不能を回避するため、読み込みエラーを無視して上演を開始できるオーバーライド機能（Force Launch：次回1回のみ/常時対応）を搭載
- **UI全面刷新 & 操作保護**
  - 演出ソート機能（Hierarchy順、名前順、GameObject名順、Manual順）
  - **Runtime演出手動ソート機能 (Runtime Manual D&D Sort)**: 従来はEditor上でのみ並べ替えが可能だったものを、ソートモードがManualの場合にVRによるドラッグ＆ドロップ（D&D）や▲/▼ボタンで直感的に演出順序を並び替え可能に
  - 遅延計算モード切り替え（Default / Fallback）
  - 人数・成否ステータス表示（`Loaded Player`、`Performer Status`、`Delay Status`）およびステータスランプ（Ready / Warning）
  - 上演中設定ロック機能（上演中の誤操作を防止）
- **演者用操作端末 (Performer Portable UI & 自動帰還トリガー)**
  - スタッフ不在時でも演者が壇上で自然に演出予約・即時発火・強制スタート・緊急停止を行える親子分離型端末
  - 親機（半設置型パネル）: 演者接近時のみ表示（近接検知）、位置固定（Position Lock）、演出予約、進行不能回避（Force Launch）、緊急停止機能
  - 子機（小型リモコン）: 立ち位置へ持ち込み、`Use` 操作で予約演出を発火。発火後または手放した瞬間に親機のホルスターへ自動帰還
  - 完全なEvent-driven方式、Owner委譲アーキテクチャ、正常終了時のみ次曲へ進むスマート・キューを搭載
- **2種類の動画プレイヤーに対応**
  - **YamaPlayer** および **iwaSync3** の双方にネイティブ対応。自動検知ブリッジコンポーネントによりイベントを疎結合に中継
- **演出管理・Timeline自動追従**
  - シーン上の演出オブジェクトに `DirectionObject` を付けるだけで自動認識
  - 上演中は毎フレーム動画の再生時間とTimelineの時間を監視し、ズレが発生した場合は自動補正
  - 演者側の視界負荷軽減・視認性確保のため、演者端末でのみ演出を非表示にするトグル（`PlayForPerformer`）を搭載
  - **操作エリア判定 (UIOperatorChecker)**: 操作盤周囲のコライダーにより、枠外からの遠隔Raycast誤操作を物理コライダー無効化で完全遮断
- **演者専用オブジェクト切り替え (Performer Object Toggle)**
  - 演者のみ/観客のみ表示するオブジェクト（返しモニターや立ち位置目印等）の自動切り替え
  - SSS関連システムオブジェクトの誤設定を自動検知して除外する誤設定防止機能を搭載
- **エディタ拡張 & 自動ビルドプロセス**
  - インスペクターからの直感的な演出並び替え（`ReorderableList`）とUIボタンの自動生成・結線
  - ルートオブジェクトのInspector上で各種システムパラメータ（基本システム遅延、フォールバック遅延、同期許容誤差、ロード待機タイムアウト、上演中断エラー率閾値等）の直接編集およびデフォルトリセットが可能
  - ワールドビルド時およびPlayモード突入時の自動検証・配線パイプライン（`StageSyncSystemBuildProcess`）

## 内容物
```
Assets/Appletea's Item/Stage Sync System/v2.0/
├─ Editor/
│  └─ StageSyncSystem_Editor.cs         # インスペクター拡張・UI自動生成・ビルド前処理
├─ Material/
├─ Script/
│  ├─ DirectionController.cs            # ルートコンポーネント (エディタ連携・互換性)
│  ├─ DirectionObject.cs                # 演出オブジェクト定義コンポーネント
│  ├─ DirectionSwitch.cs                # 物理スイッチ用トリガーコンポーネント
│  ├─ PerformerChecker.cs               # 演者コライダー判定コンポーネント
│  ├─ SSSCore.cs                        # システムコアロジック (同期・遅延計算・動画制御)
│  ├─ SSSIwaSyncBridge.cs               # iwaSync3 イベント受信ブリッジ
│  ├─ SSSManualSortItem.cs              # Runtime手動ソート各行アイテム制御
│  ├─ SSSPerformerProximityChecker.cs   # 演者端末の近接検知・表示制御
│  ├─ SSSPerformerTrigger.cs            # 演者端末の自動帰還トリガー子機制御
│  ├─ SSSPerformerUIController.cs       # 演者用操作端末の親機UI制御
│  ├─ SSSRuntimeManualSorter.cs         # Runtime手動ソート(D&D)制御
│  ├─ SSSUIController.cs                # インワールドUI管理・ソート・進行不能回避オーバーライド
│  ├─ SSSYamaBridge.cs                  # YamaPlayer イベント受信ブリッジ
│  └─ UIOperatorChecker.cs              # UI操作可能エリア判定・外部Raycast遮断
├─ Texture/                             # UIスプライト・アイコン画像群
└─ Stage Sync System.prefab             # システム全体を包括したオールインワンプレハブ
```

## 使い方
### 導入方法
1. **Prefabの配置**
   Scene上に `Stage Sync System.prefab` を配置
2. **エリアとUIの配置**
   - `UI` を操作しやすい位置へ移動
   - `Performer Space` を演者がパフォーマンスを行うステージ範囲に合わせて拡大・設置（設置後はMesh Rendererを無効化または削除）
3. **動画プレイヤーの連携確認**
   Scene上に YamaPlayer または iwaSync3 を配置してアクティブ化
   `Stage Sync System` のルートを選択してインスペクター上で「Linked」と表示されていることを確認
4. **演出・動画の登録**
   後述の「演出・動画の登録ガイド」に従い、Scene上に演出オブジェクトを作成・配置
5. **演出の読み込み**
   `Stage Sync System` のルートを選択すると、エディタが演出を自動認識し、UIボタン群を自動生成
6. **パラメータの調整（任意）**
   `Stage Sync System` のルートを選択し、インスペクターの「パラメータ設定 (Manual Parameters)」から基本システム遅延、タイムアウト秒数、上演中断エラー率閾値（%）などを自由に調整可能

### 演出・動画の登録ガイド
1. **空のGameObjectを作成**
   Hierarchy上で右クリックし、「Create Empty」で空のGameObjectを作成（曲名など分かりやすい名前にリネーム）
2. **DirectionObjectをアタッチ**
   作成したGameObjectに `DirectionObject` スクリプトをアタッチ（`PlayableDirector` コンポーネントが自動的に追加）
3. **パラメータの設定**
   - **Direction Name**: UI上に表示される演出・曲名を入力
   - **Video URL**: 再生したい動画（音源）のURLを入力
   - **PlayableDirectorの設定（任意）**: 演出用アニメーションやパーティクルを連動させる場合は、自動追加された `PlayableDirector` の `Playable` スロットに Timeline Asset をセット（未設定・Noneのままでも動画のみの曲として正常に再生可能）
   - **Play For Performer**: 演者端末で演出を再生するかどうかを選択（OFF推奨：演者の視界確保・描画負荷軽減）
4. **UIへの反映**
   設定完了後、`Stage Sync System` のルートオブジェクトを選択すると、エディタがシーン内の `DirectionObject` を自動スキャンしてUIボタンを自動生成・結線

## 通信負荷に関する仕様（ビーコン3名への負荷集中）
- インスタンス内の全員が総当たりで遅延計測Pingを送受信すると通信パケットが爆発するため、本システムでは**選出された代表3名（演者を最優先とし、残りをplayerId順で選出）が「ビーコン（Beacon）」として動作**
- ビーコンとなる3名が遅延計測用のPingパケット送信や集計処理を一手に引き受けるため、**通信・処理負荷がこの3名に集中（押し付ける仕様）**
- これにより、大多数の一般観客クライアントは自発的なPing送信の負荷から解放され、大人数インスタンスでもネットワーク帯域の枯渇や全体の同期破綻を防止

詳細な技術解説・アルゴリズム仕様については **[DOCUMENTATION.md](DOCUMENTATION.md)** を参照

## 利用規約
- MIT License

## 更新履歴
- **v2.0(2026/11/06 リリース)**
  - **動的遅延計算アルゴリズム**: 時計ズレ（クロックスキュー $\delta$）を吸収する双方向三角測量法を採用。追加で2人インスタンス用折半方式を実装
  - **読み込み状態監視・エラーハンドリング強化**: タイムアウト監視、観客・演者の読み込み状態の監視機能を追加。それに伴い、全員のロード確認が取れたらすぐに再生開始できる。加えてエラーによる再生キャンセル機構を追加(進行不能の回避策として、観客や演者の読み込みエラーを無視して上演開始できるオーバーライド機能（次回1回のみ/常時対応）も搭載)
  - **演者用操作端末 (Performer Portable UI & 自動帰還トリガー)**: スタッフ不在時でも演者が壇上で自然に演出予約・即時発火・強制スタート・緊急停止を行える親子分離型（半設置型親機＋自動帰還型小型リモコン子機）端末を実装。完全なEvent-driven方式、近接検知表示、Owner委譲アーキテクチャ、正常終了時のみ次曲へ進む仕組みを搭載
  - **Runtime演出手動ソート機能 (Runtime Manual D&D Sort)**: Editor上のみならず、動作中のシステム内UIで演出を手動ソートできる機能を実装。VRでも直感的に操作可能
  - **2種類の動画プレイヤーに対応**: IwasyncだけでなくYamaPlayerへのネイティブ対応およびそれぞれの動画プレイヤーへの自動検知ブリッジを追加
  - **UI全面刷新**: 演出ソート（Hierarchy / 名前 / GameObject名 / Manual）、ステータスランプ（Ready/Warning）、上演中設定ロック機能を追加
  - **操作エリア判定 (UIOperatorChecker)**: 操作盤周囲のコライダーにより、枠外からの遠隔Raycast誤操作を物理コライダー無効化で完全遮断
  - **演者専用オブジェクト切り替え (Performer Object Toggle)**: 演者のみ/観客のみ表示するオブジェクトの自動トグルとSSS関連システム誤設定防止機能搭載
- **v1.0 (2024/09/02 リリース)**
  - 初版リリース
  - iwaSync3との連携による動画・演出同期
  - 固定オフセットによる観客遅延制御
  - Event-driven型同期・LateJoiner基本対応
  - 基本操作UIおよびEditor拡張の実装
