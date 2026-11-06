using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;

namespace AppleteaSystems.StageSyncSystem
{
    /// <summary>
    /// 遅延時間補正検証用デバッグモニター（テスト①・テスト③用）の自動セットアップ・ワンクリック除去エディタ拡張。
    /// 検証完了後はメニュー「Tools > Stage Sync System > Remove Delay Debug Monitor (Test 1 & 3)」を実行後、
    /// SSSDelayDebugMonitor.cs と本ファイルを削除するだけで完全に除去可能。
    /// </summary>
    [InitializeOnLoad]
    public static class SSSDelayDebugMonitor_Editor
    {
        private const string MonitorObjectName = "[Debug] SSS Delay Monitor";
        private const string AutoSetupDoneSessionKey = "SSS_DelayDebugMonitor_AutoSetupDone";
        private const string ManuallyRemovedPrefKey = "SSS_DelayDebugMonitor_ManuallyRemoved";

        static SSSDelayDebugMonitor_Editor()
        {
            EditorApplication.delayCall += TryAutoSetupInActiveScene;
        }

        private static void TryAutoSetupInActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoSetupInActiveScene;
                return;
            }

            if (SessionState.GetBool(AutoSetupDoneSessionKey, false))
            {
                return;
            }

            if (EditorPrefs.GetBool(ManuallyRemovedPrefKey, false))
            {
                return;
            }

            SSSCore core = Object.FindObjectOfType<SSSCore>(true);
            if (core == null)
            {
                return;
            }

            if (!EnsureProgramAssetReady())
            {
                // UdonSharpの初回コンパイル完了後に再度セットアップを実行する
                EditorApplication.delayCall += TryAutoSetupInActiveScene;
                return;
            }

            SessionState.SetBool(AutoSetupDoneSessionKey, true);

            GameObject existing = GameObject.Find(MonitorObjectName);
            if (existing == null || existing.GetComponent<SSSDelayDebugMonitor>() == null)
            {
                SetupDelayDebugMonitorInternal(false);
            }
        }

        private static bool EnsureProgramAssetReady()
        {
            UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(SSSDelayDebugMonitor));
            if (programAsset == null)
            {
                MonoScript targetScript = FindMonitorMonoScript();
                if (targetScript == null) return false;

                string scriptPath = AssetDatabase.GetAssetPath(targetScript);
                if (string.IsNullOrEmpty(scriptPath)) return false;

                string assetPath = Path.ChangeExtension(scriptPath, ".asset");
                programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath);
                if (programAsset == null)
                {
                    programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                    programAsset.sourceCsScript = targetScript;
                    AssetDatabase.CreateAsset(programAsset, assetPath);
                    AssetDatabase.SaveAssets();
                    UdonSharpProgramAsset.CompileAllCsPrograms(true);
                    return false;
                }
            }

            if (programAsset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion ||
                programAsset.CompiledVersion < UdonSharpProgramVersion.CurrentVersion)
            {
                UdonSharpProgramAsset.CompileAllCsPrograms(true);
                return false;
            }

            return true;
        }

        private static MonoScript FindMonitorMonoScript()
        {
            string[] guids = AssetDatabase.FindAssets("SSSDelayDebugMonitor t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() == typeof(SSSDelayDebugMonitor))
                {
                    return ms;
                }
            }
            return null;
        }

        [MenuItem("Tools/Stage Sync System/Setup Delay Debug Monitor (Test 1 & 3)")]
        public static void SetupDelayDebugMonitorMenu()
        {
            EditorPrefs.SetBool(ManuallyRemovedPrefKey, false);
            if (!EnsureProgramAssetReady())
            {
                Debug.Log("[SSSDelayDebugMonitor] UdonSharpProgramAssetを生成・コンパイル中です。完了後に自動配置されます。");
                SessionState.SetBool(AutoSetupDoneSessionKey, false);
                EditorApplication.delayCall += TryAutoSetupInActiveScene;
                return;
            }
            SetupDelayDebugMonitorInternal(true);
        }

        [MenuItem("Tools/Stage Sync System/Remove Delay Debug Monitor (Test 1 & 3)")]
        public static void RemoveDelayDebugMonitorMenu()
        {
            EditorPrefs.SetBool(ManuallyRemovedPrefKey, true);

            GameObject existing = GameObject.Find(MonitorObjectName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"[SSSDelayDebugMonitor] Sceneから '{MonitorObjectName}' を削除し、シーンを保存しました。");
            }
            else
            {
                Debug.Log($"[SSSDelayDebugMonitor] Scene上に '{MonitorObjectName}' は存在しません。");
            }
        }

        private static void SetupDelayDebugMonitorInternal(bool selectObject)
        {
            SSSCore core = Object.FindObjectOfType<SSSCore>(true);
            if (core == null)
            {
                Debug.LogWarning("[SSSDelayDebugMonitor] シーン内に SSSCore が見つかりません。");
                return;
            }

            GameObject rootObj = GameObject.Find(MonitorObjectName);
            if (rootObj == null)
            {
                rootObj = new GameObject(MonitorObjectName);
                Undo.RegisterCreatedObjectUndo(rootObj, "Create SSS Delay Debug Monitor");
            }

            // 中央のTiming Testスフィア群(x=-0.25..0.25, y=0.67..1.17, z=0)のすぐ右隣・同一画角内に配置
            rootObj.transform.position = new Vector3(1.15f, 1.15f, 0.2f);
            rootObj.transform.rotation = Quaternion.identity;
            rootObj.transform.localScale = new Vector3(0.0012f, 0.0012f, 0.0012f);

            Canvas canvas = rootObj.GetComponent<Canvas>();
            if (canvas == null) canvas = rootObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            CanvasScaler scaler = rootObj.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = rootObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            RectTransform rootRect = rootObj.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.sizeDelta = new Vector2(920f, 360f);
            }

            // 背景パネル
            Transform bgTrans = rootObj.transform.Find("Background");
            GameObject bgObj = bgTrans != null ? bgTrans.gameObject : new GameObject("Background");
            bgObj.transform.SetParent(rootObj.transform, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            if (bgRect == null) bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.GetComponent<Image>();
            if (bgImage == null) bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.06f, 0.10f, 0.82f);

            // テキスト表示
            Transform textTrans = rootObj.transform.Find("TelemetryText");
            GameObject textObj = textTrans != null ? textTrans.gameObject : new GameObject("TelemetryText");
            textObj.transform.SetParent(rootObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            if (textRect == null) textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 14f);
            textRect.offsetMax = new Vector2(-16f, -14f);

            Text uiText = textObj.GetComponent<Text>();
            if (uiText == null) uiText = textObj.AddComponent<Text>();
            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null) builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (builtinFont != null) uiText.font = builtinFont;
            uiText.fontSize = 18;
            uiText.lineSpacing = 1.12f;
            uiText.alignment = TextAnchor.UpperLeft;
            uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            uiText.supportRichText = true;
            uiText.color = Color.white;
            uiText.text = "<b>[SSS Delay Telemetry - Test 1 & Test 3]</b>\nWaiting for runtime initialization...";

            SSSDelayDebugMonitor monitor = rootObj.GetComponent<SSSDelayDebugMonitor>();
            if (monitor == null && UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(SSSDelayDebugMonitor)) != null)
            {
                monitor = rootObj.AddUdonSharpComponent<SSSDelayDebugMonitor>();
                VRC.Udon.UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(monitor);
                if (backing != null)
                {
                    backing.SyncMethod = Networking.SyncType.None;
                }
            }

            if (monitor != null)
            {
                SerializedObject so = new SerializedObject(monitor);
                so.Update();
                SerializedProperty propCore = so.FindProperty("_sssCore");
                SerializedProperty propText = so.FindProperty("_debugText");
                if (propCore != null) propCore.objectReferenceValue = core;
                if (propText != null) propText.objectReferenceValue = uiText;
                so.ApplyModifiedProperties();
                UdonSharpEditorUtility.CopyProxyToUdon(monitor);
                EditorUtility.SetDirty(monitor);
            }

            EditorUtility.SetDirty(rootObj);
            EditorSceneManager.MarkSceneDirty(rootObj.scene);
            EditorSceneManager.SaveOpenScenes();

            if (selectObject)
            {
                Selection.activeGameObject = rootObj;
            }

            Debug.Log($"[SSSDelayDebugMonitor] '{MonitorObjectName}' をセットアップし、SSSCoreへ接続してシーンを保存しました。");
        }
    }
}
