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

            // 방A의 기존 RoomClearGate는 튜토리얼 전투(평×2) 감시만 계속하고, "다음 층" 계단 연결은
            // 뗍니다(서쪽 문이 이미 열려있어서 따로 안 잠가도 됨) — 실제 계단 연결은 아래에서
            // 방D로 옮깁니다.
            var roomAGate = GameObject.Find("RoomClearGate")?.GetComponent<RoomClearGate>();
            RoomClearGate roomDGate = roomAGate; // 재사용 — 방D로 이동시킴(아래)

            BuildRoomASidePath(root.transform);

            BuildConnector(root.transform, "A_B", RoomACenterX - RoomHalf, RoomBCenterX + RoomHalf);
            GameObject doorBlockerB = BuildRoom(root.transform, "B", RoomBCenterX, eastDoorWidth: DoorWidth, westDoorWidth: DoorWidth, lockWestDoor: true);
            PopulateRoomB(root.transform, RoomBCenterX, doorBlockerB);

            BuildConnector(root.transform, "B_C", RoomBCenterX - RoomHalf, RoomCCenterX + RoomHalf);
            BuildRoom(root.transform, "C", RoomCCenterX, eastDoorWidth: DoorWidth, westDoorWidth: DoorWidth, lockWestDoor: false);
            PopulateRoomC(root.transform, RoomCCenterX);

            BuildConnector(root.transform, "C_D", RoomCCenterX - RoomHalf, RoomDCenterX + RoomHalf);
            BuildRoom(root.transform, "D", RoomDCenterX, eastDoorWidth: DoorWidth, westDoorWidth: 0f, lockWestDoor: false);
            PopulateRoomD(root.transform, RoomDCenterX, roomDGate);

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

            // 구조적 개구부는 이미 뚫려있고, 그 자리에 딱 맞는 막음 큐브를 세웁니다.
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = $"DoorBlocker_{label}_West";
            blocker.transform.SetParent(parent, false);
            blocker.transform.position = new Vector3(centerX - half, WallHeight * 0.5f, CenterZ);
            blocker.transform.localScale = new Vector3(t + 0.5f, WallHeight, westDoorWidth - 0.2f);
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

        /// <summary>방B — 벽화 3개를 전부 복원해야 서쪽 문이 열리는 라이트 퍼즐 + 매복 평 2마리.</summary>
        private static void PopulateRoomB(Transform parent, float centerX, GameObject doorBlocker)
        {
            var triggers = new PaintableObject[3];
            Vector3[] muralPositions =
            {
                new Vector3(centerX + 8f, 1f, 8f),
                new Vector3(centerX, 1f, -10f),
                new Vector3(centerX - 8f, 1f, 8f),
            };
            Color[] colors =
            {
                new Color(0.6f, 0.2f, 0.8f), // 뒷골목=보라 톤과 어울리게(다음 면과의 시각적 구분용, 여기선 장식일 뿐)
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
                triggers[i] = paintable;
            }

            GameObject gateGo = new GameObject("PaintCountGate_RoomB");
            gateGo.transform.SetParent(parent, false);
            gateGo.transform.position = new Vector3(centerX, 0f, CenterZ);
            var gate = gateGo.AddComponent<PaintCountGate>();
            gate.triggers = triggers;
            gate.doorsToOpen = doorBlocker != null ? new[] { doorBlocker } : new GameObject[0];

            SpawnPyeong(parent, "EnemyAmbush_RoomB_0", new Vector3(centerX + 3f, 0f, 3f));
            SpawnPyeong(parent, "EnemyAmbush_RoomB_1", new Vector3(centerX - 3f, 0f, -3f));
        }

        /// <summary>방C — 평×3 전투 + 이동 장애물 2개(엄폐/회피용).</summary>
        private static void PopulateRoomC(Transform parent, float centerX)
        {
            SpawnPyeong(parent, "EnemyFight_RoomC_0", new Vector3(centerX + 6f, 0f, 6f));
            SpawnPyeong(parent, "EnemyFight_RoomC_1", new Vector3(centerX, 0f, -8f));
            SpawnPyeong(parent, "EnemyFight_RoomC_2", new Vector3(centerX - 6f, 0f, 6f));

            BuildMovingObstacle(parent, "MovingObstacle_RoomC_0", new Vector3(centerX + 4f, 0.8f, -2f));
            BuildMovingObstacle(parent, "MovingObstacle_RoomC_1", new Vector3(centerX - 4f, 0.8f, 2f));
        }

        /// <summary>방D — 클라이맥스 웨이브(평×4) + 색 복원 연출용 벽화 2개 + 실제 계단(방A에서 옮겨옴).</summary>
        private static void PopulateRoomD(Transform parent, float centerX, RoomClearGate movedGate)
        {
            SpawnPyeong(parent, "EnemyFinal_RoomD_0", new Vector3(centerX + 8f, 0f, 8f));
            SpawnPyeong(parent, "EnemyFinal_RoomD_1", new Vector3(centerX + 8f, 0f, -8f));
            SpawnPyeong(parent, "EnemyFinal_RoomD_2", new Vector3(centerX - 8f, 0f, 8f));
            SpawnPyeong(parent, "EnemyFinal_RoomD_3", new Vector3(centerX - 8f, 0f, -8f));

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

            var stairs = GameObject.Find("Stairs");
            if (stairs != null)
            {
                stairs.transform.position = new Vector3(centerX - RoomHalf + 3f, stairs.transform.position.y, CenterZ);
                Debug.Log($"[MultiRoomFloor1Builder] 기존 Stairs를 방D({stairs.transform.position})로 이동 — 실제 '다음 층' 트리거는 이제 여기입니다.");
            }
            else
            {
                Debug.LogWarning("[MultiRoomFloor1Builder] 기존 Stairs를 못 찾아 방D로 옮기지 못했습니다.");
            }

            if (movedGate != null)
            {
                movedGate.name = "RoomClearGate_D";
                movedGate.transform.position = new Vector3(centerX, movedGate.transform.position.y, CenterZ);
                movedGate.boundsSize = new Vector3(RoomHalf * 2f + 4f, 10f, RoomHalf * 2f + 4f);
                movedGate.stairs = stairs != null ? stairs.GetComponent<Stairs>() : null;
                Debug.Log("[MultiRoomFloor1Builder] RoomClearGate를 방D로 옮기고 재사용 — 클리어 시 위 Stairs가 열립니다.");
            }
            else
            {
                Debug.LogWarning("[MultiRoomFloor1Builder] 기존 RoomClearGate를 못 찾아 방D 클리어 판정이 없습니다.");
            }
        }

        private static void SpawnPyeong(Transform parent, string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/EnemyPyeong.prefab");
            if (prefab == null) { Debug.LogError("[MultiRoomFloor1Builder] EnemyPyeong.prefab을 못 찾았습니다."); return; }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = position;
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
