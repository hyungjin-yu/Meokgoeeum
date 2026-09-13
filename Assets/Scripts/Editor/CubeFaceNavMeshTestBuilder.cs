using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceNavMeshTestBuilder (면 단위 NavMesh 베이크 — 장애물 회피 검증)
    /// [[changelog/2026-09-12_NavMesh-비Y업면-검증]]에서 "NavMeshSurface를 회전된 부모에 붙이면
    /// 비Y업 면도 구워진다"까지는 확인했지만, 그건 장애물 없는 평면이었습니다. 실제 던전 방에는
    /// 장애물이 있으므로, 이번엔 **장애물을 실제로 피해서 길을 찾는지**까지 검증합니다.
    ///
    /// 핵심 기법: NavMeshSurface는 자기가 붙은 오브젝트의 로컬 up을 기준으로 슬로프를 계산하는
    /// 것으로 보입니다(어제 검증) — 그래서 큐브 오브젝트 자체에 붙이면 안 되고(큐브는 회전 안 된
    /// 하나의 transform이라 옆면이 여전히 "벽"으로 판정됨), 면마다 별도의 회전된 빈 오브젝트를
    /// 만들어 그 밑에 바닥/장애물을 자식으로 두고 그 오브젝트에 NavMeshSurface를 붙여야 합니다.
    ///
    /// 재실행해도 안전합니다(기존 FaceNavRoot_PlusZ를 지우고 다시 만듦).
    /// </summary>
    public static class CubeFaceNavMeshTestBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";
        private const float CubeHalfExtent = 10f; // CubePrototypeBuilder와 반드시 동일해야 함

        [MenuItem("MG/큐브 면 NavMesh 장애물회피 테스트")]
        public static void BuildFromCLI()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[CubeFaceNavMeshTestBuilder] {ScenePath}가 없습니다.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject old = GameObject.Find("FaceNavRoot_PlusZ");
            if (old != null) Object.DestroyImmediate(old);

            GameObject cube = GameObject.Find("CubePlanet");
            if (cube == null)
            {
                Debug.LogError("[CubeFaceNavMeshTestBuilder] CubePlanet을 찾을 수 없습니다.");
                return;
            }

            Vector3 faceNormal = Vector3.forward; // TestMob_PlusZ와 같은 면

            // 면 전용 회전된 루트 — 이 오브젝트의 로컬 up이 faceNormal이 되도록 회전.
            var faceRoot = new GameObject("FaceNavRoot_PlusZ");
            faceRoot.transform.position = cube.transform.position + faceNormal * CubeHalfExtent;
            faceRoot.transform.rotation = Quaternion.FromToRotation(Vector3.up, faceNormal);

            // 바닥 — 면 전체(20x20)를 덮는 평면. Unity 기본 Plane은 10x10이므로 스케일 2배.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "NavFloor";
            floor.transform.SetParent(faceRoot.transform, worldPositionStays: false);
            floor.transform.localScale = Vector3.one * (CubeHalfExtent * 2f / 10f);
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);

            // 장애물 — 면 한가운데를 막는 기둥. 플레이어/몹이 지나가려면 반드시 돌아가야 함.
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "NavObstacle";
            obstacle.transform.SetParent(faceRoot.transform, worldPositionStays: false);
            obstacle.transform.localPosition = new Vector3(0f, 1.5f, 0f); // 로컬 up 방향으로 살짝 띄움(바닥 위)
            obstacle.transform.localScale = new Vector3(4f, 3f, 4f);
            GameObjectUtility.SetStaticEditorFlags(obstacle, StaticEditorFlags.NavigationStatic);

            var surface = faceRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[CubeFaceNavMeshTestBuilder] 완료 — FaceNavRoot_PlusZ에 4x4 장애물 포함 NavMesh 베이크함.");
        }
    }
}
