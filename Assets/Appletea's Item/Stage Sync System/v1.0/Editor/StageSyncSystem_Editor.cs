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
    //Script�ɑ΂���CustomEditor�̐ݒ�
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
            //�K�v�ȃf�[�^���
            _directionController = (DirectionController)target;
            Thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("e53f95351d21c2041a16114955a6534b"));

            //Core��Set
            _SSS_Core = GetSSSCore();
            _Iwasync_Core = GetIwasyncCore();
            if (_Iwasync_Core != null) SetIwasyncCore(_Iwasync_Core, _SSS_Core);
            SetSSSCore(_directionController, _SSS_Core);

            //���o�̎擾
            GetDirectionList(out _directionList);
            //UI�X�C�b�`�֔��f
            //SW�S�폜
            Transform DirectorSWRoot = _directionController.transform.Find(_directionSWRootPath);
            if (_directionList != null)
            {
                DeleteDirectionSW(DirectorSWRoot, _directionSW_BaseName);
                GenerateDirectionSW(DirectorSWRoot, _directionSW_BaseName, _directionList);
            }
            SettingDirection_Controller(_directionController, _directionSWRootPath, _directionSW_BaseName);
            SettingDirection_Core(_SSS_Core, _directionList);
        }

        //�e�H����Class�ɂ܂Ƃ߂ď����������������ƍl������B
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
            //��A�N�e�B�u�̉��o���T��
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

        //���XInstantiate����Object�𒼐�DirectionController�ɓn���Ă������APlay���Ɋe�v�f��null�ɂȂ��肪�����������ߎg�p���Ȃ����j
        private List<GameObject> GenerateDirectionSW(Transform DirectorSWRoot, string directionSW_BaseName, DirectionObject[] directionList)
        {
            List<GameObject> tempList = new List<GameObject>();
            GameObject directionSW_Base = DirectorSWRoot.Find(directionSW_BaseName).gameObject;
            for (int i = 0; i < directionList.Length; i++)
            {
                //Prefab���琶�����Ȃ����R�Ƃ��āAButton�R���|�[�l���g�̐ݒ荀�ڂ�Prefab�ɂ���Ə����Ă��܂���
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


        //����Generic�ł܂Ƃ߂ꂻ��
        private void SettingDirection_Controller(DirectionController directionController, string directionSWRootPath, string directionSW_BaseName)
        {
            //�f�[�^�����ׂēn���Ď��s���̌v�Z���팸����\�肾�������APlay���ɑ���l��null�ɂȂ��肪�������ׁAPath��n����Script���ŒT��
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
            //�T���l�C��
            if (Thumbnail != null) ImageOnGUI(Thumbnail);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Cannot edit properties in Play mode", MessageType.Warning);
                return;
            }

            //Inspector�ɕ\���������e��Override
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginVertical(GUI.skin.box);

            string Label;
            if (Application.systemLanguage == SystemLanguage.Japanese) Label = "iwaSync�A�g��";
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

            if (Application.systemLanguage == SystemLanguage.Japanese) Label = "���o�ꗗ";
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

            // �摜�̃A�X�y�N�g����ێ����邽�߂̍������v�Z
            float aspectRatio = (float)Thumbnail.height / Thumbnail.width;
            float inspectorWidth = EditorGUIUtility.currentViewWidth - _margin;
            float imageHeight = inspectorWidth * aspectRatio;

            GUILayout.Label(Thumbnail, GUILayout.Width(inspectorWidth), GUILayout.Height(imageHeight));
        }
    }
}