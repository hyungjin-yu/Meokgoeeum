using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// DebugCombatHudInstaller (에디터 전용)
    /// [[DebugCombatHud]]를 SC_Game.unity의 "GameSystems"에 설치합니다. 다른 설치 스크립트들과
    /// 같은 관례(idempotent — 이미 있으면 다시 안 붙임).
    /// </summary>
    public static class DebugCombatHudInstaller
    {
        private const string ScenePath = "Assets/Scenes/SC_Game.unity";

        [MenuItem("MG/디버그 전투 HUD 설치")]
        public static void InstallFromMenu() => Install();

        public static void InstallFromCLI() => Install();

        private static void Install()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject gameSystems = GameObject.Find("GameSystems");
            if (gameSystems == null)
            {
                Debug.LogError("[DebugCombatHudInstaller] GameSystems를 씬에서 못 찾았습니다.");
                return;
            }

            if (gameSystems.GetComponent<DebugCombatHud>() == null)
            {
                gameSystems.AddComponent<DebugCombatHud>();
                Debug.Log("[DebugCombatHudInstaller] DebugCombatHud 추가함.");
            }
            else
            {
                Debug.Log("[DebugCombatHudInstaller] DebugCombatHud 이미 있음 — 건너뜀.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
