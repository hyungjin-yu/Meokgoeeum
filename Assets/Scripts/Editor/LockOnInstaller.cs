using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// LockOnInstaller (에디터 전용)
    /// [[LockOnController]]를 "Player"에, [[LockOnMarkerUI]]를 "GameSystems"에 각각 컴포넌트로
    /// 추가합니다([[SC_Game.unity]]). [[HintPopupManagerInstaller]]와 같은 패턴 — 재실행해도
    /// 안전(이미 붙어있으면 건너뜀).
    /// </summary>
    public static class LockOnInstaller
    {
        private const string ScenePath = "Assets/Scenes/SC_Game.unity";

        [MenuItem("MG/LockOn 설치 (Player + GameSystems)")]
        public static void InstallFromMenu() => Install();

        public static void InstallFromCLI() => Install();

        private static void Install()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool changed = false;

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[LockOnInstaller] Player를 씬에서 못 찾았습니다.");
            }
            else
            {
                LockOnController lockOn = player.GetComponent<LockOnController>();
                bool isNew = lockOn == null;
                if (isNew) lockOn = player.AddComponent<LockOnController>();

                // 2026-08-31 실측: 기본값(10/14m)이 방A 크기(대략 10x10)보다 넓어서 방 안에서는
                // 자동 해제가 거의 안 일어남 — 5m로 낮춰달라는 요청에 맞춰 lockOnRadius도 같이
                // 줄임(releaseRadius보다 항상 작아야 하는 히스테리시스 관계 유지).
                // 씬에 이미 붙어있어도 매번 이 값으로 동기화합니다(컴포넌트를 새로 만들 때만 반영되는
                // C# 기본값과 달리, 기존 인스턴스는 재실행해도 값이 갱신되게).
                lockOn.lockOnRadius = 3.5f;
                lockOn.releaseRadius = 5f;

                changed = true;
                Debug.Log(isNew ? "[LockOnInstaller] Player에 LockOnController 추가 완료." : "[LockOnInstaller] LockOnController는 이미 붙어있어서 값만 동기화했습니다.");
            }

            GameObject gameSystems = GameObject.Find("GameSystems");
            if (gameSystems == null)
            {
                Debug.LogError("[LockOnInstaller] GameSystems를 씬에서 못 찾았습니다.");
            }
            else if (gameSystems.GetComponent<LockOnMarkerUI>() != null)
            {
                Debug.Log("[LockOnInstaller] LockOnMarkerUI는 이미 붙어있어서 건너뜁니다.");
            }
            else
            {
                gameSystems.AddComponent<LockOnMarkerUI>();
                changed = true;
                Debug.Log("[LockOnInstaller] GameSystems에 LockOnMarkerUI 추가 완료.");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
