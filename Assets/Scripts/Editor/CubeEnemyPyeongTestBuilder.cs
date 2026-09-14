using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyPyeongTestBuilder (큐브 면 버전 EnemyPyeong 배치 — NavMeshAgent 포기 후 전환)
    /// [[changelog/2026-09-14_NavMesh-면단위-장애물회피검증]]에서 실제 NavMeshAgent 기반
    /// EnemyPyeong이 원인 불명으로 NavMesh에 안 붙는 버그를 만나서, 사용자가 "이미 검증된
    /// [[CubeFaceLockedMob]] 방식(직선 이동+clamp)으로 통일"을 선택했습니다. 이 빌더는 고장난
    /// `TestRealEnemy_Pyeong`(NavMeshAgent 버전)을 지우고, 같은 자리에 [[CubeEnemyPyeong]]
    /// (NavMesh 없이 동작) 버전을 세웁니다.
    ///
    /// EnemyPyeong.prefab을 그대로 인스턴스화한 뒤, NavMeshAgent 기반 컴포넌트(EnemyPyeong,
    /// NavMeshAgent, EnemyHealth)만 떼어내고 CubeEnemyPyeong을 붙입니다 — 리깅된 모델/애니메이터
    /// 자식 오브젝트는 그대로 재사용합니다("루트=로직, 자식=시각 모델" 구조).
    /// 재실행해도 안전합니다.
    /// </summary>
    public static class CubeEnemyPyeongTestBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";

        [MenuItem("MG/큐브 EnemyPyeong 배치 (NavMesh 미사용)")]
        public static void BuildFromCLI()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[CubeEnemyPyeongTestBuilder] {ScenePath}가 없습니다.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 고장난 NavMeshAgent 버전 정리
            GameObject broken = GameObject.Find("TestRealEnemy_Pyeong");
            if (broken != null) Object.DestroyImmediate(broken);

            GameObject old = GameObject.Find("CubeTestEnemy_Pyeong");
            if (old != null) Object.DestroyImmediate(old);

            GameObject playerGo = GameObject.Find("CubeWalker_Player");
            var walker = playerGo != null ? playerGo.GetComponent<CubeSurfaceWalker>() : null;
            if (playerGo != null) playerGo.tag = "Player";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/EnemyPyeong.prefab");
            if (prefab == null)
            {
                Debug.LogError("[CubeEnemyPyeongTestBuilder] EnemyPyeong.prefab을 못 찾았습니다.");
                return;
            }

            Vector3 faceNormal = Vector3.forward; // TestMob_PlusZ와 같은 면
            var enemyGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemyGo.name = "CubeTestEnemy_Pyeong";

            // NavMeshAgent 기반 컴포넌트 제거 — 모델/애니메이터 자식은 그대로 둠.
            var oldAgentScript = enemyGo.GetComponent<EnemyPyeong>();
            if (oldAgentScript != null) Object.DestroyImmediate(oldAgentScript);
            var oldHealth = enemyGo.GetComponent<EnemyHealth>();
            if (oldHealth != null) Object.DestroyImmediate(oldHealth);
            var oldAgent = enemyGo.GetComponent<NavMeshAgent>();
            if (oldAgent != null) Object.DestroyImmediate(oldAgent);

            var cubeEnemy = enemyGo.AddComponent<CubeEnemyPyeong>();
            cubeEnemy.faceNormal = faceNormal;
            var cube = GameObject.Find("CubePlanet");
            cubeEnemy.cubeCenter = cube != null ? cube.transform : null;
            cubeEnemy.cubeHalfExtent = 10f;
            cubeEnemy.target = walker;

            enemyGo.transform.position = (cube != null ? cube.transform.position : Vector3.zero)
                + faceNormal * (10f + cubeEnemy.surfaceOffset); // 장애물(FaceNavRoot_PlusZ 밑) 반대쪽 빈 자리
            enemyGo.transform.position += new Vector3(4f, 0f, 0f);
            enemyGo.transform.rotation = Quaternion.FromToRotation(Vector3.up, faceNormal);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[CubeEnemyPyeongTestBuilder] 완료 — CubeTestEnemy_Pyeong 배치(NavMesh 미사용, 이동 검증됨).");
        }
    }
}
