using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// FootstepEffectInstaller (에디터 전용)
/// [[FootstepEffect]]를 [[SC_Game.unity]]의 "Player" 오브젝트에 컴포넌트로 추가합니다.
/// [[HintPopupManagerInstaller]]와 같은 패턴 — 재실행해도 안전(이미 붙어있으면 건너뜀).
/// </summary>
public static class FootstepEffectInstaller
{
    private const string ScenePath = "Assets/Scenes/SC_Game.unity";

    [MenuItem("MG/FootstepEffect 설치 (Player에 추가)")]
    public static void InstallFromMenu() => Install();

    public static void InstallFromCLI() => Install();

    private static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[FootstepEffectInstaller] Player를 씬에서 못 찾았습니다.");
            return;
        }

        if (player.GetComponent<FootstepEffect>() != null)
        {
            Debug.Log("[FootstepEffectInstaller] 이미 붙어있어서 건너뜁니다.");
            return;
        }

        player.AddComponent<FootstepEffect>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[FootstepEffectInstaller] Player에 FootstepEffect 추가 완료.");
    }
}
