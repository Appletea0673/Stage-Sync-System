using HoshinoLabs.IwaSync3.Udon;
using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Reflection;
using UdonSharp;
using UnityEditor.TestTools.TestRunner.Api;

namespace AppleteaSystems.StageSyncSystem
{
    //Scriptに対するCustomEditorの設定
    [CustomEditor(typeof(DirectionController))]
    public class StageSyncSystem_Editor : Editor
    {
        SerializedProperty directionSWRootPath;
        SerializedProperty directionSW_BaseName;
        SerializedProperty _directions;

        DirectionController _directionController;
        Texture2D Thumbnail;

        SSSCore _SSS_Core;
        VideoCore _Iwasync_Core = null;
        DirectionObject[] _directionList;

        string _directionSWRootPath = "UI/Canvas/Panel/Scroll View/Scroll View/Viewport/Content";
        string _directionSW_BaseName = "DirectionSW";

        bool isFoldout;

        private void OnEnable()
        {
            //必要なデータ回収
            _directionController = (DirectionController)target;
            Thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("e53f95351d21c2041a16114955a6534b"));

            //CoreのSet
            _SSS_Core = GetSSSCore();
            _Iwasync_Core = GetIwasyncCore();
            if (_Iwasync_Core != null) SetIwasyncCore(_Iwasync_Core, _SSS_Core);
            SetSSSCore(_directionController, _SSS_Core);

            //演出の取得
            GetDirectionList(out _directionList);
            //UIスイッチへ反映
            //SW全削除
            Transform DirectorSWRoot = _directionController.transform.Find(_directionSWRootPath);
            if (_directionList != null)
            {
                DeleteDirectionSW(DirectorSWRoot, _directionSW_BaseName);
                GenerateDirectionSW(DirectorSWRoot, _directionSW_BaseName, _directionList);
            }
            SettingDirection_Controller(_directionController, _directionSWRootPath, _directionSW_BaseName);
            SettingDirection_Core(_SSS_Core, _directionList);
        }

        //各工程をClassにまとめて処理した方がいいと考えられる。
        private VideoCore GetIwasyncCore()
        {
            VideoCore[] coreTemp = FindObjectsByType<VideoCore>(FindObjectsSortMode.None);
            if (coreTemp != null) return coreTemp[0];
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

        private void SetIwasyncCore(VideoCore Iwasync_Core, SSSCore SSS_Core)
        {
            Undo.RecordObject(target, "SetIwasyncCore");
            SSS_Core.IwasyncCore = Iwasync_Core;
            EditorUtility.SetDirty(SSS_Core);
        }

        private void SetSSSCore(DirectionController directionController, SSSCore SSS_Core)
        {
            Undo.RecordObject(target, "SetSSSCore");
            directionController.SSS_core = SSS_Core;
            EditorUtility.SetDirty(directionController);
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
                Undo.RecordObject(target, "InstantiateSW");
                GameObject temp = Instantiate(directionSW_Base, DirectorSWRoot, false);
                temp.SetActive(true);
                //temp.name = i.ToString();
                Text Title = temp.transform.Find("Button/Text").GetComponent<Text>();
                Title.text = directionList[i].GetComponent<DirectionObject>().directionName;
                EditorUtility.SetDirty(temp);
                tempList.Add(temp);
            }
            return tempList;
        }


        //ここGenericでまとめれそう
        private void SettingDirection_Controller(DirectionController directionController, string directionSWRootPath, string directionSW_BaseName)
        {
            //データをすべて渡して実行時の計算を削減する予定だったが、Play時に代入値がnullになる問題があった為、Pathを渡してScript側で探索
            //if (directionSWList != null) directionController.GetComponent<DirectionController>().DirectionSWs = directionSWList.Select(directionSWList => directionSWList.transform.Find("Button").GetComponent<UnityEngine.UI.Button>()).ToArray();
            //else directionController.GetComponent<DirectionController>().DirectionSWs = null;
            //directionController.GetComponent<DirectionController>().DirectionSWRootPath = directionSWRootPath;
            //directionController.GetComponent<DirectionController>().DirectionSWBaseName = directionSW_BaseName;
            Undo.RecordObject(target, "SetDirection");
            directionController._directionSWRootPath = directionSWRootPath;
            directionController._directionSW_BaseName = directionSW_BaseName;
            EditorUtility.SetDirty(directionController);
        }

        private void SettingDirection_Core(SSSCore Core, in DirectionObject[] directionList)
        {
            if (directionList != null) Core.Directions = directionList.ToArray();
            else Core.Directions = null;
        }

        public override void OnInspectorGUI()
        {
            //サムネイル
            if (Thumbnail != null) ImageOnGUI(Thumbnail);

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