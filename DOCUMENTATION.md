# Stage Sync System (SSS) システム全体仕様書・技術解説書

本書は、VRChat向けステージ演出・動画同期システム **Stage Sync System** の全体仕様、アーキテクチャ設計、通信および遅延同期アルゴリズム、UI・オペレーター機能、エディタ拡張機構、技術的制約、ならびに今後の機能議論・設計検証に向けた技術論点を網羅的かつ詳細にまとめたドキュメントです。

---

## 目次

1. [システムの全体像と前提条件 (System Overview & Prerequisites)](#1-システムの全体像と前提条件-system-overview--prerequisites)
   - [1.1 システムの概要と開発目的](#11-システムの概要と開発目的)
   - [1.2 技術スタック・動作環境・インフラ](#12-技術スタック動作環境インフラ)
   - [1.3 全体アーキテクチャ（エンドツーエンドのデータの流れ）](#13-全体アーキテクチャエンドツーエンドのデータの流れ)
   - [1.4 主要コンポーネント一覧と責務](#14-主要コンポーネント一覧と責務)
2. [議論対象機能の詳細仕様 (Feature Specifications & I/O)](#2-議論対象機能の詳細仕様-feature-specifications--io)
   - [2.1 機能①：動的通信遅延計測・オフセット制御 (Danceモード相当)](#21-機能動的通信遅延計測オフセット制御-danceモード相当)
   - [2.2 機能②：全員動画事前ロード待機・同期発火 (Synchronized Pre-loading)](#22-機能全員動画事前ロード待機同期発火-synchronized-pre-loading)
   - [2.3 機能③：Timeline演出追従同期 (Timeline Drift Sync)](#23-機能timeline演出追従同期-timeline-drift-sync)
   - [2.4 機能④：インワールドUI・ソート・強制上演 (SSSUIController)](#24-機能インワールドuiソート強制上演-sssuicontroller)
   - [2.5 機能⑤：外部操作防止・エリア判定 (UIOperatorChecker)](#25-機能外部操作防止エリア判定-uioperatorchecker)
   - [2.6 機能⑥：演者専用オブジェクト切り替え (Performer Object Toggle)](#26-機能演者専用オブジェクト切り替え-performer-object-toggle)
   - [2.7 機能⑦：デュアル動画プレイヤー連携 (SSSYamaBridge / SSSIwaSyncBridge)](#27-機能デュアル動画プレイヤー連携-sssyamabridge--sssiwasyncbridge)
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
7. [遅延検証・診断システム (Debug & Telemetry Monitor)](#7-遅延検証診断システム-debug--telemetry-monitor)
   - [7.1 SSSDelayDebugMonitor の設計方針](#71-sssdelaydebugmonitor-の設計方針)
   - [7.2 テスト① (内部遅延・動画・Timeline同期検証)](#72-テスト-内部遅延動画timeline同期検証)
   - [7.3 テスト③ (双方向遅延・時計ズレδ検証)](#73-テスト-双方向遅延時計ズレδ検証)
   - [7.4 自動セットアップおよびワンクリック除去機能](#74-自動セットアップおよびワンクリック除去機能)
8. [導入・設定・運用ガイド (Setup & Operation Guide)](#8-導入設定運用ガイド-setup--operation-guide)

---

## 1. システムの全体像と前提条件 (System Overview & Prerequisites)

### 1.1 システムの概要と開発目的
**Stage Sync System (SSS)** は、VRChat上の音楽ライブ、ダンスショー、舞台演劇等のイベントにおいて、**演者（Performer）と観客（Audience）の間で音源、映像（動画プレイヤー）、演出（Timeline / PlayableDirector）を高精度に完全同期**させ、さらに演者と観客間の通信遅延やクライアント描画・再生遅延（タイムラグ）を相殺・補正するための高度なワールドギミックシステムです。

#### 解決する課題
1. **演者と観客間のモーション通信遅延（IK補間ラグ）**:
   VRChatでは演者のアバターの動き（トラッキング・IK）が観客に届くまでに、ネットワーク往復遅延に加えて、観客端末内でアバターを滑らかに補間描画するためのバッファ（約260ms）が発生します。
   そのため、全員が同じタイミングで音源を再生していると、**観客から見て「演者の動きが音や映像よりワンテンポ（約300〜500ms）遅れて見える」** という決定的なズレが発生します。
2. **動画プレイヤーの読み込みラグによる開始ズレ**:
   YouTubeや生配信URLを読み込む際、各端末のスペックや回線速度の違いにより、動画が実際に再生開始可能になるまでに数秒〜数十秒の個体差が生じます。
3. **Timeline演出と動画音源のクロックドリフト**:
   フレームレートの揺らぎやデコーダ負荷によって、UnityのTimelineアニメーションと動画プレイヤーの再生位置が次第にズレていく問題が発生します。

---

### 1.2 技術スタック・動作環境・インフラ

| 区分 | 技術項目 | バージョン / 仕様詳細 |
| :--- | :--- | :--- |
| **開発言語** | C# (UdonSharp) | UdonSharp 1.x (Unity C# サブセット) |
| **ゲームエンジン** | Unity | 2022.3.22f1 |
| **プラットフォーム / FW** | VRChat World SDK | VRCSDK3-WORLD 3.5.0 以降 |
| **実行環境 / インフラ** | クライアント端末 | Windows PC (VR / Desktop) |
| | 通信インフラ | VRChat / Photon Cloud リレーサーバー (Photon Realtime) |
| **関連ライブラリ / 外部ツール** | 動画プレイヤー | **YamaStream** (`Yamadev.YamaStream.Controller`, AVPro Video ベース)<br>**iwaSync3** (`HoshinoLabs.IwaSync3.Udon.VideoCore`, AVPro / Unity Video) |
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
        Area["演者"] -->|コライダー侵入/退出| PC["PerformerChecker (ステージコライダー)"]
        OpArea["操作者"] -->|近接侵入/退出| UOC["UIOperatorChecker (UI保護コライダー)"]
    end

    subgraph CoreLogic["2. コア制御・遅延計算層 (SSSCore)"]
        UI -->|StartHandling(index)| SSS["SSSCore (Owner)"]
        PC -->|IsPerformer = true/false| SSS
        UOC -->|SetOperatorInteractable| UI

        subgraph LatencyCalc["双方向遅延計測 (Bidirectional Triangulation)"]
            SSS <-->|NetworkCallable Ping| Peer["他クライアント SSSCore"]
            SSS -->|時計ズレδ相殺 & 中央値フィルタ| NetDelay["計算された NetworkDelay"]
        end

        subgraph Preloading["全員Ready待機・合否判定"]
            SSS -->|NetOnBeginLoading| AllClients["全参加者クライアント"]
            AllClients -->|NetNotifyPlayerLoadStatus| SSS
            SSS -->|評価: 演者合格 かつ 失敗率<50%| Launch["NetStartStagePlayback 発火"]
        end
    end

    subgraph Bridges["3. 抽象化ブリッジ層"]
        SSS --> YB["SSSYamaBridge"]
        SSS --> IB["SSSIwaSyncBridge"]
    end

    subgraph PlaybackExecution["4. 再生・演出実行層"]
        YB -->|PlayTrack / LocalDelay / Seek| Yama["YamaStream Controller"]
        IB -->|PlayURL / offsetTime / time| Iwa["iwaSync3 VideoCore"]
        SSS -->|毎フレーム追従 (SyncAccuracy=40ms)| Dir["DirectionObject (PlayableDirector)"]
        SSS -->|演者専用表示切り替え| PerfObjs["Performer Objects (返しモニター等)"]
    end
```

---

### 1.4 主要コンポーネント一覧と責務

| コンポーネント | 配置オブジェクト | 責務・役割 |
| :--- | :--- | :--- |
| **[`SSSCore`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSCore.cs)** | `Core` | システム中核。双方向遅延計測、オフセット計算、全員ロード待機判定、動画・Timeline制御、上演ライフサイクル統括。 |
| **[`SSSUIController`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSUIController.cs)** | `UI` | インワールド操作盤。演出リスト管理、リアルタイムソート、Force Launch設定、ステータス表示（人数・色・ランプ）。 |
| **[`DirectionController`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/DirectionController.cs)** | `Stage Sync System (Root)` | システム最上位コンポーネント。エディタ拡張の基点となり、各コンポーネント間の参照を自動統合。 |
| **[`DirectionObject`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/DirectionObject.cs)** | 各演出オブジェクト | シーン上の各演出（`PlayableDirector`）にアタッチ。演出名、動画URL、演者への再生可否フラグを保持。 |
| **[`DirectionSwitch`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/DirectionSwitch.cs)** | 任意トリガー/小道具 | 物理ボタンやプロップ等から特定演出をワンクリックで起動するためのトリガースクリプト。 |
| **[`PerformerChecker`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/PerformerChecker.cs)** | `Performer Space` | ステージ領域のコライダー。進入プレイヤーをローカルで演者と判定し、`SSSCore` へ通知。 |
| **[`UIOperatorChecker`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/UIOperatorChecker.cs)** | `Control Protecter` | 操作盤周囲のコライダー。枠外からの遠隔Raycast誤操作をコライダー無効化で完全に遮断。 |
| **[`SSSYamaBridge`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSYamaBridge.cs)** | `Core/Bridge` | YamaStreamの `Listener` を継承し、動画準備完了・開始・エラー・停止イベントを `SSSCore` へ中継。 |
| **[`SSSIwaSyncBridge`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSIwaSyncBridge.cs)** | `Core/Bridge` | iwaSync3の `VideoCoreEventListener` を継承し、各動画イベントを `SSSCore` へ中継。 |
| **[`SSSDelayDebugMonitor`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSDelayDebugMonitor.cs)** | `[Debug] SSS Delay Monitor` | 検証用デバッグモニター。遅延計測値、クロックスキュー $\delta$、シーク誤差、Timeline誤差をリアルタイム画面描画・ログ出力。 |

---

## 2. 議論対象機能の詳細仕様 (Feature Specifications & I/O)

### 2.1 機能①：動的通信遅延計測・オフセット制御 (Danceモード相当)

* **機能の目的（ユースケース）**:
  * 演者と観客間の通信往復遅延とVRChatアバターIK補間遅延（約260ms）をミリ秒単位で相殺し、観客から見て演者のダンスの動きと音源・映像が完全に一致した状態で上演を行う。
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
    - YamaStream: `YamaPlayer_controller.LocalDelay = targetDelay`
    - iwaSync3: `Iwasync_core.offsetTime = targetOffset`
* **想定している処理フロー**:
  1. インスタンス内の選出ノード（Beacon：演者最優先、上位3名）が `SendLatencyPing` を全クライアントへ送信。
  2. 受信側がサーバー時刻差 $rawDelay = ServerTime - sendServerTime$ を計算。直近3サンプル最小値フィルタでスパイクを除去。
  3. 逆方向の計測値との平均を取り、端末間のクロックスキュー $\delta$ を相殺:
     $$d_{undirected}(A, B) = \frac{d(A \to B) + d(B \to A)}{2}$$
  4. **3人以上のインスタンス**: 全三角形候補から三角測量値を算出し、中央値（Median）を抽出:
     $$\text{Candidate}(X, Y) = \max\left(0, \frac{d(Local, X) + d(Local, Y) - d(X, Y)}{2}\right)$$
  5. **2人インスタンス**: 2者間双方向遅延の中央値の半分を採用:
     $$\text{NetworkDelay} = \frac{\text{Median}(d(Local, Other))}{2 \times 1000}$$
  6. オフセット秒数（`GetOffsetSeconds()`）を算出し、動画プレイヤーに適用:
     - 演者: $+\left(\frac{BaseSystemDelay}{1000} + NetworkDelay\right)$
     - 観客: $-NetworkDelay$
* **異常系・エッジケースの扱い**:
  * **1人インスタンス時**: 通信相手が存在せず三角測量不可のため、自動的に **Fallbackモード**（演者:+370ms / 観客:0ms）へフォールバック。UIの `Delay Status` にも `Fallback` と明示。
  * **一時的な通信切断・巨大スパイク**: 直近3サンプル最小値フィルタ（`_directedPairPrev1`, `_directedPairPrev2`）により、URL解決時等の正方向スパイク（数千ms）を自動除去。
  * **プレイヤー途中退室**: `OnPlayerLeft` にて該当プレイヤーの履歴辞書・リストを即座に破棄し、残存メンバーで遅延を再集計。
  * **上演中のステージ端移動**: 上演中は演者フラグ（`_isPerformer`）が強制ロックされ、一時的なコライダー退出によるオフセット急変・シーク乱れを防止。上演終了時に実際のエリア在室状態へ復旧。
* **既存モジュールとの依存関係**:
  * 参照: `Networking.LocalPlayer`, `Networking.GetServerTimeInMilliseconds()`, `VRCPlayerApi`
  * 変更: `YamaPlayer_controller.LocalDelay`, `Iwasync_core.offsetTime`, `SSSUIController._delayStatusText`

---

### 2.2 機能②：全員動画事前ロード待機・同期発火 (Synchronized Pre-loading)

* **機能の目的（ユースケース）**:
  * 各自の通信速度やPCスペックによって動画の読み込み完了タイミングが異なるため、全員が0秒地点でバッファリングを完了するまで待機し、頭出しが揃った瞬間に全員同時に上演を開始する。
* **入力（Input）**:
  * **トリガー**: UIの演出ボタン押下（`SSSUIController.OnButtonClicked`）、または物理スイッチ押下（`DirectionSwitch.Interact`）
  * **データ形式**: `objectIndex` (`int`: 演出インデックス), `sessionId` (`int`: 単調増加セッションID)
* **出力（Output）**:
  * 全員の動画音量ミュート（`Vol = 0`）＆ 0秒一時停止（`PauseLocalVideoPlayerAtZero`）
  * UIの進捗表示（`Loaded Player: X/Y`、演者ランプ更新）
  * 合格時: `NetStartStagePlayback`（一斉再生開始）
  * 不合格時: `NetAbortStagePlayback`（上演中止・クリーンアップ）
* **想定している処理フロー**:
  1. 起動者がOwnerを取得し、`_playbackSessionId++` をインクリメント。UI設定をロック。
  2. `NetOnBeginLoading` を全員へブロードキャスト。各自動画プレイヤーをミュートし、URLをロード。
  3. 各クライアントで動画がロード完了（`OnVideoReady` / `OnVideoStart`）した時点で0秒地点で一時停止し、`NetNotifyPlayerLoadStatus(Ready)` をOwnerへ送信。
  4. Ownerが `EvaluatePlayersLoadState()` で進捗を集計。
  5. **中止判定**:
     - 演者が1人でもロード失敗（または未完了） $\to$ **上演中止（Abort）**
     - 観客のロード失敗率 $\ge 50\%$ $\to$ **上演中止（Abort）**
  6. **開始判定**: 全員完了、またはタイムアウト（15秒）時点で中止条件に非該当の場合、現在のサーバー時刻を刻印して `NetStartStagePlayback` を発火。
  7. 全員が音量を復帰し、オフセットを加算した目標時間へ高精度シーク（AVPro先読み+34ms）して再生開始。
* **異常系・エッジケースの扱い**:
  * **VRChat 5秒URLレートリミット**: 前回のURL発行から5.25秒未満の連打時は、内部キュー（`_isWaitingForCooldownLoad`）に格納し、クールダウン経過後に最新セッションのURLのみを単一実行。
  * **途中参加者 (Late Joiner)**: 上演中に入室したプレイヤーは `_stageStartServerTime` から経過時間をローカル計算して途中合流シーク。既存プレイヤーへは再同期（DoSync）を発火させないよう保護。
* **既存モジュールとの依存関係**:
  * 参照/変更: `SSSYamaBridge`, `SSSIwaSyncBridge`, `YamaPlayer_controller`, `Iwasync_core`, `SSSUIController`

---

### 2.3 機能③：Timeline演出追従同期 (Timeline Drift Sync)

* **機能の目的（ユースケース）**:
  * 長時間の演出において、UnityのTimelineアニメーションと動画プレイヤーの音源・映像の再生時間が徐々にズレていく（クロックドリフト）現象を防止する。
* **入力（Input）**:
  * **トリガー**: 上演中の毎フレーム `Update()`
  * **データ形式**: `VideoTime` (`float`), `playableDirector.time` (`double`)
* **出力（Output）**:
  * `currentDir.playableDirector.time = videoTime`（時間強制上書き）
* **想定している処理フロー**:
  1. 毎フレーム、現在アクティブな演出の `playableDirector.time` と動画の `GetCurrentVideoTime()` の差分を計算。
  2. 差分が `SyncAccuracy`（デフォルト: 0.04秒 / 40ms）以上離れた瞬間に、Timelineの時間を動画時間に同期。
  3. 動画が一時停止・シーク・ループした際も、即座にTimeline側へ同等の時間同期イベントを伝達。
* **異常系・エッジケースの扱い**:
  * **演者への演出非表示（`PlayForPerformer = false`）**: 演者の視界塞ぎや描画負荷を低減するため、演者端末では該当演出のGameObject自体を非アクティブ化し、Timelineも再生しない。
* **既存モジュールとの依存関係**:
  * 参照/変更: `DirectionObject.playableDirector`, `SSSCore.GetCurrentVideoTime()`

---

### 2.4 機能④：インワールドUI・ソート・強制上演 (SSSUIController)

* **機能の目的（ユースケース）**:
  * ワールド内でオペレーターが演出プレイリストの順序を整理し、ステータスを確認しながら安全に上演を行い、トラブル時には強制上演を実行する。
* **入力（Input）**:
  * **トリガー**: UIのToggle変更、ボタンクリック
  * **データ形式**: ソートモードEnum (`DirectionSortMode`), Force Launchフラグ (`bool`)
* **出力（Output）**:
  * プレイリストボタン群の並び順（SiblingIndex）更新
  * `SSSCore` への設定変数同期
* **想定している処理フロー**:
  1. **演出ソート**: Hierarchy順 / 演出名昇順・降順 / GameObject名昇順・降順 / Manual順 を選択。アロケーションフリーのインサーションソートでリストを即座に再配置。
  2. **Force Launch (強制上演)**:
     - `IgnoreThresholdToggle`: 観客の失敗率が50%以上でも強制上演。
     - `IgnorePerformerErrorToggle`: 演者のロードが失敗していても強制上演。
     - 有効期間フォーム: 「次の1回のみ（上演開始時に自動解除）」または「常時」。
  3. **上演中ロック**: 動画ロード中および上演中は全トグルをグレーアウトし、外部からの変更を拒否。
* **異常系・エッジケースの扱い**:
  * **非オペレーターの操作試行**: 後述の `UIOperatorChecker` により、コライダー枠外からのRaycastは物理的に無効化。
* **既存モジュールとの依存関係**:
  * 参照/変更: `SSSCore`, `UnityEngine.UI.Toggle`, `Button`, `Text`

---

### 2.5 機能⑤：外部操作防止・エリア判定 (UIOperatorChecker)

* **機能の目的（ユースケース）**:
  * 遠く離れた客席から観客がUIへ遠隔Raycastをクリックして誤操作を引き起こすトラブルを防止する。
* **入力（Input）**:
  * **トリガー**: 操作エリアコライダー（BoxCollider）に対する `OnPlayerTriggerEnter` / `Exit`, `OnPlayerRespawn`
  * **データ形式**: `VRCPlayerApi.isLocal`
* **出力（Output）**:
  * Canvas内の全Colliderの `enabled`（true/false）
  * 全UIコンポーネントの `interactable`（true/false）
* **想定している処理フロー**:
  1. 操作盤の前に専用のBoxCollider（Trigger）を配置。
  2. ローカルプレイヤーが枠内に入ると、Canvas内の全Colliderを有効化し、ボタン操作を許可。
  3. 枠外に出ると、全Colliderを無効化（`enabled = false`）し、遠方からのRaycastヒット自体を遮断。
* **異常系・エッジケースの扱い**:
  * **リスポーン時**: リスポーン直後に位置が枠外へ移動するため、`OnPlayerRespawn` から1フレーム遅延で状態を再適用。
  * **VRCUiShape初期化競合**: VRChat起動時にVRCUiShapeがコライダーを強制有効化する仕様に対抗し、`Start()` から1フレーム遅延でも再適用を実行。
* **既存モジュールとの依存関係**:
  * 参照/変更: `UnityEngine.Canvas`, `Collider[]`, `SSSUIController`

---

### 2.6 機能⑥：演者専用オブジェクト切り替え (Performer Object Toggle)

* **機能の目的（ユースケース）**:
  * ステージ上のプロンプター（返しモニター）や立ち位置目印など、演者にだけ見せたいオブジェクト、または客席からのみ見せたいオブジェクトを自動切り替えする。
* **入力（Input）**:
  * **トリガー**: `SSSCore.IsPerformer` の変更
  * **データ形式**: `GameObject[] _performerEnableObjects`, `_performerDisableObjects`
* **出力（Output）**:
  * 各オブジェクトの `SetActive(true / false)`
* **想定している処理フロー**:
  1. 演者エリアに入ると、`_performerEnableObjects` がアクティブ化され、`_performerDisableObjects` が非アクティブ化される。
  2. 演者エリアを出ると、逆の状態に復帰する。
* **異常系・エッジケースの扱い**:
  * **システムオブジェクト誤登録の自動除外（Sanitize機能）**:
    ユーザーが誤って `SSSCore`、`UI`、動画プレイヤー本体、演出オブジェクトを演者専用オブジェクトに設定した場合、システムごと非アクティブ化されて破綻するのを防ぐため、エディタおよびランタイムで保護対象オブジェクトを自動検知して除外。
* **既存モジュールとの依存関係**:
  * 参照: `SSSCore.IsPerformer`

---

### 2.7 機能⑦：デュアル動画プレイヤー連携 (SSSYamaBridge / SSSIwaSyncBridge)

* **機能の目的（ユースケース）**:
  * シーン内に配置されている動画プレイヤーが YamaStream であっても iwaSync3 であっても、ワールド作者がスクリプトを書き換えることなく自動的に検知・連携して動作させる。
* **入力（Input）**:
  * **トリガー**: 各動画プレイヤーのライフサイクルイベント（Ready, Start, Play, Pause, Stop, Error, SetTime）
* **出力（Output）**:
  * `SSSCore` の対応ハンドラ呼び出し（`OnVideoReady()`, `OnVideoStart()` 等）
* **既存モジュールとの依存関係**:
  * 参照: `Yamadev.YamaStream.Controller`, `HoshinoLabs.IwaSync3.Udon.VideoCore`

---

## 3. 技術的な制約・非機能要件 (Technical Constraints & Non-Functional Requirements)

### 3.1 パフォーマンス要件 (Performance Requirements)
1. **VRフレームレート維持 (60FPS / 90FPS)**:
   - メインスレッド負荷を最小限に抑え、VR酔いやトラッキング飛びを防止する。
   - 演出ソート処理はメモリアロケーションを伴わない**非再帰インサーションソート**を採用し、GC（Garbage Collection）スパイクを完全抑止。
2. **動画シーク頻度・デコーダ負荷制限**:
   - 目標時間との差が `_VIDEO_SEEK_THRESHOLD` (5ms) 未満の場合はシークをスキップ。
   - 毎フレームシークを連打するとAVPro Videoがフリーズ・競合するため、単一シーク＋1秒後フォースシンクの2段階制御を採用。
3. **低定常負荷（Event-Driven設計）**:
   - 上演中以外の待機時は不要なUpdateループ処理を停止し、ワールドの軽量化を徹底。

---

### 3.2 環境固有の制約 (Platform Specific Constraints)
1. **Udon VM のシングルスレッド制約**:
   - マルチスレッド並行処理や非同期Task（`async/await`）は利用不可。全計算はメインスレッドで安全に実行可能なアルゴリズムにする必要がある。
2. **NetworkCallable / UdonNetworkCalling の制限**:
   - RPCで送受信できる引数の型は基本プリミティブ型（`int`, `float`, `string`, `bool` 等）に限定される。
   - 過剰なRPC呼び出しはVRChatのネットワーク帯域制限・レートリミットに抵触するため、Beaconノード選出（最大3名）によりPing通信量を厳格に抑制。
3. **VRChat 動画ロードAPIの5秒レートリミット**:
   - 動画プレイヤーへのURL投入APIにはプラットフォーム全体で5秒のクールダウンが存在する（短時間での連続ロードは `VideoError.RateLimited` で弾かれる）。
   - 本システムでは5.25秒のクールダウンキュー管理によりこれを完全回避。

---

### 3.3 今後の拡張予定 (Future Roadmap)
- **演者用操作端末(UI)**:
  - スタッフ無しでも公演ができるように演者がステージ上で演出再生、設定変更が可能なUIを計画
- **Runtime上演出リスト入れ替え機能**:
  - Editor上のみならずRuntime上でも演出リストの順番を手動で入れ替えることのできるUI機能計画

---

## 4. 現在のステータスと議論の論点 (Status, Design Options & Discussion Goals)

### 4.1 現在の開発フェーズ
- [ ] アイデア・要件定義
- [ ] 設計中
- [ ] プロトタイプ実装中
- [x] **ボトルネック・バグ解消 / 実環境検証中** (Gitブランチ: `Improvement-of-Delay-Calculation-System`)

---

### 4.2 過去の設計案・アプローチと改善案の比較

| 評価項目 | 現在採用しているアプローチ | 過去のアプローチ | 採用理由・トレードオフ |
| :--- | :--- | :--- | :--- |
| **遅延計測方式** | **双方向三角測量法 (Bidirectional Triangulation)**<br>全ペア双方向Pingの中央値から各端末の片道遅延を幾何学的に特定 | **定数時間遅延**<br>経験則に基づく定数時間(0.6s)のオフセットを設ける | (ここに詳細を記述) |

---

### 4.3 今回最も解決・検証したい論点（ゴール）

- [ ] **Update関数問題**:
  - Update関数内の処理や上演中の処理は再生中のズレ修正にある程度必要だが、インスタンス人数が多い場合に演者のIK処理が一瞬硬直する問題があると報告されている。そのため、上演中の処理を極力減らす実装を想定している。
- [ ] **高精度遅延補正**:
  - あらゆるケースを想定し、可能な限り正確な遅延補正を適用し、遅延の無い同期システムを想定。
- [ ] **異常系・エッジケースの堅牢性検証**:
  - v1.0では未知の解決可能バグが存在したため、v2.0ではデバッグを徹底し既知のバグとし、解決に努める。

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
| `NetOnBeginLoading` | `int sessionId, int objectIndex` | All | 全員へ動画ロード開始を通知。動画プレイヤーをミュートしURLロード開始。 |
| `NetNotifyPlayerLoadStatus`| `int sessionId, int playerId, bool isSuccess, bool isPerformer, int sendServerTime` | All | 各自の動画ロード成否を通知。送信サーバー時刻はピギーバック遅延計測に活用。 |
| `NetStartStagePlayback` | `int sessionId, int objectIndex, int startServerTime, bool startedInFallback` | All | Ownerから全員へ上演開始を一斉発火。 |
| `NetAbortStagePlayback` | `int sessionId` | All | エラーまたはタイムアウトにより上演を中止し、全員クリーンアップ。 |
| `NetRequestBeaconPings` | なし | All | 全員へBeaconノードからのPing送信を要求。 |
| `NetReceiveLatencyPing` | `int senderId, int sendServerTime` | All | Pingパケットを受信し、片道伝搬時間を計測・履歴記録。 |
| `NetReceiveLatencyRecord`| `int senderId, int receiverId, int delayMs` | All | 計測されたペア間遅延を他クライアントへ中継共有。 |
| `NetUpdatePerformerState`| `int playerId, bool isPerformer` | All | プレイヤーの演者ステータス変更を全体共有。 |

---

### 5.3 内部データ構造 (DataDictionary / DataList) キー規則

```csharp
// ペア間双方向キー: 小さいID_大きいID (例: "1_5")
string undirectedKey = GetPairKey(senderId, receiverId);

// 指向性（片方向）キー: 送信者ID_受信者ID (例: "5_1")
string dirKey = $"{senderId}_{receiverId}";
```

- `_pairDelays` (`DataDictionary`): 時計ズレ $\delta$ 相殺済みの双方向遅延ミリ秒（Key: `undirectedKey`, Value: `int`）
- `_directedPairDelays` (`DataDictionary`): 直近3サンプル最小値フィルタ適用済みの片道遅延（Key: `dirKey`, Value: `int`）
- `_directedPairPrev1` / `Prev2` (`DataDictionary`): 最小値フィルタ用履歴バッファ
- `_performerPlayerIds` (`DataList`): 現在ステージ上にいる演者の `playerId` リスト
- `_readyPlayerIds` (`DataList`): 動画ロードに成功した `playerId` リスト
- `_failedPlayerIds` (`DataList`): 動画ロードに失敗した `playerId` リスト

---

### 5.4 主要アルゴリズムのコード抜粋

#### 1. 双方向三角測量・中央値フィルタによる `NetworkDelay` 算出 ([`SSSCore.cs`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSCore.cs#L1621-L1697))

```csharp
// 3人以上での双方向三角測量（クロックスキューδ相殺済みペア優先 & 中央値採用）
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

#### 2. AVProデコード停止ラグ先読み補正シーク ([`SSSCore.cs`](file:///D:/Development/Unity%20Terminal/VirtualReality/2022_3.22f1/Develop/Stage-Sync-System/Assets/Appletea's%20Item/Stage%20Sync%20System/v2.0/Script/SSSCore.cs#L2178-L2218))

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
Unityエディタ上で `Stage Sync System` を選択すると、専用GUIが表示されます。
- **プレイヤー連携ステータス**: YamaPlayer / iwaSync3 の存在を検知し、「Linked / Non-linked」をグラフィカルに表示。
- **演出一覧ソート**: 演出名昇順・降順、GameObject名順、Hierarchy順のワンクリックソート。
- **直感的手動並び替え**: `ReorderableList` と「▲」「▼」ボタンによる並び替え。

### 6.2 演出リストの自動検知とReorderableList
シーン内の全 `DirectionObject` を自動スキャンし、既存の順序を保持したまま新規追加分を末尾へ登録。削除された演出は自動除外。

### 6.3 プレイリストボタンスイッチの自動生成・結線
演出順序の変更に伴い、Canvas内の `DirectionSW` プレハブを自動的に増減・リネームし、`Button.OnClick` を `SSSUIController.OnButtonClicked` へ自動結線。

### 6.4 ビルド前処理 (StageSyncSystemBuildProcess)
`IProcessSceneWithReport`（`callbackOrder = -20`）により、ワールドビルド時およびPlayモード突入時に、プレイヤー参照・Bridge・UI・演出リストの最新状態を自動検証・シリアライズ保存。

---

## 7. 遅延検証・診断システム (Debug & Telemetry Monitor)

### 7.1 SSSDelayDebugMonitor の設計方針
アルゴリズムの挙動と精度を実証するための診断モニター。片方向参照のみで動作し、検証後は本番ワールドから完全に除去可能。

### 7.2 テスト① (内部遅延・動画・Timeline同期検証)
- `CalcNetDelay`（計算遅延）, `BaseSysDelay`（基準遅延）, `AppliedOffset`（適用オフセット）
- `VideoShift`（基準時間に対するシフト量）, `SeekErr`（目標時間と動画再生時間の差）, `TimelineDiff`（動画とTimelineの差）

### 7.3 テスト③ (双方向遅延・時計ズレδ検証)
- 各ペアの片道遅延 $d(X \to Y)$, $d(Y \to X)$
- 双方向平均遅延 $\text{BiAvg} = \frac{d(X \to Y) + d(Y \to X)}{2}$
- クライアント間時計ズレ $\text{Skew}(\delta) = \frac{d(X \to Y) - d(Y \to X)}{2}$

### 7.4 自動セットアップおよびワンクリック除去機能
- エディタ起動時に自動セットアップ。
- メニュー `Tools > Stage Sync System > Remove Delay Debug Monitor (Test 1 & 3)` からワンクリックで一括削除可能。

---

## 8. 導入・設定・運用ガイド (Setup & Operation Guide)

1. **Prefab配置**: `Stage Sync System.prefab` をシーンに配置。
2. **エリア調整**: `UI` を操作位置へ、`Performer Space` をステージ範囲へ配置（配置後、MeshRendererを無効化）。
3. **動画プレイヤー配置**: YamaStream または iwaSync3 を配置し、インスペクターで「Linked」を確認。
4. **演出設定**: 演出オブジェクト（`PlayableDirector` 付属）に `DirectionObject` をアタッチし、演出名・動画URLを設定。
5. **本番運用**: 演者がステージに立ち、UIの `Performer Status` が `Standby`（青ランプ点灯）になったことを確認して演出ボタンを押下。
