using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// MultiRoomFloor1Builder (1층 멀티룸 던전 확장 — 에디터 전용)
    /// [[changelog/2026-09-11_플레이타임-확장-스코프결정]]의 1층 파일럿 블루프린트를 실제로
    /// 짓습니다. 기존 [[Floor1CorridorBuilder]]가 만든 "입장 복도 + 방A"에 이어서, 서쪽으로
    /// 방B(색 복원 퍼즐) → 방C(두 번째 전투) → 방D(클라이맥스, 실제 계단)를 연결 통로로
    /// 이어붙입니다.
    ///
    /// 방A는 3배(10x10→30x30)로 키우고, 나머지 방도 전부 같은 크기로 통일해서
    /// [[28 레벨 디자인 - 인카운터 페이싱과 블록아웃]]의 "노드 크기 통일" 원칙을 지킵니다.
    ///
    /// 재실행해도 안전합니다("MultiRoom_Generated" 그룹을 지우고 재생성). 단, Floor1CorridorBuilder도
    /// 같이 재실행하므로 그쪽 생성물("Floor1_Corridor_Generated")도 매번 새로 만들어집니다.
    /// </summary>
    public static class MultiRoomFloor1Builder
    {
        private const string ScenePath = "Assets/Scenes/SC_Face_0.unity";
        private const string RootName = "MultiRoom_Generated";

        // 방A(10x10, half=5)의 3배 — [[changelog/2026-09-11_플레이타임-확장-스코프결정]] "맵 바닥
        // 크기 3배" 요청 반영. 방B/C/D도 통일해서 같은 크기로 만듭니다.
        private const float RoomHalf = 15f;
        private const float WallHeight = 3f;
        private const float WallThickness = 2f;
        private const float FloorThickness = 2f;

        // Floor1CorridorBuilder.CorridorCenterZ와 동일값 — 던전 전체를 한 축으로 정렬하기 위해 맞춤.
        private const float CenterZ = 0.47f;
        private const float DoorWidth = 4f;
        private const float ConnectorLength = 8f;
        private const float RoomPitch = RoomHalf * 2f + ConnectorLength; // 방 중심 간 거리(38)

        private const float RoomACenterX = 0f;
        private const float RoomBCenterX = RoomACenterX - RoomPitch;
        private const float RoomCCenterX = RoomBCenterX - RoomPitch;
        private const float RoomDCenterX = RoomCCenterX - RoomPitch;

        [MenuItem("MG/1층 멀티룸 던전 확장 (Multi-Room Floor1)")]
        public static void BuildFromMenu() => BuildFromCLI();

        public static void BuildFromCLI()
        {
            // ⚠️ 2026-09-11 발견 — 실제 플레이 중 "전진하면 바닥으로 떨어진다" 버그 리포트로 발견.
            // Floor1CorridorBuilder.BuildFromCLI()는 내부에서 씬을 "디스크에서 다시" 엽니다
            // (EditorSceneManager.OpenScene) — 그래서 여기서 Ground를 먼저 키워놔도 저장 전에
            // 그 호출이 씬을 새로 열어버리면 방금 한 변경이 통째로 날아갑니다. 벽/복도는 새
            // 크기(half=15)로 정상 생성되는데 실제 바닥(Ground)만 옛 10x10 그대로 남아서, 벽과
            // 바닥 사이에 거대한 구멍이 뚫리는 버그였습니다. Ground 리스케일은 반드시
            // Floor1CorridorBuilder 호출 "이후"에 해야 합니다.
            Floor1CorridorBuilder.BuildFromCLI(); // 내부에서 씬을 다시 열고 저장함
            Scene scene = EditorSceneManager.GetActiveScene();

            GameObject ground = GameObject.Find("Ground");
            if (ground != null) ground.transform.localScale = new Vector3(3f, 1f, 3f);

            GameObject prevRoot = GameObject.Find(RootName);
            if (prevRoot != null) Object.DestroyImmediate(prevRoot);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            // ⚠️ 2026-09-11 2차 발견 — 사용자 실측 "방마다 20초도 안 걸림". 원인 두 가지:
            // ① 방A/C 서쪽 문이 애초에 안 잠겨있어서 전투 없이 그냥 통과 가능했음
            // ② ColorWaveEffect.maxRadius가 30인데 방이 30x30이라, 몹 하나만 잡아도 파동이 방 안
            //    벽화를 전부(심지어 옆방까지) 한 번에 칠해버려서 방B "퍼즐"이 사실상 무의미했음
            // → 모든 방을 전투 클리어 게이트로 강제 잠그고, 벽화/PaintCountGate는 순수 장식으로
            //   격하(문을 안 엶). RoomClearGate에 doorsToOpen을 추가해서(RoomClearGate.cs 수정)
            //   "계단 대신 문을 여는" 중간 방 게이트로도 쓸 수 있게 함.
            // ⚠️ 이름으로 찾으면 재실행할 때 깨집니다 — 이전 실행에서 이미 이 오브젝트를
            // "RoomClearGate_D"로 개명해서 방D로 옮겨놨을 수 있기 때문입니다(그러면
            // GameObject.Find("RoomClearGate")가 항상 null). 대신 "부모가 없는(=내가 만든
            // MultiRoom_Generated 밑이 아닌) RoomClearGate"로 찾아서 몇 번을 재실행해도 항상
            // 같은 원본 오브젝트를 재사용하도록 합니다.
            RoomClearGate roomAGate = null;
            foreach (var g in Object.FindObjectsByType<RoomClearGate>(FindObjectsSortMode.None))
            {
                if (g.transform.parent == null) { roomAGate = g; break; }
            }

            BuildRoomASidePath(root.transform);

            // 방A 서쪽 문도 잠금 — 기존 튜토리얼 전투(평×2)를 반드시 끝내야 방B로 갈 수 있음.
            GameObject doorBlockerA = BuildDoorBlockerAt(root.transform, "A", RoomACenterX - RoomHalf);
            if (roomAGate != null)
            {
                roomAGate.name = "RoomClearGate_A";
                roomAGate.transform.position = new Vector3(RoomACenterX, roomAGate.transform.position.y, CenterZ);
                roomAGate.stairs = null; // 이전 실행에서 방D로 옮겨졌을 때 남은 참조가 있으면 제거
                roomAGate.doorsToOpen = new[] { doorBlockerA };
                roomAGate.boundsSize = new Vector3(RoomHalf * 2f + 4f, 10f, RoomHalf * 2f + 4f); // 30x30로 커진 방A 전체 커버
            }
            else
            {
                Debug.LogWarning("[MultiRoomFloor1Builder] 방A 원본 RoomClearGate를 못 찾았습니다 — 방A 문이 안 잠깁니다.");
            }

            BuildConnector(root.transform, "A_B", RoomACenterX - RoomHalf, RoomBCenterX + RoomHalf);
            GameObject doorBlockerB = BuildRoom(root.transform, "B", RoomBCenterX, eastDoorWidth: DoorWidth, westDoorWidth: DoorWidth, lockWestDoor: true);
            PopulateRoomB(root.transform, RoomBCenterX, doorBlockerB);

            BuildConnector(root.transform, "B_C", RoomBCenterX - RoomHalf, RoomCCenterX + RoomHalf);
            GameObject doorBlockerC = BuildRoom(root.transform, "C", RoomCCenterX, eastDoorWidth: DoorWidth, westDoorWidth: DoorWidth, lockWestDoor: true);
            PopulateRoomC(root.transform, RoomCCenterX, doorBlockerC);

            BuildConnector(root.transform, "C_D", RoomCCenterX - RoomHalf, RoomDCenterX + RoomHalf);
            BuildRoom(root.transform, "D", RoomDCenterX, eastDoorWidth: DoorWidth, westDoorWidth: 0f, lockWestDoor: false);
            PopulateRoomD(root.transform, RoomDCenterX);

            RebuildArtDressing(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[MultiRoomFloor1Builder] 완료 — 방A(0) → 방B({RoomBCenterX}) → 방C({RoomCCenterX}) → 방D({RoomDCenterX}), {ScenePath} 저장함.");
        }

        // ------------------------------------------------------------------
        // 공용 지오메트리 헬퍼
        // ------------------------------------------------------------------

        private static GameObject BuildBoxWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, worldPositionStays: false);
            wall.transform.position = center;
            wall.transform.localScale = size;
            return wall;
        }

        /// <summary>
        /// centerX에 30x30 방 하나(바닥+사방 벽)를 짓습니다. 동/서 벽은 각각 폭 door*Width만큼
        /// 문을 냅니다(0이면 통째로 막음). lockWestDoor가 true면 서쪽 문 자리에 별도의 "문 막음"
        /// 큐브를 하나 더 세워서(구조적 벽 개구부는 그대로 두고) [[PaintCountGate]] 등으로 나중에
        /// 치울 수 있게 합니다 — 반환값이 그 막음 오브젝트입니다(안 잠그면 null).
        /// </summary>
        private static GameObject BuildRoom(Transform parent, string label, float centerX, float eastDoorWidth, float westDoorWidth, bool lockWestDoor)
        {
            float half = RoomHalf;
            float t = WallThickness;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = $"Floor_{label}";
            floor.transform.SetParent(parent, false);
            floor.transform.position = new Vector3(centerX, -FloorThickness * 0.5f, CenterZ);
            floor.transform.localScale = new Vector3(half * 2f, FloorThickness, half * 2f);

            BuildBoxWall(parent, $"Wall_{label}_PlusZ",
                new Vector3(centerX, WallHeight * 0.5f, CenterZ + half + t * 0.5f),
                new Vector3(half * 2f + t * 2f, WallHeight, t));
            BuildBoxWall(parent, $"Wall_{label}_MinusZ",
                new Vector3(centerX, WallHeight * 0.5f, CenterZ - half - t * 0.5f),
                new Vector3(half * 2f + t * 2f, WallHeight, t));

            BuildEastWestWall(parent, $"Wall_{label}_PlusX", centerX + half + t * 0.5f, eastDoorWidth, half, t);
            BuildEastWestWall(parent, $"Wall_{label}_MinusX", centerX - half - t * 0.5f, westDoorWidth, half, t);

            if (!lockWestDoor || westDoorWidth <= 0.01f) return null;

            return BuildDoorBlockerAt(parent, label, centerX - half);
        }

        /// <summary>서쪽 문 위치(wallBoundaryX)에 딱 맞는 막음 큐브를 세웁니다 — RoomClearGate.doorsToOpen이 치웁니다.</summary>
        private static GameObject BuildDoorBlockerAt(Transform parent, string label, float wallBoundaryX)
        {
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = $"DoorBlocker_{label}_West";
            blocker.transform.SetParent(parent, false);
            blocker.transform.position = new Vector3(wallBoundaryX, WallHeight * 0.5f, CenterZ);
            blocker.transform.localScale = new Vector3(WallThickness + 0.5f, WallHeight, DoorWidth - 0.2f);
            return blocker;
        }

        /// <summary>동/서(남북 방향으로 긴) 벽 하나 — doorWidth가 0보다 크면 중앙에 문을 냄.</summary>
        private static void BuildEastWestWall(Transform parent, string name, float wallX, float doorWidth, float half, float t)
        {
            if (doorWidth <= 0.01f)
            {
                BuildBoxWall(parent, name, new Vector3(wallX, WallHeight * 0.5f, CenterZ), new Vector3(t, WallHeight, half * 2f));
                return;
            }

            float doorHalf = doorWidth * 0.5f;
            float doorMinZ = CenterZ - doorHalf;
            float doorMaxZ = CenterZ + doorHalf;
            float roomMinZ = CenterZ - half;
            float roomMaxZ = CenterZ + half;

            float southLen = doorMinZ - roomMinZ;
            if (southLen > 0.01f)
                BuildBoxWall(parent, name + "_South", new Vector3(wallX, WallHeight * 0.5f, (roomMinZ + doorMinZ) * 0.5f), new Vector3(t, WallHeight, southLen));

            float northLen = roomMaxZ - doorMaxZ;
            if (northLen > 0.01f)
                BuildBoxWall(parent, name + "_North", new Vector3(wallX, WallHeight * 0.5f, (doorMaxZ + roomMaxZ) * 0.5f), new Vector3(t, WallHeight, northLen));
        }

        /// <summary>두 방 사이를 잇는 짧은 연결 통로(바닥+양옆 벽), 폭은 DoorWidth로 통일.</summary>
        private static void BuildConnector(Transform parent, string label, float startX, float endX)
        {
            float length = startX - endX; // startX(동쪽 방 서쪽 경계) > endX(서쪽 방 동쪽 경계)
            float midX = (startX + endX) * 0.5f;
            float t = WallThickness;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = $"Floor_Connector_{label}";
            floor.transform.SetParent(parent, false);
            floor.transform.position = new Vector3(midX, -FloorThickness * 0.5f, CenterZ);
            floor.transform.localScale = new Vector3(length, FloorThickness, DoorWidth);

            BuildBoxWall(parent, $"Wall_Connector_{label}_PlusZ",
                new Vector3(midX, WallHeight * 0.5f, CenterZ + DoorWidth * 0.5f + t * 0.5f),
                new Vector3(length, WallHeight, t));
            BuildBoxWall(parent, $"Wall_Connector_{label}_MinusZ",
                new Vector3(midX, WallHeight * 0.5f, CenterZ - DoorWidth * 0.5f - t * 0.5f),
                new Vector3(length, WallHeight, t));
        }

        // ------------------------------------------------------------------
        // 방별 콘텐츠
        // ------------------------------------------------------------------

        /// <summary>방A(기존 튜토리얼 전투 유지)에 곁가지 탐색 알코브를 하나 붙입니다 — 벽화 복원 → 숨겨진 구슬.</summary>
        private static void BuildRoomASidePath(Transform parent)
        {
            GameObject mural = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mural.name = "Mural_RoomA_Side";
            mural.transform.SetParent(parent, false);
            mural.transform.position = new Vector3(10f, 1f, 10f);
            mural.transform.localScale = new Vector3(2f, 2f, 0.2f);
            var paintable = mural.AddComponent<PaintableObject>();
            paintable.trueColor = new Color(0.9f, 0.2f, 0.2f); // 정육점 골목 = 빨강
            paintable.startGray = true;

            GameObject orbSpawnerGo = new GameObject("HiddenOrbSpawner_RoomA_Side");
            orbSpawnerGo.transform.SetParent(parent, false);
            orbSpawnerGo.transform.position = new Vector3(10f, 0.5f, 9f);
            var spawner = orbSpawnerGo.AddComponent<HiddenOrbSpawner>();
            spawner.trigger = paintable;
            spawner.orbColor = OrbColor.Red;
        }

        /// <summary>
        /// 방B — 벽화 3개(순수 장식 — ColorWaveEffect 반경 문제로 진짜 게이트로는 못 씀, 아래
        /// "2차 발견" 참고)는 색 복원 분위기만 내고, 실제 서쪽 문은 웨이브 전투(평 1+1)를
        /// 전부 클리어해야 열립니다.
        /// </summary>
        private static void PopulateRoomB(Transform parent, float centerX, GameObject doorBlocker)
        {
            Vector3[] muralPositions =
            {
                new Vector3(centerX + 8f, 1f, 8f),
                new Vector3(centerX, 1f, -10f),
                new Vector3(centerX - 8f, 1f, 8f),
            };
            Color[] colors =
            {
                new Color(0.6f, 0.2f, 0.8f),
                new Color(0.9f, 0.2f, 0.2f),
                new Color(0.9f, 0.7f, 0.2f),
            };
            for (int i = 0; i < muralPositions.Length; i++)
            {
                GameObject mural = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mural.name = $"Mural_RoomB_{i}";
                mural.transform.SetParent(parent, false);
                mural.transform.position = muralPositions[i];
                mural.transform.localScale = new Vector3(2f, 2f, 0.2f);
                var paintable = mural.AddComponent<PaintableObject>();
                paintable.trueColor = colors[i];
                paintable.startGray = true;
            }

            BuildWaveSpawner(parent, "EncounterSpawner_RoomB", new Vector3(centerX, 0f, CenterZ), new[] { 1, 1 });
            BuildRoomGate(parent, "RoomClearGate_B", centerX, doorBlocker);
        }

        /// <summary>방C — 평×3 전투(2웨이브) + 이동 장애물 2개(엄폐/회피용). 서쪽 문은 전투 클리어 시 열림.</summary>
        private static void PopulateRoomC(Transform parent, float centerX, GameObject doorBlocker)
        {
            BuildWaveSpawner(parent, "EncounterSpawner_RoomC", new Vector3(centerX, 0f, CenterZ), new[] { 2, 1 });
            BuildRoomGate(parent, "RoomClearGate_C", centerX, doorBlocker);

            BuildMovingObstacle(parent, "MovingObstacle_RoomC_0", new Vector3(centerX + 4f, 0.8f, -2f));
            BuildMovingObstacle(parent, "MovingObstacle_RoomC_1", new Vector3(centerX - 4f, 0.8f, 2f));
        }

        /// <summary>방D — 클라이맥스 웨이브(평×4, 2웨이브) + 색 복원 연출용 벽화 2개 + 실제 계단(방A에서 옮겨옴).</summary>
        private static void PopulateRoomD(Transform parent, float centerX)
        {
            BuildWaveSpawner(parent, "EncounterSpawner_RoomD", new Vector3(centerX, 0f, CenterZ), new[] { 2, 2 });

            for (int i = 0; i < 2; i++)
            {
                GameObject mural = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mural.name = $"Mural_RoomD_Climax_{i}";
                mural.transform.SetParent(parent, false);
                mural.transform.position = new Vector3(centerX, 1.2f, (i == 0 ? 12f : -12f));
                mural.transform.localScale = new Vector3(3f, 2.4f, 0.2f);
                var paintable = mural.AddComponent<PaintableObject>();
                paintable.trueColor = new Color(0.9f, 0.2f, 0.2f);
                paintable.startGray = true;
            }

            var stairsGo = GameObject.Find("Stairs");
            Stairs stairs = stairsGo != null ? stairsGo.GetComponent<Stairs>() : null;
            if (stairsGo != null)
            {
                stairsGo.transform.position = new Vector3(centerX - RoomHalf + 3f, stairsGo.transform.position.y, CenterZ);
                Debug.Log($"[MultiRoomFloor1Builder] 기존 Stairs를 방D({stairsGo.transform.position})로 이동 — 실제 '다음 층' 트리거는 이제 여기입니다.");
            }
            else
            {
                Debug.LogWarning("[MultiRoomFloor1Builder] 기존 Stairs를 못 찾아 방D로 옮기지 못했습니다.");
            }

            GameObject gateGo = new GameObject("RoomClearGate_D");
            gateGo.transform.SetParent(parent, false);
            gateGo.transform.position = new Vector3(centerX, 0f, CenterZ);
            var gate = gateGo.AddComponent<RoomClearGate>();
            gate.boundsSize = new Vector3(RoomHalf * 2f + 4f, 10f, RoomHalf * 2f + 4f);
            gate.stairs = stairs;
            Debug.Log("[MultiRoomFloor1Builder] 방D 신규 RoomClearGate — 클리어 시 Stairs가 열립니다.");
        }

        /// <summary>centerX 방 하나를 감시하는 RoomClearGate를 만들어 doorBlocker를 연결합니다.</summary>
        private static void BuildRoomGate(Transform parent, string name, float centerX, GameObject doorBlocker)
        {
            GameObject gateGo = new GameObject(name);
            gateGo.transform.SetParent(parent, false);
            gateGo.transform.position = new Vector3(centerX, 0f, CenterZ);
            var gate = gateGo.AddComponent<RoomClearGate>();
            gate.boundsSize = new Vector3(RoomHalf * 2f + 4f, 10f, RoomHalf * 2f + 4f);
            gate.doorsToOpen = doorBlocker != null ? new[] { doorBlocker } : new GameObject[0];
        }

        /// <summary>
        /// position을 중심으로 원형으로 흩어진 스폰 지점을 가진 EncounterSpawner를 만듭니다.
        /// enemiesPerWave = {1,1}이면 1마리씩 2웨이브, {2,1}이면 2마리→1마리 순으로 등장합니다
        /// (기존 튜토리얼 웨이브 패턴과 같은 이유 — 한꺼번에 다 나오는 것보다 순차 전투가 실제
        /// 소요 시간을 벌어줌).
        /// </summary>
        private static EncounterSpawner BuildWaveSpawner(Transform parent, string name, Vector3 position, int[] enemiesPerWave)
        {
            var pyeongPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/EnemyPyeong.prefab");
            if (pyeongPrefab == null) { Debug.LogError("[MultiRoomFloor1Builder] EnemyPyeong.prefab을 못 찾았습니다."); return null; }

            GameObject spawnerGo = new GameObject(name);
            spawnerGo.transform.SetParent(parent, false);
            spawnerGo.transform.position = position;
            var spawner = spawnerGo.AddComponent<EncounterSpawner>();
            spawner.waveInterval = 2.5f;
            spawner.autoStart = true;

            var waves = new EncounterSpawner.Wave[enemiesPerWave.Length];
            for (int w = 0; w < enemiesPerWave.Length; w++)
            {
                int count = enemiesPerWave[w];
                var prefabs = new GameObject[count];
                var points = new Transform[count];
                for (int i = 0; i < count; i++)
                {
                    prefabs[i] = pyeongPrefab;
                    float angle = count > 0 ? (360f / count) * i + w * 45f : 0f;
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 6f;
                    GameObject spGo = new GameObject($"{name}_W{w}_{i}");
                    spGo.transform.SetParent(spawnerGo.transform, false);
                    spGo.transform.position = position + offset;
                    points[i] = spGo.transform;
                }
                waves[w] = new EncounterSpawner.Wave { waveName = $"웨이브{w + 1}", enemyPrefabs = prefabs, spawnPoints = points, hpMultiplier = 1f };
            }
            spawner.waves = waves;
            return spawner;
        }

        private static void BuildMovingObstacle(Transform parent, string name, Vector3 position)
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.transform.SetParent(parent, false);
            obstacle.transform.position = position;
            obstacle.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            obstacle.transform.localScale = new Vector3(1.2f, 1.8f, 1.5f);
            var mover = obstacle.AddComponent<MovingObstacle>();
            mover.travelDistance = 3f;
            mover.period = 4f;
        }

        /// <summary>
        /// [[Face0ArtDressingBuilder]]가 서쪽 벽(원래 문 없는 통짜 벽)에 세웠던 건물+등불을,
        /// 이제 서쪽 벽에 방B로 가는 문이 생겼으므로 남쪽(문 없는 통짜) 벽으로 옮깁니다.
        /// 좌표는 어림하지 않고 실측 후 역산(이 세션의 기존 방식 그대로).
        /// </summary>
        private static void RebuildArtDressing(Transform parent)
        {
            GameObject prevArt = GameObject.Find("Face0_ArtDressing_Generated");
            if (prevArt != null) Object.DestroyImmediate(prevArt);

            var artRoot = new GameObject("Face0_ArtDressing_Generated");
            SceneManager.MoveGameObjectToScene(artRoot, parent.gameObject.scene);

            var buildingAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/BuildingModules/Building_StreetUnit_Demo.fbx");
            var lanternAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/Lanterns/RedLantern_01.fbx");
            if (buildingAsset == null || lanternAsset == null) return;

            // 남쪽 벽(-Z, 문 없음)에 정면(+Z)이 방 안쪽을 보도록 배치 — 기본 방향 그대로 두면
            // 정면이 +Z를 보므로 추가 회전이 필요 없습니다(1층 첫 배치 때와 동일 논리).
            Bounds b = ArtDressingUtils.MeasureBounds(buildingAsset);
            float southZ = CenterZ - RoomHalf; // 방A 남쪽 벽 안쪽 경계
            float offsetZ = southZ - b.min.z;

            GameObject building = ArtDressingUtils.Instantiate(buildingAsset, artRoot.transform, "StreetUnit_Demo_South",
                new Vector3(RoomACenterX - (b.min.x + b.max.x) * 0.5f, 0f, offsetZ));

            GameObject lantern = ArtDressingUtils.Instantiate(lanternAsset, artRoot.transform, "RedLantern_South",
                new Vector3(RoomACenterX, 2.3f, southZ + 2.6f));
        }
    }
}
