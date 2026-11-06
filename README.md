# Stage-Sync-System

> [!NOTE]
> システム全体の詳細な内部設計・遅延計算アルゴリズム・通信プロトコル仕様については、Root直下の **[DOCUMENTATION.md](DOCUMENTATION.md)** を参照してください。

## 概要
イベント等でパフォーマンスをする人向けのVRChatワールドギミックです。
音源、演出（Timeline / PlayableDirector）、動画（YamaStream / iwaSync3）を各プレイヤー間で高精度に同期し、演者と観客間の通信・IK補間遅延（タイムラグ）を相殺するオフセットを自動計算・適用します。

## 更新履歴
- **v2.0 (現在開発中)**
  - **演者用操作端末 (Performer Portable UI & 自動帰還トリガー)**: スタッフ不在時でも演者が壇上で自然に演出予約・即時発火・強制スタート・緊急停止を行える親子分離型（半設置型親機＋自動帰還型小型リモコン子機）端末を実装。完全イベント駆動（Updateゼロ）、近接検知表示、Owner委譲アーキテクチャ、正常終了時のみ次曲へ進むスマート・キューを搭載。
  - **Runtime演出手動ソート機能 (Runtime Manual D&D Sort)**: インワールドUIのManualモード選択時に下部ドロワーが展開し、VRレーザーポインターおよびDesktopマウスによるドラッグ＆ドロップ（D&D）や▲/▼ボタンで直感的に演出順序を並び替え可能に。オンデマンドLerpアニメーションと全参加者・演者端末へのリアルタイム順序同期を実装。
  - **デュアル動画プレイヤー対応**: YamaStreamへのネイティブ対応および自動検知ブリッジ（`SSSYamaBridge`）を追加。
  - **動的通信遅延計算アルゴリズム**: 時計ズレ（クロックスキュー $\delta$）を相殺する双方向三角測量法（Bidirectional Triangulation）および2人インスタンス用折半方式を実装。
  - **AVProデコード停止ラグ補正**: シーク時の停止ラグ（約34ms）先読み補正および高精度閾値（5ms）によるデコーダ競合・フリーズ防止。
  - **ロード待機・エラーハンドリング強化**: タイムアウト監視、失敗率閾値アボート、演者失敗時即時アボート機構を追加。
  - **強制上演機能 (Force Launch)**: 閾値エラーや演者エラーを無視して上演を開始できるオーバーライド機能（次回1回のみ/常時対応）を実装。
  - **インワールドUI全面刷新**: 演出ソート（Hierarchy / 名前 / GameObject名 / Manual）、ステータスランプ（Ready/Warning）、上演中設定ロック機能を追加。
  - **操作エリア判定 (UIOperatorChecker)**: 操作盤周囲のコライダーにより、枠外からの遠隔Raycast誤操作を物理コライダー無効化で完全遮断。
  - **演者専用オブジェクト切り替え (Performer Object Toggle)**: 演者のみ/観客のみ表示するオブジェクトの自動トグルと誤設定防止（Sanitize）機能。
  - **検証モニター (SSSDelayDebugMonitor)**: 内部遅延、シーク誤差、Timeline誤差、双方向遅延、クロックスキュー $\delta$ をリアルタイム検証可能なモニターを追加。
- **v1.0 (2024/09/02 リリース)**
  - 初版リリース。
  - iwaSync3との連携による音源・演出同期。
  - Collider（`Performer Space`）による演者検知機能。
  - 固定オフセットによる観客遅延制御。
  - Event-driven型同期・LateJoiner基本対応。
  - 基本操作UIおよびエディタ拡張（`StageSyncSystem_Editor`）の実装。

## 主要機能
- **双方向遅延計測 & 観客遅延制御 (Danceモード相当)**
  - インスタンス内のプレイヤー間でNetworkCallableによる双方向Ping送受信を行い、端末間の時計ズレ（クロックスキュー $\delta$）を完全相殺。
  - 3人以上では**双方向三角測量（Bidirectional Triangulation）**、2人では**双方向ペア遅延折半方式**により動的に片道通信遅延を算出。
  - 演者エリア（`Performer Space`）に入ったプレイヤーは先読み再生（`+Offset`）、観客は遅延再生（`-Offset`）することで、観客視点で演者のダンスと音源・映像がミリ秒単位で完全に一致します。
- **全プレイヤー動画事前ロード待機同期**
  - 動画が全員（または許容閾値以上）に読み込まれ、0秒地点で準備が完了するまで一斉待機。
  - 演者の読み込み失敗、または失敗率50%以上で自動アボートし、曲が聴けない状態での開始事故を防止。
- **デュアル動画プレイヤー対応**
  - **YamaStream** および **iwaSync3** の双方に対応。ブリッジコンポーネントによりイベントを疎結合に中継。
- **演出管理・Timeline自動追従**
  - シーン上の演出オブジェクトに `DirectionObject` を付けるだけで自動認識。
  - 上演中は毎フレーム動画の再生時間とTimelineの時間を監視し、ズレが発生した場合は自動補正。
  - 演者側の視界負荷軽減のため、演者端末でのみ演出を非表示にするトグル（`PlayForPerformer`）を搭載。
- **インワールド専用UI & 操作保護**
  - 演出ソート機能（Hierarchy順、名前順、GameObject名順、Manual順）。
  - **Runtime演出手動ソート (Runtime Manual D&D Sort)**：Manualモード時に下部ドロワーが展開し、VRレーザー/マウスでの直感的なドラッグ＆ドロップ並び替え（オンデマンドLerpアニメーション・リアルタイム同期）。
  - 強制上演機能（`Force Launch`：閾値エラーや演者エラーを無視して上演可能、次回1回のみ/常時の設定対応）。
  - 遅延計算モード切り替え（Default / Fallback）。
  - 人数・成否ステータス表示（`Loaded Player`、`Performer Status`、`Delay Status`）。
  - `UIOperatorChecker` により、操作盤周囲の枠外からの遠隔Raycast誤操作を物理コライダー無効化で完全遮断。
- **演者用操作端末 (Performer Portable UI & 自動帰還トリガー)**
  - スタッフ不在のステージ上でも演者が自然に演出を制御できる親子分離型端末。
  - 親機（半設置型パネル）：演者接近時のみ表示（近接コライダー制御）、位置固定（Position Lock）、演出予約、Force Launch、緊急停止機能。
  - 子機（小型リモコン）：立ち位置へ持ち込み、`Use` 操作で予約演出を発火。発火後または手放した瞬間に親機のホルスターへ自動帰還。
  - 正常終了時のみセットリスト順に自動進行するスマート・キュー（オートアドバンス）を搭載。
- **演者専用オブジェクト切り替え (Performer Object Toggle)**
  - 演者エリア内でのみ有効化/無効化するオブジェクト群（返しモニターや目印等）を設定可能。
- **エディタ拡張 & 自動ビルドプロセス**
  - インスペクターからの直感的な演出並び替え（`ReorderableList`）とUIボタンの自動生成・結線。
  - ワールドビルド時およびPlayモード突入時の自動検証・配線パイプライン（`StageSyncSystemBuildProcess`）。

## 内容物
```
Assets/Appletea's Item/Stage Sync System/v2.0/
├─ Editor/
│  ├─ StageSyncSystem_Editor.cs         # インスペクター拡張・UI自動生成・ビルド前処理
│  └─ SSSDelayDebugMonitor_Editor.cs     # 検証モニターの自動配置・ワンクリック除去エディタ
├─ Material/
│  └─ Performer Space.mat               # 演者エリア確認用マテリアル
├─ Script/
│  ├─ DirectionController.cs            # ルートコンポーネント (エディタ連携・互換性)
│  ├─ DirectionObject.cs                # 演出オブジェクト定義コンポーネント
│  ├─ DirectionSwitch.cs                # 物理スイッチ用トリガーコンポーネント
│  ├─ PerformerChecker.cs               # 演者コライダー判定コンポーネント
│  ├─ SSSCore.cs                        # システムコアロジック (同期・遅延計算・動画制御)
│  ├─ SSSDelayDebugMonitor.cs           # 遅延・クロックスキュー検証用モニター
│  ├─ SSSIwaSyncBridge.cs               # iwaSync3 イベント受信ブリッジ
│  ├─ SSSManualSortItem.cs              # Runtime手動ソート各行アイテム制御
│  ├─ SSSPerformerProximityChecker.cs   # 演者端末の近接検知・表示制御
│  ├─ SSSPerformerTrigger.cs            # 演者端末の自動帰還トリガー子機制御
│  ├─ SSSPerformerUIController.cs       # 演者用操作端末の親機UI制御
│  ├─ SSSRuntimeManualSorter.cs         # Runtime手動ソート(D&D)制御
│  ├─ SSSUIController.cs                # インワールドUI管理・ソート・ForceLaunch
│  ├─ SSSYamaBridge.cs                  # YamaStream イベント受信ブリッジ
│  └─ UIOperatorChecker.cs              # UI操作可能エリア判定・外部Raycast遮断
├─ Texture/                          # UIスプライト・アイコン画像群
└─ Stage Sync System.prefab          # システム全体を包括したオールインワンプレハブ
```

## 使い方
### 導入方法
1. **Prefabの配置**:
   Scene上に `Stage Sync System.prefab` を配置します。
2. **エリアとUIの配置**:
   - `UI` を操作しやすい位置へ移動します。
   - `Performer Space` を演者がパフォーマンスを行うステージ範囲に合わせて拡大・設置します（設置後はMesh Rendererを無効化または削除してください）。
3. **動画プレイヤーの連携確認**:
   Scene上に YamaStream または iwaSync3 を配置し、アクティブにします。
   `Stage Sync System` のルートを選択してインスペクター上で「Linked」と表示されていることを確認します。
4. **演出の設定**:
   Scene上の演出オブジェクト（`PlayableDirector` 付属）に `DirectionObject` スクリプトをアタッチし、演出名（`Direction Name`）と動画URL（`Video URL`）を設定します。
5. **演出の読み込み**:
   `Stage Sync System` のルートを選択すると、エディタが演出を自動認識し、UIボタン群を自動生成します。

詳細な技術解説・アルゴリズム仕様は **[DOCUMENTATION.md](DOCUMENTATION.md)** をご覧ください。

## 利用規約
- MIT License
