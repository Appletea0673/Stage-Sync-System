using System;
using System.Collections.Generic;
using HoshinoLabs.IwaSync3.Udon;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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

        Texture2D Thumbnail;
        SSSCore _SSSCore;
        DirectionController _directionController;

        Controller _YamaPlayer_Controller = null;
        VideoCore _Iwasync_Core = null;
        List<DirectionObject> _directionList = new List<DirectionObject>();
        ReorderableList _reorderableList;

        const string _directionSWRootPath = "UI/Canvas/Panel/Scroll View/Scroll View/Viewport/Content";
        const string _directionSW_BaseName = "DirectionSW";
        const string _bridgeObjectName = "Bridge";

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

            // シーン上のオブジェクトである場合、Bridgeの自動構成とUIスイッチの生成を実行
            if (!EditorUtility.IsPersistent(_directionController.gameObject))
            {
                SetupBridges(_SSSCore, _YamaPlayer_Controller, _Iwasync_Core, SSS_Core);

                GetSyncedDirectionList(SSS_Core, out _directionList);
                ApplyDirectionOrder(_directionController, directionController, _SSSCore, SSS_Core, _directionList);
                SetupReorderableList();
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
                ApplyDirectionOrder(_directionController, directionController, _SSSCore, SSS_Core, _directionList);
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
            List<DirectionObject> directionList)
        {
            if (dirController == null || sssCore == null || directionList == null) return;

            List<GameObject> directionSWList = new List<GameObject>();
            Transform directorSWRoot = dirController.transform.Find(_directionSWRootPath);
            if (directorSWRoot != null)
            {
                DeleteDirectionSW(directorSWRoot, _directionSW_BaseName);
                directionSWList = GenerateDirectionSW(directorSWRoot, _directionSW_BaseName, directionList);
            }

            SettingDirection_Controller(dirControllerSO, directionSWList);
            SettingDirection_Core(sssCoreSO, directionList);

            UdonSharpEditorUtility.CopyProxyToUdon(dirController);
            UdonSharpEditorUtility.CopyProxyToUdon(sssCore);
            EditorUtility.SetDirty(dirController);
            EditorUtility.SetDirty(sssCore);
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

        internal static void SettingDirection_Controller(SerializedObject dirControllerSO, in List<GameObject> directionSWList)
        {
            if (dirControllerSO == null) return;
            dirControllerSO.Update();
            SerializedProperty DirectionSWs = dirControllerSO.FindProperty("_directionSWs");

            if (directionSWList == null)
            {
                DirectionSWs.arraySize = 0;
                dirControllerSO.ApplyModifiedProperties();
                return;
            }
            DirectionSWs.arraySize = directionSWList.Count;

            for (int i = 0; i < directionSWList.Count; i++)
            {
                SerializedProperty element = DirectionSWs.GetArrayElementAtIndex(i);
                element.objectReferenceValue = directionSWList[i].transform.Find("Button").GetComponent<Button>();
            }
            dirControllerSO.ApplyModifiedProperties();
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
            ApplyDirectionOrder(_directionController, directionController, _SSSCore, SSS_Core, _directionList);
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
                    ApplyDirectionOrder(_directionController, directionController, _SSSCore, SSS_Core, _directionList);
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

            // 2. DirectionControllerルート・UI階層の保護
            if (dirController != null)
            {
                Transform dcTransform = dirController.transform;
                if (t == dcTransform || dcTransform.IsChildOf(t)) return true;

                Transform uiRoot = dcTransform.Find("UI");
                if (uiRoot != null && (t == uiRoot || uiRoot.IsChildOf(t) || t.IsChildOf(uiRoot))) return true;
            }

            // 3. SSS主要コンポーネント・動画プレイヤー・演出コンポーネントを含むオブジェクト（または親・子階層）の保護
            if (obj.GetComponentInChildren<SSSCore>(true) != null || obj.GetComponentInParent<SSSCore>(true) != null) return true;
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

    // Playモード突入時およびワールドビルド時に自動的にプレイヤー参照・Bridge・演出リストをセットアップ
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
                StageSyncSystem_Editor.ApplyDirectionOrder(dirController, dirControllerSO, sssCore, sssCoreSO, directions);
            }
            else if (directions != null && directions.Count > 0)
            {
                StageSyncSystem_Editor.SettingDirection_Core(sssCoreSO, directions);
                UdonSharpEditorUtility.CopyProxyToUdon(sssCore);
            }
        }
    }
}