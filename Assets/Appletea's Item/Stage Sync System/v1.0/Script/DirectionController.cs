using UnityEngine;
using UdonSharp;
using UnityEngine.UI;

namespace AppleteaSystems.StageSyncSystem
{
    //[ExecuteInEditMode]
    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class DirectionController : UdonSharpBehaviour
    {
        [Header("Base")]
        public SSSCore SSS_core;

        //Serialize化しつつInspectorから隠す措置
        public string _directionSWRootPath;
        public string _directionSW_BaseName;

        public string DirectionSWRootPath
        {
            set { _directionSWRootPath = value; }
        }

        public string DirectionSWBaseName
        {
            set { _directionSW_BaseName = value; }
        }

        public void OnButtonClicked()
        {
            var sender = FindSender();
            if (sender < 0)
                return;

            SSS_core.StartHandling(sender);
        }

        int FindSender()
        {
            Transform rootTransform = this.transform.Find(_directionSWRootPath);
            Button[] _directionSWs;
            GetChildrenByOrder(rootTransform, out _directionSWs);
            if (_directionSWs != null)
            {
                for (int i = 0; i < _directionSWs.Length; i++)
                {
                    if (!_directionSWs[i].enabled)
                        return i;
                }
            }
            return -1;
        }
        void GetChildrenByOrder(Transform root, out Button[] list)
        {
            Button[] temp = root.GetComponentsInChildren<Button>();
            Debug.Log(temp);
            
            list = new Button[temp.Length];
            Debug.Log(list);
            int index = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                Button childComponent = root.GetChild(i).GetChild(0).GetComponent<Button>();
                if (childComponent != null)
                {
                    if (root.GetChild(i).name == _directionSW_BaseName)
                        continue;
                    list[index] = childComponent;
                    index++;
                }
            }
        }
    }
}