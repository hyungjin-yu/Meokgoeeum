using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubePrototypeBuilder (큐브 표면 보행 프로토타입 씬 생성 — 에디터 전용)
    /// 사용자 아이디어("에버플래닛처럼 큐브 표면을 걷고, 모서리를 넘으면 중력이 도는 카메라")를
    /// 검증하기 위한 최소 테스트 씬. 실제 게임 씬은 전혀 안 건드립니다 — 이 아이디어가
    /// "느낌이 좋다"고 확인되기 전까지는 독립된 프로토타입으로만 존재합니다.
    ///
    /// 재실행해도 안전합니다.
    /// </summary>
    public static class CubePrototypeBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";
        private const float CubeHalfExtent = 5f;

        [MenuItem("MG/큐브 표면 보행 프로토타입 생성")]
        public static void BuildFromCLI()
        {
            Scene scene;
            if (System.IO.File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    Object.DestroyImmediate(root);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            // 조명
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 큐브 "행성"
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "CubePlanet";
            cube.transform.position = Vector3.zero;
            cube.transform.localScale = Vector3.one * (CubeHalfExtent * 2f);

            // 플레이어 — 윗면(Y = +half) 위, +Z쪽 모서리 근처에 배치해서 몇 걸음만 걸어도
            // 모서리를 넘어가도록 함.
            // ⚠️ CharacterController는 안 씁니다 — [[CubeSurfaceWalker]] 주석 참고
            // (매 프레임 임의 축 재정렬과 CC의 내부 "위 방향" 가정이 충돌해서 엉뚱하게 미끄러짐).
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "CubeWalker_Player";

            float capsuleHalfHeight = 1f;
            player.transform.position = new Vector3(0f, CubeHalfExtent + capsuleHalfHeight, CubeHalfExtent - 1.5f);
            player.transform.rotation = Quaternion.identity;

            var walker = player.AddComponent<CubeSurfaceWalker>();
            walker.cubeCenter = cube.transform;
            walker.cubeHalfExtent = CubeHalfExtent;

            // 트리거 이벤트가 발생하려면 둘 중 하나는 Rigidbody가 있어야 합니다 — 플레이어 쪽에
            // 킨매틱 Rigidbody를 답니다(직접 transform을 옮기는 방식이라 물리 힘의 영향은 안 받아야 함).
            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // 6개 면 트리거 — [[CubeFaceZone]] 참고. 각 면의 실제 넓이(가로세로 CubeHalfExtent*2)만큼
            // 커버하고, 표면에서 바깥으로 zoneThickness만큼 두께를 줘서 플레이어가 그 안에 있게 함.
            BuildFaceZones();

            // 카메라
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = player.transform.position + Vector3.up * 3f - Vector3.forward * 6f;
            var camScript = camGo.AddComponent<CubeSurfaceCamera>();
            camScript.target = walker;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[CubePrototypeBuilder] 완료 — {ScenePath} 생성/갱신함. 플레이어 시작 위치={player.transform.position}");
        }

        private static void BuildFaceZones()
        {
            // ⚠️ 2026-09-12 발견 — 처음엔 존을 CubePlanet(localScale=10,10,10)의 자식으로 붙였는데,
            // BoxCollider.size는 부모의 스케일까지 곱해진 "로시(lossy) 스케일"로 월드에 적용되기
            // 때문에, size=(4,10,10)로 지정해도 실제로는 (40,100,100)짜리 거대한 트리거가 되어
            // 6개 존이 전부 서로 잔뜩 겹쳐버렸다(실측: 시작하자마자 엉뚱한 +X 면으로 순간이동).
            // 스케일이 없는 별도 빈 오브젝트를 부모로 써서 해결.
            var zonesRoot = new GameObject("FaceZones");
            zonesRoot.transform.position = Vector3.zero;

            const float zoneThickness = 4f;
            float faceSpan = CubeHalfExtent * 2f;

            Vector3[] normals =
            {
                Vector3.right, Vector3.left,
                Vector3.up, Vector3.down,
                Vector3.forward, Vector3.back,
            };
            string[] labels = { "PlusX", "MinusX", "PlusY", "MinusY", "PlusZ", "MinusZ" };

            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 n = normals[i];
                GameObject zone = new GameObject($"FaceZone_{labels[i]}");
                zone.transform.SetParent(zonesRoot.transform, worldPositionStays: false);
                zone.transform.position = n * (CubeHalfExtent + zoneThickness * 0.5f);

                var col = zone.AddComponent<BoxCollider>();
                col.isTrigger = true;
                // 법선 축은 두께만, 나머지 두 축은 면 전체를 커버.
                col.size = new Vector3(
                    Mathf.Abs(n.x) > 0.5f ? zoneThickness : faceSpan,
                    Mathf.Abs(n.y) > 0.5f ? zoneThickness : faceSpan,
                    Mathf.Abs(n.z) > 0.5f ? zoneThickness : faceSpan);

                var faceZone = zone.AddComponent<CubeFaceZone>();
                faceZone.faceNormal = n;
            }
        }
    }
}
