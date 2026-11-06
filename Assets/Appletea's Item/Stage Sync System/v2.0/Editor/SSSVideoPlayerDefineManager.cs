using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AppleteaSystems.StageSyncSystem
{
    /// <summary>
    /// プロジェクト内に YamaPlayer (net.kwxxw.yama-stream) または iwaSync3 (HoshinoLabs.IwaSync3)
    /// が導入されているかを検出し、対応する Scripting Define Symbols (SSS_YAMAPLAYER / SSS_IWASYNC)
    /// を自動的に設定・管理するクラス。
    /// 各動画プレイヤーが存在しないプロジェクトでもコンパイルエラーを発生させずに安全に動作させる。
    /// </summary>
    [InitializeOnLoad]
    public static class SSSVideoPlayerDefineManager
    {
        public const string YamaPlayerDefine = "SSS_YAMAPLAYER";
        public const string IwaSyncDefine = "SSS_IWASYNC";

        private const string YamaControllerTypeName = "Yamadev.YamaStream.Controller";
        private const string IwaSyncCoreTypeName = "HoshinoLabs.IwaSync3.Udon.VideoCore";

        static SSSVideoPlayerDefineManager()
        {
            EditorApplication.delayCall += CheckAndUpdateDefines;
        }

        [MenuItem("Tools/Stage Sync System/Check Video Player Dependencies")]
        public static void CheckAndUpdateDefines()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            bool hasYama = TypeExists(YamaControllerTypeName);
            bool hasIwa = TypeExists(IwaSyncCoreTypeName);

            bool changed = false;

            // 主要なBuildTargetGroup（Standaloneおよび現在の選択プラットフォーム）に対してシンボルを同期
            BuildTargetGroup currentGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            changed |= SyncDefinesForGroup(currentGroup, hasYama, hasIwa);

            if (currentGroup != BuildTargetGroup.Standalone)
            {
                changed |= SyncDefinesForGroup(BuildTargetGroup.Standalone, hasYama, hasIwa);
            }

            if (changed)
            {
                Debug.Log($"[Stage Sync System] Video Player Defines Updated: {YamaPlayerDefine}={(hasYama ? "ON" : "OFF")}, {IwaSyncDefine}={(hasIwa ? "ON" : "OFF")}");
            }
        }

        public static bool HasYamaPlayerPackage()
        {
            return TypeExists(YamaControllerTypeName);
        }

        public static bool HasIwaSyncPackage()
        {
            return TypeExists(IwaSyncCoreTypeName);
        }

        private static bool TypeExists(string fullTypeName)
        {
            Type t = Type.GetType(fullTypeName);
            if (t != null) return true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(fullTypeName) != null)
                    {
                        return true;
                    }
                }
                catch
                {
                    // アセンブリの読み込み制限等は無視
                }
            }
            return false;
        }

        private static bool SyncDefinesForGroup(BuildTargetGroup group, bool hasYama, bool hasIwa)
        {
            if (group == BuildTargetGroup.Unknown) return false;

            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            string currentDefinesStr = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            string[] tokens = currentDefinesStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            HashSet<string> defines = new HashSet<string>(tokens);
            bool modified = false;

            if (hasYama && !defines.Contains(YamaPlayerDefine))
            {
                defines.Add(YamaPlayerDefine);
                modified = true;
            }
            else if (!hasYama && defines.Contains(YamaPlayerDefine))
            {
                defines.Remove(YamaPlayerDefine);
                modified = true;
            }

            if (hasIwa && !defines.Contains(IwaSyncDefine))
            {
                defines.Add(IwaSyncDefine);
                modified = true;
            }
            else if (!hasIwa && defines.Contains(IwaSyncDefine))
            {
                defines.Remove(IwaSyncDefine);
                modified = true;
            }

            if (modified)
            {
                string newDefinesStr = string.Join(";", defines);
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefinesStr);
                return true;
            }

            return false;
        }
    }
}
