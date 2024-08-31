using UnityEngine;
using VRC.SDKBase;
using HoshinoLabs.IwaSync3.Udon;
using UdonSharp;


namespace AppleteaSystems.StageSyncSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SSSCore : UdonSharpBehaviour
    {
        [Header("Main")]
        [SerializeField]
        VideoCore Iwasync_core;

        [SerializeField]
        [Tooltip("Milliseconds")]
        int AudienceDelays = 600;

        [SerializeField]
        [Tooltip("Milliseconds")]
        int VideoOffset = 10000;

        [SerializeField]
        DirectionObject[] _directions;

        [SerializeField]
        private float SyncAccuracy = 0.1f;

        //Local Valiable
        bool _isPerformer = false;
        [FieldChangeCallback(nameof(PlayingObjectIndex))]
        int _playingObjectIndex;
        const uint _MODE_VIDEO = 0x00000010;
        float _volumeTemp = 0.0f;

        #region Accessor

        public DirectionObject[] Directions
        {
            set { _directions = value; }
        }

        public VideoCore IwasyncCore
        {
            set { Iwasync_core = value; }
        }

        public bool IsPerformer
        {
            get => _isPerformer;
            set
            { 
                _isPerformer = value;
                Iwasync_core.offsetTime = GetOffsetTime();
            }
        }

        public int PlayingObjectIndex
        {
            get => _playingObjectIndex;
            set
            {
                _playingObjectIndex = value;
                TakeOwnership();
                RequestSerialization();
            }
        }
        #endregion

        void Start()
        {
            //PlayerbleDirector�̏����ݒ�
            foreach (DirectionObject _dir in _directions)
            {
                DirectionSetting(_dir, false);
                _dir.playableDirector.playOnAwake = true;
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player) 
        {
            if (Iwasync_core.isOwner)
            {
                Iwasync_core.RequestSerialization();
                RequestSerialization();
            }
            if (player == Networking.LocalPlayer) Iwasync_core.offsetTime = GetOffsetTime();
        }

        void Update()
        {
            if(_directions == null) return;
            if (Iwasync_core.isPlaying)
            {
                //���������m�F
                bool _loadedFlag = isLoaded();
                if (_loadedFlag)
                {
                    Iwasync_core.volume = _volumeTemp;
                    DirectionSetting(_directions[_playingObjectIndex], true);
                }
                else
                {
                    Iwasync_core.volume = 0.0f;
                    //�����Y������������܂œ��������
                    //float offset = (float)Iwasync_core.GetProgramVariable("_time") + (Networking.GetServerTimeInMilliseconds() - Iwasync_core.clockTime) / 1000f * Iwasync_core.speed;
                    //float offsetLocal = Iwasync_core.offsetTime / 1000f;
                    //if (SyncAccuracy <= Mathf.Abs(Iwasync_core.time - offsetLocal - offset)) Iwasync_core.offsetTime = GetOffsetTime();
                    Iwasync_core.offsetTime = GetOffsetTime();
                }
            }
            else
            {
                //���ʂ��ꎞ�ۑ�
                _volumeTemp = Iwasync_core.volume;

                foreach (DirectionObject _dir in _directions)
                {
                    DirectionSetting(_dir, false);
                }
            }
        }

        //�O���ďo���p
        public void StartHandling(int objectIndex)
        {
            //�Đ����̋Ȑݒ�����L
            PlayingObjectIndex = objectIndex;

            //����Đ��J�n
            Iwasync_core.TakeOwnership();
            Iwasync_core.PlayURL(_MODE_VIDEO, _directions[_playingObjectIndex].videoURL);
            Iwasync_core.RequestSerialization();

            //�x������&������������
            //Iwasync_core.offsetTime = GetOffsetTime();
        }

        private void DirectionSetting(DirectionObject _do, bool _toggle)
        {
            if (_toggle)
            {
                _do.playableDirector.Play();
                _do.gameObject.SetActive(true);
                _do.playableDirector.time = Iwasync_core.time;
            }
            else
            {
                _do.playableDirector.Stop();
                _do.gameObject.SetActive(false);
                _do.playableDirector.time = 0.0f;
            }
        }

        private int GetOffsetTime()
        {
            return -VideoOffset + (_isPerformer ? AudienceDelays : 0);
        }

        private bool isLoaded()
        {
            return Iwasync_core.clockTime - GetOffsetTime() < Networking.GetServerTimeInMilliseconds();
        }
        
        private void TakeOwnership()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
    }
}