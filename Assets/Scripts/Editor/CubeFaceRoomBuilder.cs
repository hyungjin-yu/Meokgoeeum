using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceRoomBuilder (실제 던전 방 A~D를 큐브 면 구조로 포팅 — 에디터 전용)
    /// [[MultiRoomFloor1Builder]]가 SC_Face_0.unity에 지은 1층 멀티룸 던전(방A~D, 각 30x30,
    /// 일렬 배치)을, [[CubeSurfaceWalker]]가 걷는 실제 3D 큐브(SC_CubePrototype.unity) 표면
    /// 위로 옮기는 첫 시도입니다.
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
    /// - **방향 축**: 각 면에서 "위(up)"에 가까운 접선을 v축, `cross(normal, v)`를 u축으로 고정.
    ///   문/게이트는 전부 v±15 벽에 냄(그게 다음/이전 면과 실제로 맞닿는 모서리이기 때문 — u±15
    ///   벽은 링에 안 쓰는 ±X 면과 맞닿으므로 항상 막음).
    ///
    /// ## 오늘 스코프 밖 (알고 하는 단순화)
    /// - EncounterSpawner의 "웨이브(시간차 등장)"는 안 씀 — 각 방의 적을 처음부터 전부 배치.
    ///   방 콘텐츠가 큐브 위로 옮겨지는지 자체를 검증하는 게 먼저라 판단.
    /// - 방D 클리어 후 "다음 층"은 없음 — [[CubeMapManager]]는 씬 전환 기반이라 이 리터럴 큐브
    ///   프로토타입과 아직 연결 안 됨. 방D의 RoomClearGate는 stairs/doorsToOpen 둘 다 비워둬서
    ///   "클리어됨" 로그만 남기는 종착점으로 둠(경고 로그 뜨는 거 정상, 의도된 것).
    /// - 벽화(PaintableObject)/움직이는 장애물(MovingObstacle) 등 장식 요소는 이번엔 생략 —
    ///   벽+적+게이트라는 핵심 골격만 먼저 포팅.
    ///
    /// 재실행해도 안전합니다("DungeonRooms_Generated" 루트만 지우고 재생성 — 벽 리사이즈나
    /// 몹 재배치는 이름으로 찾아 덮어씀).
    /// </summary>
    public static class CubeFaceRoomBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";
        private const string RootName = "DungeonRooms_Generated";

        private const float CubeHalfExtent = 17f;
        private const float RoomHalf = 15f;
        private const float WallHeight = 3f;
        private const float WallThickness = 2f;
        private const float DoorWidth = 4f;

        private enum WallMode { Closed, Open, GatedExit }

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

            // 방A(+Z) — 진입 없음(플레이어 시작 지점), B로 가는 출구만 게이트
            var doorsA = BuildRoom(root.transform, "A", Vector3.forward, WallMode.Closed, WallMode.GatedExit);
            PopulateRoomA(root.transform, Vector3.forward, doorsA.plus);
            PlacePlayerSpawn(Vector3.forward);

            // 방B(+Y) — A에서 진입(열림), C로 가는 출구 게이트
            var doorsB = BuildRoom(root.transform, "B", Vector3.up, WallMode.GatedExit, WallMode.Open);
            PopulateRoomB(root.transform, Vector3.up, doorsB.minus);

            // 방C(-Z) — B에서 진입(열림), D로 가는 출구 게이트
            var doorsC = BuildRoom(root.transform, "C", Vector3.back, WallMode.GatedExit, WallMode.Open);
            PopulateRoomC(root.transform, Vector3.back, doorsC.minus);

            // 방D(-Y) — C에서 진입(열림), 출구 없음(클라이맥스, 다음 층 미연결)
            BuildRoom(root.transform, "D", Vector3.down, WallMode.Open, WallMode.Closed);
            PopulateRoomD(root.transform, Vector3.down);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[CubeFaceRoomBuilder] 완료 — 방A(+Z)→B(+Y)→C(-Z)→D(-Y) 포팅, " +
                      $"큐브 half={CubeHalfExtent}로 확대, {ScenePath} 저장함.");
        }

        // ------------------------------------------------------------------
        // 큐브 크기 조정 / 기존 오브젝트 정리
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

            // 플레이어의 CubeSurfaceWalker
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
            GetFaceBasis(newNormal, out Vector3 u, out Vector3 v);

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

        // ------------------------------------------------------------------
        // 면 좌표계 헬퍼
        // ------------------------------------------------------------------

        /// <summary>
        /// 면 법선 하나로부터 그 면 위의 국소 2축(u, v)을 고정적으로 뽑습니다. v는 "위"에 가까운
        /// 접선(윗/아랫면은 "앞"으로 대체), u = cross(normal, v). 몹 이동에 쓰는
        /// [[CubeSurfaceWalker]].AnyTangentTo()와 같은 논리를 재사용하되, 여기선 벽 배치용으로
        /// u까지 같이 고정합니다.
        /// </summary>
        private static void GetFaceBasis(Vector3 n, out Vector3 u, out Vector3 v)
        {
            v = Vector3.ProjectOnPlane(Vector3.up, n);
            if (v.sqrMagnitude < 0.0001f) v = Vector3.ProjectOnPlane(Vector3.forward, n);
            v.Normalize();
            u = Vector3.Cross(n, v).normalized;
        }

        private static Vector3 CubeCenterPos()
        {
            GameObject cube = GameObject.Find("CubePlanet");
            return cube != null ? cube.transform.position : Vector3.zero;
        }

        // ------------------------------------------------------------------
        // 벽 지오메트리
        // ------------------------------------------------------------------

        /// <summary>
        /// 면 위에 박스 하나를 세웁니다. uSize는 u축 방향 크기, vSize는 v축 방향 크기,
        /// 높이(법선 방향)는 항상 WallHeight로 고정. 회전은 LookRotation(v, normal)로 맞춰서
        /// 로컬 X→u, Y→normal, Z→v가 되게 합니다(localScale이 그대로 월드 크기와 대응).
        /// </summary>
        private static GameObject BuildFaceWall(Transform parent, string name, Vector3 cubeCenter, Vector3 normal,
            Vector3 u, Vector3 v, float uCenter, float vCenter, float uSize, float vSize)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = cubeCenter + normal * (CubeHalfExtent + WallHeight * 0.5f) + u * uCenter + v * vCenter;
            wall.transform.rotation = Quaternion.LookRotation(v, normal);
            wall.transform.localScale = new Vector3(uSize, WallHeight, vSize);
            return wall;
        }

        /// <summary>
        /// v = ±(half+t/2) 위치의 벽 한 쌍(문 쪽) — Closed면 통짜, Open/GatedExit면 가운데
        /// DoorWidth만큼 틈을 내고 양옆만 세웁니다. GatedExit면 그 틈을 막는 블로커도 하나
        /// 더 세워서 반환합니다(RoomClearGate.doorsToOpen이 나중에 비활성화).
        /// </summary>
        private static GameObject BuildDoorWall(Transform parent, string label, string side, Vector3 center,
            Vector3 normal, Vector3 u, Vector3 v, float vSign, WallMode mode)
        {
            float half = RoomHalf, t = WallThickness;
            float vCenter = vSign * (half + t * 0.5f);

            if (mode == WallMode.Closed)
            {
                BuildFaceWall(parent, $"Wall_{label}_{side}", center, normal, u, v, 0f, vCenter, half * 2f, t);
                return null;
            }

            float doorHalf = DoorWidth * 0.5f;
            float segLen = half - doorHalf;
            if (segLen > 0.01f)
            {
                BuildFaceWall(parent, $"Wall_{label}_{side}_Neg", center, normal, u, v, -(doorHalf + segLen * 0.5f), vCenter, segLen, t);
                BuildFaceWall(parent, $"Wall_{label}_{side}_Pos", center, normal, u, v, (doorHalf + segLen * 0.5f), vCenter, segLen, t);
            }

            if (mode == WallMode.GatedExit)
            {
                GameObject blocker = BuildFaceWall(parent, $"DoorBlocker_{label}_{side}", center, normal, u, v, 0f, vCenter, DoorWidth - 0.2f, t + 0.5f);
                return blocker;
            }

            return null; // Open — 틈만 내고 막음 없음(항상 통과 가능)
        }

        /// <summary>
        /// 면 하나에 방 하나(사방 벽)를 짓습니다. u±15 벽(다른 링 면과 안 맞닿는 쪽)은 항상
        /// 막힘. v±15 벽이 이웃 면과 실제로 맞닿는 모서리라 여기에 문/게이트를 냅니다.
        /// </summary>
        private static (GameObject minus, GameObject plus) BuildRoom(Transform parent, string label, Vector3 normal,
            WallMode minusVMode, WallMode plusVMode)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 center = CubeCenterPos();
            float half = RoomHalf, t = WallThickness;

            BuildFaceWall(parent, $"Wall_{label}_PlusU", center, normal, u, v, half + t * 0.5f, 0f, t, half * 2f + t * 2f);
            BuildFaceWall(parent, $"Wall_{label}_MinusU", center, normal, u, v, -(half + t * 0.5f), 0f, t, half * 2f + t * 2f);

            GameObject blockerMinus = BuildDoorWall(parent, label, "MinusV", center, normal, u, v, -1f, minusVMode);
            GameObject blockerPlus = BuildDoorWall(parent, label, "PlusV", center, normal, u, v, 1f, plusVMode);

            return (blockerMinus, blockerPlus);
        }

        // ------------------------------------------------------------------
        // 방별 콘텐츠
        // ------------------------------------------------------------------

        private static void PlacePlayerSpawn(Vector3 normal)
        {
            GameObject playerGo = GameObject.Find("CubeWalker_Player");
            if (playerGo == null) { Debug.LogWarning("[CubeFaceRoomBuilder] CubeWalker_Player를 못 찾았습니다."); return; }

            Vector3 pos = CubeCenterPos() + normal * (CubeHalfExtent + 1f); // surfaceOffset 기본값(1) 기준
            playerGo.transform.position = pos;
            playerGo.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 방 중앙에 [[EncounterSpawner]]를 큐브 면 모드로 세웁니다. `enemyPrefabs`는 평소처럼
        /// `EnemyPyeong.prefab`(NavMesh 버전)을 그대로 참조해도 됩니다 — `EncounterSpawner`가
        /// `cubeCenter`가 설정돼 있으면 스폰 시점에 자동으로 [[CubeEnemyPyeong]]으로 교체합니다.
        /// `enemiesPerWave = {1,1}`이면 1마리씩 2웨이브(원본 [[MultiRoomFloor1Builder]].BuildWaveSpawner와 동일 패턴).
        /// </summary>
        private static EncounterSpawner BuildWaveSpawner(Transform parent, string name, Vector3 normal,
            Vector3 u, Vector3 v, CubeSurfaceWalker target, int[] enemiesPerWave)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/EnemyPyeong.prefab");
            if (prefab == null) { Debug.LogError("[CubeFaceRoomBuilder] EnemyPyeong.prefab을 못 찾았습니다."); return null; }

            Vector3 spawnerPos = CubeCenterPos() + normal * (CubeHalfExtent + 1f);
            GameObject spawnerGo = new GameObject(name);
            spawnerGo.transform.SetParent(parent, false);
            spawnerGo.transform.position = spawnerPos;

            var spawner = spawnerGo.AddComponent<EncounterSpawner>();
            spawner.waveInterval = 2.5f;
            spawner.autoStart = true;
            spawner.cubeCenter = GameObject.Find("CubePlanet")?.transform;
            spawner.cubeFaceNormal = normal;
            spawner.cubeHalfExtent = CubeHalfExtent;
            spawner.cubeTarget = target;

            var waves = new EncounterSpawner.Wave[enemiesPerWave.Length];
            for (int w = 0; w < enemiesPerWave.Length; w++)
            {
                int count = enemiesPerWave[w];
                var prefabs = new GameObject[count];
                var points = new Transform[count];
                for (int i = 0; i < count; i++)
                {
                    prefabs[i] = prefab;
                    float angleDeg = count > 0 ? (360f / count) * i + w * 45f : 0f;
                    float rad = angleDeg * Mathf.Deg2Rad;
                    Vector3 tangentOffset = (Mathf.Cos(rad) * u + Mathf.Sin(rad) * v) * 6f;

                    GameObject spGo = new GameObject($"{name}_W{w}_{i}");
                    spGo.transform.SetParent(spawnerGo.transform, false);
                    spGo.transform.position = spawnerPos + tangentOffset;
                    spGo.transform.rotation = Quaternion.LookRotation(v, normal);
                    points[i] = spGo.transform;
                }
                waves[w] = new EncounterSpawner.Wave { waveName = $"웨이브{w + 1}", enemyPrefabs = prefabs, spawnPoints = points, hpMultiplier = 1f };
            }
            spawner.waves = waves;
            return spawner;
        }

        /// <summary>
        /// 면 위 (uOffset, vOffset) 지점에 얇은 벽화(장식용, 문 게이트 아님)를 바닥에 박아 넣습니다.
        /// 원본([[MultiRoomFloor1Builder]] PopulateRoomB 등)은 세워진 그림이지만, 여기선 면 위에
        /// 눕혀서 "바닥 문양"으로 단순화했습니다.
        /// </summary>
        private static PaintableObject BuildMural(Transform parent, string name, Vector3 normal, Vector3 u, Vector3 v,
            float uOffset, float vOffset, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = CubeCenterPos() + normal * (CubeHalfExtent + 0.1f) + u * uOffset + v * vOffset;
            go.transform.rotation = Quaternion.LookRotation(v, normal);
            go.transform.localScale = new Vector3(2.5f, 0.2f, 2.5f);

            var paintable = go.AddComponent<PaintableObject>();
            paintable.trueColor = color;
            paintable.startGray = true;
            return paintable;
        }

        /// <summary>면 위 (uOffset, vOffset)에 좌우(u축)로 왕복하는 [[MovingObstacle]]을 세웁니다.</summary>
        private static void BuildRoomObstacle(Transform parent, string name, Vector3 normal, Vector3 u, Vector3 v,
            float uOffset, float vOffset)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = CubeCenterPos() + normal * (CubeHalfExtent + 0.9f) + u * uOffset + v * vOffset;
            go.transform.rotation = Quaternion.LookRotation(v, normal); // 로컬 X→u라서 MovingObstacle의 transform.right 왕복이 u축을 따라감
            go.transform.localScale = new Vector3(1.5f, 1.8f, 1.2f);

            var mover = go.AddComponent<MovingObstacle>();
            mover.travelDistance = 3f;
            mover.period = 4f;
        }

        private static RoomClearGate BuildRoomGate(Transform parent, string name, Vector3 normal, GameObject doorBlocker)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            GameObject gateGo = new GameObject(name);
            gateGo.transform.SetParent(parent, false);
            gateGo.transform.position = CubeCenterPos() + normal * (CubeHalfExtent + 5f);
            gateGo.transform.rotation = Quaternion.LookRotation(v, normal);

            var gate = gateGo.AddComponent<RoomClearGate>();
            gate.boundsSize = new Vector3(RoomHalf * 2f + 4f, 10f, RoomHalf * 2f + 4f);
            gate.doorsToOpen = doorBlocker != null ? new[] { doorBlocker } : new GameObject[0];
            return gate;
        }

        /// <summary>방A — 원본 튜토리얼 전투(평×1+1, 2웨이브) + 벽화/숨겨진 구슬 곁가지.</summary>
        private static void PopulateRoomA(Transform parent, Vector3 normal, GameObject doorBlocker)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            var walker = GameObject.Find("CubeWalker_Player")?.GetComponent<CubeSurfaceWalker>();

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomA", normal, u, v, walker, new[] { 1, 1 });
            spawner.showFirstAttackHint = true; // 첫 방이니 원본처럼 좌클릭 공격 힌트를 여기서 띄움

            var mural = BuildMural(parent, "Mural_RoomA_Side", normal, u, v, 10f, 8f, new Color(0.9f, 0.2f, 0.2f));
            GameObject orbGo = new GameObject("HiddenOrbSpawner_RoomA_Side");
            orbGo.transform.SetParent(parent, false);
            orbGo.transform.position = CubeCenterPos() + normal * (CubeHalfExtent + 0.5f) + u * 10f + v * 7f;
            var orbSpawner = orbGo.AddComponent<HiddenOrbSpawner>();
            orbSpawner.trigger = mural;
            orbSpawner.orbColor = OrbColor.Red;

            BuildRoomGate(parent, "RoomClearGate_A", normal, doorBlocker);
        }

        /// <summary>방B — 원본 평×1+1(2웨이브) + 장식 벽화 3개(게이트 아님, 순수 장식).</summary>
        private static void PopulateRoomB(Transform parent, Vector3 normal, GameObject doorBlocker)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            var walker = GameObject.Find("CubeWalker_Player")?.GetComponent<CubeSurfaceWalker>();

            BuildWaveSpawner(parent, "EncounterSpawner_RoomB", normal, u, v, walker, new[] { 1, 1 });

            BuildMural(parent, "Mural_RoomB_0", normal, u, v, 8f, 8f, new Color(0.6f, 0.2f, 0.8f));
            BuildMural(parent, "Mural_RoomB_1", normal, u, v, 0f, -10f, new Color(0.9f, 0.2f, 0.2f));
            BuildMural(parent, "Mural_RoomB_2", normal, u, v, -8f, 8f, new Color(0.9f, 0.7f, 0.2f));

            BuildRoomGate(parent, "RoomClearGate_B", normal, doorBlocker);
        }

        /// <summary>방C — 원본 평×2+1(2웨이브) + 왕복 장애물 2개.</summary>
        private static void PopulateRoomC(Transform parent, Vector3 normal, GameObject doorBlocker)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            var walker = GameObject.Find("CubeWalker_Player")?.GetComponent<CubeSurfaceWalker>();

            BuildWaveSpawner(parent, "EncounterSpawner_RoomC", normal, u, v, walker, new[] { 2, 1 });

            BuildRoomObstacle(parent, "MovingObstacle_RoomC_0", normal, u, v, 4f, -2f);
            BuildRoomObstacle(parent, "MovingObstacle_RoomC_1", normal, u, v, -4f, 2f);

            BuildRoomGate(parent, "RoomClearGate_C", normal, doorBlocker);
        }

        /// <summary>방D — 원본 평×2+2(2웨이브, 클라이맥스) + 벽화 2개. 다음 층 미연결.</summary>
        private static void PopulateRoomD(Transform parent, Vector3 normal)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            var walker = GameObject.Find("CubeWalker_Player")?.GetComponent<CubeSurfaceWalker>();

            BuildWaveSpawner(parent, "EncounterSpawner_RoomD", normal, u, v, walker, new[] { 2, 2 });

            BuildMural(parent, "Mural_RoomD_Climax_0", normal, u, v, 0f, 12f, new Color(0.9f, 0.2f, 0.2f));
            BuildMural(parent, "Mural_RoomD_Climax_1", normal, u, v, 0f, -12f, new Color(0.9f, 0.2f, 0.2f));

            // stairs/doorsToOpen 둘 다 비움 — 다음 층이 아직 없는 종착점이라 의도적으로 비워둠
            // (RoomClearGate가 경고 로그를 남기지만 정상입니다).
            BuildRoomGate(parent, "RoomClearGate_D", normal, null);
        }
    }
}
