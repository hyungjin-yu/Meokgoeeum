using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeBrushWeaponSetup (큐브 프로토타입에 실제 BrushWeapon 부착 — 검증용)
    /// [[changelog/2026-09-14_브러시웨폰-면검사]] — 실제 게임의 [[BrushWeapon]]이 큐브 면
    /// 프로토타입에서도 "같은 면인지" 검사가 잘 걸리는지 확인하기 위해, 지금까지 쓰던
    /// [[CubeFaceAttackTester]](F키, 단순 거리+면 체크)를 떼고 실제 무기로 교체합니다.
    /// 재실행해도 안전합니다.
    /// </summary>
    public static class CubeBrushWeaponSetup
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";

        [MenuItem("MG/큐브 프로토타입에 실제 BrushWeapon 부착")]
        public static void BuildFromCLI()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[CubeBrushWeaponSetup] {ScenePath}가 없습니다.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject playerGo = GameObject.Find("CubeWalker_Player");
            if (playerGo == null)
            {
                Debug.LogError("[CubeBrushWeaponSetup] CubeWalker_Player를 찾을 수 없습니다.");
                return;
            }

            var oldTester = playerGo.GetComponent<CubeFaceAttackTester>();
            if (oldTester != null) Object.DestroyImmediate(oldTester);

            var weapon = playerGo.GetComponent<BrushWeapon>();
            if (weapon == null) weapon = playerGo.AddComponent<BrushWeapon>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[CubeBrushWeaponSetup] 완료 — CubeFaceAttackTester 제거, 실제 BrushWeapon 부착함(좌클릭 3콤보).");
        }
    }
}
