using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceMobTestBuilder (면 고정 몹 + 공격 판정 테스트 배치 — 에디터 전용)
    /// 사용자 요구사항 검증: "몹들은 면과 면을 이동할 수 없고, 캐릭터도 다른 면에 있는 몹은
    /// 공격이 불가능하다." [[CubeFaceLockedMob]]/[[CubeFaceAttackTester]]를 기존
    /// SC_CubePrototype.unity에 추가만 합니다 — [[CubePrototypeBuilder]]와 달리 씬을 통째로
    /// 지우지 않습니다(이미 배치된 [[CubeStreetDressingBuilder]] 결과물을 보존하기 위함).
    ///
    /// 테스트 몹은 도로가 깔린 윗면(+Y)과 맞닿은 +Z 면 한가운데에 둡니다 — 플레이어가
    /// 스폰 지점(윗면, +Z 모서리 근처)에서 몇 걸음만 걸으면 자연스럽게 몹의 면으로 넘어갈 수
    /// 있습니다.
    /// 재실행해도 안전합니다(기존 TestMob_PlusZ를 지우고 다시 만듦).
    /// </summary>
    public static class CubeFaceMobTestBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";
        private const float CubeHalfExtent = 10f; // CubePrototypeBuilder와 반드시 동일해야 함

        [MenuItem("MG/큐브 면고정 몹 테스트 배치")]
        public static void BuildFromCLI()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[CubeFaceMobTestBuilder] {ScenePath}가 없습니다. 먼저 CubePrototypeBuilder를 실행하세요.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject old = GameObject.Find("TestMob_PlusZ");
            if (old != null) Object.DestroyImmediate(old);

            GameObject cube = GameObject.Find("CubePlanet");
            GameObject playerGo = GameObject.Find("CubeWalker_Player");
            if (cube == null || playerGo == null)
            {
                Debug.LogError("[CubeFaceMobTestBuilder] CubePlanet/CubeWalker_Player를 찾을 수 없습니다.");
                return;
            }

            var walker = playerGo.GetComponent<CubeSurfaceWalker>();

            // 테스트 몹 — +Z 면 한가운데, 도로가 깔린 +Y 면 바로 옆 (플레이어 스폰 지점에서 가까움).
            Vector3 faceNormal = Vector3.forward;
            GameObject mob = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mob.name = "TestMob_PlusZ";
            var mobRenderer = mob.GetComponent<Renderer>();
            var mobMat = new Material(mobRenderer.sharedMaterial);
            mobMat.color = new Color(0.8f, 0.15f, 0.15f); // 빨강 — 몹임을 눈으로 바로 구분
            mobRenderer.sharedMaterial = mobMat;

            mob.transform.position = cube.transform.position + faceNormal * (CubeHalfExtent + 1f);
            mob.transform.rotation = Quaternion.FromToRotation(Vector3.up, faceNormal);

            var mobComp = mob.AddComponent<CubeFaceLockedMob>();
            mobComp.faceNormal = faceNormal;
            mobComp.cubeCenter = cube.transform;
            mobComp.cubeHalfExtent = CubeHalfExtent;
            mobComp.target = walker;

            // 플레이어 쪽 — 공격 판정 테스터 부착(이미 있으면 재사용).
            var attackTester = playerGo.GetComponent<CubeFaceAttackTester>();
            if (attackTester == null) attackTester = playerGo.AddComponent<CubeFaceAttackTester>();
            attackTester.walker = walker;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[CubeFaceMobTestBuilder] 완료 — TestMob_PlusZ 배치, CubeFaceAttackTester 부착(F키로 공격).");
        }
    }
}
