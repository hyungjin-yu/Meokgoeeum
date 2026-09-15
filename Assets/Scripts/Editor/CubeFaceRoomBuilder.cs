using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceRoomBuilder (실제 던전 방 A~D를 큐브 면 구조로 포팅 — 에디터 전용, 최초 씬 세팅)
    /// [[MultiRoomFloor1Builder]]가 SC_Face_0.unity에 지은 1층 멀티룸 던전(방A~D, 각 30x30,
    /// 일렬 배치)을, [[CubeSurfaceWalker]]가 걷는 실제 3D 큐브(SC_CubePrototype.unity) 표면
    /// 위로 옮기는 최초 세팅. 실제 벽/스폰/장식 로직은 전부 [[CubeDungeonRoomKit]](런타임에서도
    /// 쓸 수 있는 순수 로직)로 옮겼고, 여기선 씬 열기/저장 + 큐브 리사이즈 + 몹 재배치 +
    /// `EnemyPyeong.prefab` 로드처럼 에디터 전용/최초 1회성 작업만 담당합니다.
    ///
    /// ⚠️ 2026-09-15 — "방D 클리어 → 다음 층" 전환은 이제 [[CubeDungeonProgressionManager]](런타임)이
    /// 담당합니다. 이 스크립트는 "1층 최초 세팅"만 하고, 이후 층 재생성은 그쪽이 [[CubeDungeonRoomKit]]을
    /// 직접 불러서 처리합니다 — `UnityEditor` API(AssetDatabase 등)는 실제 빌드에 못 들어가서
    /// 게임플레이 중 트리거되는 로직은 반드시 에디터 스크립트 밖에 있어야 하기 때문입니다.
    ///
    /// ## 핵심 설계 결정 (2026-09-15, 사용자와 논의)
    /// - **"1개 방 = 큐브 1개 면"**: 방 하나를 통째로 한 면에 배치. 원래 일렬(A→B→C→D) 구조를
    ///   유지하되, 다음 방으로 가는 이동이 "문을 지나 연결 통로를 걷는 것"이 아니라 "큐브 모서리를
    ///   넘어가는 것" 자체가 됩니다 — [[CubeFaceZone]]이 이미 모서리 전환을 처리해주므로 별도
    ///   연결 통로가 필요 없습니다.
    /// - **큐브 크기 10→17로 확대**: 방이 30x30인데 기존 큐브 한 면은 20x20(half=10)이라 방
    ///   하나도 안 들어감. half=17(한 변 34)로 키워서 30x30 방 + 벽 두께 + 여유 2유닛이 딱 맞게 함.
    /// - **면 배정(X축 링)**: 방A=+Z(기존 5종 몹 AI 테스트 자리, 몹은 +X로 옮김), 방B=+Y(기존
    ///   골목 환경 아트가 깔린 자리), 방C=-Z(새 면), 방D=-Y(새 면). +Z/+Y/-Z/-Y는 서로 모두
    ///   인접(수직)하므로 이 순서대로 걸어서 넘어갈 수 있음. ±X는 안 건드림(기존 AI 테스트 보존용).
    ///
    /// 재실행해도 안전합니다("DungeonRooms_Generated" 루트만 지우고 재생성 — 벽 리사이즈나
    /// 몹 재배치는 이름으로 찾아 덮어씀).
    /// </summary>
    public static class CubeFaceRoomBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";
        private const string RootName = "DungeonRooms_Generated";

        private const float CubeHalfExtent = 17f;

        [MenuItem("MG/큐브 면 - 실제 던전 방 A~D 포팅")]
        public static void BuildFromCLI()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[CubeFaceRoomBuilder] {ScenePath}가 없습니다.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ResizeCube();
            RelocateExistingTestMobsToPlusX();

            GameObject prevRoot = GameObject.Find(RootName);
            if (prevRoot != null) Object.DestroyImmediate(prevRoot);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/EnemyPyeong.prefab");
            if (prefab == null) { Debug.LogError("[CubeFaceRoomBuilder] EnemyPyeong.prefab을 못 찾았습니다."); return; }

            GameObject cube = GameObject.Find("CubePlanet");
            GameObject playerGo = GameObject.Find("CubeWalker_Player");
            var walker = playerGo != null ? playerGo.GetComponent<CubeSurfaceWalker>() : null;

            CubeDungeonRoomKit.BuildFullFloor(root.transform, cube.transform, CubeHalfExtent, walker, prefab, 1f);

            // 다음 층 전환 매니저를 씬에 세팅 — 처음 만드는 거면 새로 추가, 있으면 참조만 갱신.
            var progression = Object.FindFirstObjectByType<CubeDungeonProgressionManager>();
            if (progression == null)
            {
                var progressionGo = new GameObject("CubeDungeonProgressionManager");
                SceneManager.MoveGameObjectToScene(progressionGo, scene);
                progression = progressionGo.AddComponent<CubeDungeonProgressionManager>();
            }
            progression.cubePlanet = cube.transform;
            progression.player = walker;
            progression.enemyPyeongPrefab = prefab;
            progression.dungeonRootName = RootName;
            progression.cubeHalfExtent = CubeHalfExtent;
            progression.currentFloor = 1;
            // ⚠️ 여기서 HookGate()를 불러도 소용없음 — C# 이벤트 구독은 씬 저장 대상이 아니라서
            // Play를 누르는 순간 사라짐. 실제 구독은 CubeDungeonProgressionManager.Start()가
            // 런타임에 스스로 "RoomClearGate_D"를 찾아서 겁니다.

            PlacePlayerSpawn(cube.transform, Vector3.forward);
            walker?.SetCurrentFaceNormal(Vector3.forward);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[CubeFaceRoomBuilder] 완료 — 방A(+Z)→B(+Y)→C(-Z)→D(-Y) 포팅, " +
                      $"큐브 half={CubeHalfExtent}로 확대, 다음 층 전환 매니저 연결, {ScenePath} 저장함.");
        }

        // ------------------------------------------------------------------
        // 큐브 크기 조정 / 기존 오브젝트 정리 (최초 세팅 전용 — 재실행해도 안전)
        // ------------------------------------------------------------------

        /// <summary>기존 큐브(half=10)와 그 위에 이미 배치된 모든 큐브 면 오브젝트를 half=17 기준으로 갱신합니다.</summary>
        private static void ResizeCube()
        {
            GameObject cube = GameObject.Find("CubePlanet");
            if (cube == null) { Debug.LogError("[CubeFaceRoomBuilder] CubePlanet을 못 찾았습니다."); return; }
            cube.transform.localScale = Vector3.one * (CubeHalfExtent * 2f);

            // FaceZones — CubePrototypeBuilder.BuildFaceZones()와 동일한 레이아웃, half만 교체
            GameObject zonesRoot = GameObject.Find("FaceZones");
            if (zonesRoot != null)
            {
                const float zoneThickness = 4f;
                float faceSpan = CubeHalfExtent * 2f;
                foreach (Transform zone in zonesRoot.transform)
                {
                    var faceZone = zone.GetComponent<CubeFaceZone>();
                    if (faceZone == null) continue;
                    Vector3 n = faceZone.faceNormal;
                    zone.position = n * (CubeHalfExtent + zoneThickness * 0.5f);
                    var col = zone.GetComponent<BoxCollider>();
                    if (col != null)
                    {
                        col.size = new Vector3(
                            Mathf.Abs(n.x) > 0.5f ? zoneThickness : faceSpan,
                            Mathf.Abs(n.y) > 0.5f ? zoneThickness : faceSpan,
                            Mathf.Abs(n.z) > 0.5f ? zoneThickness : faceSpan);
                    }
                }
            }

            GameObject playerGo = GameObject.Find("CubeWalker_Player");
            var walker = playerGo != null ? playerGo.GetComponent<CubeSurfaceWalker>() : null;
            if (walker != null) walker.cubeHalfExtent = CubeHalfExtent;

            // 씬에 이미 배치된 모든 CubeEnemyBase 계열 몹의 cubeHalfExtent도 같이 갱신
            // (안 해주면 몹은 옛 half=10 경계에서 클램프되고 벽/플레이어는 half=17 기준이라 어긋남)
            foreach (var enemy in Object.FindObjectsByType<CubeEnemyBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                enemy.cubeHalfExtent = CubeHalfExtent;
            foreach (var mob in Object.FindObjectsByType<CubeFaceLockedMob>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                mob.cubeHalfExtent = CubeHalfExtent;
        }

        /// <summary>
        /// 기존 5종 AI 검증용 테스트 몹(+Z 면)은 이제 방A 콘텐츠가 들어올 자리라, 안 쓰는 +X
        /// 면으로 그대로 옮깁니다(삭제하지 않음 — AI 동작 참고/재검증용으로 남겨둠).
        /// </summary>
        private static void RelocateExistingTestMobsToPlusX()
        {
            GameObject cube = GameObject.Find("CubePlanet");
            Vector3 cubeCenter = cube != null ? cube.transform.position : Vector3.zero;
            Vector3 newNormal = Vector3.right; // +X
            CubeDungeonRoomKit.GetFaceBasis(newNormal, out Vector3 u, out Vector3 v);

            string[] names =
            {
                "CubeTestEnemy_Pyeong", "CubeTestEnemy_Won", "CubeTestEnemy_Bun",
                "CubeTestEnemy_Heup", "CubeTestEnemy_Gwang", "TestMob_PlusZ",
            };
            float[] uOffsets = { -6f, -6f, 6f, 6f, 0f, 0f };
            float[] vOffsets = { 6f, -6f, 6f, -6f, 0f, 3f };

            for (int i = 0; i < names.Length; i++)
            {
                GameObject go = GameObject.Find(names[i]);
                if (go == null) continue;

                var cubeEnemy = go.GetComponent<CubeEnemyBase>();
                if (cubeEnemy != null) cubeEnemy.faceNormal = newNormal;
                var lockedMob = go.GetComponent<CubeFaceLockedMob>();
                if (lockedMob != null) lockedMob.faceNormal = newNormal;

                go.transform.position = cubeCenter + newNormal * (CubeHalfExtent + 1f) + u * uOffsets[i] + v * vOffsets[i];
                go.transform.rotation = Quaternion.LookRotation(Vector3.Cross(newNormal, u) * -1f, newNormal);
            }

            Debug.Log("[CubeFaceRoomBuilder] 기존 AI 테스트 몹 5종 + 프로토타입 몹을 +X 면으로 이동함.");
        }

        private static void PlacePlayerSpawn(Transform cube, Vector3 normal)
        {
            GameObject playerGo = GameObject.Find("CubeWalker_Player");
            if (playerGo == null) { Debug.LogWarning("[CubeFaceRoomBuilder] CubeWalker_Player를 못 찾았습니다."); return; }

            Vector3 pos = cube.position + normal * (CubeHalfExtent + 1f); // surfaceOffset 기본값(1) 기준
            playerGo.transform.position = pos;
            playerGo.transform.rotation = Quaternion.identity;
        }
    }
}
