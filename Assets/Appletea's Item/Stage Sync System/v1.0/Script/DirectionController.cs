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
        public Button[] _directionSWs;

        public void OnButtonClicked()
        {
            var sender = FindSender();
            if (sender < 0)
                return;

            SSS_core.StartHandling(0);
        }

        int FindSender()
        {            
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
    }
}