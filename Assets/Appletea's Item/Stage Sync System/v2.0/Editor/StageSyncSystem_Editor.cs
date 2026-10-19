using System;
using System.Collections.Generic;
using HoshinoLabs.IwaSync3.Udon;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
        DirectionObject[] _directionList;

        const string _directionSWRootPath = "UI/Canvas/Panel/Scroll View/Scroll View/Viewport/Content";
        const string _directionSW_BaseName = "DirectionSW";
        const string _bridgeObjectName = "Bridge";

        bool isFoldout;

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

                GetDirectionList(out _directionList);
                List<GameObject> directionSWList = new List<GameObject>();
                Transform DirectorSWRoot = _directionController.transform.Find(_directionSWRootPath);
                if (_directionList != null && DirectorSWRoot != null)
                {
                    DeleteDirectionSW(DirectorSWRoot, _directionSW_BaseName);
                    directionSWList = GenerateDirectionSW(DirectorSWRoot, _directionSW_BaseName, _directionList);
                }
                SettingDirection_Controller(directionController, directionSWList);
                SettingDirection_Core(SSS_Core, _directionList);
            }
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

        internal static void GetDirectionList(out DirectionObject[] directionList)
        {
            // 非アクティブの演出も探索
            directionList = FindObjectsByType<DirectionObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(directionList, (x, y) => string.Compare(x.directionName, y.directionName, StringComparison.Ordinal));
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

        private void DeleteDirectionSW(Transform DirectorSWRoot, string directionSW_BaseName)
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

        private List<GameObject> GenerateDirectionSW(Transform DirectorSWRoot, string directionSW_BaseName, DirectionObject[] directionList)
        {
            List<GameObject> tempList = new List<GameObject>();
            Transform baseTransform = DirectorSWRoot.Find(directionSW_BaseName);
            if (baseTransform == null) return tempList;

            GameObject directionSW_Base = baseTransform.gameObject;
            for (int i = 0; i < directionList.Length; i++)
            {
                // Prefabから生成しない理由として、Buttonコンポーネントの設定項目がPrefabにすると消えてしまう為
                GameObject temp = Instantiate(directionSW_Base, DirectorSWRoot, false);
                temp.SetActive(true);
                Text Title = temp.transform.Find("Button/Text").GetComponent<Text>();
                Title.text = directionList[i].GetComponent<DirectionObject>().directionName;
                tempList.Add(temp);
            }
            return tempList;
        }

        private void SettingDirection_Controller(SerializedObject dirControllerSO, in List<GameObject> directionSWList)
        {
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

        private void SettingDirection_Core(SerializedObject Core, in DirectionObject[] directionList)
        {
            Core.Update();
            SerializedProperty Directions = Core.FindProperty("_directions");

            if (directionList == null)
            {
                Directions.arraySize = 0;
                Core.ApplyModifiedProperties();
                return;
            }
            Directions.arraySize = directionList.Length;

            for (int i = 0; i < directionList.Length; i++)
            {
                SerializedProperty element = Directions.GetArrayElementAtIndex(i);
                element.objectReferenceValue = directionList[i];
            }
            Core.ApplyModifiedProperties();
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

            // 演出一覧
            EditorGUILayout.BeginVertical(GUI.skin.box);
            string listLabel = isJapanese ? "演出一覧" : "Direction List";
            isFoldout = EditorGUILayout.Foldout(isFoldout, listLabel);
            if (isFoldout && _directionList != null)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < _directionList.Length; i++)
                {
                    if (_directionList[i] != null)
                    {
                        EditorGUILayout.LabelField((i + 1) + ". " + _directionList[i].directionName, EditorStyles.boldLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            if (SSS_Core.ApplyModifiedProperties())
            {
                UdonSharpEditorUtility.CopyProxyToUdon(_SSSCore);
            }
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

    // Playモード突入時およびワールドビルド時に自動的にプレイヤー参照・Bridgeをセットアップ
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

            StageSyncSystem_Editor.GetDirectionList(out DirectionObject[] directions);
            if (directions != null && directions.Length > 0)
            {
                SerializedProperty dirProp = sssCoreSO.FindProperty("_directions");
                if (dirProp != null && dirProp.arraySize == 0)
                {
                    dirProp.arraySize = directions.Length;
                    for (int i = 0; i < directions.Length; i++)
                    {
                        dirProp.GetArrayElementAtIndex(i).objectReferenceValue = directions[i];
                    }
                    sssCoreSO.ApplyModifiedProperties();
                }
            }

            UdonSharpEditorUtility.CopyProxyToUdon(sssCore);
        }
    }
}