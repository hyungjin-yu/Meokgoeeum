using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TutorialSkipManagerInstaller (에디터 전용)
/// [[TutorialSkipManager]]를 [[SC_Game.unity]]의 "GameSystems" 오브젝트에 컴포넌트로 추가합니다.
/// [[HintPopupManagerInstaller]]와 같은 이유 — 기존 전역 시스템들이 전부 그 오브젝트 하나에
/// 모여있는 패턴을 그대로 따랐습니다.
///
/// 재실행해도 안전 — 이미 붙어있으면 건너뜁니다.
/// </summary>
public static class TutorialSkipManagerInstaller
{
    private const string ScenePath = "Assets/Scenes/SC_Game.unity";

    [MenuItem("MG/TutorialSkipManager 설치 (GameSystems에 추가)")]
    public static void InstallFromMenu() => Install();

    public static void InstallFromCLI() => Install();

    private static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject gameSystems = GameObject.Find("GameSystems");
        if (gameSystems == null)
        {
            Debug.LogError("[TutorialSkipManagerInstaller] GameSystems를 씬에서 못 찾았습니다.");
            return;
        }

        if (gameSystems.GetComponent<TutorialSkipManager>() != null)
        {
            Debug.Log("[TutorialSkipManagerInstaller] 이미 붙어있어서 건너뜁니다.");
            return;
        }

        gameSystems.AddComponent<TutorialSkipManager>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[TutorialSkipManagerInstaller] GameSystems에 TutorialSkipManager 추가 완료.");
    }
}
