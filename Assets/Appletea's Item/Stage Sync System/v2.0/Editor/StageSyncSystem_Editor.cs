using System;
using System.Collections.Generic;
using System.IO;
using HoshinoLabs.IwaSync3.Udon;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using Yamadev.YamaStream;

namespace AppleteaSystems.StageSyncSystem
{
    // Scriptに対するCustomEditorの設定
    [CustomEditor(typeof(DirectionController))]
    public class StageSyncSystem_Editor : Editor
    {
        // SerializedObjectでScriptのInstanceを管理
        SerializedObject SSS_Core;
        SerializedObject directionController;
        SerializedObject uiControllerSO;
        SerializedObject performerUIControllerSO;

        Texture2D Thumbnail;
        SSSCore _SSSCore;
        DirectionController _directionController;
        SSSUIController _uiController;
        SSSPerformerUIController _performerUIController;

        Controller _YamaPlayer_Controller = null;
        VideoCore _Iwasync_Core = null;
        List<DirectionObject> _directionList = new List<DirectionObject>();
        ReorderableList _reorderableList;

        const string _directionSWRootPath = "UI/Canvas/Panel/Playlist Menu/Scroll View/Scroll View/Viewport/Content";
        const string _performerSWRootPath = "Performer Portable UI/Canvas/Panel/StandbyPanel/Scroll View/Viewport/Content";
        const string _directionSW_BaseName = "DirectionSW";
        const string _bridgeObjectName = "Bridge";
        const string _uiObjectName = "UI";
        const string _performerRootObjectName = "Performer Portable UI";

        bool isFoldout = true;
        int _pendingMoveFrom = -1;
        int _pendingMoveTo = -1;

        private void OnEnable()
        {
            if (Application.isPlaying) return;

            _directionController = (DirectionController)target;
            if (_directionController == null) return;

            directionController = new SerializedObject(_directionController);
            Thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("e53f95351d21c2041a16114955a6534b"));

            // Coreの取得
            _SSSCore = GetSSSCore(_directionController);
            if (_SSSCore == null) return;

            SSS_Core = new SerializedObject(_SSSCore);

            // 各動画プレイヤーの取得・設定
            _YamaPlayer_Controller = GetYamaPlayerController();
            _Iwasync_Core = GetIwasyncCore();

            SetVideoPlayers(_YamaPlayer_Controller, _Iwasync_Core, SSS_Core);
            SetSSSCore(directionController, _SSSCore);

            // シーン上のオブジェクトである場合、Bridge・UIController・PerformerPortableUIの自動構成とUIスイッチの生成を実行
            if (!EditorUtility.IsPersistent(_directionController.gameObject))
            {
                SetupBridges(_SSSCore, _YamaPlayer_Controller, _Iwasync_Core, SSS_Core);
                SetupUIController(_directionController, directionController, _SSSCore, SSS_Core, out _uiController, out uiControllerSO);
                SetupPerformerPortableUI(_directionController, directionController, _SSSCore, SSS_Core, _uiController, uiControllerSO, out _performerUIController, out performerUIControllerSO);

                GetSyncedDirectionList(SSS_Core, out _directionList);
                ApplyDirectionOrder(_directionController, directionController, _SSSCore, SSS_Core, _uiController, uiControllerSO, _directionList, _performerUIController, performerUIControllerSO);
                SetupReorderableList();
            }
            else
            {
                _uiController = GetUIController(_directionController);
                if (_uiController != null)
                {
                    uiControllerSO = new SerializedObject(_uiController);
                }
                _performerUIController = GetPerformerUIController(_directionController);
                if (_performerUIController != null)
                {
                    performerUIControllerSO = new SerializedObject(_performerUIController);
                }
            }
        }

        private void SetupReorderableList()
        {
            _reorderableList = new ReorderableList(_directionList, typeof(DirectionObject), true, false, false, false)
            {
                headerHeight = 2f,
                footerHeight = 0f,
                elementHeight = EditorGUIUtility.singleLineHeight + 6f
            };

            _reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index < 0 || index >= _directionList.Count) return;
                DirectionObject item = _directionList[index];
                if (item == null) return;

                rect.y += 3f;
                rect.height = EditorGUIUtility.singleLineHeight;

                const float buttonWidth = 24f;
                const float spacing = 4f;
                const float objFieldWidth = 115f;

                float rightButtonsTotal = (buttonWidth * 2f) + spacing;
                float availableWidth = rect.width - rightButtonsTotal - objFieldWidth - (spacing * 2f);

                Rect labelRect = new Rect(rect.x, rect.y, Mathf.Max(60f, availableWidth), rect.height);
                Rect objRect = new Rect(labelRect.xMax + spacing, rect.y, objFieldWidth, rect.height);
                Rect upBtnRect = new Rect(objRect.xMax + spacing, rect.y, buttonWidth, rect.height);
                Rect downBtnRect = new Rect(upBtnRect.xMax + spacing, rect.y, buttonWidth, rect.height);

                EditorGUI.LabelField(labelRect, $"{index + 1}. {item.directionName}", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.ObjectField(objRect, item.gameObject, typeof(GameObject), true);
                }

                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUI.Button(upBtnRect, "▲"))
                    {
                        _pendingMoveFrom = index;
                        _pendingMoveTo = index - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(index >= _directionList.Count - 1))
                {
                    if (GUI.Button(downBtnRect, "▼"))
                    {
                        _pendingMoveFrom = index;
                        _pendingMoveTo = index + 1;
                    }
                }
            };

            _reorderableList.onReorderCallback = (ReorderableList list) =>
            {
                ApplyDirectionOrder(_directionController, directionController, _SSSCore, SSS_Core, _uiController, uiControllerSO, _directionList, _performerUIController, performerUIControllerSO);
            };
        }

        internal static Controller GetYamaPlayerController()
        {
            Controller[] controllers = FindObjectsByType<Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers != null && controllers.Length > 0) return controllers[0];
            return null;
        }

        internal static VideoCore GetIwasyncCore()
        {
            VideoCore[] coreTemp = FindObjectsByType<VideoCore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (coreTemp != null && coreTemp.Length > 0) return coreTemp[0];
            return null;
        }

        internal static SSSCore GetSSSCore(DirectionController dirController = null)
        {
            if (dirController != null)
            {
                SSSCore childCore = dirController.GetComponentInChildren<SSSCore>(true);
                if (childCore != null) return childCore;
            }
            SSSCore[] coreTemp = FindObjectsByType<SSSCore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (coreTemp != null && coreTemp.Length > 0) return coreTemp[0];
            return null;
        }

        internal static SSSUIController GetUIController(DirectionController dirController = null)
        {
            if (dirController != null)
            {
                SSSUIController childUI = dirController.GetComponentInChildren<SSSUIController>(true);
                if (childUI != null) return childUI;
            }
            SSSUIController[] uiTemp = FindObjectsByType<SSSUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (uiTemp != null && uiTemp.Length > 0) return uiTemp[0];
            return null;
        }

        internal static SSSPerformerUIController GetPerformerUIController(DirectionController dirController = null)
        {
            if (dirController != null)
            {
                SSSPerformerUIController childPerfUI = dirController.GetComponentInChildren<SSSPerformerUIController>(true);
                if (childPerfUI != null) return childPerfUI;
            }
            SSSPerformerUIController[] perfTemp = FindObjectsByType<SSSPerformerUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (perfTemp != null && perfTemp.Length > 0) return perfTemp[0];
            return null;
        }

        /// <summary>
        /// SSSCore._directions に保存されている既存の並び順を保持しつつ、
        /// シーン上に新規追加された DirectionObject を末尾へ追加し、削除されたものを除外したリストを取得する。
        /// </summary>
        internal static void GetSyncedDirectionList(SerializedObject sssCoreSO, out List<DirectionObject> directionList)
        {
            directionList = new List<DirectionObject>();

            DirectionObject[] foundObjects = FindObjectsByType<DirectionObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<DirectionObject> foundSet = new HashSet<DirectionObject>(foundObjects);
            HashSet<DirectionObject> addedSet = new HashSet<DirectionObject>();

            // 1. 既存の保存順序を優先して引き継ぐ
            if (sssCoreSO != null)
            {
                sssCoreSO.Update();
                SerializedProperty dirProp = sssCoreSO.FindProperty("_directions");
                if (dirProp != null)
                {
                    for (int i = 0; i < dirProp.arraySize; i++)
                    {
                        DirectionObject existing = dirProp.GetArrayElementAtIndex(i).objectReferenceValue as DirectionObject;
                        if (existing != null && foundSet.Contains(existing) && !addedSet.Contains(existing))
                        {
                            directionList.Add(existing);
                            addedSet.Add(existing);
                        }
                    }
                }
            }

            // 2. 新規追加された DirectionObject を収集（初回や複数同時追加時はHierarchy順で末尾に追加）
            List<DirectionObject> newObjects = new List<DirectionObject>();
            for (int i = 0; i < foundObjects.Length; i++)
            {
                DirectionObject candidate = foundObjects[i];
                if (candidate != null && !addedSet.Contains(candidate))
                {
                    newObjects.Add(candidate);
                    addedSet.Add(candidate);
                }
            }

            if (newObjects.Count > 0)
            {
                newObjects.Sort(CompareByHierarchyOrder);
                directionList.AddRange(newObjects);
            }
        }

        internal static int CompareByDirectionNameAsc(DirectionObject x, DirectionObject y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;
            int cmp = string.Compare(x.directionName, y.directionName, StringComparison.Ordinal);
            return cmp != 0 ? cmp : CompareByHierarchyOrder(x, y);
        }

        internal static int CompareByDirectionNameDesc(DirectionObject x, DirectionObject y)
        {
            return -CompareByDirectionNameAsc(x, y);
        }

        internal static int CompareByGameObjectNameAsc(DirectionObject x, DirectionObject y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;
            int cmp = string.Compare(x.gameObject.name, y.gameObject.name, StringComparison.Ordinal);
            return cmp != 0 ? cmp : CompareByHierarchyOrder(x, y);
        }

        internal static int CompareByGameObjectNameDesc(DirectionObject x, DirectionObject y)
        {
            return -CompareByGameObjectNameAsc(x, y);
        }

        internal static int CompareByHierarchyOrder(DirectionObject x, DirectionObject y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;
            return CompareTransformHierarchy(x.transform, y.transform);
        }

        private static int CompareTransformHierarchy(Transform a, Transform b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            List<Transform> pathA = GetTransformPath(a);
            List<Transform> pathB = GetTransformPath(b);

            int minDepth = Mathf.Min(pathA.Count, pathB.Count);
            for (int i = 0; i < minDepth; i++)
            {
                if (pathA[i] != pathB[i])
                {
                    return pathA[i].GetSiblingIndex().CompareTo(pathB[i].GetSiblingIndex());
                }
            }

            return pathA.Count.CompareTo(pathB.Count);
        }

        private static List<Transform> GetTransformPath(Transform t)
        {
            List<Transform> path = new List<Transform>();
            Transform current = t;
            while (current != null)
            {
                path.Add(current);
                current = current.parent;
            }
            path.Reverse();
            return path;
        }

        internal static void ApplyDirectionOrder(
            DirectionController dirController,
            SerializedObject dirControllerSO,
            SSSCore sssCore,
            SerializedObject sssCoreSO,
            SSSUIController uiController,
            SerializedObject uiControllerSO,
            List<DirectionObject> directionList,
            SSSPerformerUIController performerUIController = null,
            SerializedObject performerUIControllerSO = null)
        {
            if (dirController == null || sssCore == null || directionList == null) return;

            if (performerUIController == null)
            {
                performerUIController = GetPerformerUIController(dirController);
            }
            if (performerUIController != null && performerUIControllerSO == null)
            {
                performerUIControllerSO = new SerializedObject(performerUIController);
            }

            List<GameObject> directionSWList = new List<GameObject>();
            Transform directorSWRoot = dirController.transform.Find(_directionSWRootPath);
            if (directorSWRoot != null)
            {
                if (uiController != null)
                {
                    VRC.Udon.UdonBehaviour uiUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(uiController);
                    Transform baseBtnTransform = directorSWRoot.Find($"{_directionSW_BaseName}/Button");
                    if (uiUdon != null && baseBtnTransform != null)
                    {
                        Button baseBtn = baseBtnTransform.GetComponent<Button>();
                        if (baseBtn != null)
                        {
                            ConfigureDirectionSWTemplateButton(baseBtn, uiUdon, "OnButtonClicked");
                        }
                    }
                }

                DeleteDirectionSW(directorSWRoot, _directionSW_BaseName);
                directionSWList = GenerateDirectionSW(directorSWRoot, _directionSW_BaseName, directionList);
            }

            SettingDirection_UIController(uiControllerSO, directionSWList);

            // ランタイム手動並び替えドロワー (ManualSortDrawer) の構築とアイテム生成・結線
            SetupRuntimeManualSorter(dirController, sssCore, uiController, uiControllerSO, directionList);

            // 演者用操作端末 (Performer Portable UI) 側の演出予約ボタン群も同期生成・結線
            List<GameObject> performerSWList = new List<GameObject>();
            Transform performerSWRoot = dirController.transform.Find(_performerSWRootPath);
            if (performerSWRoot != null)
            {
                if (performerUIController != null)
                {
                    VRC.Udon.UdonBehaviour perfUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(performerUIController);
                    Transform perfBaseBtnTransform = performerSWRoot.Find($"{_directionSW_BaseName}/Button");
                    if (perfUdon != null && perfBaseBtnTransform != null)
                    {
                        Button perfBaseBtn = perfBaseBtnTransform.GetComponent<Button>();
                        if (perfBaseBtn != null)
                        {
                            ConfigureDirectionSWTemplateButton(perfBaseBtn, perfUdon, "OnButtonClicked");
                        }
                    }
                }

                DeleteDirectionSW(performerSWRoot, _directionSW_BaseName);
                performerSWList = GenerateDirectionSW(performerSWRoot, _directionSW_BaseName, directionList);
            }

            SettingDirection_UIController(performerUIControllerSO, performerSWList);
            SettingDirection_Core(sssCoreSO, directionList);

            UdonSharpEditorUtility.CopyProxyToUdon(dirController);
            UdonSharpEditorUtility.CopyProxyToUdon(sssCore);
            EditorUtility.SetDirty(dirController);
            EditorUtility.SetDirty(sssCore);

            if (uiController != null)
            {
                UdonSharpEditorUtility.CopyProxyToUdon(uiController);
                EditorUtility.SetDirty(uiController);
            }

            if (performerUIController != null)
            {
                UdonSharpEditorUtility.CopyProxyToUdon(performerUIController);
                EditorUtility.SetDirty(performerUIController);
            }
        }

        internal static void SetVideoPlayers(Controller yamaController, VideoCore iwasyncCore, SerializedObject sssCoreSO)
        {
            sssCoreSO.Update();

            SerializedProperty yamaProp = sssCoreSO.FindProperty("YamaPlayer_controller");
            if (yamaProp != null) yamaProp.objectReferenceValue = yamaController;

            SerializedProperty iwaProp = sssCoreSO.FindProperty("Iwasync_core");
            if (iwaProp != null) iwaProp.objectReferenceValue = iwasyncCore;

            // 片方のプレイヤーのみシーンに存在する場合は自動的にそのプレイヤー種別を選択
            SerializedProperty typeProp = sssCoreSO.FindProperty("_videoPlayerType");
            if (typeProp != null)
            {
                if (yamaController != null && iwasyncCore == null)
                {
                    typeProp.enumValueIndex = (int)VideoPlayerType.YamaPlayer;
                }
                else if (iwasyncCore != null && yamaController == null)
                {
                    typeProp.enumValueIndex = (int)VideoPlayerType.IwaSync;
                }
            }

            sssCoreSO.ApplyModifiedProperties();
        }

        internal static void SetupBridges(SSSCore sssCore, Controller yamaController, VideoCore iwasyncCore, SerializedObject sssCoreSO)
        {
            if (sssCore == null || EditorUtility.IsPersistent(sssCore.gameObject)) return;

            Transform bridgeTransform = sssCore.transform.Find(_bridgeObjectName);
            GameObject bridgeGO;
            if (bridgeTransform == null)
            {
                bridgeGO = new GameObject(_bridgeObjectName);
                bridgeGO.transform.SetParent(sssCore.transform, false);
            }
            else
            {
                bridgeGO = bridgeTransform.gameObject;
            }

            // YamaPlayer Bridge
            SSSYamaBridge yamaBridge = sssCore.GetComponentInChildren<SSSYamaBridge>(true);
            if (yamaBridge == null && UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(SSSYamaBridge)) != null)
            {
                yamaBridge = bridgeGO.AddUdonSharpComponent<SSSYamaBridge>();
                UdonSharpEditorUtility.GetBackingUdonBehaviour(yamaBridge).SyncMethod = Networking.SyncType.None;
            }
            if (yamaBridge != null)
            {
                SerializedObject yamaBridgeSO = new SerializedObject(yamaBridge);
                yamaBridgeSO.Update();
                yamaBridgeSO.FindProperty("_sssCore").objectReferenceValue = sssCore;
                yamaBridgeSO.FindProperty("_controller").objectReferenceValue = yamaController;
                yamaBridgeSO.ApplyModifiedProperties();
                UdonSharpEditorUtility.CopyProxyToUdon(yamaBridge);
            }

            // IwaSync Bridge
            SSSIwaSyncBridge iwaBridge = sssCore.GetComponentInChildren<SSSIwaSyncBridge>(true);
            if (iwaBridge == null && UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(SSSIwaSyncBridge)) != null)
            {
                iwaBridge = bridgeGO.AddUdonSharpComponent<SSSIwaSyncBridge>();
                UdonSharpEditorUtility.GetBackingUdonBehaviour(iwaBridge).SyncMethod = Networking.SyncType.None;
            }
            if (iwaBridge != null)
            {
                SerializedObject iwaBridgeSO = new SerializedObject(iwaBridge);
                iwaBridgeSO.Update();
                iwaBridgeSO.FindProperty("_sssCore").objectReferenceValue = sssCore;
                iwaBridgeSO.FindProperty("_iwasyncCore").objectReferenceValue = iwasyncCore;
                iwaBridgeSO.ApplyModifiedProperties();
                UdonSharpEditorUtility.CopyProxyToUdon(iwaBridge);
            }

            sssCoreSO.Update();
            SerializedProperty yamaBridgeProp = sssCoreSO.FindProperty("_yamaBridge");
            if (yamaBridgeProp != null) yamaBridgeProp.objectReferenceValue = yamaBridge;
            SerializedProperty iwaBridgeProp = sssCoreSO.FindProperty("_iwaSyncBridge");
            if (iwaBridgeProp != null) iwaBridgeProp.objectReferenceValue = iwaBridge;
            sssCoreSO.ApplyModifiedProperties();
        }

        internal static void SetupUIController(
            DirectionController dirController,
            SerializedObject dirControllerSO,
            SSSCore sssCore,
            SerializedObject sssCoreSO,
            out SSSUIController uiController,
            out SerializedObject uiSO)
        {
            uiController = null;
            uiSO = null;

            if (dirController == null || EditorUtility.IsPersistent(dirController.gameObject)) return;

            uiController = dirController.GetComponentInChildren<SSSUIController>(true);
            if (uiController == null && UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(SSSUIController)) != null)
            {
                Transform uiTransform = dirController.transform.Find(_uiObjectName);
                GameObject uiGO = uiTransform != null ? uiTransform.gameObject : dirController.gameObject;
                uiController = uiGO.AddUdonSharpComponent<SSSUIController>();
                UdonSharpEditorUtility.GetBackingUdonBehaviour(uiController).SyncMethod = Networking.SyncType.Manual;
            }

            if (uiController == null) return;

            uiSO = new SerializedObject(uiController);
            uiSO.Update();

            SerializedProperty coreProp = uiSO.FindProperty("_sssCore");
            if (coreProp != null) coreProp.objectReferenceValue = sssCore;

            Transform root = dirController.transform;

            // Status UI
            AssignIfNull(uiSO, "_loadedPlayerCountText", FindComponent<Text>(root, "UI/Canvas/Panel/Status/Loaded Player/Loaded Player num"));
            AssignIfNull(uiSO, "_performerStatusText", FindComponent<Text>(root, "UI/Canvas/Panel/Status/Performer Status/Performer Status"));
            AssignIfNull(uiSO, "_performerLoadedObject", FindGameObject(root, "UI/Canvas/Panel/Status/Performer Status/Ready"));
            AssignIfNull(uiSO, "_performerErrorObject", FindGameObject(root, "UI/Canvas/Panel/Status/Performer Status/Warning"));
            AssignIfNull(uiSO, "_delayStatusText", FindComponent<Text>(root, "UI/Canvas/Panel/Status/Delay Status/Delay Status"));

            // Direction Sort UI
            Transform sortHorizon = root.Find("UI/Canvas/Panel/Settings Menu/Direction Sort/Horizon");
            Toggle sortHierarchyToggle = GetChildComponent<Toggle>(sortHorizon, 0);
            Toggle sortByNameToggle = GetChildComponent<Toggle>(sortHorizon, 1);
            Toggle sortManualToggle = GetChildComponent<Toggle>(sortHorizon, 2);
            AssignIfNull(uiSO, "_sortHierarchyToggle", sortHierarchyToggle);
            AssignIfNull(uiSO, "_sortByNameToggle", sortByNameToggle);
            AssignIfNull(uiSO, "_sortManualToggle", sortManualToggle);

            // Error Detection (Force Launch) UI
            Transform errorVertical = root.Find("UI/Canvas/Panel/Settings Menu/Error Detection/Vertical");
            Toggle ignoreThresholdToggle = GetChildComponent<Toggle>(errorVertical, 0);
            Toggle ignorePerformerToggle = GetChildComponent<Toggle>(errorVertical, 1);
            Transform durationFormTransform = errorVertical != null && errorVertical.childCount > 2 ? errorVertical.GetChild(2) : null;
            Transform durationHorizon = durationFormTransform != null ? durationFormTransform.Find("Horizon") : null;
            Toggle durationOnceToggle = GetChildComponent<Toggle>(durationHorizon, 0);
            Toggle durationAlwaysToggle = GetChildComponent<Toggle>(durationHorizon, 1);

            AssignIfNull(uiSO, "_ignoreThresholdToggle", ignoreThresholdToggle);
            AssignIfNull(uiSO, "_ignorePerformerErrorToggle", ignorePerformerToggle);
            AssignIfNull(uiSO, "_forceLaunchDurationForm", durationFormTransform != null ? durationFormTransform.gameObject : null);
            AssignIfNull(uiSO, "_durationOnceToggle", durationOnceToggle);
            AssignIfNull(uiSO, "_durationAlwaysToggle", durationAlwaysToggle);

            // Delay Calculation UI
            Transform delayHorizon = root.Find("UI/Canvas/Panel/Settings Menu/Delay Calculation/Horizon");
            Toggle delayDefaultToggle = GetChildComponent<Toggle>(delayHorizon, 0);
            Toggle delayFallbackToggle = GetChildComponent<Toggle>(delayHorizon, 1);
            AssignIfNull(uiSO, "_delayDefaultToggle", delayDefaultToggle);
            AssignIfNull(uiSO, "_delayFallbackToggle", delayFallbackToggle);

            uiSO.ApplyModifiedProperties();

            // Backing UdonBehaviour への Toggle イベント自動結線
            VRC.Udon.UdonBehaviour uiUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(uiController);
            if (uiUdon != null)
            {
                ConfigureToggleForUdon(uiSO.FindProperty("_sortHierarchyToggle")?.objectReferenceValue as Toggle, uiUdon, "OnSortToggleChanged");
                ConfigureToggleForUdon(uiSO.FindProperty("_sortByNameToggle")?.objectReferenceValue as Toggle, uiUdon, "OnSortToggleChanged");
                ConfigureToggleForUdon(uiSO.FindProperty("_sortManualToggle")?.objectReferenceValue as Toggle, uiUdon, "OnSortToggleChanged");
                ConfigureToggleForUdon(uiSO.FindProperty("_sortByNameDescToggle")?.objectReferenceValue as Toggle, uiUdon, "OnSortToggleChanged");
                ConfigureToggleForUdon(uiSO.FindProperty("_sortByGameObjectNameAscToggle")?.objectReferenceValue as Toggle, uiUdon, "OnSortToggleChanged");
                ConfigureToggleForUdon(uiSO.FindProperty("_sortByGameObjectNameDescToggle")?.objectReferenceValue as Toggle, uiUdon, "OnSortToggleChanged");

                // Force Launchの2つのスイッチは独立トグルのためToggleGroupを解除して結線
                ConfigureToggleForUdon(uiSO.FindProperty("_ignoreThresholdToggle")?.objectReferenceValue as Toggle, uiUdon, "OnForceLaunchToggleChanged", clearGroup: true);
                ConfigureToggleForUdon(uiSO.FindProperty("_ignorePerformerErrorToggle")?.objectReferenceValue as Toggle, uiUdon, "OnForceLaunchToggleChanged", clearGroup: true);

                // 有効期間ラジオボタンは初期状態（Force Launch両方OFF）では Toggle.interactable のみ操作無効化（フォーム自体は常に表示）
                bool anyForceLaunchOn = (ignoreThresholdToggle != null && ignoreThresholdToggle.isOn) || (ignorePerformerToggle != null && ignorePerformerToggle.isOn);
                ConfigureToggleForUdon(uiSO.FindProperty("_durationOnceToggle")?.objectReferenceValue as Toggle, uiUdon, "OnForceLaunchDurationToggleChanged", clearGroup: false, interactable: anyForceLaunchOn);
                ConfigureToggleForUdon(uiSO.FindProperty("_durationAlwaysToggle")?.objectReferenceValue as Toggle, uiUdon, "OnForceLaunchDurationToggleChanged", clearGroup: false, interactable: anyForceLaunchOn);
                GameObject durationFormGO = uiSO.FindProperty("_forceLaunchDurationForm")?.objectReferenceValue as GameObject;
                if (durationFormGO != null && !durationFormGO.activeSelf)
                {
                    durationFormGO.SetActive(true);
                    EditorUtility.SetDirty(durationFormGO);
                }

                ConfigureToggleForUdon(uiSO.FindProperty("_delayDefaultToggle")?.objectReferenceValue as Toggle, uiUdon, "OnDelayCalculationToggleChanged");
                ConfigureToggleForUdon(uiSO.FindProperty("_delayFallbackToggle")?.objectReferenceValue as Toggle, uiUdon, "OnDelayCalculationToggleChanged");
            }

            // DirectionController と SSSCore へ UIController 参照を保存
            if (dirControllerSO != null)
            {
                dirControllerSO.Update();
                SerializedProperty dcUiProp = dirControllerSO.FindProperty("_uiController");
                if (dcUiProp != null) dcUiProp.objectReferenceValue = uiController;
                dirControllerSO.ApplyModifiedProperties();
            }

            if (sssCoreSO != null)
            {
                sssCoreSO.Update();
                SerializedProperty coreUiProp = sssCoreSO.FindProperty("_uiController");
                if (coreUiProp != null) coreUiProp.objectReferenceValue = uiController;
                sssCoreSO.ApplyModifiedProperties();
            }

            UdonSharpEditorUtility.CopyProxyToUdon(uiController);
        }

        private static void AssignIfNull(SerializedObject so, string propName, UnityEngine.Object targetObj)
        {
            if (so == null || targetObj == null) return;
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = targetObj;
            }
        }

        private static T FindComponent<T>(Transform root, string relativePath) where T : Component
        {
            if (root == null) return null;
            Transform t = root.Find(relativePath);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static GameObject FindGameObject(Transform root, string relativePath)
        {
            if (root == null) return null;
            Transform t = root.Find(relativePath);
            return t != null ? t.gameObject : null;
        }

        private static T GetChildComponent<T>(Transform parent, int childIndex) where T : Component
        {
            if (parent == null || childIndex < 0 || childIndex >= parent.childCount) return null;
            return parent.GetChild(childIndex).GetComponent<T>();
        }

        private static void ConfigureToggleForUdon(Toggle toggle, VRC.Udon.UdonBehaviour targetUdon, string eventName, bool clearGroup = false, bool? interactable = null)
        {
            if (toggle == null || targetUdon == null) return;

            SerializedObject toggleSO = new SerializedObject(toggle);
            toggleSO.Update();

            SerializedProperty groupProp = toggleSO.FindProperty("m_Group");
            if (clearGroup)
            {
                if (groupProp != null && groupProp.objectReferenceValue != null)
                {
                    groupProp.objectReferenceValue = null;
                }
            }
            else if (groupProp != null)
            {
                ToggleGroup tg = groupProp.objectReferenceValue as ToggleGroup;
                if (tg != null)
                {
                    SerializedObject tgSO = new SerializedObject(tg);
                    tgSO.Update();
                    SerializedProperty allowOffProp = tgSO.FindProperty("m_AllowSwitchOff");
                    if (allowOffProp != null && allowOffProp.boolValue)
                    {
                        allowOffProp.boolValue = false;
                        tgSO.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            if (interactable.HasValue)
            {
                SerializedProperty interactableProp = toggleSO.FindProperty("m_Interactable");
                if (interactableProp != null) interactableProp.boolValue = interactable.Value;
            }

            SerializedProperty targetGraphicProp = toggleSO.FindProperty("m_TargetGraphic");
            if (targetGraphicProp != null && targetGraphicProp.objectReferenceValue == null)
            {
                Transform slideBg = toggle.transform.Find("Toggle");
                if (slideBg != null)
                {
                    Graphic g = slideBg.GetComponent<Graphic>();
                    if (g != null) targetGraphicProp.objectReferenceValue = g;
                }
            }

            EnsurePersistentSendCustomEvent(toggleSO, "onValueChanged.m_PersistentCalls.m_Calls", targetUdon, eventName);
            toggleSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsurePersistentSendCustomEvent(SerializedObject so, string callsPath, VRC.Udon.UdonBehaviour targetUdon, string eventName)
        {
            if (so == null || targetUdon == null || string.IsNullOrEmpty(eventName)) return;
            SerializedProperty callsProp = so.FindProperty(callsPath);
            if (callsProp == null || !callsProp.isArray) return;

            for (int i = 0; i < callsProp.arraySize; i++)
            {
                SerializedProperty call = callsProp.GetArrayElementAtIndex(i);
                SerializedProperty methodProp = call.FindPropertyRelative("m_MethodName");
                SerializedProperty strArgProp = call.FindPropertyRelative("m_Arguments.m_StringArgument");
                if (methodProp != null && methodProp.stringValue == "SendCustomEvent" &&
                    strArgProp != null && strArgProp.stringValue == eventName)
                {
                    SerializedProperty targetProp = call.FindPropertyRelative("m_Target");
                    if (targetProp != null && targetProp.objectReferenceValue != targetUdon)
                    {
                        targetProp.objectReferenceValue = targetUdon;
                    }
                    return;
                }
            }

            int newIdx = callsProp.arraySize;
            callsProp.InsertArrayElementAtIndex(newIdx);
            SerializedProperty newCall = callsProp.GetArrayElementAtIndex(newIdx);
            newCall.FindPropertyRelative("m_Target").objectReferenceValue = targetUdon;
            newCall.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(VRC.Udon.UdonBehaviour).AssemblyQualifiedName;
            newCall.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
            newCall.FindPropertyRelative("m_Mode").intValue = 5; // PersistentListenerMode.String
            newCall.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue = null;
            newCall.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue = "UnityEngine.Object, UnityEngine";
            newCall.FindPropertyRelative("m_Arguments.m_IntArgument").intValue = 0;
            newCall.FindPropertyRelative("m_Arguments.m_FloatArgument").floatValue = 0f;
            newCall.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = eventName;
            newCall.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue = false;
            newCall.FindPropertyRelative("m_CallState").intValue = 2; // UnityEventCallState.RuntimeOnly
        }

        private static void ConfigureButtonForUdon(Button button, VRC.Udon.UdonBehaviour targetUdon, string eventName, bool? interactable = null)
        {
            if (button == null || targetUdon == null) return;

            SerializedObject btnSO = new SerializedObject(button);
            btnSO.Update();

            if (interactable.HasValue)
            {
                SerializedProperty interactableProp = btnSO.FindProperty("m_Interactable");
                if (interactableProp != null) interactableProp.boolValue = interactable.Value;
            }

            EnsurePersistentSendCustomEvent(btnSO, "m_OnClick.m_PersistentCalls.m_Calls", targetUdon, eventName);
            btnSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDirectionSWTemplateButton(Button baseBtn, VRC.Udon.UdonBehaviour targetUdon, string eventName)
        {
            if (baseBtn == null || targetUdon == null) return;

            SerializedObject baseBtnSO = new SerializedObject(baseBtn);
            baseBtnSO.Update();

            SerializedProperty callsProp = baseBtnSO.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (callsProp != null && callsProp.isArray)
            {
                callsProp.ClearArray();

                // Call 0: baseBtn.enabled = false (FindSender識別用)
                callsProp.InsertArrayElementAtIndex(0);
                SerializedProperty call0 = callsProp.GetArrayElementAtIndex(0);
                call0.FindPropertyRelative("m_Target").objectReferenceValue = baseBtn;
                call0.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(Behaviour).AssemblyQualifiedName;
                call0.FindPropertyRelative("m_MethodName").stringValue = "set_enabled";
                call0.FindPropertyRelative("m_Mode").intValue = 6; // PersistentListenerMode.Bool
                call0.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue = null;
                call0.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue = "UnityEngine.Object, UnityEngine";
                call0.FindPropertyRelative("m_Arguments.m_IntArgument").intValue = 0;
                call0.FindPropertyRelative("m_Arguments.m_FloatArgument").floatValue = 0f;
                call0.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = "";
                call0.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue = false;
                call0.FindPropertyRelative("m_CallState").intValue = 2;

                // Call 1: targetUdon.SendCustomEvent(eventName)
                callsProp.InsertArrayElementAtIndex(1);
                SerializedProperty call1 = callsProp.GetArrayElementAtIndex(1);
                call1.FindPropertyRelative("m_Target").objectReferenceValue = targetUdon;
                call1.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(VRC.Udon.UdonBehaviour).AssemblyQualifiedName;
                call1.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
                call1.FindPropertyRelative("m_Mode").intValue = 5; // PersistentListenerMode.String
                call1.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue = null;
                call1.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue = "UnityEngine.Object, UnityEngine";
                call1.FindPropertyRelative("m_Arguments.m_IntArgument").intValue = 0;
                call1.FindPropertyRelative("m_Arguments.m_FloatArgument").floatValue = 0f;
                call1.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = eventName;
                call1.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue = false;
                call1.FindPropertyRelative("m_CallState").intValue = 2;

                // Call 2: baseBtn.enabled = true
                callsProp.InsertArrayElementAtIndex(2);
                SerializedProperty call2 = callsProp.GetArrayElementAtIndex(2);
                call2.FindPropertyRelative("m_Target").objectReferenceValue = baseBtn;
                call2.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(Behaviour).AssemblyQualifiedName;
                call2.FindPropertyRelative("m_MethodName").stringValue = "set_enabled";
                call2.FindPropertyRelative("m_Mode").intValue = 6; // PersistentListenerMode.Bool
                call2.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue = null;
                call2.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue = "UnityEngine.Object, UnityEngine";
                call2.FindPropertyRelative("m_Arguments.m_IntArgument").intValue = 0;
                call2.FindPropertyRelative("m_Arguments.m_FloatArgument").floatValue = 0f;
                call2.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = "";
                call2.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue = true;
                call2.FindPropertyRelative("m_CallState").intValue = 2;
            }

            baseBtnSO.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static bool EnsurePerformerProgramAssetsReady()
        {
            bool allReady = true;
            allReady &= EnsureSingleProgramAssetReady(typeof(SSSPerformerUIController), "SSSPerformerUIController");
            allReady &= EnsureSingleProgramAssetReady(typeof(SSSPerformerTrigger), "SSSPerformerTrigger");
            allReady &= EnsureSingleProgramAssetReady(typeof(SSSPerformerProximityChecker), "SSSPerformerProximityChecker");
            allReady &= EnsureSingleProgramAssetReady(typeof(SSSManualSortItem), "SSSManualSortItem");
            allReady &= EnsureSingleProgramAssetReady(typeof(SSSRuntimeManualSorter), "SSSRuntimeManualSorter");
            return allReady;
        }

        private static void ConfigureSliderForUdon(Slider slider, VRC.Udon.UdonBehaviour targetUdon, string eventName)
        {
            if (slider == null || targetUdon == null) return;
            SerializedObject sliderSO = new SerializedObject(slider);
            sliderSO.Update();
            EnsurePersistentSendCustomEvent(sliderSO, "m_OnValueChanged.m_PersistentCalls.m_Calls", targetUdon, eventName);
            sliderSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureScrollbarForUdon(Scrollbar scrollbar, VRC.Udon.UdonBehaviour targetUdon, string eventName)
        {
            if (scrollbar == null || targetUdon == null) return;
            SerializedObject sbSO = new SerializedObject(scrollbar);
            sbSO.Update();
            EnsurePersistentSendCustomEvent(sbSO, "m_OnValueChanged.m_PersistentCalls.m_Calls", targetUdon, eventName);
            sbSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEventTriggerForUdon(EventTrigger trigger, EventTriggerType triggerType, VRC.Udon.UdonBehaviour targetUdon, string eventName)
        {
            if (trigger == null || targetUdon == null || string.IsNullOrEmpty(eventName)) return;

            SerializedObject trigSO = new SerializedObject(trigger);
            trigSO.Update();

            SerializedProperty delegatesProp = trigSO.FindProperty("m_Delegates");
            if (delegatesProp == null || !delegatesProp.isArray) return;

            int entryIdx = -1;
            for (int i = 0; i < delegatesProp.arraySize; i++)
            {
                SerializedProperty entry = delegatesProp.GetArrayElementAtIndex(i);
                SerializedProperty idProp = entry.FindPropertyRelative("eventID");
                if (idProp != null && idProp.intValue == (int)triggerType)
                {
                    entryIdx = i;
                    break;
                }
            }

            if (entryIdx < 0)
            {
                entryIdx = delegatesProp.arraySize;
                delegatesProp.InsertArrayElementAtIndex(entryIdx);
                SerializedProperty newEntry = delegatesProp.GetArrayElementAtIndex(entryIdx);
                newEntry.FindPropertyRelative("eventID").intValue = (int)triggerType;
                SerializedProperty calls = newEntry.FindPropertyRelative("callback.m_PersistentCalls.m_Calls");
                if (calls != null && calls.isArray)
                {
                    calls.ClearArray();
                }
            }

            EnsurePersistentSendCustomEvent(
                trigSO,
                $"m_Delegates.Array.data[{entryIdx}].callback.m_PersistentCalls.m_Calls",
                targetUdon,
                eventName
            );
            trigSO.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetupRuntimeManualSorter(
            DirectionController dirController,
            SSSCore sssCore,
            SSSUIController uiController,
            SerializedObject uiControllerSO,
            List<DirectionObject> directionList)
        {
            if (dirController == null || sssCore == null || uiController == null || directionList == null) return;
            if (EditorUtility.IsPersistent(dirController.gameObject)) return;
            if (!EnsureSingleProgramAssetReady(typeof(SSSRuntimeManualSorter), "SSSRuntimeManualSorter") ||
                !EnsureSingleProgramAssetReady(typeof(SSSManualSortItem), "SSSManualSortItem"))
            {
                return;
            }

            Transform directionSortRoot = dirController.transform.Find("UI/Canvas/Panel/Settings Menu/Direction Sort");
            if (directionSortRoot == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            Sprite roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("9af44d402feae2245837a6dbcfd10ab2"));
            Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("66dedc5f5aa13774a96eeb074a0de05c"));

            // 1. ManualSortDrawer ルート (Direction Sort直下・LayoutElement(ignoreLayout=true)で既存レイアウトに影響させず下部空き領域に配置)
            Transform drawerTrans = directionSortRoot.Find("ManualSortDrawer");
            GameObject drawerGO = drawerTrans != null ? drawerTrans.gameObject : CreateUIElement("ManualSortDrawer", directionSortRoot);
            RectTransform drawerRect = drawerGO.GetComponent<RectTransform>();
            drawerRect.anchorMin = new Vector2(0f, 0f);
            drawerRect.anchorMax = new Vector2(1f, 0f);
            drawerRect.pivot = new Vector2(0.5f, 1f);
            drawerRect.anchoredPosition = new Vector2(0f, -24f);
            drawerRect.sizeDelta = new Vector2(0f, 960f);

            LayoutElement drawerLE = drawerGO.GetComponent<LayoutElement>();
            if (drawerLE == null) drawerLE = drawerGO.AddComponent<LayoutElement>();
            drawerLE.ignoreLayout = true;

            SSSRuntimeManualSorter sorter = drawerGO.GetComponent<SSSRuntimeManualSorter>();
            if (sorter == null)
            {
                sorter = drawerGO.AddUdonSharpComponent<SSSRuntimeManualSorter>();
                VRC.Udon.UdonBehaviour sorterBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(sorter);
                if (sorterBacking != null) sorterBacking.SyncMethod = Networking.SyncType.None;
            }
            sorter.enabled = false;

            // 2. DrawerHeader (シンプルなタイトルラベルのみ)
            Transform headerTrans = drawerGO.transform.Find("DrawerHeader");
            GameObject headerGO = headerTrans != null ? headerTrans.gameObject : CreateUIElement("DrawerHeader", drawerGO.transform);
            RectTransform headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 36f);

            Transform headerTitleTrans = headerGO.transform.Find("TitleText");
            Text headerTitleText = headerTitleTrans != null
                ? headerTitleTrans.GetComponent<Text>()
                : CreateTextElement("TitleText", headerGO.transform, font, "Manual Sort Order (Drag or ▲/▼ to Reorder)", 23, TextAnchor.MiddleLeft, new Color(0.3019608f, 0.3019608f, 0.3019608f, 1f));
            headerTitleText.raycastTarget = false;
            SetStretchRect(headerTitleText.GetComponent<RectTransform>(), 4f, 0f, -4f, 0f);

            // 3. SortScrollView
            Transform scrollTrans = drawerGO.transform.Find("SortScrollView");
            GameObject scrollGO = scrollTrans != null ? scrollTrans.gameObject : CreateUIElement("SortScrollView", drawerGO.transform);
            RectTransform scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.offsetMin = new Vector2(0f, 0f);
            scrollRect.offsetMax = new Vector2(0f, -44f);

            Image scrollBg = scrollGO.GetComponent<Image>();
            if (scrollBg == null) scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = new Color(0.82f, 0.82f, 0.84f, 1f);
            scrollBg.raycastTarget = false;
            if (roundedSprite != null)
            {
                scrollBg.sprite = roundedSprite;
                scrollBg.type = Image.Type.Sliced;
                scrollBg.pixelsPerUnitMultiplier = 2f;
            }

            // 4. Viewport (Mask)
            Transform vpTrans = scrollGO.transform.Find("Viewport");
            GameObject vpGO = vpTrans != null ? vpTrans.gameObject : CreateUIElement("Viewport", scrollGO.transform);
            RectTransform vpRect = vpGO.GetComponent<RectTransform>();
            vpRect.anchorMin = new Vector2(0f, 0f);
            vpRect.anchorMax = new Vector2(1f, 1f);
            vpRect.pivot = new Vector2(0.5f, 1f);
            vpRect.offsetMin = new Vector2(12f, 12f);
            vpRect.offsetMax = new Vector2(-28f, -12f);

            Image vpImg = vpGO.GetComponent<Image>();
            if (vpImg == null) vpImg = vpGO.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.01f);
            vpImg.raycastTarget = false;

            Mask vpMask = vpGO.GetComponent<Mask>();
            if (vpMask == null) vpMask = vpGO.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;

            // 5. Content
            Transform contentTrans = vpGO.transform.Find("Content");
            GameObject contentGO = contentTrans != null ? contentTrans.gameObject : CreateUIElement("Content", vpGO.transform);
            RectTransform contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            const float itemHeight = 68f;
            const float itemSpacing = 8f;
            const float stride = itemHeight + itemSpacing;
            int count = directionList.Count;
            float totalContentHeight = count > 0 ? (count * itemHeight) + (Mathf.Max(0, count - 1) * itemSpacing) : 0f;
            contentRect.sizeDelta = new Vector2(0f, totalContentHeight);

            // 6. PlaceholderBar (挿入先スロットガイド)
            Transform phTrans = contentGO.transform.Find("PlaceholderBar");
            GameObject phGO = phTrans != null ? phTrans.gameObject : CreateUIElement("PlaceholderBar", contentGO.transform);
            RectTransform phRect = phGO.GetComponent<RectTransform>();
            phRect.anchorMin = new Vector2(0f, 1f);
            phRect.anchorMax = new Vector2(1f, 1f);
            phRect.pivot = new Vector2(0.5f, 1f);
            phRect.anchoredPosition = Vector2.zero;
            phRect.sizeDelta = new Vector2(0f, itemHeight);

            Image phImg = phGO.GetComponent<Image>();
            if (phImg == null) phImg = phGO.AddComponent<Image>();
            phImg.color = new Color(0.31764707f, 0.1882353f, 0.81960785f, 0.28f);
            phImg.raycastTarget = false;
            if (roundedSprite != null)
            {
                phImg.sprite = roundedSprite;
                phImg.type = Image.Type.Sliced;
                phImg.pixelsPerUnitMultiplier = 2f;
            }
            phGO.SetActive(false);

            // 既存の SortItem_* を削除して最新の directionList に合わせて再生成
            for (int i = contentGO.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = contentGO.transform.GetChild(i);
                if (child != phGO.transform)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            List<SSSManualSortItem> generatedItems = new List<SSSManualSortItem>(count);
            for (int i = 0; i < count; i++)
            {
                DirectionObject dirObj = directionList[i];
                string dirTitle = dirObj != null ? dirObj.directionName : $"Direction {i + 1}";

                GameObject itemGO = CreateUIElement($"SortItem_{i}", contentGO.transform);
                RectTransform itemRect = itemGO.GetComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = new Vector2(1f, 1f);
                itemRect.pivot = new Vector2(0.5f, 1f);
                itemRect.anchoredPosition = new Vector2(0f, -i * stride);
                itemRect.sizeDelta = new Vector2(0f, itemHeight);

                Image itemBg = itemGO.AddComponent<Image>();
                itemBg.color = new Color(0.3019608f, 0.3019608f, 0.3019608f, 1f);
                itemBg.raycastTarget = false;
                if (roundedSprite != null)
                {
                    itemBg.sprite = roundedSprite;
                    itemBg.type = Image.Type.Sliced;
                    itemBg.pixelsPerUnitMultiplier = 2f;
                }

                // DragHandleIcon (≡)
                GameObject handleGO = CreateUIElement("DragHandleIcon", itemGO.transform);
                RectTransform handleRect = handleGO.GetComponent<RectTransform>();
                handleRect.anchorMin = new Vector2(0f, 0f);
                handleRect.anchorMax = new Vector2(0f, 1f);
                handleRect.pivot = new Vector2(0.5f, 0.5f);
                handleRect.offsetMin = new Vector2(14f, 12f);
                handleRect.offsetMax = new Vector2(50f, -12f);
                Image handleImg = handleGO.AddComponent<Image>();
                handleImg.color = new Color(1f, 1f, 1f, 0.16f);
                handleImg.raycastTarget = false;
                if (pillSprite != null)
                {
                    handleImg.sprite = pillSprite;
                    handleImg.type = Image.Type.Sliced;
                    handleImg.pixelsPerUnitMultiplier = 2.5f;
                }
                Text handleTxt = CreateTextElement("GripSymbol", handleGO.transform, font, "≡", 26, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
                handleTxt.raycastTarget = false;
                SetStretchRect(handleTxt.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

                // OrderNumberText ("01", "02"...)
                string rankText = (i + 1) < 10 ? $"0{i + 1}" : (i + 1).ToString();
                Text orderNumText = CreateTextElement("OrderNumberText", itemGO.transform, font, rankText, 24, TextAnchor.MiddleCenter, new Color(0.78f, 0.74f, 1f, 1f));
                orderNumText.fontStyle = FontStyle.Bold;
                orderNumText.raycastTarget = false;
                RectTransform numRect = orderNumText.GetComponent<RectTransform>();
                numRect.anchorMin = new Vector2(0f, 0f);
                numRect.anchorMax = new Vector2(0f, 1f);
                numRect.pivot = new Vector2(0.5f, 0.5f);
                numRect.offsetMin = new Vector2(56f, 0f);
                numRect.offsetMax = new Vector2(114f, 0f);

                // DirectionTitleText
                Text titleText = CreateTextElement("DirectionTitleText", itemGO.transform, font, dirTitle, 24, TextAnchor.MiddleLeft, Color.white);
                titleText.raycastTarget = false;
                RectTransform titleRect = titleText.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0f, 0f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.offsetMin = new Vector2(122f, 4f);
                titleRect.offsetMax = new Vector2(-148f, -4f);

                // MoveUpButton (▲)
                Button upBtn = CreateActionButton("MoveUpButton", itemGO.transform, font, "▲", 20, new Color(0.20f, 0.20f, 0.22f, 1f));
                RectTransform upRect = upBtn.GetComponent<RectTransform>();
                upRect.anchorMin = new Vector2(1f, 0.5f);
                upRect.anchorMax = new Vector2(1f, 0.5f);
                upRect.pivot = new Vector2(1f, 0.5f);
                upRect.anchoredPosition = new Vector2(-76f, 0f);
                upRect.sizeDelta = new Vector2(58f, 52f);
                Image upImg = upBtn.GetComponent<Image>();
                if (upImg != null && pillSprite != null)
                {
                    upImg.sprite = pillSprite;
                    upImg.type = Image.Type.Sliced;
                    upImg.pixelsPerUnitMultiplier = 2.5f;
                }
                Text upTxt = upBtn.GetComponentInChildren<Text>(true);
                if (upTxt != null) upTxt.raycastTarget = false;

                // MoveDownButton (▼)
                Button downBtn = CreateActionButton("MoveDownButton", itemGO.transform, font, "▼", 20, new Color(0.20f, 0.20f, 0.22f, 1f));
                RectTransform downRect = downBtn.GetComponent<RectTransform>();
                downRect.anchorMin = new Vector2(1f, 0.5f);
                downRect.anchorMax = new Vector2(1f, 0.5f);
                downRect.pivot = new Vector2(1f, 0.5f);
                downRect.anchoredPosition = new Vector2(-12f, 0f);
                downRect.sizeDelta = new Vector2(58f, 52f);
                Image downImg = downBtn.GetComponent<Image>();
                if (downImg != null && pillSprite != null)
                {
                    downImg.sprite = pillSprite;
                    downImg.type = Image.Type.Sliced;
                    downImg.pixelsPerUnitMultiplier = 2.5f;
                }
                Text downTxt = downBtn.GetComponentInChildren<Text>(true);
                if (downTxt != null) downTxt.raycastTarget = false;

                // SSSManualSortItem コンポーネント付与・結線
                SSSManualSortItem sortItem = itemGO.AddUdonSharpComponent<SSSManualSortItem>();
                VRC.Udon.UdonBehaviour itemUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(sortItem);
                if (itemUdon != null) itemUdon.SyncMethod = Networking.SyncType.None;

                SerializedObject itemSO = new SerializedObject(sortItem);
                itemSO.Update();
                itemSO.FindProperty("_sorter").objectReferenceValue = sorter;
                itemSO.FindProperty("_objectIndex").intValue = i;
                itemSO.FindProperty("_rectTransform").objectReferenceValue = itemRect;
                itemSO.FindProperty("_bgImage").objectReferenceValue = itemBg;
                itemSO.FindProperty("_orderNumberText").objectReferenceValue = orderNumText;
                itemSO.FindProperty("_directionTitleText").objectReferenceValue = titleText;
                itemSO.FindProperty("_moveUpButton").objectReferenceValue = upBtn;
                itemSO.FindProperty("_moveDownButton").objectReferenceValue = downBtn;
                itemSO.FindProperty("_dragHandleImage").objectReferenceValue = handleImg;
                itemSO.ApplyModifiedPropertiesWithoutUndo();

                if (itemUdon != null)
                {
                    ConfigureButtonForUdon(upBtn, itemUdon, "OnMoveUpClicked", interactable: i > 0);
                    ConfigureButtonForUdon(downBtn, itemUdon, "OnMoveDownClicked", interactable: i < count - 1);
                }

                UdonSharpEditorUtility.CopyProxyToUdon(sortItem);
                generatedItems.Add(sortItem);
            }

            // 7. DragInputOverlaySlider (Viewportの左〜中央領域をカバーし、右端の▲/▼ボタン列とScrollbarを干渉させない)
            Transform overlayTrans = scrollGO.transform.Find("DragInputOverlaySlider");
            GameObject overlayGO = overlayTrans != null ? overlayTrans.gameObject : CreateUIElement("DragInputOverlaySlider", scrollGO.transform);
            RectTransform overlayRect = overlayGO.GetComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(1f, 1f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.offsetMin = new Vector2(12f, 12f);
            overlayRect.offsetMax = new Vector2(-176f, -12f);

            Transform rayTargetTrans = overlayGO.transform.Find("OverlayRaycastTarget");
            GameObject rayTargetGO = rayTargetTrans != null ? rayTargetTrans.gameObject : CreateUIElement("OverlayRaycastTarget", overlayGO.transform);
            SetStretchRect(rayTargetGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image rayTargetImg = rayTargetGO.GetComponent<Image>();
            if (rayTargetImg == null) rayTargetImg = rayTargetGO.AddComponent<Image>();
            rayTargetImg.color = new Color(1f, 1f, 1f, 0.003f);
            rayTargetImg.raycastTarget = true;

            // Slider は handleRect / fillRect のどちらも未設定だと UpdateDrag 内でクリック位置を値に変換できず value が常に 0 のままになるため、
            // オーバーレイ全面をカバーする不可視の Handle Slide Area / Handle を用意してポインター位置 → value 変換を有効化する
            Transform overlayHandleAreaTrans = overlayGO.transform.Find("Handle Slide Area");
            GameObject overlayHandleAreaGO = overlayHandleAreaTrans != null ? overlayHandleAreaTrans.gameObject : CreateUIElement("Handle Slide Area", overlayGO.transform);
            SetStretchRect(overlayHandleAreaGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            Transform overlayHandleTrans = overlayHandleAreaGO.transform.Find("Handle");
            GameObject overlayHandleGO = overlayHandleTrans != null ? overlayHandleTrans.gameObject : CreateUIElement("Handle", overlayHandleAreaGO.transform);
            RectTransform overlayHandleRect = overlayHandleGO.GetComponent<RectTransform>();
            overlayHandleRect.sizeDelta = Vector2.zero;

            Slider overlaySlider = overlayGO.GetComponent<Slider>();
            if (overlaySlider == null) overlaySlider = overlayGO.AddComponent<Slider>();
            overlaySlider.fillRect = null;
            overlaySlider.handleRect = overlayHandleRect;
            overlaySlider.transition = Selectable.Transition.None;
            overlaySlider.direction = Slider.Direction.TopToBottom;
            overlaySlider.minValue = 0f;
            overlaySlider.maxValue = 1f;
            overlaySlider.wholeNumbers = false;
            overlaySlider.targetGraphic = rayTargetImg;
            Navigation noneNav = new Navigation { mode = Navigation.Mode.None };
            overlaySlider.navigation = noneNav;

            EventTrigger overlayTrigger = overlayGO.GetComponent<EventTrigger>();
            if (overlayTrigger == null) overlayTrigger = overlayGO.AddComponent<EventTrigger>();

            // 8. VerticalScrollbar
            Transform sbTrans = scrollGO.transform.Find("VerticalScrollbar");
            GameObject sbGO = sbTrans != null ? sbTrans.gameObject : CreateUIElement("VerticalScrollbar", scrollGO.transform);
            RectTransform sbRect = sbGO.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1f, 0f);
            sbRect.anchorMax = new Vector2(1f, 1f);
            sbRect.pivot = new Vector2(1f, 0.5f);
            sbRect.offsetMin = new Vector2(-22f, 12f);
            sbRect.offsetMax = new Vector2(-6f, -12f);

            Image sbBg = sbGO.GetComponent<Image>();
            if (sbBg == null) sbBg = sbGO.AddComponent<Image>();
            sbBg.color = new Color(0.72f, 0.72f, 0.75f, 1f);

            Transform slidingAreaTrans = sbGO.transform.Find("Sliding Area");
            GameObject slidingAreaGO = slidingAreaTrans != null ? slidingAreaTrans.gameObject : CreateUIElement("Sliding Area", sbGO.transform);
            SetStretchRect(slidingAreaGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            Transform sbHandleTrans = slidingAreaGO.transform.Find("Handle");
            GameObject sbHandleGO = sbHandleTrans != null ? sbHandleTrans.gameObject : CreateUIElement("Handle", slidingAreaGO.transform);
            SetStretchRect(sbHandleGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image sbHandleImg = sbHandleGO.GetComponent<Image>();
            if (sbHandleImg == null) sbHandleImg = sbHandleGO.AddComponent<Image>();
            sbHandleImg.color = new Color(0.45f, 0.45f, 0.48f, 1f);

            Scrollbar verticalScrollbar = sbGO.GetComponent<Scrollbar>();
            if (verticalScrollbar == null) verticalScrollbar = sbGO.AddComponent<Scrollbar>();
            verticalScrollbar.direction = Scrollbar.Direction.BottomToTop;
            verticalScrollbar.targetGraphic = sbHandleImg;
            verticalScrollbar.handleRect = sbHandleGO.GetComponent<RectTransform>();
            verticalScrollbar.size = 1f;
            verticalScrollbar.value = 1f;

            // 9. SSSRuntimeManualSorter のシリアライズ参照とイベント結線
            SerializedObject sorterSO = new SerializedObject(sorter);
            sorterSO.Update();
            sorterSO.FindProperty("_sssCore").objectReferenceValue = sssCore;
            sorterSO.FindProperty("_uiController").objectReferenceValue = uiController;
            sorterSO.FindProperty("_sortViewportRect").objectReferenceValue = vpRect;
            sorterSO.FindProperty("_sortContentRect").objectReferenceValue = contentRect;
            sorterSO.FindProperty("_placeholderBarRect").objectReferenceValue = phRect;
            sorterSO.FindProperty("_dragOverlaySlider").objectReferenceValue = overlaySlider;
            sorterSO.FindProperty("_verticalScrollbar").objectReferenceValue = verticalScrollbar;

            SerializedProperty itemsProp = sorterSO.FindProperty("_itemsByObjectIndex");
            if (itemsProp != null)
            {
                itemsProp.arraySize = generatedItems.Count;
                for (int i = 0; i < generatedItems.Count; i++)
                {
                    itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = generatedItems[i];
                }
            }
            sorterSO.ApplyModifiedPropertiesWithoutUndo();

            VRC.Udon.UdonBehaviour sorterUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(sorter);
            if (sorterUdon != null)
            {
                ConfigureSliderForUdon(overlaySlider, sorterUdon, "OnDragSliderValueChanged");
                ConfigureEventTriggerForUdon(overlayTrigger, EventTriggerType.PointerDown, sorterUdon, "OnDragSliderPointerDown");
                ConfigureEventTriggerForUdon(overlayTrigger, EventTriggerType.PointerUp, sorterUdon, "OnDragSliderPointerUp");
                ConfigureScrollbarForUdon(verticalScrollbar, sorterUdon, "OnVerticalScrollbarValueChanged");
            }

            UdonSharpEditorUtility.CopyProxyToUdon(sorter);

            // 初期表示状態は Manual トグルがONの場合のみ Active、それ以外は非表示
            Toggle manualToggle = uiControllerSO != null ? uiControllerSO.FindProperty("_sortManualToggle")?.objectReferenceValue as Toggle : null;
            bool isManualOn = manualToggle != null && manualToggle.isOn;
            drawerGO.SetActive(isManualOn);

            if (uiControllerSO != null)
            {
                uiControllerSO.Update();
                SerializedProperty sorterRefProp = uiControllerSO.FindProperty("_runtimeManualSorter");
                if (sorterRefProp != null) sorterRefProp.objectReferenceValue = sorter;
                uiControllerSO.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool EnsureSingleProgramAssetReady(Type scriptType, string scriptName)
        {
            UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(scriptType);
            if (programAsset == null)
            {
                string[] guids = AssetDatabase.FindAssets($"{scriptName} t:MonoScript");
                MonoScript targetScript = null;
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (ms != null && ms.GetClass() == scriptType)
                    {
                        targetScript = ms;
                        break;
                    }
                }
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

            // UdonSharpのコンパイルエラーが存在する場合は再コンパイルを繰り返し要求しない
            if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
            {
                return false;
            }

            if (programAsset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion ||
                programAsset.CompiledVersion < UdonSharpProgramVersion.CurrentVersion)
            {
                UdonSharpProgramAsset.CompileAllCsPrograms(true);
                return false;
            }

            return true;
        }

        internal static void SetupPerformerPortableUI(
            DirectionController dirController,
            SerializedObject dirControllerSO,
            SSSCore sssCore,
            SerializedObject sssCoreSO,
            SSSUIController uiController,
            SerializedObject uiControllerSO,
            out SSSPerformerUIController performerUI,
            out SerializedObject performerUISO)
        {
            performerUI = null;
            performerUISO = null;

            if (dirController == null || sssCore == null || EditorUtility.IsPersistent(dirController.gameObject)) return;
            if (!EnsurePerformerProgramAssetsReady()) return;

            Transform root = dirController.transform;
            Transform perfRootTrans = root.Find(_performerRootObjectName);
            GameObject perfRootGO;
            if (perfRootTrans == null)
            {
                perfRootGO = new GameObject(_performerRootObjectName);
                perfRootGO.transform.SetParent(root, false);
                perfRootGO.transform.localPosition = new Vector3(-1.2f, 1.15f, 0.5f);
                perfRootGO.transform.localRotation = Quaternion.identity;
                perfRootGO.transform.localScale = Vector3.one;
            }
            else
            {
                perfRootGO = perfRootTrans.gameObject;
            }

            // 1. 親機 Rigidbody / BoxCollider / VRCPickup / SSSPerformerUIController (VRCObjectSyncは付与しない完全ローカルPickup)
            Rigidbody parentRb = perfRootGO.GetComponent<Rigidbody>();
            if (parentRb == null) parentRb = perfRootGO.AddComponent<Rigidbody>();
            parentRb.isKinematic = true;
            parentRb.useGravity = false;

            BoxCollider parentCol = perfRootGO.GetComponent<BoxCollider>();
            if (parentCol == null) parentCol = perfRootGO.AddComponent<BoxCollider>();
            parentCol.isTrigger = false;
            parentCol.center = new Vector3(0f, -0.2086f, -0.0404f);
            parentCol.size = new Vector3(0.42f, 0.035f, 0.06f);

            VRCPickup parentPickup = perfRootGO.GetComponent<VRCPickup>();
            if (parentPickup == null) parentPickup = perfRootGO.AddComponent<VRCPickup>();
            parentPickup.pickupable = true;
            parentPickup.AutoHold = VRC_Pickup.AutoHoldMode.Yes;
            parentPickup.InteractionText = "Move Performer Panel";
            parentPickup.UseText = "";

            performerUI = perfRootGO.GetComponent<SSSPerformerUIController>();
            if (performerUI == null)
            {
                performerUI = perfRootGO.AddUdonSharpComponent<SSSPerformerUIController>();
                VRC.Udon.UdonBehaviour perfBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(performerUI);
                if (perfBacking != null) perfBacking.SyncMethod = Networking.SyncType.None;
            }

            // 2. ProximityChecker [SphereCollider(isTrigger=true, Radius=1.8m), SSSPerformerProximityChecker]
            Transform proxTrans = perfRootGO.transform.Find("ProximityChecker");
            GameObject proxGO = proxTrans != null ? proxTrans.gameObject : new GameObject("ProximityChecker");
            proxGO.transform.SetParent(perfRootGO.transform, false);
            proxGO.transform.localPosition = Vector3.zero;
            proxGO.transform.localRotation = Quaternion.identity;
            proxGO.transform.localScale = Vector3.one;

            SphereCollider proxCol = proxGO.GetComponent<SphereCollider>();
            if (proxCol == null) proxCol = proxGO.AddComponent<SphereCollider>();
            proxCol.isTrigger = true;
            proxCol.radius = 1.8f;

            SSSPerformerProximityChecker proxChecker = proxGO.GetComponent<SSSPerformerProximityChecker>();
            if (proxChecker == null)
            {
                proxChecker = proxGO.AddUdonSharpComponent<SSSPerformerProximityChecker>();
                VRC.Udon.UdonBehaviour proxBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxChecker);
                if (proxBacking != null) proxBacking.SyncMethod = Networking.SyncType.None;
            }
            if (proxChecker != null)
            {
                SerializedObject proxSO = new SerializedObject(proxChecker);
                proxSO.Update();
                proxSO.FindProperty("_performerUIController").objectReferenceValue = performerUI;
                proxSO.FindProperty("_proximityCollider").objectReferenceValue = proxCol;
                proxSO.ApplyModifiedPropertiesWithoutUndo();
                UdonSharpEditorUtility.CopyProxyToUdon(proxChecker);
            }

            // 3. Pickup Bar (親機把持ハンドル / Lock時はオブジェクト自体を非表示)
            Transform oldVisualMeshTrans = perfRootGO.transform.Find("VisualMesh");
            if (oldVisualMeshTrans != null)
            {
                DestroyImmediate(oldVisualMeshTrans.gameObject);
            }

            Transform pickupBarTrans = perfRootGO.transform.Find("Pickup Bar");
            GameObject pickupBarGO;
            if (pickupBarTrans == null)
            {
                pickupBarGO = new GameObject("Pickup Bar");
                pickupBarGO.transform.SetParent(perfRootGO.transform, false);
                pickupBarGO.transform.localPosition = Vector3.zero;
                pickupBarGO.transform.localRotation = Quaternion.identity;
                pickupBarGO.transform.localScale = Vector3.one;

                GameObject barMeshGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barMeshGO.name = "Bar v1.0";
                barMeshGO.transform.SetParent(pickupBarGO.transform, false);
                barMeshGO.transform.localPosition = new Vector3(0f, -0.2086f, -0.0504f);
                barMeshGO.transform.localRotation = Quaternion.identity;
                barMeshGO.transform.localScale = new Vector3(0.42f, 0.02f, 0.02f);
                Collider extraCol = barMeshGO.GetComponent<Collider>();
                if (extraCol != null) DestroyImmediate(extraCol);
            }
            else
            {
                pickupBarGO = pickupBarTrans.gameObject;
            }

            // 4. TriggerHolster (Pedestal) & PerformerTrigger
            Transform holsterTrans = perfRootGO.transform.Find("TriggerHolster");
            GameObject holsterGO = holsterTrans != null ? holsterTrans.gameObject : new GameObject("TriggerHolster");
            holsterGO.transform.SetParent(perfRootGO.transform, false);
            if (holsterTrans == null)
            {
                holsterGO.transform.localPosition = new Vector3(0.28f, -0.122f, 0f);
                holsterGO.transform.localRotation = Quaternion.identity;
                holsterGO.transform.localScale = Vector3.one;
            }

            Transform oldHolsterVisual = holsterGO.transform.Find("HolsterVisual");
            if (oldHolsterVisual != null && holsterGO.transform.childCount > 1)
            {
                DestroyImmediate(oldHolsterVisual.gameObject);
            }

            Transform pedestalTrans = holsterGO.transform.Find("Pedestal");
            if (pedestalTrans == null)
            {
                Transform existingChild = holsterGO.transform.Find("Circle v1.0");
                if (existingChild == null) existingChild = holsterGO.transform.Find("HolsterVisual");
                if (existingChild == null && holsterGO.transform.childCount > 0)
                {
                    existingChild = holsterGO.transform.GetChild(0);
                }

                if (existingChild != null)
                {
                    existingChild.gameObject.name = "Pedestal";
                    pedestalTrans = existingChild;
                }
                else
                {
                    GameObject ringGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ringGO.name = "Pedestal";
                    ringGO.transform.SetParent(holsterGO.transform, false);
                    ringGO.transform.localPosition = new Vector3(0f, -0.03f, 0f);
                    ringGO.transform.localScale = new Vector3(0.06f, 0.008f, 0.06f);
                    Collider extraCol = ringGO.GetComponent<Collider>();
                    if (extraCol != null) DestroyImmediate(extraCol);
                    pedestalTrans = ringGO.transform;
                }
            }

            Transform trigTrans = perfRootGO.transform.Find("PerformerTrigger");
            GameObject trigGO = trigTrans != null ? trigTrans.gameObject : new GameObject("PerformerTrigger");
            trigGO.transform.SetParent(perfRootGO.transform, false);
            if (trigTrans == null)
            {
                trigGO.transform.position = holsterGO.transform.position;
                trigGO.transform.rotation = holsterGO.transform.rotation;
                trigGO.transform.localScale = Vector3.one;
            }

            Rigidbody trigRb = trigGO.GetComponent<Rigidbody>();
            if (trigRb == null) trigRb = trigGO.AddComponent<Rigidbody>();
            trigRb.isKinematic = true;
            trigRb.useGravity = false;

            SphereCollider trigCol = trigGO.GetComponent<SphereCollider>();
            if (trigCol == null) trigCol = trigGO.AddComponent<SphereCollider>();
            trigCol.isTrigger = false;
            trigCol.radius = 0.045f;

            VRCPickup trigPickup = trigGO.GetComponent<VRCPickup>();
            if (trigPickup == null) trigPickup = trigGO.AddComponent<VRCPickup>();
            trigPickup.pickupable = true;
            trigPickup.AutoHold = VRC_Pickup.AutoHoldMode.Yes;
            trigPickup.InteractionText = "Performer Trigger";
            trigPickup.UseText = "Fire Next Cue";

            Transform trigMeshTrans = trigGO.transform.Find("TriggerMesh");
            GameObject trigMeshGO;
            if (trigMeshTrans == null)
            {
                trigMeshGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                trigMeshGO.name = "TriggerMesh";
                trigMeshGO.transform.SetParent(trigGO.transform, false);
                trigMeshGO.transform.localPosition = Vector3.zero;
                trigMeshGO.transform.localRotation = Quaternion.identity;
                trigMeshGO.transform.localScale = new Vector3(0.042f, 0.055f, 0.042f);
                Collider extraCol = trigMeshGO.GetComponent<Collider>();
                if (extraCol != null) DestroyImmediate(extraCol);
            }
            else
            {
                trigMeshGO = trigMeshTrans.gameObject;
            }

            SSSPerformerTrigger perfTrigger = trigGO.GetComponent<SSSPerformerTrigger>();
            if (perfTrigger == null)
            {
                perfTrigger = trigGO.AddUdonSharpComponent<SSSPerformerTrigger>();
                VRC.Udon.UdonBehaviour trigBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(perfTrigger);
                if (trigBacking != null) trigBacking.SyncMethod = Networking.SyncType.None;
            }
            if (perfTrigger != null)
            {
                SerializedObject trigSO = new SerializedObject(perfTrigger);
                trigSO.Update();
                trigSO.FindProperty("_performerUIController").objectReferenceValue = performerUI;
                trigSO.FindProperty("_holsterTransform").objectReferenceValue = holsterGO.transform;
                trigSO.FindProperty("_triggerVisualObject").objectReferenceValue = trigMeshGO;
                trigSO.FindProperty("_pickup").objectReferenceValue = trigPickup;
                trigSO.FindProperty("_rigidbody").objectReferenceValue = trigRb;
                trigSO.FindProperty("_pickupCollider").objectReferenceValue = trigCol;
                trigSO.ApplyModifiedPropertiesWithoutUndo();
                UdonSharpEditorUtility.CopyProxyToUdon(perfTrigger);
            }

            // 5. Canvas [Canvas, CanvasScaler, GraphicRaycaster, VRCUiShape] & 3状態パネルの検索・構築
            BuildPerformerCanvasHierarchy(
                root,
                perfRootGO.transform,
                out Canvas perfCanvas,
                out GameObject standbyPanelGO,
                out GameObject loadingPanelGO,
                out GameObject playingPanelGO,
                out Text nextCueText,
                out Toggle ignoreThresholdToggle,
                out Toggle ignorePerformerToggle,
                out Toggle posLockToggle,
                out GameObject posUnlockIcon,
                out GameObject posLockIcon,
                out Text loadingLoadedCountText,
                out Text loadingPerfStatusText,
                out GameObject loadingPerfReadyObj,
                out GameObject loadingPerfWarningObj,
                out Text loadingDelayStatusText,
                out Text loadingDirText,
                out Text loadingPctText,
                out Slider loadingBarSlider,
                out Text loadingProgText,
                out Button forceStartBtn,
                out Button cancelLoadBtn,
                out Text playingLoadedCountText,
                out Text playingPerfStatusText,
                out GameObject playingPerfReadyObj,
                out GameObject playingPerfWarningObj,
                out Text playingDelayStatusText,
                out Text playingDirText,
                out Text playingVideoTimeText,
                out Slider playingBarSlider,
                out Toggle unlockStopToggle,
                out Button emergencyStopBtn);

            performerUISO = new SerializedObject(performerUI);
            performerUISO.Update();

            performerUISO.FindProperty("_sssCore").objectReferenceValue = sssCore;
            performerUISO.FindProperty("_mainUIController").objectReferenceValue = uiController;
            performerUISO.FindProperty("_performerTrigger").objectReferenceValue = perfTrigger;
            performerUISO.FindProperty("_proximityChecker").objectReferenceValue = proxChecker;
            performerUISO.FindProperty("_parentPickup").objectReferenceValue = parentPickup;
            performerUISO.FindProperty("_parentPickupCollider").objectReferenceValue = parentCol;
            performerUISO.FindProperty("_parentRigidbody").objectReferenceValue = parentRb;
            performerUISO.FindProperty("_targetCanvas").objectReferenceValue = perfCanvas;
            performerUISO.FindProperty("_standbyPanel").objectReferenceValue = standbyPanelGO;
            performerUISO.FindProperty("_loadingPanel").objectReferenceValue = loadingPanelGO;
            performerUISO.FindProperty("_playingPanel").objectReferenceValue = playingPanelGO;

            AssignIfNull(performerUISO, "_positionLockToggle", posLockToggle);
            AssignIfNull(performerUISO, "_positionUnlockIcon", posUnlockIcon);
            AssignIfNull(performerUISO, "_positionLockIcon", posLockIcon);
            AssignIfNull(performerUISO, "_pickupBarObject", pickupBarGO);
            AssignIfNull(performerUISO, "_triggerHolsterObject", holsterGO);

            AssignIfNull(performerUISO, "_nextCueText", nextCueText);
            AssignIfNull(performerUISO, "_ignoreThresholdToggle", ignoreThresholdToggle);
            AssignIfNull(performerUISO, "_ignorePerformerErrorToggle", ignorePerformerToggle);

            AssignIfNull(performerUISO, "_loadingLoadedPlayerCountText", loadingLoadedCountText);
            AssignIfNull(performerUISO, "_loadingPerformerStatusText", loadingPerfStatusText);
            AssignIfNull(performerUISO, "_loadingPerformerReadyObject", loadingPerfReadyObj);
            AssignIfNull(performerUISO, "_loadingPerformerWarningObject", loadingPerfWarningObj);
            AssignIfNull(performerUISO, "_loadingDelayStatusText", loadingDelayStatusText);
            AssignIfNull(performerUISO, "_loadingDirectionText", loadingDirText);
            AssignIfNull(performerUISO, "_loadingPercentageText", loadingPctText);
            AssignIfNull(performerUISO, "_loadingProgressBar", loadingBarSlider);
            AssignIfNull(performerUISO, "_loadingProgressText", loadingProgText);
            AssignIfNull(performerUISO, "_forceStartNowButton", forceStartBtn);
            AssignIfNull(performerUISO, "_cancelLoadingButton", cancelLoadBtn);

            AssignIfNull(performerUISO, "_playingLoadedPlayerCountText", playingLoadedCountText);
            AssignIfNull(performerUISO, "_playingPerformerStatusText", playingPerfStatusText);
            AssignIfNull(performerUISO, "_playingPerformerReadyObject", playingPerfReadyObj);
            AssignIfNull(performerUISO, "_playingPerformerWarningObject", playingPerfWarningObj);
            AssignIfNull(performerUISO, "_playingDelayStatusText", playingDelayStatusText);
            AssignIfNull(performerUISO, "_playingDirectionText", playingDirText);
            AssignIfNull(performerUISO, "_playingVideoTimeText", playingVideoTimeText);
            AssignIfNull(performerUISO, "_playingProgressBar", playingBarSlider);
            AssignIfNull(performerUISO, "_unlockEmergencyStopToggle", unlockStopToggle);
            AssignIfNull(performerUISO, "_emergencyStopButton", emergencyStopBtn);

            performerUISO.ApplyModifiedPropertiesWithoutUndo();

            VRC.Udon.UdonBehaviour perfUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(performerUI);
            if (perfUdon != null)
            {
                ConfigureToggleForUdon(performerUISO.FindProperty("_ignoreThresholdToggle")?.objectReferenceValue as Toggle, perfUdon, "OnForceLaunchToggleChanged", clearGroup: true);
                ConfigureToggleForUdon(performerUISO.FindProperty("_ignorePerformerErrorToggle")?.objectReferenceValue as Toggle, perfUdon, "OnForceLaunchToggleChanged", clearGroup: true);
                ConfigureToggleForUdon(performerUISO.FindProperty("_positionLockToggle")?.objectReferenceValue as Toggle, perfUdon, "OnPositionLockToggleChanged", clearGroup: true);

                ConfigureButtonForUdon(performerUISO.FindProperty("_forceStartNowButton")?.objectReferenceValue as Button, perfUdon, "OnForceStartNowClicked");
                ConfigureButtonForUdon(performerUISO.FindProperty("_cancelLoadingButton")?.objectReferenceValue as Button, perfUdon, "OnCancelLoadingClicked");

                ConfigureToggleForUdon(performerUISO.FindProperty("_unlockEmergencyStopToggle")?.objectReferenceValue as Toggle, perfUdon, "OnUnlockEmergencyStopToggleChanged", clearGroup: true);
                ConfigureButtonForUdon(performerUISO.FindProperty("_emergencyStopButton")?.objectReferenceValue as Button, perfUdon, "OnEmergencyStopClicked", interactable: false);
            }

            // DirectionController, SSSCore, SSSUIController へ相互参照を保存
            if (dirControllerSO != null)
            {
                dirControllerSO.Update();
                SerializedProperty dcPerfProp = dirControllerSO.FindProperty("_performerUIController");
                if (dcPerfProp != null) dcPerfProp.objectReferenceValue = performerUI;
                dirControllerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (sssCoreSO != null)
            {
                sssCoreSO.Update();
                SerializedProperty corePerfProp = sssCoreSO.FindProperty("_performerUIController");
                if (corePerfProp != null) corePerfProp.objectReferenceValue = performerUI;
                sssCoreSO.ApplyModifiedPropertiesWithoutUndo();
            }

            if (uiControllerSO != null && uiController != null)
            {
                uiControllerSO.Update();
                SerializedProperty uiPerfProp = uiControllerSO.FindProperty("_performerUIController");
                if (uiPerfProp != null) uiPerfProp.objectReferenceValue = performerUI;
                uiControllerSO.ApplyModifiedPropertiesWithoutUndo();
                UdonSharpEditorUtility.CopyProxyToUdon(uiController);
            }

            UdonSharpEditorUtility.CopyProxyToUdon(performerUI);
        }

        private static void BuildPerformerCanvasHierarchy(
            Transform sssRoot,
            Transform perfRoot,
            out Canvas canvas,
            out GameObject standbyPanel,
            out GameObject loadingPanel,
            out GameObject playingPanel,
            out Text nextCueText,
            out Toggle ignoreThresholdToggle,
            out Toggle ignorePerformerToggle,
            out Toggle posLockToggle,
            out GameObject posUnlockIcon,
            out GameObject posLockIcon,
            out Text loadingLoadedCountText,
            out Text loadingPerfStatusText,
            out GameObject loadingPerfReadyObj,
            out GameObject loadingPerfWarningObj,
            out Text loadingDelayStatusText,
            out Text loadingDirText,
            out Text loadingPctText,
            out Slider loadingBarSlider,
            out Text loadingProgText,
            out Button forceStartBtn,
            out Button cancelLoadBtn,
            out Text playingLoadedCountText,
            out Text playingPerfStatusText,
            out GameObject playingPerfReadyObj,
            out GameObject playingPerfWarningObj,
            out Text playingDelayStatusText,
            out Text playingDirText,
            out Text playingVideoTimeText,
            out Slider playingBarSlider,
            out Toggle unlockStopToggle,
            out Button emergencyStopBtn)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            Transform canvasTrans = perfRoot.Find("Canvas");
            GameObject canvasGO = canvasTrans != null ? canvasTrans.gameObject : new GameObject("Canvas");
            canvasGO.transform.SetParent(perfRoot, false);

            canvas = canvasGO.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();

            if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            if (canvasGO.GetComponent<VRCUiShape>() == null)
            {
                canvasGO.AddComponent<VRCUiShape>();
            }

            // VRCUiShape用のメインBoxColliderと、Canvas右外側に配置されたUI Lock Toggle用のBoxColliderを保証
            BoxCollider[] canvasBoxCols = canvasGO.GetComponents<BoxCollider>();
            BoxCollider mainCanvasCol = (canvasBoxCols != null && canvasBoxCols.Length >= 1) ? canvasBoxCols[0] : canvasGO.AddComponent<BoxCollider>();
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRect != null && canvasRect.sizeDelta.sqrMagnitude > 0f ? canvasRect.sizeDelta : new Vector2(920f, 950f);
            mainCanvasCol.isTrigger = true;
            mainCanvasCol.center = Vector3.zero;
            mainCanvasCol.size = new Vector3(canvasSize.x, canvasSize.y, 1f);

            BoxCollider lockToggleCol = (canvasBoxCols != null && canvasBoxCols.Length >= 2) ? canvasBoxCols[1] : canvasGO.AddComponent<BoxCollider>();
            lockToggleCol.isTrigger = true;
            lockToggleCol.center = new Vector3(560f, -391f, 0f);
            lockToggleCol.size = new Vector3(160f, 105f, 1f);

            // 既存の Canvas/Panel 構造がある場合はそちらを優先使用（重複生成しない）
            Transform panelRoot = canvasGO.transform.Find("Panel");
            if (panelRoot == null)
            {
                panelRoot = canvasGO.transform;
            }

            // Position Lock Toggle (Canvas/UI Lock Toggle)
            posLockToggle = FindComponent<Toggle>(canvasGO.transform, "UI Lock Toggle");
            if (posLockToggle == null)
            {
                posLockToggle = FindComponent<Toggle>(canvasGO.transform, "Toggle");
            }
            if (posLockToggle != null)
            {
                posUnlockIcon = FindGameObject(posLockToggle.transform, "Toggle/Button/Unlock");
                posLockIcon = FindGameObject(posLockToggle.transform, "Toggle/Button/Lock");
            }
            else
            {
                posUnlockIcon = FindGameObject(canvasGO.transform, "UI Lock Toggle/Toggle/Button/Unlock");
                posLockIcon = FindGameObject(canvasGO.transform, "UI Lock Toggle/Toggle/Button/Lock");
            }

            // 1. StandbyPanel
            Transform standbyTrans = panelRoot.Find("StandbyPanel");
            standbyPanel = standbyTrans != null ? standbyTrans.gameObject : CreateUIElement("StandbyPanel", panelRoot);
            if (standbyTrans == null)
            {
                SetStretchRect(standbyPanel.GetComponent<RectTransform>(), 20f, 16f, -20f, -68f);
            }

            nextCueText = FindComponent<Text>(standbyPanel.transform, "NextCueBanner/NextCueText");
            if (nextCueText == null)
            {
                GameObject cueBanner = CreateUIElement("NextCueBanner", standbyPanel.transform);
                RectTransform cueRect = cueBanner.GetComponent<RectTransform>();
                cueRect.anchorMin = new Vector2(0f, 1f);
                cueRect.anchorMax = new Vector2(1f, 1f);
                cueRect.pivot = new Vector2(0.5f, 1f);
                cueRect.anchoredPosition = Vector2.zero;
                cueRect.sizeDelta = new Vector2(0f, 52f);
                Image cueBg = cueBanner.AddComponent<Image>();
                cueBg.color = new Color(0.8f, 0.8f, 0.8f, 1f);

                nextCueText = CreateTextElement("NextCueText", cueBanner.transform, font, "Next Cue: (Select Direction)", 24, TextAnchor.MiddleLeft, new Color(0.3019608f, 0.3019608f, 0.3019608f, 1f));
                SetStretchRect(nextCueText.GetComponent<RectTransform>(), 16f, 4f, -16f, -4f);
            }

            Transform scrollTrans = standbyPanel.transform.Find("Scroll View");
            if (scrollTrans == null)
            {
                BuildPlaylistScrollView(sssRoot, standbyPanel.transform, font);
            }

            Transform bottomTrans = standbyPanel.transform.Find("BottomControls");
            ignorePerformerToggle = FindComponent<Toggle>(standbyPanel.transform, "BottomControls/IgnorePerformerErrorToggle");
            ignoreThresholdToggle = FindComponent<Toggle>(standbyPanel.transform, "BottomControls/IgnoreThresholdToggle");
            if (bottomTrans != null)
            {
                if (ignorePerformerToggle == null && bottomTrans.childCount > 0)
                {
                    ignorePerformerToggle = bottomTrans.GetChild(0).GetComponent<Toggle>();
                }
                if (ignoreThresholdToggle == null && bottomTrans.childCount > 1)
                {
                    ignoreThresholdToggle = bottomTrans.GetChild(1).GetComponent<Toggle>();
                }
            }
            if (posLockToggle == null)
            {
                posLockToggle = FindComponent<Toggle>(standbyPanel.transform, "BottomControls/PositionLockToggle");
            }

            // 2. LoadingPanel
            Transform loadingTrans = panelRoot.Find("LoadingPanel");
            loadingPanel = loadingTrans != null ? loadingTrans.gameObject : CreateUIElement("LoadingPanel", panelRoot);
            if (loadingTrans == null)
            {
                SetStretchRect(loadingPanel.GetComponent<RectTransform>(), 28f, 24f, -28f, -74f);
                loadingPanel.SetActive(false);
            }

            loadingLoadedCountText = FindComponent<Text>(loadingPanel.transform, "Status/Loaded Player/Loaded Player num");
            loadingPerfStatusText = FindComponent<Text>(loadingPanel.transform, "Status/Performer Status/Performer Status");
            loadingPerfReadyObj = FindGameObject(loadingPanel.transform, "Status/Performer Status/Ready");
            loadingPerfWarningObj = FindGameObject(loadingPanel.transform, "Status/Performer Status/Warning");
            loadingDelayStatusText = FindComponent<Text>(loadingPanel.transform, "Status/Delay Status/Delay Status");
            loadingDirText = FindComponent<Text>(loadingPanel.transform, "Loading Status/LoadingDirectionText");
            if (loadingDirText == null)
            {
                loadingDirText = FindComponent<Text>(loadingPanel.transform, "LoadingDirectionText");
            }
            loadingPctText = FindComponent<Text>(loadingPanel.transform, "Loading Status/Percentage");
            loadingBarSlider = FindComponent<Slider>(loadingPanel.transform, "Loading Bar");
            loadingProgText = FindComponent<Text>(loadingPanel.transform, "LoadingProgressText");
            forceStartBtn = FindComponent<Button>(loadingPanel.transform, "ForceStartNowButton");
            cancelLoadBtn = FindComponent<Button>(loadingPanel.transform, "CancelLoadingButton");

            // 3. PlayingPanel
            Transform playingTrans = panelRoot.Find("PlayingPanel");
            playingPanel = playingTrans != null ? playingTrans.gameObject : CreateUIElement("PlayingPanel", panelRoot);
            if (playingTrans == null)
            {
                SetStretchRect(playingPanel.GetComponent<RectTransform>(), 28f, 24f, -28f, -74f);
                playingPanel.SetActive(false);
            }

            playingLoadedCountText = FindComponent<Text>(playingPanel.transform, "Status/Loaded Player/Loaded Player num");
            playingPerfStatusText = FindComponent<Text>(playingPanel.transform, "Status/Performer Status/Performer Status");
            playingPerfReadyObj = FindGameObject(playingPanel.transform, "Status/Performer Status/Ready");
            playingPerfWarningObj = FindGameObject(playingPanel.transform, "Status/Performer Status/Warning");
            playingDelayStatusText = FindComponent<Text>(playingPanel.transform, "Status/Delay Status/Delay Status");
            playingDirText = FindComponent<Text>(playingPanel.transform, "Video Player Status/PlayingDirectionText");
            if (playingDirText == null)
            {
                playingDirText = FindComponent<Text>(playingPanel.transform, "PlayingDirectionText");
            }
            playingVideoTimeText = FindComponent<Text>(playingPanel.transform, "Video Player Status/Video Time");
            playingBarSlider = FindComponent<Slider>(playingPanel.transform, "Loading Bar");
            unlockStopToggle = FindComponent<Toggle>(playingPanel.transform, "UnlockEmergencyStopToggle");
            emergencyStopBtn = FindComponent<Button>(playingPanel.transform, "EmergencyStopButton");
        }

        private static void BuildPlaylistScrollView(Transform sssRoot, Transform standbyPanel, Font font)
        {
            GameObject scrollGO = CreateUIElement("Scroll View", standbyPanel);
            RectTransform scrollRectTrans = scrollGO.GetComponent<RectTransform>();
            scrollRectTrans.anchorMin = new Vector2(0f, 0f);
            scrollRectTrans.anchorMax = new Vector2(1f, 1f);
            scrollRectTrans.offsetMin = new Vector2(0f, 208f);
            scrollRectTrans.offsetMax = new Vector2(0f, -62f);

            Image scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = new Color(0.04f, 0.05f, 0.07f, 0.85f);

            ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 25f;

            GameObject viewportGO = CreateUIElement("Viewport", scrollGO.transform);
            RectTransform vpRect = viewportGO.GetComponent<RectTransform>();
            SetStretchRect(vpRect, 6f, 6f, -6f, -6f);
            Image vpImg = viewportGO.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.01f);
            Mask vpMask = viewportGO.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;

            GameObject contentGO = CreateUIElement("Content", viewportGO.transform);
            RectTransform contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vpRect;
            sr.content = contentRect;

            // メインUIの DirectionSW テンプレートが存在すれば複製し、なければ新規構築する
            Transform mainBaseSW = sssRoot != null ? sssRoot.Find($"{_directionSWRootPath}/{_directionSW_BaseName}") : null;
            if (mainBaseSW != null)
            {
                GameObject clonedBase = Instantiate(mainBaseSW.gameObject, contentGO.transform, false);
                clonedBase.name = _directionSW_BaseName;
                clonedBase.SetActive(false);
            }
            else
            {
                GameObject baseSW = CreateUIElement(_directionSW_BaseName, contentGO.transform);
                RectTransform swRect = baseSW.GetComponent<RectTransform>();
                swRect.sizeDelta = new Vector2(0f, 52f);
                LayoutElement le = baseSW.AddComponent<LayoutElement>();
                le.preferredHeight = 52f;

                GameObject btnGO = CreateUIElement("Button", baseSW.transform);
                SetStretchRect(btnGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
                Image btnImg = btnGO.AddComponent<Image>();
                btnImg.color = new Color(0.18f, 0.20f, 0.26f, 1f);
                Button btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = btnImg;

                Text txt = CreateTextElement("Text", btnGO.transform, font, "Direction Title", 22, TextAnchor.MiddleLeft, Color.white);
                SetStretchRect(txt.GetComponent<RectTransform>(), 16f, 0f, -16f, 0f);

                baseSW.SetActive(false);
            }
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetStretchRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static Text CreateTextElement(string name, Transform parent, Font font, string text, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject go = CreateUIElement(name, parent);
            Text uiText = go.AddComponent<Text>();
            if (font != null) uiText.font = font;
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.alignment = alignment;
            uiText.color = color;
            uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            return uiText;
        }

        private static Button CreateActionButton(string name, Transform parent, Font font, string label, int fontSize, Color bgColor)
        {
            GameObject go = CreateUIElement(name, parent);
            Image bg = go.AddComponent<Image>();
            bg.color = bgColor;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;

            Text txt = CreateTextElement("Text", go.transform, font, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            txt.fontStyle = FontStyle.Bold;
            SetStretchRect(txt.GetComponent<RectTransform>(), 12f, 4f, -12f, -4f);
            return btn;
        }

        private static Toggle CreateLabeledToggleRow(string name, Transform parent, Font font, string label, float bottomOffset, bool defaultOn)
        {
            GameObject rowGO = CreateUIElement(name, parent);
            RectTransform rowRect = rowGO.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(1f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.anchoredPosition = new Vector2(0f, bottomOffset);
            rowRect.sizeDelta = new Vector2(0f, 56f);

            Image rowBg = rowGO.AddComponent<Image>();
            rowBg.color = new Color(0.13f, 0.15f, 0.20f, 0.95f);

            Text lbl = CreateTextElement("Label", rowGO.transform, font, label, 20, TextAnchor.MiddleLeft, Color.white);
            RectTransform lblRect = lbl.GetComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0f, 0f);
            lblRect.anchorMax = new Vector2(1f, 1f);
            lblRect.offsetMin = new Vector2(16f, 4f);
            lblRect.offsetMax = new Vector2(-120f, -4f);

            // メインUIの UpdateSlideSwitchVisual と互換の "Toggle/Button" 階層を持つスライドスイッチ構造
            GameObject slideBgGO = CreateUIElement("Toggle", rowGO.transform);
            RectTransform slideRect = slideBgGO.GetComponent<RectTransform>();
            slideRect.anchorMin = new Vector2(1f, 0.5f);
            slideRect.anchorMax = new Vector2(1f, 0.5f);
            slideRect.pivot = new Vector2(1f, 0.5f);
            slideRect.anchoredPosition = new Vector2(-16f, 0f);
            slideRect.sizeDelta = new Vector2(88f, 38f);
            Image slideBgImg = slideBgGO.AddComponent<Image>();
            slideBgImg.color = defaultOn ? new Color(0.31764707f, 0.1882353f, 0.81960785f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);

            GameObject knobGO = CreateUIElement("Button", slideBgGO.transform);
            RectTransform knobRect = knobGO.GetComponent<RectTransform>();
            knobRect.anchorMin = new Vector2(0.5f, 0.5f);
            knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.anchoredPosition = new Vector2(defaultOn ? 23.8f : -23.8f, 0f);
            knobRect.sizeDelta = new Vector2(32f, 32f);
            Image knobImg = knobGO.AddComponent<Image>();
            knobImg.color = defaultOn ? Color.white : new Color(0.34117648f, 0.34117648f, 0.34117648f, 1f);

            Toggle toggle = rowGO.AddComponent<Toggle>();
            toggle.targetGraphic = slideBgImg;
            toggle.isOn = defaultOn;
            return toggle;
        }
        private void SetSSSCore(SerializedObject dirControllerSO, SSSCore sssCore)
        {
            dirControllerSO.Update();
            dirControllerSO.FindProperty("SSS_core").objectReferenceValue = sssCore;
            dirControllerSO.ApplyModifiedProperties();
        }

        internal static void DeleteDirectionSW(Transform DirectorSWRoot, string directionSW_BaseName)
        {
            if (DirectorSWRoot == null) return;
            for (int i = DirectorSWRoot.childCount - 1; 0 <= i; i--)
            {
                Transform item = DirectorSWRoot.GetChild(i);
                if (item.name == directionSW_BaseName)
                    continue;
                DestroyImmediate(item.gameObject);
            }
        }

        internal static List<GameObject> GenerateDirectionSW(Transform DirectorSWRoot, string directionSW_BaseName, List<DirectionObject> directionList)
        {
            List<GameObject> tempList = new List<GameObject>();
            Transform baseTransform = DirectorSWRoot.Find(directionSW_BaseName);
            if (baseTransform == null || directionList == null) return tempList;

            GameObject directionSW_Base = baseTransform.gameObject;
            for (int i = 0; i < directionList.Count; i++)
            {
                if (directionList[i] == null) continue;
                // Prefabから生成しない理由として、Buttonコンポーネントの設定項目がPrefabにすると消えてしまう為
                GameObject temp = Instantiate(directionSW_Base, DirectorSWRoot, false);
                temp.SetActive(true);
                Text Title = temp.transform.Find("Button/Text").GetComponent<Text>();
                Title.text = directionList[i].directionName;
                tempList.Add(temp);
            }
            return tempList;
        }

        internal static void SettingDirection_UIController(SerializedObject targetUIControllerSO, in List<GameObject> directionSWList)
        {
            if (targetUIControllerSO == null) return;
            targetUIControllerSO.Update();
            SerializedProperty DirectionSWs = targetUIControllerSO.FindProperty("_directionSWs");
            if (DirectionSWs == null) return;

            if (directionSWList == null)
            {
                DirectionSWs.arraySize = 0;
                targetUIControllerSO.ApplyModifiedProperties();
                return;
            }
            DirectionSWs.arraySize = directionSWList.Count;

            for (int i = 0; i < directionSWList.Count; i++)
            {
                SerializedProperty element = DirectionSWs.GetArrayElementAtIndex(i);
                element.objectReferenceValue = directionSWList[i].transform.Find("Button").GetComponent<Button>();
            }
            targetUIControllerSO.ApplyModifiedProperties();
        }

        internal static void SettingDirection_Core(SerializedObject Core, in List<DirectionObject> directionList)
        {
            if (Core == null) return;
            Core.Update();
            SerializedProperty Directions = Core.FindProperty("_directions");

            if (directionList == null)
            {
                Directions.arraySize = 0;
                Core.ApplyModifiedProperties();
                return;
            }
            Directions.arraySize = directionList.Count;

            for (int i = 0; i < directionList.Count; i++)
            {
                SerializedProperty element = Directions.GetArrayElementAtIndex(i);
                element.objectReferenceValue = directionList[i];
            }
            Core.ApplyModifiedProperties();
        }

        private void SortDirections(Comparison<DirectionObject> comparison)
        {
            if (_directionList == null || _directionList.Count <= 1) return;
            _directionList.Sort(comparison);
            ApplyDirectionOrder(_directionController, directionController, _SSSCore, SSS_Core, _uiController, uiControllerSO, _directionList, _performerUIController, performerUIControllerSO);
        }

        public override void OnInspectorGUI()
        {
            // サムネイル
            if (Thumbnail != null) ImageOnGUI(Thumbnail);

            base.OnInspectorGUI();

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Cannot edit properties in Play mode", MessageType.Warning);
                return;
            }

            if (_SSSCore == null || SSS_Core == null) return;

            SSS_Core.Update();
            bool isJapanese = Application.systemLanguage == SystemLanguage.Japanese;

            // Inspectorに表示される内容をOverride
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 使用動画プレイヤー選択
            EditorGUILayout.BeginVertical(GUI.skin.box);
            SerializedProperty playerTypeProp = SSS_Core.FindProperty("_videoPlayerType");
            if (playerTypeProp != null)
            {
                string modeLabel = isJapanese ? "使用動画プレイヤー" : "Target Video Player";
                EditorGUILayout.PropertyField(playerTypeProp, new GUIContent(modeLabel));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // YamaPlayer 連携状況
            DrawIntegrationStatusBox(
                isJapanese ? "YamaPlayer連携状況" : "YamaPlayer Integration Status",
                _YamaPlayer_Controller != null
            );

            EditorGUILayout.Space();

            // iwaSync 連携状況
            DrawIntegrationStatusBox(
                isJapanese ? "iwaSync連携状況" : "iwaSync Integration Status",
                _Iwasync_Core != null
            );

            EditorGUILayout.Space();

            // 演者専用オブジェクト切り替え設定
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(
                isJapanese ? "演者専用オブジェクト切り替え (Performer Object Toggle)" : "Performer Object Toggle",
                EditorStyles.boldLabel
            );
            EditorGUI.indentLevel++;
            SerializedProperty enableObjsProp = SSS_Core.FindProperty("_performerEnableObjects");
            if (enableObjsProp != null)
            {
                string enableLabel = isJapanese
                    ? "演者のみ有効化するオブジェクト"
                    : "Objects Enabled Only for Performers";
                EditorGUILayout.PropertyField(enableObjsProp, new GUIContent(enableLabel), true);
                SanitizePerformerObjectArray(enableObjsProp, _SSSCore, _directionController);
            }
            SerializedProperty disableObjsProp = SSS_Core.FindProperty("_performerDisableObjects");
            if (disableObjsProp != null)
            {
                string disableLabel = isJapanese
                    ? "演者のみ無効化するオブジェクト"
                    : "Objects Disabled Only for Performers";
                EditorGUILayout.PropertyField(disableObjsProp, new GUIContent(disableLabel), true);
                SanitizePerformerObjectArray(disableObjsProp, _SSSCore, _directionController);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 演出一覧（ソート＆手動並び替え対応）
            EditorGUILayout.BeginVertical(GUI.skin.box);
            int dirCount = _directionList != null ? _directionList.Count : 0;
            string listLabel = isJapanese ? $"演出一覧 ({dirCount})" : $"Direction List ({dirCount})";
            isFoldout = EditorGUILayout.Foldout(isFoldout, listLabel, true);
            if (isFoldout && _directionList != null)
            {
                EditorGUILayout.Space(2f);

                // ソートボタン群
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(isJapanese ? "ソート (並び替え)" : "Sort Directions", EditorStyles.miniBoldLabel);

                // 1. 演出名ソート
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(isJapanese ? "演出名" : "Direction Name", GUILayout.Width(105f));
                if (GUILayout.Button(isJapanese ? "昇順 (A→Z)" : "Asc (A→Z)"))
                {
                    SortDirections(CompareByDirectionNameAsc);
                }
                if (GUILayout.Button(isJapanese ? "降順 (Z→A)" : "Desc (Z→A)"))
                {
                    SortDirections(CompareByDirectionNameDesc);
                }
                EditorGUILayout.EndHorizontal();

                // 2. GameObject名ソート
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(isJapanese ? "GameObject名" : "GameObject Name", GUILayout.Width(105f));
                if (GUILayout.Button(isJapanese ? "昇順 (A→Z)" : "Asc (A→Z)"))
                {
                    SortDirections(CompareByGameObjectNameAsc);
                }
                if (GUILayout.Button(isJapanese ? "降順 (Z→A)" : "Desc (Z→A)"))
                {
                    SortDirections(CompareByGameObjectNameDesc);
                }
                EditorGUILayout.EndHorizontal();

                // 3. Hierarchy順ソート
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(isJapanese ? "Hierarchy" : "Hierarchy", GUILayout.Width(105f));
                if (GUILayout.Button(isJapanese ? "Hierarchy順でソート" : "Sort by Hierarchy Order"))
                {
                    SortDirections(CompareByHierarchyOrder);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(4f);

                // 手動並び替えリスト (ReorderableList + ▲/▼ボタン)
                if (_reorderableList == null)
                {
                    SetupReorderableList();
                }

                _pendingMoveFrom = -1;
                _pendingMoveTo = -1;
                _reorderableList.DoLayoutList();

                if (_pendingMoveFrom >= 0 && _pendingMoveTo >= 0 &&
                    _pendingMoveFrom < _directionList.Count && _pendingMoveTo < _directionList.Count)
                {
                    DirectionObject movedItem = _directionList[_pendingMoveFrom];
                    _directionList.RemoveAt(_pendingMoveFrom);
                    _directionList.Insert(_pendingMoveTo, movedItem);
                    _reorderableList.index = _pendingMoveTo;
                    ApplyDirectionOrder(_directionController, directionController, _SSSCore, SSS_Core, _uiController, uiControllerSO, _directionList, _performerUIController, performerUIControllerSO);
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            if (SSS_Core.ApplyModifiedProperties())
            {
                UdonSharpEditorUtility.CopyProxyToUdon(_SSSCore);
            }
        }

        internal static void SanitizePerformerObjectArray(SerializedProperty arrayProp, SSSCore sssCore, DirectionController dirController)
        {
            if (arrayProp == null || !arrayProp.isArray) return;

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                SerializedProperty elem = arrayProp.GetArrayElementAtIndex(i);
                GameObject obj = elem.objectReferenceValue as GameObject;
                if (obj != null && IsProtectedSystemObject(obj, sssCore, dirController))
                {
                    Debug.LogWarning(
                        $"[Stage Sync System] '{obj.name}' はSSSの主要機能（または動画プレイヤー・演出オブジェクト）を含むため、" +
                        "演者専用オブジェクト切り替えの対象には設定できません。登録を解除しました。",
                        obj
                    );
                    elem.objectReferenceValue = null;
                }
            }
        }

        internal static bool IsProtectedSystemObject(GameObject obj, SSSCore sssCore, DirectionController dirController)
        {
            if (obj == null) return false;
            Transform t = obj.transform;

            // 1. SSSCore自身・子階層（Bridge等）・親階層の保護
            if (sssCore != null)
            {
                Transform coreTransform = sssCore.transform;
                if (t == coreTransform || coreTransform.IsChildOf(t) || t.IsChildOf(coreTransform)) return true;
            }

            // 2. DirectionControllerルート・UI階層・演者端末階層の保護
            if (dirController != null)
            {
                Transform dcTransform = dirController.transform;
                if (t == dcTransform || dcTransform.IsChildOf(t)) return true;

                Transform uiRoot = dcTransform.Find("UI");
                if (uiRoot != null && (t == uiRoot || uiRoot.IsChildOf(t) || t.IsChildOf(uiRoot))) return true;

                Transform perfUiRoot = dcTransform.Find(_performerRootObjectName);
                if (perfUiRoot != null && (t == perfUiRoot || perfUiRoot.IsChildOf(t) || t.IsChildOf(perfUiRoot))) return true;
            }

            // 3. SSS主要コンポーネント・動画プレイヤー・演出コンポーネントを含むオブジェクト（または親・子階層）の保護
            if (obj.GetComponentInChildren<SSSCore>(true) != null || obj.GetComponentInParent<SSSCore>(true) != null) return true;
            if (obj.GetComponentInChildren<SSSUIController>(true) != null || obj.GetComponentInParent<SSSUIController>(true) != null) return true;
            if (obj.GetComponentInChildren<SSSPerformerUIController>(true) != null || obj.GetComponentInParent<SSSPerformerUIController>(true) != null) return true;
            if (obj.GetComponentInChildren<SSSPerformerTrigger>(true) != null || obj.GetComponentInParent<SSSPerformerTrigger>(true) != null) return true;
            if (obj.GetComponentInChildren<SSSPerformerProximityChecker>(true) != null || obj.GetComponentInParent<SSSPerformerProximityChecker>(true) != null) return true;
            if (obj.GetComponentInChildren<SSSRuntimeManualSorter>(true) != null || obj.GetComponentInParent<SSSRuntimeManualSorter>(true) != null) return true;
            if (obj.GetComponentInChildren<SSSManualSortItem>(true) != null || obj.GetComponentInParent<SSSManualSortItem>(true) != null) return true;
            if (obj.GetComponentInChildren<DirectionController>(true) != null) return true;
            if (obj.GetComponentInChildren<DirectionObject>(true) != null || obj.GetComponentInParent<DirectionObject>(true) != null) return true;
            if (obj.GetComponentInChildren<DirectionSwitch>(true) != null || obj.GetComponentInParent<DirectionSwitch>(true) != null) return true;
            if (obj.GetComponentInChildren<PerformerChecker>(true) != null || obj.GetComponentInParent<PerformerChecker>(true) != null) return true;
            if (obj.GetComponentInChildren<UIOperatorChecker>(true) != null || obj.GetComponentInParent<UIOperatorChecker>(true) != null) return true;
            if (obj.GetComponentInChildren<Controller>(true) != null || obj.GetComponentInParent<Controller>(true) != null) return true;
            if (obj.GetComponentInChildren<VideoCore>(true) != null || obj.GetComponentInParent<VideoCore>(true) != null) return true;

            return false;
        }

        private void DrawIntegrationStatusBox(string title, bool isLinked)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            GUIContent content = new GUIContent(isLinked ? "Linked" : "Non-linked");
            Texture icon = isLinked
                ? EditorGUIUtility.IconContent("sv_icon_dot3_pix16_gizmo").image
                : EditorGUIUtility.IconContent("sv_icon_dot6_pix16_gizmo").image;

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle style = new GUIStyle
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.white;
            GUILayout.Label(content, style, GUILayout.Height(20));
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ImageOnGUI(Texture2D _image)
        {
            int _margin = 40;

            float aspectRatio = (float)Thumbnail.height / Thumbnail.width;
            float inspectorWidth = EditorGUIUtility.currentViewWidth - _margin;
            float imageHeight = inspectorWidth * aspectRatio;

            GUILayout.Label(Thumbnail, GUILayout.Width(inspectorWidth), GUILayout.Height(imageHeight));
        }
    }

    // Playモード突入時およびワールドビルド時に自動的にプレイヤー参照・Bridge・UIController・演出リストをセットアップ
    public class StageSyncSystemBuildProcess : IProcessSceneWithReport
    {
        // YamaPlayerBuildProcess (-10) より前に実行して親子関係変更前にControllerを確実に捕捉する
        public int callbackOrder => -20;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            SSSCore sssCore = StageSyncSystem_Editor.GetSSSCore();
            if (sssCore == null) return;

            Controller yamaController = StageSyncSystem_Editor.GetYamaPlayerController();
            VideoCore iwasyncCore = StageSyncSystem_Editor.GetIwasyncCore();

            SerializedObject sssCoreSO = new SerializedObject(sssCore);
            StageSyncSystem_Editor.SetVideoPlayers(yamaController, iwasyncCore, sssCoreSO);
            StageSyncSystem_Editor.SetupBridges(sssCore, yamaController, iwasyncCore, sssCoreSO);

            StageSyncSystem_Editor.GetSyncedDirectionList(sssCoreSO, out List<DirectionObject> directions);
            DirectionController dirController = sssCore.GetComponentInParent<DirectionController>();

            sssCoreSO.Update();
            StageSyncSystem_Editor.SanitizePerformerObjectArray(sssCoreSO.FindProperty("_performerEnableObjects"), sssCore, dirController);
            StageSyncSystem_Editor.SanitizePerformerObjectArray(sssCoreSO.FindProperty("_performerDisableObjects"), sssCore, dirController);
            sssCoreSO.ApplyModifiedProperties();

            if (dirController != null)
            {
                SerializedObject dirControllerSO = new SerializedObject(dirController);
                StageSyncSystem_Editor.SetupUIController(
                    dirController,
                    dirControllerSO,
                    sssCore,
                    sssCoreSO,
                    out SSSUIController uiController,
                    out SerializedObject uiControllerSO);
                StageSyncSystem_Editor.SetupPerformerPortableUI(
                    dirController,
                    dirControllerSO,
                    sssCore,
                    sssCoreSO,
                    uiController,
                    uiControllerSO,
                    out SSSPerformerUIController performerUI,
                    out SerializedObject performerUISO);
                StageSyncSystem_Editor.ApplyDirectionOrder(
                    dirController,
                    dirControllerSO,
                    sssCore,
                    sssCoreSO,
                    uiController,
                    uiControllerSO,
                    directions,
                    performerUI,
                    performerUISO);
            }
            else if (directions != null && directions.Count > 0)
            {
                StageSyncSystem_Editor.SettingDirection_Core(sssCoreSO, directions);
                UdonSharpEditorUtility.CopyProxyToUdon(sssCore);
            }
        }
    }

    /// <summary>
    /// シーン上に Stage Sync System が存在する場合、新規UdonSharpProgramAssetの生成・コンパイル完了後に
    /// 演者用操作端末 (Performer Portable UI) のHierarchy構築および演出リスト結線を自動実行するエディタ初期化クラス。
    /// </summary>
    [InitializeOnLoad]
    public static class SSSPerformerPortableUI_AutoSetup
    {
        private const string AutoSetupSessionKey = "SSS_PerformerPortableUI_AutoSetupDone_v5";

        static SSSPerformerPortableUI_AutoSetup()
        {
            EditorApplication.delayCall += TryAutoSetup;
            Undo.postprocessModifications += OnPostprocessModifications;
        }

        /// <summary>
        /// デバッグ用: Editモード中に Inspector 上で Sort 系 Toggle の isOn が変更された場合、
        /// Manual Toggle の状態に合わせて ManualSortDrawer の表示/非表示を同期する。
        /// </summary>
        private static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || modifications == null) return modifications;

            for (int i = 0; i < modifications.Length; i++)
            {
                PropertyModification pm = modifications[i].currentValue;
                if (pm != null && pm.target is Toggle && pm.propertyPath == "m_IsOn")
                {
                    EditorApplication.delayCall -= SyncManualSortDrawerVisibilityInEditMode;
                    EditorApplication.delayCall += SyncManualSortDrawerVisibilityInEditMode;
                    break;
                }
            }
            return modifications;
        }

        private static void SyncManualSortDrawerVisibilityInEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            SSSUIController[] uiControllers = UnityEngine.Object.FindObjectsByType<SSSUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SSSUIController ui in uiControllers)
            {
                if (ui == null || EditorUtility.IsPersistent(ui.gameObject)) continue;

                SerializedObject uiSO = new SerializedObject(ui);
                Toggle manualToggle = uiSO.FindProperty("_sortManualToggle")?.objectReferenceValue as Toggle;
                SSSRuntimeManualSorter sorter = uiSO.FindProperty("_runtimeManualSorter")?.objectReferenceValue as SSSRuntimeManualSorter;
                if (manualToggle == null || sorter == null) continue;

                bool show = manualToggle.isOn;
                GameObject drawerGO = sorter.gameObject;
                if (drawerGO.activeSelf != show)
                {
                    Undo.RecordObject(drawerGO, "Toggle Manual Sort Drawer");
                    drawerGO.SetActive(show);
                    EditorUtility.SetDirty(drawerGO);
                    if (drawerGO.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(drawerGO.scene);
                    }
                }
            }
        }

        private static void TryAutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoSetup;
                return;
            }

            if (SessionState.GetBool(AutoSetupSessionKey, false))
            {
                return;
            }

            DirectionController dirController = StageSyncSystem_Editor.GetSSSCore()?.GetComponentInParent<DirectionController>();
            if (dirController == null)
            {
                dirController = UnityEngine.Object.FindObjectOfType<DirectionController>(true);
            }
            if (dirController == null)
            {
                return;
            }

            if (!StageSyncSystem_Editor.EnsurePerformerProgramAssetsReady())
            {
                EditorApplication.delayCall += TryAutoSetup;
                return;
            }

            SessionState.SetBool(AutoSetupSessionKey, true);
            ExecuteSetup(dirController, false);
        }

        [MenuItem("Tools/Stage Sync System/Setup Performer Portable UI")]
        public static void SetupPerformerPortableUIMenu()
        {
            DirectionController dirController = UnityEngine.Object.FindObjectOfType<DirectionController>(true);
            if (dirController == null)
            {
                Debug.LogWarning("[Stage Sync System] シーン内に DirectionController が見つかりません。");
                return;
            }

            if (!StageSyncSystem_Editor.EnsurePerformerProgramAssetsReady())
            {
                SessionState.SetBool(AutoSetupSessionKey, false);
                EditorApplication.delayCall += TryAutoSetup;
                return;
            }

            ExecuteSetup(dirController, true);
        }

        private static void ExecuteSetup(DirectionController dirController, bool selectObject)
        {
            SSSCore sssCore = StageSyncSystem_Editor.GetSSSCore(dirController);
            if (sssCore == null) return;

            SerializedObject dirControllerSO = new SerializedObject(dirController);
            SerializedObject sssCoreSO = new SerializedObject(sssCore);

            Controller yamaController = StageSyncSystem_Editor.GetYamaPlayerController();
            VideoCore iwasyncCore = StageSyncSystem_Editor.GetIwasyncCore();

            StageSyncSystem_Editor.SetVideoPlayers(yamaController, iwasyncCore, sssCoreSO);
            StageSyncSystem_Editor.SetupBridges(sssCore, yamaController, iwasyncCore, sssCoreSO);
            StageSyncSystem_Editor.SetupUIController(
                dirController,
                dirControllerSO,
                sssCore,
                sssCoreSO,
                out SSSUIController uiController,
                out SerializedObject uiControllerSO);
            StageSyncSystem_Editor.SetupPerformerPortableUI(
                dirController,
                dirControllerSO,
                sssCore,
                sssCoreSO,
                uiController,
                uiControllerSO,
                out SSSPerformerUIController performerUI,
                out SerializedObject performerUISO);

            StageSyncSystem_Editor.GetSyncedDirectionList(sssCoreSO, out List<DirectionObject> directions);
            StageSyncSystem_Editor.ApplyDirectionOrder(
                dirController,
                dirControllerSO,
                sssCore,
                sssCoreSO,
                uiController,
                uiControllerSO,
                directions,
                performerUI,
                performerUISO);

            if (performerUI != null)
            {
                EditorSceneManager.MarkSceneDirty(performerUI.gameObject.scene);
                EditorSceneManager.SaveOpenScenes();
                if (selectObject)
                {
                    Selection.activeGameObject = performerUI.gameObject;
                }
                Debug.Log("[Stage Sync System] 'Performer Portable UI' の構築・演出リスト結線を完了し、シーンを保存しました。");
            }
        }
    }
}