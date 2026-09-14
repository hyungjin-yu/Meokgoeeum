using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyRemainingTestBuilder (나머지 4종 큐브 몹 배치 — 원/분/흡/광)
    /// [[changelog/2026-09-14_큐브몹-나머지4종이식]]에서 로직만 리플렉션으로 검증했던 4종을
    /// [[CubeEnemyPyeongTestBuilder]]와 같은 방식으로 실제 프리팹에 연결해 배치합니다 —
    /// 모델/애니메이터는 재사용하고 NavMeshAgent 계열 컴포넌트만 교체.
    ///
    /// +Z 면(기존 [[CubeEnemyPyeong]]이 있는 면) 안에서, 가운데 장애물과 Pyeong 자리를 피해
    /// 네 귀퉁이에 하나씩 배치합니다. 재실행해도 안전합니다.
    /// </summary>
    public static class CubeEnemyRemainingTestBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";
        private const float CubeHalfExtent = 10f;

        [MenuItem("MG/큐브 나머지 4종 몹 배치 (원분흡광)")]
        public static void BuildFromCLI()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[CubeEnemyRemainingTestBuilder] {ScenePath}가 없습니다.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject cube = GameObject.Find("CubePlanet");
            GameObject playerGo = GameObject.Find("CubeWalker_Player");
            if (cube == null || playerGo == null)
            {
                Debug.LogError("[CubeEnemyRemainingTestBuilder] CubePlanet/CubeWalker_Player를 찾을 수 없습니다.");
                return;
            }
            playerGo.tag = "Player";
            var walker = playerGo.GetComponent<CubeSurfaceWalker>();

            Vector3 faceNormal = Vector3.forward;
            Vector3 facePos = cube.transform.position + faceNormal * CubeHalfExtent;

            BuildOne<EnemyWon, CubeEnemyWon>("CubeTestEnemy_Won", "Assets/Scenes/EnemyWon.prefab",
                facePos + new Vector3(-6f, 6f, 0f), faceNormal, cube.transform, walker);
            BuildOne<EnemyBun, CubeEnemyBun>("CubeTestEnemy_Bun", "Assets/Scenes/EnemyBun.prefab",
                facePos + new Vector3(-6f, -6f, 0f), faceNormal, cube.transform, walker);
            BuildOne<EnemyHeup, CubeEnemyHeup>("CubeTestEnemy_Heup", "Assets/Scenes/EnemyHeup.prefab",
                facePos + new Vector3(6f, 6f, 0f), faceNormal, cube.transform, walker);
            BuildOne<EnemyGwang, CubeEnemyGwang>("CubeTestEnemy_Gwang", "Assets/Scenes/EnemyGwang.prefab",
                facePos + new Vector3(6f, -6f, 0f), faceNormal, cube.transform, walker);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[CubeEnemyRemainingTestBuilder] 완료 — 원/분/흡/광 4종 전부 +Z 면에 배치함(F키로 전부 공격 가능).");
        }

        private static void BuildOne<TOldEnemy, TNewEnemy>(string name, string prefabPath, Vector3 worldPos,
            Vector3 faceNormal, Transform cubeCenter, CubeSurfaceWalker walker)
            where TOldEnemy : EnemyBase
            where TNewEnemy : CubeEnemyBase
        {
            GameObject old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[CubeEnemyRemainingTestBuilder] {prefabPath}를 못 찾았습니다.");
                return;
            }

            var enemyGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemyGo.name = name;

            var oldScript = enemyGo.GetComponent<TOldEnemy>();
            if (oldScript != null) Object.DestroyImmediate(oldScript);
            var oldHealth = enemyGo.GetComponent<EnemyHealth>();
            if (oldHealth != null) Object.DestroyImmediate(oldHealth);
            var oldAgent = enemyGo.GetComponent<NavMeshAgent>();
            if (oldAgent != null) Object.DestroyImmediate(oldAgent);

            var newEnemy = enemyGo.AddComponent<TNewEnemy>();
            newEnemy.faceNormal = faceNormal;
            newEnemy.cubeCenter = cubeCenter;
            newEnemy.cubeHalfExtent = CubeHalfExtent;
            newEnemy.target = walker;

            enemyGo.transform.position = worldPos + faceNormal * newEnemy.surfaceOffset;
            enemyGo.transform.rotation = Quaternion.FromToRotation(Vector3.up, faceNormal);
        }
    }
}
