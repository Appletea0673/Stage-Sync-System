# Stage Sync System (SSS) システム全体仕様書・技術解説書

本書は、VRChat向けステージ演出・動画同期システム **Stage Sync System** の全体仕様、アーキテクチャ設計、通信および遅延同期アルゴリズム、UI・オペレーター機能、エディタ拡張機構、技術的制約、ならびに今後の機能議論・設計検証に向けた技術論点を網羅的かつ詳細にまとめたドキュメントです。

---

## 目次

1. [システムの全体像と前提条件 (System Overview & Prerequisites)](#1-システムの全体像と前提条件-system-overview--prerequisites)
   - [1.1 システムの概要と開発目的](#11-システムの概要と開発目的)
   - [1.2 技術スタック・動作環境](#12-技術スタック動作環境)
   - [1.3 全体アーキテクチャ（エンドツーエンドのデータの流れ）](#13-全体アーキテクチャエンドツーエンドのデータの流れ)
   - [1.4 主要コンポーネント一覧と責務](#14-主要コンポーネント一覧と責務)
2. [議論対象機能の詳細仕様 (Feature Specifications & I/O)](#2-議論対象機能の詳細仕様-feature-specifications--io)
   - [2.1 機能①：動的通信遅延計測・オフセット制御](#21-機能動的通信遅延計測オフセット制御)
   - [2.2 機能②：全員動画事前ロード待機・同期発火 (Synchronized Pre-loading)](#22-機能全員動画事前ロード待機同期発火-synchronized-pre-loading)
   - [2.3 機能③：Timeline演出追従同期 (Timeline Drift Sync)](#23-機能timeline演出追従同期-timeline-drift-sync)
   - [2.4 機能④：インワールドUI・ソート・進行不能回避 (SSSUIController)](#24-機能インワールドuiソート進行不能回避-sssuicontroller)
   - [2.5 機能⑤：外部操作防止・エリア判定 (UIOperatorChecker)](#25-機能外部操作防止エリア判定-uioperatorchecker)
   - [2.6 機能⑥：演者専用オブジェクト切り替え (Performer Object Toggle)](#26-機能演者専用オブジェクト切り替え-performer-object-toggle)
   - [2.7 機能⑦：デュアル動画プレイヤー連携 (SSSYamaBridge / SSSIwaSyncBridge)](#27-機能デュアル動画プレイヤー連携-sssyamabridge--sssiwasyncbridge)
   - [2.8 機能⑧：演者用操作端末 (Performer Portable UI & Auto-Return Trigger)](#28-機能演者用操作端末-performer-portable-ui--auto-return-trigger)
   - [2.9 機能⑨：Runtime演出手動ソート機能 (Runtime Manual D&D Sort)](#29-機能runtime演出手動ソート機能-runtime-manual-dd-sort)
3. [技術的な制約・非機能要件 (Technical Constraints & Non-Functional Requirements)](#3-技術的な制約非機能要件-technical-constraints--non-functional-requirements)
   - [3.1 パフォーマンス要件 (Performance Requirements)](#31-パフォーマンス要件-performance-requirements)
   - [3.2 環境固有の制約 (Platform Specific Constraints)](#32-環境固有の制約-platform-specific-constraints)
   - [3.3 今後の拡張予定 (Future Roadmap)](#33-今後の拡張予定-future-roadmap)
4. [現在のステータスと議論の論点 (Status, Design Options & Discussion Goals)](#4-現在のステータスと議論の論点-status-design-options--discussion-goals)
   - [4.1 現在の開発フェーズ](#41-現在の開発フェーズ)
   - [4.2 過去の設計案・アプローチと改善案の比較](#42-過去の設計案・アプローチと改善案の比較)
   - [4.3 今回最も解決・検証したい論点（ゴール）](#43-今回最も解決検証したい論点ゴール)
5. [参考コード・データ構造・ネットワーク仕様 (Code & Data Specifications)](#5-参考コードデータ構造ネットワーク仕様-code--data-specifications)
   - [5.1 同期変数 (UdonSynced Variables) 一覧](#51-同期変数-udonsynced-variables-一覧)
   - [5.2 NetworkCallable (RPC) 仕様一覧](#52-networkcallable-rpc-仕様一覧)
   - [5.3 内部データ構造 (DataDictionary / DataList) キー規則](#53-内部データ構造-datadictionary--datalist-キー規則)
   - [5.4 主要アルゴリズムのコード抜粋](#54-主要アルゴリズムのコード抜粋)
6. [エディタ拡張・自動化パイプライン (Editor & Build Pipeline)](#6-エディタ拡張自動化パイプライン-editor--build-pipeline)
   - [6.1 StageSyncSystem_Editor カスタムインスペクター](#61-stagesyncsystem_editor-カスタムインスペクター)
   - [6.2 演出リストの自動検知とReorderableList](#62-演出リストの自動検知とreorderablelist)
   - [6.3 プレイリストボタンスイッチの自動生成・結線](#63-プレイリストボタンスイッチの自動生成結線)
   - [6.4 ビルド前処理 (StageSyncSystemBuildProcess)](#64-ビルド前処理-stagesyncsystembuildprocess)
7. [導入・設定・運用ガイド (Setup & Operation Guide)](#7-導入設定運用ガイド-setup--operation-guide)

---

## 1. システムの全体像と前提条件 (System Overview & Prerequisites)

### 1.1 システムの概要と開発目的
**Stage Sync System (SSS)** は、VRChat上の音楽ライブ、ダンスショー、舞台演劇等のイベントにおいて、**演者（Performer）と観客（Audience）の間で音源、映像（動画プレイヤー）、演出（Timeline / PlayableDirector）を高精度に完全同期**させ、さらに演者と観客間の通信遅延やクライアント描画・再生遅延（タイムラグ）を吸収・補正するための高度なワールドギミックシステムです。

#### 解決する課題
1. **演者と観客間のモーション通信遅延（IK補間ラグ）**:
   VRChatでは演者のアバターの動き（トラッキング・IK）が観客に届くまでに、ネットワーク往復遅延に加えて、観客端末内でアバターを滑らかに補間描画するためのバッファ（約260ms）が発生。そのため、全員が同じタイミングで音源を再生していると、**観客から見て「演者の動きが音や映像よりワンテンポ（約300〜500ms）遅れて見える」** という決定的なズレが発生
2. **動画プレイヤーの読み込みラグによる開始ズレ**:
   YouTubeや生配信URLを読み込む際、各端末のスペックや回線速度の違いにより、動画が実際に再生開始可能になるまでに数秒〜数十秒の個体差が発生
3. **Timeline演出と動画音源のクロックドリフト**:
   フレームレートの揺らぎやデコーダ負荷によって、UnityのTimelineアニメーションと動画プレイヤーの再生位置が次第にズレていく問題が発生

---

### 1.2 技術スタック・動作環境

| 区分 | 技術項目 | バージョン / 仕様詳細 |
| :--- | :--- | :--- |
| **開発言語** | C# (UdonSharp) | UdonSharp 1.x (Unity C# サブセット) |
| **ゲームエンジン** | Unity | 2022.3.22f1 |
| **プラットフォーム / FW** | VRChat World SDK | VRCSDK3-WORLD 3.5.0 以降 |
| **実行環境** | クライアント端末 | Windows PC (VR / Desktop) |
| | 通信ネットワーク | VRChat / Photon Cloud リレーサーバー (Photon Realtime) |
| **関連ライブラリ / 外部ツール** | 動画プレイヤー | **YamaPlayer** (`Yamadev.YamaStream.Controller`, AVPro Video ベース)<br>**iwaSync3** (`HoshinoLabs.IwaSync3.Udon.VideoCore`, AVPro / Unity Video) |
| | 演出エンジン | Unity Playables API (`UnityEngine.Playables.PlayableDirector`) |
| | データコンテナ | VRChat Data Token (`VRC.SDK3.Data.DataDictionary`, `DataList`) |
| | RPCフレームワーク | VRChat UdonNetworkCalling (`[NetworkCallable]`) |
| **レンダリング** | パイプライン | Built-in Render Pipeline (Standard Shader / Custom Toon Shader) |

---

### 1.3 全体アーキテクチャ（エンドツーエンドのデータの流れ）

```mermaid
flowchart TD
    subgraph Control["1. 操作・トリガー層"]
        User["オペレーター / 観客"] -->|ボタン押下 / Interact| UI["SSSUIController / DirectionSwitch"]
        User -->|D&D並び替え| Sorter["SSSRuntimeManualSorter (手動ソート)"]
        Sorter -->|順序確定| UI
        Area["演者"] -->|コライダー侵入 / 退出| PC["PerformerChecker (ステージコライダー)"]
        OpArea["操作者"] -->|近接侵入 / 退出| UOC["UIOperatorChecker (UI保護コライダー)"]
        
        subgraph PerformerTerminal["演者用操作端末 (Performer Portable UI)"]
            PerfProximity["SSSPerformerProximityChecker (近接検知)"] -->|表示・Collider切替| PerfParent["SSSPerformerUIController (親機)"]
            PerfActor["演者 (壇上)"] -->|予約選択 / Force Start / 緊急停止| PerfParent
            PerfActor -->|子機Pickup持ち込み & Use発火| PerfChild["SSSPerformerTrigger (子機リモコン)"]
            PerfChild -->|発火通知 & 自動帰還| PerfParent
        end
    end

    subgraph CoreLogic["2. コア制御・遅延計算層 (SSSCore)"]
        UI -->|演出再生要求| SSS["SSSCore (Owner)"]
        PerfParent -->|再生開始・強制上演・停止要求| SSS
        PC -->|演者判定フラグ通知| SSS
        UOC -->|操作許可フラグ切替| UI

        subgraph LatencyCalc["双方向遅延計測 (Bidirectional Triangulation)"]
            SSS <-->|NetworkCallable Ping| Peer["他クライアント SSSCore"]
            SSS -->|時計ズレδ吸収 & 中央値フィルタ| NetDelay["計算された NetworkDelay"]
        end

        subgraph Preloading["全員Ready待機・合否判定"]
            SSS -->|NetOnBeginLoading| AllClients["全参加者クライアント"]
            AllClients -->|NetNotifyPlayerLoadStatus| SSS
            SSS -->|ロード成功・失敗率判定| Launch["NetStartStagePlayback 発火"]
        end
    end

    subgraph Bridges["3. 抽象化ブリッジ層"]
        SSS --> YB["SSSYamaBridge"]
        SSS --> IB["SSSIwaSyncBridge"]
    end

    subgraph PlaybackExecution["4. 再生・演出実行層"]
        YB -->|再生・遅延・シーク制御| Yama["YamaPlayer Controller"]
        IB -->|再生・遅延・シーク制御| Iwa["iwaSync3 VideoCore"]
        SSS -->|毎フレーム時間追従| Dir["DirectionObject (PlayableDirector)"]
        SSS -->|演者専用表示切り替え| PerfObjs["Performer Objects (返しモニター等)"]
        SSS -->|進捗・状態通知| PerfParent
    end
```

---

### 1.4 主要コンポーネント一覧と責務

| コンポーネント | 配置オブジェクト | 責務・役割 |
| :--- | :--- | :--- |
| **[`SSSCore`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSCore.cs)** | `Core` | システム中核。双方向遅延計測、オフセット計算、全員ロード待機判定、動画・Timeline制御、上演ライフサイクル統括 |
| **[`SSSUIController`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSUIController.cs)** | `UI` | インワールド操作盤。演出リスト管理、ソートモード切替、進行不能回避（Force Launch）設定、ステータス表示（人数・色・ランプ） |
| **[`SSSPerformerUIController`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSPerformerUIController.cs)** | `Performer Portable UI` | 演者用端末の親機UI制御。演出予約（Standby）、オートアドバンス計算、Pickupロック、状態別画面遷移、`SSSCore` へのRPC送信 |
| **[`SSSPerformerTrigger`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSPerformerTrigger.cs)** | `PerformerTrigger` | 演者用端末の子機トリガー。立ち位置へ持ち込み `Use` で予約演出を発火。発火後または手放した瞬間にホルスターへ自動帰還 |
| **[`SSSPerformerProximityChecker`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSPerformerProximityChecker.cs)** | `ProximityChecker` | 演者端末の近接検知（1.8m）。演者接近時のみ親機Canvasを表示・コライダー有効化し、離脱時は完全非表示化 |
| **[`SSSRuntimeManualSorter`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSRuntimeManualSorter.cs)** | `ManualSortDrawer` | メインUI下部のRuntime手動ソートドロワー制御。VR/Desktop対応D&D、オンデマンドLerpアニメーション、順序同期 |
| **[`SSSManualSortItem`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSManualSortItem.cs)** | `SortItem_X` | 手動ソートドロワー内の各演出行アイテム。タイトル表示、ドラッグ中ハイライト、▲/▼手動移動ボタンの中継 |
| **[`DirectionController`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/DirectionController.cs)** | `Stage Sync System (Root)` | システム最上位コンポーネント。エディタ拡張の基点となり、各コンポーネント間の参照を自動統合 |
| **[`DirectionObject`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/DirectionObject.cs)** | 各演出オブジェクト | シーン上の各演出（`PlayableDirector`）にアタッチ。演出名、動画URL、演者への再生可否フラグを保持 |
| **[`DirectionSwitch`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/DirectionSwitch.cs)** | 任意トリガー/小道具 | 物理ボタンやプロップ等から特定演出をワンクリックで起動するためのトリガースクリプト |
| **[`PerformerChecker`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/PerformerChecker.cs)** | `Performer Space` | ステージ領域のコライダー。進入プレイヤーをローカルで演者と判定し、`SSSCore` へ通知 |
| **[`UIOperatorChecker`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/UIOperatorChecker.cs)** | `Control Protecter` | 操作盤周囲のコライダー。枠外からの遠隔Raycast誤操作をコライダー無効化で完全に遮断 |
| **[`SSSYamaBridge`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSYamaBridge.cs)** | `Core/Bridge` | YamaPlayerの `Listener` を継承し、動画準備完了・開始・エラー・停止イベントを `SSSCore` へ中継 |
| **[`SSSIwaSyncBridge`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSIwaSyncBridge.cs)** | `Core/Bridge` | iwaSync3の `VideoCoreEventListener` を継承し、各動画イベントを `SSSCore` へ中継 |

---

## 2. 議論対象機能の詳細仕様 (Feature Specifications & I/O)

### 2.1 機能①：動的通信遅延計測・オフセット制御

* **機能の目的（ユースケース）**: 
  * 演者と観客間の通信往復遅延とVRChatアバターIK補間遅延（約260ms）をミリ秒単位で吸収し、観客から見て演者のダンスの動きと音源・映像を数十ミリ秒単位で調整した状態で上演を行う
* **入力（Input）**: 
  * **トリガー**: 
    - 演者がステージエリアへ進入した瞬間（`PerformerChecker.OnPlayerTriggerEnter`）
    - 演出の動画ロード完了通知受信時（`NetNotifyPlayerLoadStatus` のピギーバック）
    - 定期・手動要求（`RequestLatencyUpdate`）
  * **データ形式**: 
    - `senderId` (`int`), `sendServerTime` (`int`: ミリ秒サーバー時刻)
* **出力（Output）**: 
  * `_networkDelay` (`float`: 片道通信遅延秒数, 0.0f〜1.0f)
  * `_pairDelays` (`DataDictionary`: ペア間双方向遅延ミリ秒)
  * 各プレイヤーの動画プレイヤーへのオフセット適用: 
    - YamaPlayer: `YamaPlayer_controller.LocalDelay = targetDelay`
    - iwaSync3: `Iwasync_core.offsetTime = targetOffset`
* **想定している処理フロー**: 
  1. インスタンス内の選出ノード（Beacon：演者最優先、上位3名）が `SendLatencyPing` を全クライアントへ送信
  2. 受信側がサーバー時刻差 $rawDelay = ServerTime - sendServerTime$ を計算。直近3サンプル最小値フィルタでスパイクを除去
  3. 逆方向の計測値との平均を取り、端末間の時計ズレ（クロックスキュー $\delta$）を吸収:
     $$d_{undirected}(A, B) = \frac{d(A \to B) + d(B \to A)}{2}$$
  4. **3人以上のインスタンス**: 全三角形候補から三角測量値を算出し、中央値（Median）を抽出:
     $$\text{Candidate}(X, Y) = \max\left(0, \frac{d(Local, X) + d(Local, Y) - d(X, Y)}{2}\right)$$
  5. **2人インスタンス**: 2者間双方向遅延の中央値の半分を採用:
     $$\text{NetworkDelay} = \frac{\text{Median}(d(Local, Other))}{2 \times 1000}$$
  6. オフセット秒数（`GetOffsetSeconds()`）を算出し、動画プレイヤーに適用: 
     - 演者: $+\left(\frac{BaseSystemDelay}{1000} + NetworkDelay\right)$
     - 観客: $-NetworkDelay$
* **異常系・エッジケースの扱い**: 
  * **1人インスタンス時**: 通信相手が存在せず三角測量不可のため、自動的に **Fallbackモード**（演者:+370ms / 観客:0ms）へフォールバック。UIの `Delay Status` にも `Fallback` と明示
  * **一時的な通信切断・巨大スパイク**: 直近3サンプル最小値フィルタ（`_directedPairPrev1`, `_directedPairPrev2`）により、URL解決時等の正方向スパイク（数千ms）を自動除去
  * **プレイヤー途中退室**: `OnPlayerLeft` にて該当プレイヤーの履歴辞書・リストを即座に破棄し、残存メンバーで遅延を再集計
  * **上演中のステージ端移動**: 上演中は演者フラグ（`_isPerformer`）が強制ロックされ、一時的なコライダー退出によるオフセット急変・シーク乱れを防止。上演終了時に実際のエリア在室状態へ復旧
* **既存モジュールとの依存関係**: 
  * 参照: `Networking.LocalPlayer`, `Networking.GetServerTimeInMilliseconds()`, `VRCPlayerApi`
  * 変更: `YamaPlayer_controller.LocalDelay`, `Iwasync_core.offsetTime`, `SSSUIController._delayStatusText`

---

### 2.2 機能②：全員動画事前ロード待機・同期発火 (Synchronized Pre-loading)

* **機能の目的（ユースケース）**: 
  * 各自の通信速度やPCスペックによって動画の読み込み完了タイミングが異なるため、全員が0秒地点でバッファリングを完了するまで待機し、頭出しが揃った瞬間に全員同時に上演を開始する
* **入力（Input）**: 
  * **トリガー**: UIの演出ボタン押下（`SSSUIController.OnButtonClicked`）、または物理スイッチ押下（`DirectionSwitch.Interact`）
  * **データ形式**: `objectIndex` (`int`: 演出インデックス), `sessionId` (`int`: 単調増加セッションID)
* **出力（Output）**: 
  * 全員の動画音量ミュート（`Vol = 0`）＆ 0秒一時停止（`PauseLocalVideoPlayerAtZero`）
  * UIの進捗表示（`Loaded Player: X/Y`、演者ランプ更新）
  * 合格時: `NetStartStagePlayback`（一斉再生開始）
  * 不合格時: `NetAbortStagePlayback`（上演中止・クリーンアップ）
* **想定している処理フロー**: 
  1. 起動者がOwnerを取得し、`_playbackSessionId++` をインクリメント。UI設定をロック
  2. `NetOnBeginLoading` を全員へブロードキャスト。各自動画プレイヤーをミュートし、URLをロード
  3. 各クライアントで動画がロード完了（`OnVideoReady` / `OnVideoStart`）した時点で0秒地点で一時停止し、`NetNotifyPlayerLoadStatus(Ready)` をOwnerへ送信
  4. Ownerが `EvaluatePlayersLoadState()` で進捗を集計
  5. **中止判定**: 
     - 演者が1人でもロード失敗（または未完了） $\to$ **上演中止（Abort）**
     - 観客のロード失敗率 $\ge 50\%$ $\to$ **上演中止（Abort）**
  6. **開始判定**: 全員完了、またはタイムアウト（15秒）時点で中止条件に非該当の場合、現在のサーバー時刻を刻印して `NetStartStagePlayback` を発火
  7. 全員が音量を復帰し、オフセットを加算した目標時間へ高精度シーク（AVPro先読み+34ms）して再生開始
* **異常系・エッジケースの扱い**: 
  * **進行不能の回避（Force Launch オーバーライド）**: 観客や演者の通信環境・プレイヤー不具合等で動画が読み込めず中止が繰り返される場合、エラーを無視して上演を開始できるオーバーライド機能（次回1回のみ/常時対応）により進行不能を回避
  * **VRChat 5秒URLレートリミット**: 前回のURL発行から5.25秒未満の連打時は、内部キュー（`_isWaitingForCooldownLoad`）に格納し、クールダウン経過後に最新セッションのURLのみを単一実行
  * **途中参加者 (Late Joiner)**: 上演中に入室したプレイヤーは `_stageStartServerTime` から経過時間をローカル計算して途中合流シーク。既存プレイヤーへは再同期（DoSync）を発火させないよう保護
* **既存モジュールとの依存関係**: 
  * 参照/変更: `SSSYamaBridge`, `SSSIwaSyncBridge`, `YamaPlayer_controller`, `Iwasync_core`, `SSSUIController`

---

### 2.3 機能③：Timeline演出追従同期 (Timeline Drift Sync)

* **機能の目的（ユースケース）**: 
  * 長時間の演出において、UnityのTimelineアニメーションと動画プレイヤーの音源・映像の再生時間が徐々にズレていく（クロックドリフト）現象を防止する
* **入力（Input）**: 
  * **トリガー**: 上演中の毎フレーム `Update()`
  * **データ形式**: `VideoTime` (`float`), `playableDirector.time` (`double`)
* **出力（Output）**: 
  * `currentDir.playableDirector.time = videoTime`（時間強制上書き）
* **想定している処理フロー**: 
  1. 毎フレーム、現在アクティブな演出の `playableDirector.time` と動画の `GetCurrentVideoTime()` の差分を計算
  2. 差分が `SyncAccuracy`（デフォルト: 0.04秒 / 40ms）以上離れた瞬間に、Timelineの時間を動画時間に同期
  3. 動画が一時停止・シーク・ループした際も、即座にTimeline側へ同等の時間同期イベントを伝達
* **異常系・エッジケースの扱い**: 
  * **演者への演出非表示（`PlayForPerformer = false`）**: 演者の視界塞ぎや描画負荷を低減するため、演者端末では該当演出のGameObject自体を非アクティブ化し、Timelineも再生しない
* **既存モジュールとの依存関係**: 
  * 参照/変更: `DirectionObject.playableDirector`, `SSSCore.GetCurrentVideoTime()`

---

### 2.4 機能④：インワールドUI・ソート・進行不能回避 (SSSUIController)

* **機能の目的（ユースケース）**: 
  * ワールド内でオペレーターが演出プレイリストの順序を整理し、ステータスを確認しながら安全に上演を行い、動画読み込みトラブル等の発生時には進行不能を回避するオーバーライドを実行する
* **入力（Input）**: 
  * **トリガー**: UIのToggle変更、ボタンクリック
  * **データ形式**: ソートモードEnum (`DirectionSortMode`), Force Launchフラグ (`bool`)
* **出力（Output）**: 
  * プレイリストボタン群の並び順（SiblingIndex）更新
  * `SSSCore` への設定変数同期
* **想定している処理フロー**: 
  1. **演出ソート**: Hierarchy順 / 演出名昇順・降順 / GameObject名昇順・降順 / Manual順 を選択。アロケーションフリーのインサーションソートでリストを即座に再配置
  2. **進行不能の回避（Force Launch オーバーライド）**: 
     - `IgnoreThresholdToggle`: 誰かの動画読み込み失敗率が50%以上でも、進行不能を回避して強制上演開始
     - `IgnorePerformerErrorToggle`: 演者のロードが失敗している場合でも、進行不能を回避して強制上演開始
     - 有効期間フォーム: 「次の1回のみ（上演開始時に自動解除）」または「常時」
  3. **上演中ロック**: 動画ロード中および上演中は全トグルをグレーアウトし、外部からの変更を拒否
* **異常系・エッジケースの扱い**: 
  * **非オペレーターの操作試行**: 後述の `UIOperatorChecker` により、コライダー枠外からのRaycastは物理的に無効化
* **既存モジュールとの依存関係**: 
  * 参照/変更: `SSSCore`, `UnityEngine.UI.Toggle`, `Button`, `Text`

---

### 2.5 機能⑤：外部操作防止・エリア判定 (UIOperatorChecker)

* **機能の目的（ユースケース）**: 
  * 遠く離れた客席から観客がUIへ遠隔Raycastをクリックして誤操作を引き起こすトラブルを防止する
* **入力（Input）**: 
  * **トリガー**: 操作エリアコライダー（BoxCollider）に対する `OnPlayerTriggerEnter` / `Exit`, `OnPlayerRespawn`
  * **データ形式**: `VRCPlayerApi.isLocal`
* **出力（Output）**: 
  * Canvas内の全Colliderの `enabled`（true/false）
  * 全UIコンポーネントの `interactable`（true/false）
* **想定している処理フロー**: 
  1. 操作盤の前に専用のBoxCollider（Trigger）を配置
  2. ローカルプレイヤーが枠内に入ると、Canvas内の全Colliderを有効化し、ボタン操作を許可
  3. 枠外に出ると、全Colliderを無効化（`enabled = false`）し、遠方からのRaycastヒット自体を遮断
* **異常系・エッジケースの扱い**: 
  * **リスポーン時**: リスポーン直後に位置が枠外へ移動するため、`OnPlayerRespawn` から1フレーム遅延で状態を再適用
  * **VRCUiShape初期化競合**: VRChat起動時にVRCUiShapeがコライダーを強制有効化する仕様に対抗し、`Start()` から1フレーム遅延でも再適用を実行
* **既存モジュールとの依存関係**: 
  * 参照/変更: `UnityEngine.Canvas`, `Collider[]`, `SSSUIController`

---

### 2.6 機能⑥：演者専用オブジェクト切り替え (Performer Object Toggle)

* **機能の目的（ユースケース）**: 
  * ステージ上のプロンプター（返しモニター）や立ち位置目印など、演者にだけ見せたいオブジェクト、または客席からのみ見せたいオブジェクトを自動切り替えする
* **入力（Input）**: 
  * **トリガー**: `SSSCore.IsPerformer` の変更
  * **データ形式**: `GameObject[] _performerEnableObjects`, `_performerDisableObjects`
* **出力（Output）**: 
  * 各オブジェクトの `SetActive(true / false)`
* **想定している処理フロー**: 
  1. 演者エリアに入ると、`_performerEnableObjects` がアクティブ化され、`_performerDisableObjects` が非アクティブ化される
  2. 演者エリアを出ると、逆の状態に復帰する
* **異常系・エッジケースの扱い**: 
  * **システムオブジェクト誤登録の自動除外（Sanitize機能）**: 
    ユーザーが誤って `SSSCore`、`UI`、動画プレイヤー本体、演出オブジェクトを演者専用オブジェクトに設定した場合、システムごと非アクティブ化されて破綻するのを防ぐため、エディタおよびランタイムで保護対象オブジェクトを自動検知して除外
* **既存モジュールとの依存関係**: 
  * 参照: `SSSCore.IsPerformer`

---

### 2.7 機能⑦：デュアル動画プレイヤー連携 (SSSYamaBridge / SSSIwaSyncBridge)

* **機能の目的（ユースケース）**: 
  * シーン内に配置されている動画プレイヤーが YamaPlayer であっても iwaSync3 であっても、ワールド作者がスクリプトを書き換えることなく自動的に検知・連携して動作させる
* **入力（Input）**: 
  * **トリガー**: 各動画プレイヤーのライフサイクルイベント（Ready, Start, Play, Pause, Stop, Error, SetTime）
* **出力（Output）**: 
  * `SSSCore` の対応ハンドラ呼び出し（`OnVideoReady()`, `OnVideoStart()` 等）
* **既存モジュールとの依存関係**: 
  * 参照: `Yamadev.YamaStream.Controller`, `HoshinoLabs.IwaSync3.Udon.VideoCore`

---

### 2.8 機能⑧：演者用操作端末 (Performer Portable UI & Auto-Return Trigger)

* **機能の目的（ユースケース）**: 
  * 裏手のオペレーター（PA・照明スタッフ）が不在の公演や、スタッフも壇上に上がるワンマンライブ・即興ショーにおいて、演者自身がステージ上で自然な所作で演出の選択（予約）・即時発火・進行不能回避（Force Launch）・緊急停止を行えるようにする
* **入力（Input）**: 
  * **トリガー**: 
    - 親機近接コライダーへの進入・離脱（`SSSPerformerProximityChecker.OnPlayerTriggerEnter / Exit`）
    - 親機UIの演出ボタン押下（`OnSelectStandbyDirection(index)`）
    - 子機リモコンの把持・トリガー操作（`SSSPerformerTrigger.OnPickupUseDown()` / `OnDrop()`）
    - 壇上からの即時強制スタートボタン押下（`OnForceStartNowButtonClicked()`）
    - 緊急停止の2ステップ安全トグル解除 ＆ 実行ボタン押下（`OnEmergencyStopButtonClicked()`）
    - 親機の位置固定切り替え（`_positionLockToggle`）
  * **データ形式**: 
    - `objectIndex` (`int`), `sessionId` (`int`), `ignoreThreshold` (`bool`), `ignorePerformerError` (`bool`)
* **出力（Output）**: 
  * 演出の予約（`_standbyObjectIndex`）およびハイライト・「Next Cue」テキスト更新
  * 親機Canvasの表示/非表示（演者接近時のみ表示、離脱時および上演中は完全非表示）
  * 子機リモコンのホルスター自動スナップ帰還（`ReturnToHolster()`）
  * `SSSCore` の Owner へ `[NetworkCallable]` RPC送信: 
    - `NetRequestStartFromPerformer(objectIndex, ignoreThreshold, ignorePerformerError)`
    - `NetRequestForceLaunchNow(sessionId)`
    - `NetRequestStopFromPerformer(sessionId)`
* **想定している処理フロー**: 
  1. **親機の配置**: 演者が親機をステージ脇や手元など任意の場所にPickupして運び、`Position Lock` トグルをONにして固定する（`pickupable = false`）
  2. **接近と予約**: 演者が親機に近づく（1.8m以内）と自動的にCanvasが表示され、`StandbyPanel` で次に流したい演出ボタンを選択する（この時点では再生されず「次曲予約 Standby」として保持）
  3. **子機持ち出し**: 演者が親機のホルスターから子機リモコンをPickupし、ステージ中央の立ち位置へ持ち込む。演者が離れると親機Canvasは自動的に非表示となり、観客席からの視界や返しモニターを遮らない
  4. **発火と自動帰還**: 演者が曲振りの決めポーズやMCのタイミングで子機のトリガー（`Use`）を引く $\to$ `SSSCore` の Owner へ再生開始RPCが送信され、子機は自動的に親機のホルスター位置へスナップ帰還する
  5. **ロード中画面**: 親機に近づくと `LoadingPanel` が表示され、進捗ゲージ（`Loaded: X/Y`）、進行不能回避の「Force Start Now」ボタン、中止「Cancel」ボタンが操作可能
  6. **上演中画面と緊急停止**: 上演中に演者が親機に近づくと `PlayingPanel` が表示され、再生時間・プログレスバーとともに、誤タップ防止の「2ステップ安全ロック解除トグル ＋ EMERGENCY STOPボタン」が表示される
  7. **スマート・キュー（オートアドバンス）**: 演出が**最後まで正常完了した場合のみ**、UI並び順における「次の演出」へ自動的に予約が進む。ロード失敗によるAbortや手動Stopで中断した場合は**予約を変更せず同一曲を維持**し、即座にリトライできるようにする
* **異常系・エッジケースの扱い**: 
  * **非演者（観客）の不正操作防止**: `SSSCore.IsPerformer == false` のプレイヤーに対しては、親機・子機ともに `VRC_Pickup.pickupable = false` に固定され、親機Canvasも常時非表示・コライダー無効となる
  * **ステージエリア退出時の強制Drop**: 演者が親機や子機を持ったままステージ（`Performer Space`）から退出した場合、その瞬間に `VRC_Pickup.Drop()` が強制発火し、子機はホルスターへ帰還する
  * **完全イベント駆動（Update関数ゼロ）**: 定常的なフレーム負荷をゼロにするため、スクリプト内に `Update()` を一切持たず、コライダーTriggerイベントおよび `SSSCore` からのライフサイクルフック通知のみで駆動する
  * **システムオブジェクト誤登録防止 (Sanitize)**: `SSSCore` の演者専用オブジェクト切り替え対象から、演者用端末自身を自動除外
* **既存モジュールとの依存関係**: 
  * 参照/変更: `SSSCore`, `SSSUIController`, `SSSPerformerTrigger`, `SSSPerformerProximityChecker`, `VRC_Pickup`

---

### 2.9 機能⑨：Runtime演出手動ソート機能 (Runtime Manual D&D Sort)

* **機能の目的（ユースケース）**: 
  * イベント当日の現場判断により、上演直前にセットリスト（演出順）を入れ替えたり、特定の演出を後回しにしたりする操作を、Unityエディタを開き直すことなくVRChatインスタンス内（Runtime）で直感的に行えるようにする
* **入力（Input）**: 
  * **トリガー**: 
    - メインUIのソートモード `Manual` トグルのON/OFF（`SSSUIController.OnSortToggleChanged`）
    - ドラッグ用透明Sliderプロキシの操作（`OnDragSliderPointerDown`, `OnDragSliderPointerUp`, `OnDragSliderValueChanged`）
    - 手動微調整用の各行「▲（1つ上へ）」「▼（1つ下へ）」ボタン押下
  * **データ形式**: 
    - `int[] currentOrder` (初期順序配列), `int[] _workingOrder` (作業用順序配列)
* **出力（Output）**: 
  * 下部手動ソートドロワー（`ManualSortDrawer`）の開閉（`SetActive(true / false)`）
  * ドラッグ中のアイテムのLive Reflow（他行のスムーズな退避 `Mathf.Lerp` アニメーション）
  * 確定順序のメインUI反映（`DirectionSW` ボタン群の `SiblingIndex` 一括更新）
  * ネットワーク同期（全クライアントおよび演者用端末 `SSSPerformerUIController` のリスト順序・オートアドバンス順序へリアルタイム反映）
* **想定している処理フロー**: 
  1. **ドロワー展開と順序引き継ぎ**: ユーザーがメインUIで `Manual` を選択すると、下部に手動ソートパネルが展開される。この際、**直前に表示されていた順序（例：名前順ソートの結果）をそのまま初期順序として引き継ぐ**ため、1曲だけ位置を微調整する運用が極めてスムーズに行える
  2. **VRレーザー ＆ Desktopマウス両対応のD&D機構**: UdonSharpで `IDragHandler` が直接利用できない制約を解決するため、ドラッグハンドル列に**透明なVertical Sliderプロキシ ＋ EventTrigger**を配置。ポインターの高さから掴んだ行を瞬時に特定し、VRコントローラーのレーザーポインターとDesktopマウスの双方で正確なD&Dを実現
  3. **ドラッグ中のフィードバック**: 掴まれた行はスケールがわずかに拡大（1.03倍）し、最前面描画＋ハイライト色に変化。元の位置には点線枠（`PlaceholderBar`）を表示
  4. **Live Reflow（滑らかな退避）**: 掴んだ行を上下に動かすと、境界を超えた瞬間に内部インデックスがスワップされ、他行が新しい目標Y座標へ向かって `Mathf.Lerp` でスッと滑らかにスライド移動する
  5. **エッジ・オートスクロール**: リストが長くスクロールビューを構成している場合、上下端15%の領域にポインターがある間、自動的にスクロールオフセットを滑らかに移動
  6. **ドロップ確定と全体同期**: トリガー（クリック）を離すと目標位置へスナップ収束し、確定した順序が `SSSUIController` に渡されて全参加者および演者用端末へ一括同期される
* **異常系・エッジケースの扱い**: 
  * **オンデマンド駆動（定常Update負荷ゼロ）**: 定常時は `this.enabled = false` でUpdateループが完全停止しており、ドラッグ開始からアニメーション収束完了（座標誤差0.5px未満）までの間だけ `enabled = true` になる
  * **上演中・ロード中の強制停止**: 上演開始またはロード待機に入った瞬間、ドラッグを強制キャンセルし `enabled = false` にロックすることで、上演中のアバターIK硬直や同期競合リスクを100%排除
  * **アロケーションフリー設計**: 配列や作業バッファはすべて初期化時に固定確保され、D&D操作中のGCスパイク（フレーム落ち）を完全防止
* **既存モジュールとの依存関係**: 
  * 参照/変更: `SSSUIController`, `SSSCore`, `SSSManualSortItem`, `SSSPerformerUIController`

---

## 3. 技術的な制約・非機能要件 (Technical Constraints & Non-Functional Requirements)

### 3.1 パフォーマンス要件 (Performance Requirements)
1. **VRフレームレート維持 (60FPS / 90FPS)**: 
   - メインスレッド負荷を最小限に抑え、VR酔いやトラッキング飛びを防止
   - 演出ソート処理はメモリアロケーションを伴わない**非再帰インサーションソート**を採用し、GC（Garbage Collection）スパイクを完全抑止
   - Runtime手動ソートも**オンデマンド駆動（定常時Updateゼロ）**を採用
2. **動画シーク頻度・デコーダ負荷制限**: 
   - 目標時間との差が `_VIDEO_SEEK_THRESHOLD` (5ms) 未満の場合はシークをスキップ
   - 毎フレームシークを連打するとAVPro Videoがフリーズ・競合するため、単一シーク＋1秒後フォースシンクの2段階制御を採用
3. **低定常負荷（Event-Driven設計）**: 
   - 上演中以外の待機時は不要なUpdateループ処理を停止し、ワールドの軽量化を徹底。演者用端末（`SSSPerformerUIController`）もUpdate関数ゼロで設計

---

### 3.2 環境固有の制約 (Platform Specific Constraints)
1. **Udon VM のシングルスレッド制約**: 
   - マルチスレッド並行処理や非同期Task（`async/await`）は利用不可。全計算はメインスレッドで安全に実行可能なアルゴリズムにする必要がある
2. **NetworkCallable / UdonNetworkCalling の制限**: 
   - RPCで送受信できる引数の型は基本プリミティブ型（`int`, `float`, `string`, `bool` 等）に限定
   - 過剰なRPC呼び出しはVRChatのネットワーク帯域制限・レートリミットに抵触するため、Beaconノード選出（最大3名）によりPing通信量を厳格に抑制
3. **VRChat 動画ロードAPIの5秒レートリミット**: 
   - 動画プレイヤーへのURL投入APIにはプラットフォーム全体で5秒のクールダウンが存在（短時間での連続ロードは `VideoError.RateLimited` で弾かれる）
   - 本システムでは5.25秒のクールダウンキュー管理によりこれを完全回避

---

### 3.3 今後の拡張予定 (Future Roadmap)
- **演者用操作端末(UI)**: 
  - スタッフ無しでも公演ができるように演者がステージ上で演出再生、設定変更が可能なUIを実装完了（2.8節参照）。さらなる所作の自然化や視認性向上を継続計画
- **Runtime上演出リスト入れ替え機能**: 
  - Editor上のみならずRuntime上でも演出リストの順番を手動で入れ替えることのできるUI機能を実装完了（2.9節参照）
- **UIの改善**: 
  - UIの操作時アニメーション等はまだなく、使用感を向上させる改良を計画
- **Animator / StateMachine 演出への対応**: 
  - 現在の `PlayableDirector` (Timeline) に加え、Animatorのステート時間追従にも対応予定
- **3D音響・空間距離遅延補正（Acoustic Delay Compensation）**: 
  - 巨大なスタジアムワールド等において、ステージと客席最後列との物理的な音速遅延（約340m/s）を計算し、客席位置に応じたローカル音響ディレイを付加する機能
- **外部OSC / DMX 照明制御連携**: 
  - VRChat外部の照明コントローラーやリアルライブ演出卓からのOSC信号を受信し、SSSのライフサイクルと同期させる拡張

---

## 4. 現在のステータスと議論の論点 (Status, Design Options & Discussion Goals)

### 4.1 現在の開発フェーズ
- [ ] アイデア・要件定義
- [ ] 設計中
- [ ] プロトタイプ実装中
- [x] **ボトルネック・バグ解消 / 実環境検証中** (Gitブランチ: `v2.0-Update`)

---

### 4.2 過去の設計案・アプローチと改善案の比較

| 評価項目 | 現在採用しているアプローチ | 過去のアプローチ | 採用理由・トレードオフ |
| :--- | :--- | :--- | :--- |
| **遅延計測方式** | **双方向三角測量法 (Bidirectional Triangulation)**<br>全ペア双方向Pingの中央値から各端末の片道遅延を幾何学的に特定 | **定数時間遅延**<br>経験則に基づく定数時間(0.6s)のオフセットを設ける | (ここに詳細を記述) |
| **演者用端末設計** | **親子分離型 ＋ Owner委譲RPC方式**<br>半設置型親機（接近時のみ表示）＋自動帰還型小型リモコン子機。操作はSSSCoreのOwnerへRPC委譲し完全イベント駆動（Updateゼロ） | **単一端末自律同期方式**<br>端末自身がSynced変数を持ちOwner権限を奪取して自律制御 | メインUIとのOwner競合を100%防止し、演者の視界・返しモニター遮りをゼロにし、定常負荷を抑えるため |
| **Runtime手動ソート** | **透明Sliderプロキシ ＋ オンデマンドLerp**<br>VRレーザー/Desktop両対応の透明Slider座標取得 ＋ 非ドラッグ時はUpdateゼロ | **IDragHandler直接利用**<br>標準のUnity UI EventSystemを利用 | UdonSharpにおけるVRコントローラーのPointerEventData取得制約を突破し、定常Update負荷をゼロにするため |

---

### 4.3 今回最も解決・検証したい論点（ゴール）

- [ ] **Update関数問題**: 
  - Update関数内の処理や上演中の処理は再生中のズレ修正にある程度必要だが、インスタンス人数が多い場合に演者のIK処理が一瞬硬直する問題があると報告されている。そのため、上演中の処理を極力減らす実装を想定している
- [ ] **高精度遅延補正**: 
  - あらゆるケースを想定し、可能な限り正確な遅延補正を適用し、遅延の無い同期システムを想定
- [ ] **異常系・エッジケースの堅牢性検証**: 
  - v1.0では未知の解決可能バグが存在したため、v2.0ではデバッグを徹底し既知のバグとし、解決に努める

---

## 5. 参考コード・データ構造・ネットワーク仕様 (Code & Data Specifications)

### 5.1 同期変数 (UdonSynced Variables) 一覧

| 変数名 | 型 | 同期モード | 役割・説明 |
| :--- | :--- | :--- | :--- |
| `_playingObjectIndex` | `int` | Manual (FieldChangeCallback) | 現在選択・上演中の演出インデックス（-1: 停止中） |
| `_isWaitingForReady` | `bool` | Manual | 全員の動画ロード待機中フラグ（true時: 全員ミュート） |
| `_isStagePlaying` | `bool` | Manual | 上演中フラグ（true時: 音量復帰・Timeline追従稼働） |
| `_playbackSessionId` | `int` | Manual | 上演セッションID（連打や古い遅延イベントを破棄するための単調増加ID） |
| `_stageStartServerTime` | `int` | Manual | 上演開始時のサーバー時刻ミリ秒 |
| `_stageStartedInFallback`| `bool` | Manual | 上演開始時にFallbackモードで開始されたかどうかの記録 |

---

### 5.2 NetworkCallable (RPC) 仕様一覧

| メソッド名 | 引数仕様 | 対象 | 説明 |
| :--- | :--- | :--- | :--- |
| `NetOnBeginLoading` | `int sessionId, int objectIndex` | All | 全員へ動画ロード開始を通知。動画プレイヤーをミュートしURLロード開始 |
| `NetNotifyPlayerLoadStatus`| `int sessionId, int playerId, bool isSuccess, bool isPerformer, int sendServerTime` | All | 各自の動画ロード成否を通知。送信サーバー時刻はピギーバック遅延計測に活用 |
| `NetStartStagePlayback` | `int sessionId, int objectIndex, int startServerTime, bool startedInFallback` | All | Ownerから全員へ上演開始を一斉発火 |
| `NetAbortStagePlayback` | `int sessionId` | All | エラーまたはタイムアウトにより上演を中止し、全員クリーンアップ |
| `NetRequestBeaconPings` | なし | All | 全員へBeaconノードからのPing送信を要求 |
| `NetReceiveLatencyPing` | `int senderId, int sendServerTime` | All | Pingパケットを受信し、片道伝搬時間を計測・履歴記録 |
| `NetReceiveLatencyRecord`| `int senderId, int receiverId, int delayMs` | All | 計測されたペア間遅延を他クライアントへ中継共有 |
| `NetUpdatePerformerState`| `int playerId, bool isPerformer` | All | プレイヤーの演者ステータス変更を全体共有 |
| `NetRequestStartFromPerformer` | `int objectIndex, bool ignoreThreshold, bool ignorePerformerError` | Owner | 演者端末からの再生開始リクエストを受理し一元処理 |
| `NetRequestForceLaunchNow` | `int sessionId` | Owner | 演者端末からの進行不能回避（Force Launch）リクエストを受理 |
| `NetRequestStopFromPerformer` | `int sessionId` | Owner | 演者端末からの緊急停止・キャンセルリクエストを受理 |

---

### 5.3 内部データ構造 (DataDictionary / DataList) キー規則

```csharp
// ペア間双方向キー: 小さいID_大きいID (例: "1_5")
string undirectedKey = GetPairKey(senderId, receiverId);

// 指向性（片方向）キー: 送信者ID_受信者ID (例: "5_1")
string dirKey = $"{senderId}_{receiverId}";
```

- `_pairDelays` (`DataDictionary`): 時計ズレ $\delta$ 吸収済みの双方向遅延ミリ秒（Key: `undirectedKey`, Value: `int`）
- `_directedPairDelays` (`DataDictionary`): 直近3サンプル最小値フィルタ適用済みの片道遅延（Key: `dirKey`, Value: `int`）
- `_directedPairPrev1` / `Prev2` (`DataDictionary`): 最小値フィルタ用履歴バッファ
- `_performerPlayerIds` (`DataList`): 現在ステージ上にいる演者の `playerId` リスト
- `_readyPlayerIds` (`DataList`): 動画ロードに成功した `playerId` リスト
- `_failedPlayerIds` (`DataList`): 動画ロードに失敗した `playerId` リスト

---

### 5.4 主要アルゴリズムのコード抜粋

#### 1. 双方向三角測量・中央値フィルタによる `NetworkDelay` 算出 ([`SSSCore.cs`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSCore.cs#L1621-L1697))

```csharp
// 3人以上での双方向三角測量（クロックスキューδ吸収済みペア優先 & 中央値採用）
for (int x = keyCount - 1; x >= 0; x--)
{
    for (int y = x - 1; y >= 0; y--)
    {
        int playerX = keys[x].Int;
        int playerY = keys[y].Int;
        int delayXY = GetDelayWithTwoPlayers(playerX, playerY);
        if (delayXY < 0) continue;

        int delayX = records[playerX].Int;
        int delayY = records[playerY].Int;
        
        // 三角不等式に基づく片道遅延候補値の算出
        int candidateMs = Mathf.Max(0, (delayX + delayY - delayXY) / 2);
        
        bool isFullyBidirectional = localId >= 0
            && HasBidirectionalPairDelay(localId, playerX)
            && HasBidirectionalPairDelay(localId, playerY)
            && HasBidirectionalPairDelay(playerX, playerY);

        if (isFullyBidirectional)
        {
            biAllTriangles[biAllCount++] = candidateMs;
        }
    }
}

// 候補値バッファから中央値（Median）を抽出して単一スパイクを排除
if (biAllCount > 0)
{
    float medianTriangleMs = ComputeMedianFromBuffer(biAllTriangles, biAllCount);
    return medianTriangleMs / 1000f;
}
```

#### 2. AVProデコード停止ラグ先読み補正シーク ([`SSSCore.cs`](Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSCore.cs#L2178-L2218))

```csharp
// 目標時間との差が5ms以上の場合のみシークを実行
if (Mathf.Abs(YamaPlayer_controller.VideoTime - targetTime) >= _VIDEO_SEEK_THRESHOLD)
{
    // AVProシーク時のデコード停止ラグ（約34ms）を先読みしてシーク先に加算
    float compensatedTarget = targetTime > 0.001f
        ? ClampStageVideoTime(targetTime + _AVPRO_SEEK_LAG_COMPENSATION)
        : targetTime;
        
    YamaPlayer_controller.VideoPlayerHandle.VideoTime = compensatedTarget;
}
```

---

## 6. エディタ拡張・自動化パイプライン (Editor & Build Pipeline)

### 6.1 StageSyncSystem_Editor カスタムインスペクター
Unityエディタ上で `Stage Sync System` を選択すると専用GUIが表示される
- **プレイヤー連携ステータス**: YamaPlayer / iwaSync3 の存在を検知し、「Linked / Non-linked」をグラフィカルに表示
- **パラメータ設定 (Manual Parameters)**: `Core` を直接開くことなく、ルートのInspector上で基本システム遅延（ms）、フォールバック遅延（ms）、同期許容誤差（秒）、ロード待機タイムアウト（秒）、上演中断エラー率閾値（%）の編集およびデフォルトリセットが可能
- **演出一覧ソート**: 演出名昇順・降順、GameObject名順、Hierarchy順のワンクリックソート
- **直感的手動並び替え**: `ReorderableList` と「▲」「▼」ボタンによる並び替え

### 6.2 演出リストの自動検知とReorderableList
シーン内の全 `DirectionObject` を自動スキャンし、既存の順序を保持したまま新規追加分を末尾へ登録。削除された演出は自動除外

### 6.3 プレイリストボタンスイッチの自動生成・結線
演出順序の変更に伴い、Canvas内の `DirectionSW` プレハブを自動的に増減・リネームし、`Button.OnClick` を `SSSUIController.OnButtonClicked` へ自動結線

### 6.4 ビルド前処理 (StageSyncSystemBuildProcess)
`IProcessSceneWithReport`（`callbackOrder = -20`）により、ワールドビルド時およびPlayモード突入時に、プレイヤー参照・Bridge・UI・演出リストの最新状態を自動検証・シリアライズ保存

---

## 7. 導入・設定・運用ガイド (Setup & Operation Guide)

1. **Prefab配置**: `Stage Sync System.prefab` をシーンに配置
2. **エリア調整**: `UI` を操作位置へ、`Performer Space` をステージ範囲へ配置（配置後、MeshRendererを無効化）
3. **動画プレイヤー配置**: YamaPlayer または iwaSync3 を配置し、インスペクターで「Linked」を確認
4. **演出設定**: 演出オブジェクト（`PlayableDirector` 付属）に `DirectionObject` をアタッチし、演出名・動画URLを設定
5. **パラメータ調整（任意）**: `Stage Sync System` のルートを選択し、Inspectorの「パラメータ設定 (Manual Parameters)」から基本システム遅延、タイムアウト秒数、上演中断エラー率閾値（%）などを設定（必要に応じて「デフォルトに戻す」で初期化可能）
6. **本番運用**: 演者がステージに立ち、UIの `Performer Status` が `Standby`（青ランプ点灯）になったことを確認して演出ボタンを押下
