using HoshinoLabs.IwaSync3.Udon;
using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

namespace AppleteaSystems.StageSyncSystem
{
    //Scriptに対するCustomEditorの設定
    [CustomEditor(typeof(DirectionController))]
    public class StageSyncSystem_Editor : Editor
    {
        //SerializeObjectでScriptのInstanceを管理
        SerializedObject SSS_Core;
        SerializedObject directionController;

        Texture2D Thumbnail;
        SSSCore _SSSCore;
        DirectionController _directionController;

        VideoCore _Iwasync_Core = null;
        DirectionObject[] _directionList;

        string _directionSWRootPath = "UI/Canvas/Panel/Scroll View/Scroll View/Viewport/Content";
        string _directionSW_BaseName = "DirectionSW";

        bool isFoldout;

        private void OnEnable()
        {
            if (Application.isPlaying) return;

            //必要なデータ回収
            _directionController = (DirectionController)target;
            directionController = new SerializedObject(_directionController);
            Thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("e53f95351d21c2041a16114955a6534b"));

            //Coreの取得
            _SSSCore = GetSSSCore();
            SSS_Core = new SerializedObject(_SSSCore);

            _Iwasync_Core = GetIwasyncCore();
            if (_Iwasync_Core != null) SetIwasyncCore(_Iwasync_Core, SSS_Core);
            SetSSSCore(directionController, _SSSCore);

            //演出の取得
            GetDirectionList(out _directionList);
            List<GameObject> directionSWList = new List<GameObject>();
            //UIスイッチへ反映
            //SW全削除
            Transform DirectorSWRoot = _directionController.transform.Find(_directionSWRootPath);
            if (_directionList != null)
            {
                DeleteDirectionSW(DirectorSWRoot, _directionSW_BaseName);
                directionSWList = GenerateDirectionSW(DirectorSWRoot, _directionSW_BaseName, _directionList);
            }
            SettingDirection_Controller(directionController, directionSWList);
            SettingDirection_Core(SSS_Core, _directionList);
        }

        private void OnValidate()
        {
            
            
            
        }

        //各工程をClassにまとめて処理した方がいいと考えられる。
        private VideoCore GetIwasyncCore()
        {
            VideoCore[] coreTemp = FindObjectsByType<VideoCore>(FindObjectsSortMode.None);
            if (coreTemp != null && coreTemp.Length > 0) return coreTemp[0];
            else return null;
        }

        private SSSCore GetSSSCore()
        {
            SSSCore[] coreTemp = FindObjectsByType<SSSCore>(FindObjectsSortMode.None);
            return coreTemp[0];
        }

        private void GetDirectionList(out DirectionObject[] directionList)
        {
            //非アクティブの演出も探索
            directionList = FindObjectsByType<DirectionObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(directionList, (x, y) => string.Compare(x.directionName, y.directionName));
        }

        private void SetIwasyncCore(VideoCore Iwasync_Core, SerializedObject SSS_Core)
        {
            SSS_Core.FindProperty("Iwasync_core").objectReferenceValue = Iwasync_Core;
            SSS_Core.ApplyModifiedProperties();
        }

        private void SetSSSCore(SerializedObject directionController, SSSCore SSS_Core)
        {
            directionController.FindProperty("SSS_core").objectReferenceValue = SSS_Core;
            directionController.ApplyModifiedProperties();
        }
        
        private void DeleteDirectionSW(Transform DirectorSWRoot, string directionSW_BaseName)
        {
            if (DirectorSWRoot == null) return;
            for (int i = DirectorSWRoot.childCount - 1; 0 <= i ; i--)
            {
                Transform item = DirectorSWRoot.GetChild(i);
                if (item.name == directionSW_BaseName)
                    continue;
                DestroyImmediate(item.gameObject);
            }
        }

        //元々InstantiateしたObjectを直接DirectionControllerに渡していたが、Play時に各要素がnullになる問題が発生したため使用しない方針
        private List<GameObject> GenerateDirectionSW(Transform DirectorSWRoot, string directionSW_BaseName, DirectionObject[] directionList)
        {
            List<GameObject> tempList = new List<GameObject>();
            GameObject directionSW_Base = DirectorSWRoot.Find(directionSW_BaseName).gameObject;
            for (int i = 0; i < directionList.Length; i++)
            {
                //Prefabから生成しない理由として、Buttonコンポーネントの設定項目がPrefabにすると消えてしまう為
                GameObject temp = Instantiate(directionSW_Base, DirectorSWRoot, false);
                temp.SetActive(true);
                //temp.name = i.ToString();
                Text Title = temp.transform.Find("Button/Text").GetComponent<Text>();
                Title.text = directionList[i].GetComponent<DirectionObject>().directionName;
                tempList.Add(temp);
            }
            return tempList; 
        }


        //ここGenericでまとめれそう
        private void SettingDirection_Controller(SerializedObject directionController, in List<GameObject> directionSWList)
        {
            SerializedProperty DirectionSWs = directionController.FindProperty("_directionSWs");

            if (directionSWList == null)
            {
                DirectionSWs.arraySize = 0;
                directionController.ApplyModifiedProperties();
                return;
            }
            DirectionSWs.arraySize = directionSWList.Count;

            // 各要素にアクセスして値を設定
            for (int i = 0; i < directionSWList.Count; i++)
            {
                SerializedProperty element = DirectionSWs.GetArrayElementAtIndex(i);
                element.objectReferenceValue = directionSWList[i].transform.Find("Button").GetComponent<UnityEngine.UI.Button>();
            }
            directionController.ApplyModifiedProperties();
        }

        private void SettingDirection_Core(SerializedObject Core, in DirectionObject[] directionList)
        {
            SerializedProperty Directions = Core.FindProperty("_directions");

            if (directionList == null)
            {
                Directions.arraySize = 0;
                Core.ApplyModifiedProperties();
                return;
            }
            Directions.arraySize = directionList.Length;

            // 各要素にアクセスして値を設定
            for (int i = 0; i < directionList.Length; i++)
            {
                SerializedProperty element = Directions.GetArrayElementAtIndex(i);
                element.objectReferenceValue = directionList[i];
            }
            Core.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            //サムネイル
            if (Thumbnail != null) ImageOnGUI(Thumbnail);

            base.OnInspectorGUI();

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Cannot edit properties in Play mode", MessageType.Warning);
                return;
            }

            //Inspectorに表示される内容をOverride
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginVertical(GUI.skin.box);

            string Label;
            if (Application.systemLanguage == SystemLanguage.Japanese) Label = "iwaSync連携状況";
            else Label = "iwaSync Integration Status";

            EditorGUILayout.LabelField(Label, EditorStyles.boldLabel);
            bool ExistIwasync = _Iwasync_Core != null;
            GUIContent content = new GUIContent(ExistIwasync ? "Linked" : "Non-linked");
            Texture icon = ExistIwasync ? EditorGUIUtility.IconContent("sv_icon_dot3_pix16_gizmo").image : EditorGUIUtility.IconContent("sv_icon_dot6_pix16_gizmo").image;
            
            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 18;
            style.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label(content, style, GUILayout.Height(20));
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (Application.systemLanguage == SystemLanguage.Japanese) Label = "演出一覧";
            else Label = "Direction List";

            isFoldout = EditorGUILayout.Foldout(isFoldout, Label);
            if (isFoldout)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < _directionList.Length; i++)
                {
                    EditorGUILayout.LabelField(i+1 + ". " + _directionList[i].directionName, EditorStyles.boldLabel);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private void ImageOnGUI(Texture2D _image)
        {
            int _margin = 40;

            // 画像のアスペクト比を維持するための高さを計算
            float aspectRatio = (float)Thumbnail.height / Thumbnail.width;
            float inspectorWidth = EditorGUIUtility.currentViewWidth - _margin;
            float imageHeight = inspectorWidth * aspectRatio;

            GUILayout.Label(Thumbnail, GUILayout.Width(inspectorWidth), GUILayout.Height(imageHeight));
        }
    }
}