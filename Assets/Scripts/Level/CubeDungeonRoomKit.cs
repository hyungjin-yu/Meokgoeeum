using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeDungeonRoomKit (큐브 면 던전 방 빌드 — 런타임/에디터 공유 순수 로직)
    /// [[CubeFaceRoomBuilder]](에디터, 최초 씬 세팅)와 [[CubeDungeonProgressionManager]](런타임,
    /// 방F 클리어 시 다음 층 재생성)가 똑같은 코드를 쓰도록 뽑아낸 것입니다. `UnityEditor` API를
    /// 전혀 안 써서 실제 빌드에도 그대로 포함되고 런타임에서 안전하게 호출할 수 있습니다.
    ///
    /// [[changelog/2026-09-15_실제던전-큐브면포팅-1차]]/[[changelog/2026-09-15_큐브던전-웨이브스포너와장식]]에서
    /// `CubeFaceRoomBuilder` 안에 직접 있던 벽/스폰/장식 빌더들을 그대로 옮긴 것 — 로직 자체는
    /// 안 바뀌었고, `cubeCenter`/`cubeHalfExtent`/`enemyPrefab`을 (전엔 `GameObject.Find`나
    /// `AssetDatabase`로 안에서 직접 구했던 것을) 전부 매개변수로 받도록만 바꿨습니다.
    ///
    /// ⚠️ 2026-09-16 — 방 6개(A~F)로 확장, 큐브 6면 전부 사용
    /// 기존엔 방 4개(A~D)만 큐브 4개 면(+Z/+Y/-Z/-Y)에 돌아가며 배치되고 ±X 2개 면은 AI
    /// 테스트용으로 안 쓰였는데, 사용자 요청으로 그 두 면도 실제 던전 방(E=+X, F=-X)으로
    /// 편입시켜 **한 층이 큐브 6면 전부를 쓰는** 구조로 확장했습니다 — "이 세계는 큐브 1개"
    /// 기획 의도([[05 맵 시스템 - 큐브 구조]])에 더 가까움. 정육면체의 면-인접 그래프는
    /// 팔면체 그래프(각 면이 반대편 면 1개만 빼고 나머지 4개와 인접)라서 6면을 한붓그리기로
    /// 도는 고리(Hamiltonian cycle)가 항상 존재합니다 — 순서는 A(+Z)→B(+Y)→E(+X)→C(-Z)→D(-Y)→F(-X).
    /// 이동/면 전환 수학([[CubeSurfaceWalker]], [[CubeEnemyBase]], `GetFaceBasis`/`BuildFaceWall`)은
    /// 전혀 안 바뀌었습니다 — 축이 정렬된 정육면체 면은 원래도 6개 다 똑같은 방식으로 다뤄지므로,
    /// 방 개수/문 배선만 확장하면 됩니다.
    ///
    /// **문 배선을 4방향으로 일반화**: 기존엔 방마다 v±(RoomHalf) 벽에만 문을 낼 수 있었고
    /// (u±는 항상 막힘 — "링에 안 쓰는 면과 맞닿는 쪽"이라 막아뒀던 것), 이제 6면을 전부 쓰면서
    /// u±에도 이웃 방이 생기므로 `BuildRoom()`이 4방향(±U/±V) 전부를 `WallMode`로 받도록 확장했고
    /// `BuildDoorWall()`도 축 하나만 처리하던 걸 U/V 어느 쪽이든 처리하도록 일반화했습니다.
    ///
    /// **각 방의 문 배선표** (직접 손으로 유도 — 각 면의 u,v축이 항상 월드 좌표축 중 하나와
    /// 정확히 일치한다는 사실을 이용해, "면 법선 n의 +u/-u/+v/-v 방향에 있는 이웃 면의 법선은
    /// 각각 정확히 u/-u/v/-v 그 자체"라는 규칙으로 계산함):
    /// - A(+Z): -U(→E)닫힘, +U(→F)닫힘, -V(→D)닫힘, +V(→B)열림(진행 방향)
    /// - B(+Y): -U(→F)닫힘, +U(→E)열림(진행 방향), -V(→C)닫힘, +V(→A)열림(입장, 게이트 없음)
    /// - E(+X): -U(→C)열림(진행 방향), +U(→A)닫힘, -V(→D)닫힘, +V(→B)열림(입장, 게이트 없음)
    /// - C(-Z): -U(→F)닫힘, +U(→E)열림(입장, 게이트 없음), -V(→D)열림(진행 방향), +V(→B)닫힘
    /// - D(-Y): -U(→E)닫힘, +U(→F)열림(진행 방향), -V(→C)열림(입장, 게이트 없음), +V(→A)닫힘
    /// - F(-X): -U(→A)닫힘(다음 층 전환은 물리적 문이 아니라 재생성+텔레포트로 처리), +U(→C)닫힘,
    ///   -V(→D)열림(입장, 게이트 없음), +V(→B)닫힘
    /// A→F로 돌아가는 6번째 간선은 일부러 물리적 문을 안 냄(F 클리어 시 [[CubeDungeonProgressionManager]]가
    /// 기존 방을 통째로 지우고 새 A를 지어 텔레포트하므로, 그 순간엔 이미 옛 A가 없어서 걸어서 갈 방법도 없음).
    /// </summary>
    public static class CubeDungeonRoomKit
    {
        public const float RoomHalf = 15f;
        public const float WallHeight = 3f;
        public const float WallThickness = 2f;
        public const float DoorWidth = 4f;

        public enum WallMode { Closed, Open, GatedExit }
        private enum WallSide { MinusU, PlusU, MinusV, PlusV }

        /// <summary>BuildRoom()이 돌려주는 4방향 문 블로커. GatedExit이 아닌 방향은 null입니다.</summary>
        public struct RoomDoors
        {
            public GameObject minusU, plusU, minusV, plusV;
        }

        // ------------------------------------------------------------------
        // 면 좌표계 헬퍼
        // ------------------------------------------------------------------

        /// <summary>
        /// 면 법선 하나로부터 그 면 위의 국소 2축(u, v)을 고정적으로 뽑습니다. v는 "위"에 가까운
        /// 접선(윗/아랫면은 "앞"으로 대체), u = cross(normal, v). [[CubeSurfaceWalker]]의
        /// AnyTangentTo()와 같은 논리를 재사용하되, 여기선 벽 배치용으로 u까지 같이 고정합니다.
        /// </summary>
        public static void GetFaceBasis(Vector3 n, out Vector3 u, out Vector3 v)
        {
            v = Vector3.ProjectOnPlane(Vector3.up, n);
            if (v.sqrMagnitude < 0.0001f) v = Vector3.ProjectOnPlane(Vector3.forward, n);
            v.Normalize();
            u = Vector3.Cross(n, v).normalized;
        }

        // ------------------------------------------------------------------
        // 벽 지오메트리
        // ------------------------------------------------------------------

        /// <summary>
        /// 면 위에 박스 하나를 세웁니다. uSize는 u축 방향 크기, vSize는 v축 방향 크기,
        /// 높이(법선 방향)는 항상 WallHeight로 고정. 회전은 LookRotation(v, normal)로 맞춰서
        /// 로컬 X→u, Y→normal, Z→v가 되게 합니다(localScale이 그대로 월드 크기와 대응).
        /// </summary>
        public static GameObject BuildFaceWall(Transform parent, string name, Vector3 cubeCenter, float cubeHalfExtent,
            Vector3 normal, Vector3 u, Vector3 v, float uCenter, float vCenter, float uSize, float vSize)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = cubeCenter + normal * (cubeHalfExtent + WallHeight * 0.5f) + u * uCenter + v * vCenter;
            wall.transform.rotation = Quaternion.LookRotation(v, normal);
            wall.transform.localScale = new Vector3(uSize, WallHeight, vSize);
            return wall;
        }

        /// <summary>
        /// 방의 네 변 중 하나(±U 또는 ±V, RoomHalf만큼 떨어진 위치)에 벽을 세웁니다. Closed면
        /// 통짜, Open/GatedExit면 가운데 DoorWidth만큼 틈을 내고 양옆만 세웁니다. GatedExit면
        /// 그 틈을 막는 블로커도 하나 더 세워서 반환합니다([[RoomClearGate]].doorsToOpen이 나중에
        /// 비활성화). U변이든 V변이든 "고정 축"과 "따라가는 축"만 바뀔 뿐 로직은 동일해서 하나로
        /// 통합했습니다(2026-09-16 — 기존엔 V변 전용이었고 U변은 항상 통짜로 하드코딩돼 있었음).
        /// </summary>
        private static GameObject BuildDoorWall(Transform parent, string label, Vector3 cubeCenter, float cubeHalfExtent,
            Vector3 normal, Vector3 u, Vector3 v, WallSide side, WallMode mode)
        {
            float half = RoomHalf, t = WallThickness;
            bool isVSide = side == WallSide.MinusV || side == WallSide.PlusV;
            float sign = (side == WallSide.PlusU || side == WallSide.PlusV) ? 1f : -1f;
            float fixedCenter = sign * (half + t * 0.5f);
            string sideName = side.ToString();

            // isVSide면 기존과 동일하게(고정=v, 따라감=u), 아니면 두 축 역할을 바꿔서(고정=u, 따라감=v) 재사용.
            GameObject Wall(string suffix, float spanCenter, float spanSize) => isVSide
                ? BuildFaceWall(parent, $"Wall_{label}_{sideName}{suffix}", cubeCenter, cubeHalfExtent, normal, u, v, spanCenter, fixedCenter, spanSize, t)
                : BuildFaceWall(parent, $"Wall_{label}_{sideName}{suffix}", cubeCenter, cubeHalfExtent, normal, u, v, fixedCenter, spanCenter, t, spanSize);

            if (mode == WallMode.Closed)
            {
                Wall("", 0f, half * 2f);
                return null;
            }

            float doorHalf = DoorWidth * 0.5f;
            float segLen = half - doorHalf;
            if (segLen > 0.01f)
            {
                Wall("_Neg", -(doorHalf + segLen * 0.5f), segLen);
                Wall("_Pos", (doorHalf + segLen * 0.5f), segLen);
            }

            if (mode == WallMode.GatedExit)
                return isVSide
                    ? BuildFaceWall(parent, $"DoorBlocker_{label}_{sideName}", cubeCenter, cubeHalfExtent, normal, u, v, 0f, fixedCenter, DoorWidth - 0.2f, t + 0.5f)
                    : BuildFaceWall(parent, $"DoorBlocker_{label}_{sideName}", cubeCenter, cubeHalfExtent, normal, u, v, fixedCenter, 0f, t + 0.5f, DoorWidth - 0.2f);

            return null; // Open — 틈만 내고 막음 없음(항상 통과 가능)
        }

        /// <summary>
        /// 면 하나에 방 하나(사방 벽)를 짓습니다. 2026-09-16 — 예전엔 v±만 문을 낼 수 있었는데
        /// (u±는 항상 막힘, "링에 안 쓰는 면과 맞닿는 쪽"이라서), 이제 6면을 전부 써서 u±에도
        /// 실제 이웃 방이 생기므로 4방향 전부 WallMode를 받도록 확장했습니다.
        /// </summary>
        public static RoomDoors BuildRoom(Transform parent, string label, Vector3 cubeCenter, float cubeHalfExtent,
            Vector3 normal, WallMode minusU, WallMode plusU, WallMode minusV, WallMode plusV)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            return new RoomDoors
            {
                minusU = BuildDoorWall(parent, label, cubeCenter, cubeHalfExtent, normal, u, v, WallSide.MinusU, minusU),
                plusU = BuildDoorWall(parent, label, cubeCenter, cubeHalfExtent, normal, u, v, WallSide.PlusU, plusU),
                minusV = BuildDoorWall(parent, label, cubeCenter, cubeHalfExtent, normal, u, v, WallSide.MinusV, minusV),
                plusV = BuildDoorWall(parent, label, cubeCenter, cubeHalfExtent, normal, u, v, WallSide.PlusV, plusV),
            };
        }

        // ------------------------------------------------------------------
        // 콘텐츠 (스폰/장식)
        // ------------------------------------------------------------------

        /// <summary>
        /// 방 중앙에 [[EncounterSpawner]]를 큐브 면 모드로 세웁니다. `enemyPrefab`은 평소처럼
        /// `EnemyPyeong.prefab`(NavMesh 버전)을 그대로 넘기면 됩니다 — `EncounterSpawner`가
        /// `cubeCenter`가 설정돼 있으면 스폰 시점에 자동으로 [[CubeEnemyPyeong]]으로 교체합니다.
        /// </summary>
        public static EncounterSpawner BuildWaveSpawner(Transform parent, string name, Transform cubePlanet, float cubeHalfExtent,
            Vector3 normal, Vector3 u, Vector3 v, CubeSurfaceWalker target, GameObject enemyPrefab, int[] enemiesPerWave)
        {
            if (enemyPrefab == null) { Debug.LogError("[CubeDungeonRoomKit] enemyPrefab이 비어있습니다."); return null; }

            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;
            Vector3 spawnerPos = cubeCenter + normal * (cubeHalfExtent + 1f);
            GameObject spawnerGo = new GameObject(name);
            spawnerGo.transform.SetParent(parent, false);
            spawnerGo.transform.position = spawnerPos;

            var spawner = spawnerGo.AddComponent<EncounterSpawner>();
            spawner.waveInterval = 2.5f;
            spawner.autoStart = true;
            spawner.cubeCenter = cubePlanet;
            spawner.cubeFaceNormal = normal;
            spawner.cubeHalfExtent = cubeHalfExtent;
            spawner.cubeTarget = target;

            var waves = new EncounterSpawner.Wave[enemiesPerWave.Length];
            for (int w = 0; w < enemiesPerWave.Length; w++)
            {
                int count = enemiesPerWave[w];
                var prefabs = new GameObject[count];
                var points = new Transform[count];
                for (int i = 0; i < count; i++)
                {
                    prefabs[i] = enemyPrefab;
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
        public static PaintableObject BuildMural(Transform parent, string name, Vector3 cubeCenter, float cubeHalfExtent,
            Vector3 normal, Vector3 u, Vector3 v, float uOffset, float vOffset, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = cubeCenter + normal * (cubeHalfExtent + 0.1f) + u * uOffset + v * vOffset;
            go.transform.rotation = Quaternion.LookRotation(v, normal);
            go.transform.localScale = new Vector3(2.5f, 0.2f, 2.5f);

            // 2026-09-15: [[CubeSurfaceWalker]]가 벽/문 막음을 실제로 막도록 콜라이더 충돌 검사를
            // 새로 추가하면서, 바닥에 눕혀놓은 이 장식용 벽화까지 덩달아 막혀버리는 부작용이
            // 생겼음(원래 "바닥 문양"일 뿐 장애물이 아님). Collider.isTrigger를 켜서 플레이어는
            // 그냥 지나가되, [[ColorWaveEffect]]의 Physics.OverlapSphere 색칠 판정은 트리거도
            // 잡으므로 그대로 정상 작동함.
            go.GetComponent<Collider>().isTrigger = true;

            var paintable = go.AddComponent<PaintableObject>();
            paintable.trueColor = color;
            paintable.startGray = true;
            return paintable;
        }

        /// <summary>면 위 (uOffset, vOffset)에 좌우(u축)로 왕복하는 [[MovingObstacle]]을 세웁니다.</summary>
        public static void BuildRoomObstacle(Transform parent, string name, Vector3 cubeCenter, float cubeHalfExtent,
            Vector3 normal, Vector3 u, Vector3 v, float uOffset, float vOffset)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = cubeCenter + normal * (cubeHalfExtent + 0.9f) + u * uOffset + v * vOffset;
            go.transform.rotation = Quaternion.LookRotation(v, normal); // 로컬 X→u라서 MovingObstacle의 transform.right 왕복이 u축을 따라감
            go.transform.localScale = new Vector3(1.5f, 1.8f, 1.2f);

            var mover = go.AddComponent<MovingObstacle>();
            mover.travelDistance = 3f;
            mover.period = 4f;
        }

        public static RoomClearGate BuildRoomGate(Transform parent, string name, Vector3 cubeCenter, float cubeHalfExtent,
            Vector3 normal, GameObject doorBlocker)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            GameObject gateGo = new GameObject(name);
            gateGo.transform.SetParent(parent, false);
            gateGo.transform.position = cubeCenter + normal * (cubeHalfExtent + 5f);
            gateGo.transform.rotation = Quaternion.LookRotation(v, normal);

            var gate = gateGo.AddComponent<RoomClearGate>();
            gate.boundsSize = new Vector3(RoomHalf * 2f + 4f, 10f, RoomHalf * 2f + 4f);
            gate.doorsToOpen = doorBlocker != null ? new[] { doorBlocker } : new GameObject[0];
            return gate;
        }

        // ------------------------------------------------------------------
        // 방별 콘텐츠 (원본 [[MultiRoomFloor1Builder]] 방A~D + 신규 E/F를 면 기준으로 포팅)
        // ------------------------------------------------------------------

        /// <summary>방A — 튜토리얼 전투(평×1+1, 2웨이브) + 벽화/숨겨진 구슬 곁가지.</summary>
        public static void PopulateRoomA(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            GameObject doorBlocker, CubeSurfaceWalker target, GameObject enemyPrefab, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomA", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPrefab, new[] { 1, 1 });
            spawner.showFirstAttackHint = true;
            ApplyHpMultiplier(spawner, hpMultiplier);

            var mural = BuildMural(parent, "Mural_RoomA_Side", cubeCenter, cubeHalfExtent, normal, u, v, 10f, 8f, new Color(0.9f, 0.2f, 0.2f));
            GameObject orbGo = new GameObject("HiddenOrbSpawner_RoomA_Side");
            orbGo.transform.SetParent(parent, false);
            orbGo.transform.position = cubeCenter + normal * (cubeHalfExtent + 0.5f) + u * 10f + v * 7f;
            var orbSpawner = orbGo.AddComponent<HiddenOrbSpawner>();
            orbSpawner.trigger = mural;
            orbSpawner.orbColor = OrbColor.Red;

            BuildRoomGate(parent, "RoomClearGate_A", cubeCenter, cubeHalfExtent, normal, doorBlocker);
        }

        /// <summary>방B — 평×1+1(2웨이브) + 장식 벽화 3개(게이트 아님, 순수 장식).</summary>
        public static void PopulateRoomB(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            GameObject doorBlocker, CubeSurfaceWalker target, GameObject enemyPrefab, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomB", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPrefab, new[] { 1, 1 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildMural(parent, "Mural_RoomB_0", cubeCenter, cubeHalfExtent, normal, u, v, 8f, 8f, new Color(0.6f, 0.2f, 0.8f));
            BuildMural(parent, "Mural_RoomB_1", cubeCenter, cubeHalfExtent, normal, u, v, 0f, -10f, new Color(0.9f, 0.2f, 0.2f));
            BuildMural(parent, "Mural_RoomB_2", cubeCenter, cubeHalfExtent, normal, u, v, -8f, 8f, new Color(0.9f, 0.7f, 0.2f));

            BuildRoomGate(parent, "RoomClearGate_B", cubeCenter, cubeHalfExtent, normal, doorBlocker);
        }

        /// <summary>방C — 평×2+1(2웨이브) + 왕복 장애물 2개.</summary>
        public static void PopulateRoomC(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            GameObject doorBlocker, CubeSurfaceWalker target, GameObject enemyPrefab, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomC", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPrefab, new[] { 2, 1 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildRoomObstacle(parent, "MovingObstacle_RoomC_0", cubeCenter, cubeHalfExtent, normal, u, v, 4f, -2f);
            BuildRoomObstacle(parent, "MovingObstacle_RoomC_1", cubeCenter, cubeHalfExtent, normal, u, v, -4f, 2f);

            BuildRoomGate(parent, "RoomClearGate_C", cubeCenter, cubeHalfExtent, normal, doorBlocker);
        }

        /// <summary>방D — 평×2+1(2웨이브) + 왕복 장애물 1개. 2026-09-16 — 6방 구조에서는 클라이맥스가 아니라 중간 방.</summary>
        public static void PopulateRoomD(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            GameObject doorBlocker, CubeSurfaceWalker target, GameObject enemyPrefab, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomD", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPrefab, new[] { 2, 1 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildRoomObstacle(parent, "MovingObstacle_RoomD_0", cubeCenter, cubeHalfExtent, normal, u, v, 0f, 3f);

            BuildRoomGate(parent, "RoomClearGate_D", cubeCenter, cubeHalfExtent, normal, doorBlocker);
        }

        /// <summary>방E — 평×2(2웨이브) + 장식 벽화 2개. 2026-09-16 신규(+X면, 예전엔 AI 테스트용).</summary>
        public static void PopulateRoomE(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            GameObject doorBlocker, CubeSurfaceWalker target, GameObject enemyPrefab, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomE", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPrefab, new[] { 1, 1 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildMural(parent, "Mural_RoomE_0", cubeCenter, cubeHalfExtent, normal, u, v, 6f, -6f, new Color(0.9f, 0.7f, 0.2f));
            BuildMural(parent, "Mural_RoomE_1", cubeCenter, cubeHalfExtent, normal, u, v, -6f, 6f, new Color(0.6f, 0.2f, 0.8f));

            BuildRoomGate(parent, "RoomClearGate_E", cubeCenter, cubeHalfExtent, normal, doorBlocker);
        }

        /// <summary>
        /// 방F — 평×2+2(2웨이브, 클라이맥스) + 벽화 2개. 2026-09-16 신규(-X면, 예전엔 AI 테스트용) —
        /// 6방 구조의 마지막 방. 게이트를 반환 — 다음 층 전환에 구독해서 씁니다.
        /// </summary>
        public static RoomClearGate PopulateRoomF(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            CubeSurfaceWalker target, GameObject enemyPrefab, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomF", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPrefab, new[] { 2, 2 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildMural(parent, "Mural_RoomF_Climax_0", cubeCenter, cubeHalfExtent, normal, u, v, 0f, 12f, new Color(0.9f, 0.2f, 0.2f));
            BuildMural(parent, "Mural_RoomF_Climax_1", cubeCenter, cubeHalfExtent, normal, u, v, 0f, -12f, new Color(0.9f, 0.2f, 0.2f));

            // stairs는 안 씀 — [[CubeDungeonProgressionManager]]가 반환된 게이트의 OnCleared를
            // 구독해서 "다음 층 재생성"을 직접 트리거함(2026-09-15, F로 이관 2026-09-16).
            return BuildRoomGate(parent, "RoomClearGate_F", cubeCenter, cubeHalfExtent, normal, null);
        }

        private static void ApplyHpMultiplier(EncounterSpawner spawner, float hpMultiplier)
        {
            if (spawner == null || Mathf.Approximately(hpMultiplier, 1f)) return;
            foreach (var wave in spawner.waves)
                wave.hpMultiplier *= hpMultiplier;
        }

        /// <summary>
        /// 방A~F 전체(큐브 6면 전부)를 한 번에 짓습니다. `root`는 호출하는 쪽이 만들어서
        /// 넘겨야 합니다(이전 층 잔재 삭제 여부는 호출자 책임 — 에디터는 DestroyImmediate,
        /// 런타임은 Destroy를 써야 해서 여기선 관여하지 않습니다). `hpMultiplier`는 층이
        /// 올라갈수록 적을 강하게 만들 때 씀(1층=1.0 기본).
        ///
        /// 경로: A(+Z)→B(+Y)→E(+X)→C(-Z)→D(-Y)→F(-X) — 클래스 doc의 "각 방의 문 배선표" 참고.
        /// </summary>
        public static RoomClearGate BuildFullFloor(Transform root, Transform cubePlanet, float cubeHalfExtent,
            CubeSurfaceWalker player, GameObject enemyPrefab, float hpMultiplier)
        {
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var doorsA = BuildRoom(root, "A", cubeCenter, cubeHalfExtent, Vector3.forward,
                WallMode.Closed, WallMode.Closed, WallMode.Closed, WallMode.GatedExit);
            PopulateRoomA(root, cubePlanet, cubeHalfExtent, Vector3.forward, doorsA.plusV, player, enemyPrefab, hpMultiplier);

            var doorsB = BuildRoom(root, "B", cubeCenter, cubeHalfExtent, Vector3.up,
                WallMode.Closed, WallMode.GatedExit, WallMode.Closed, WallMode.Open);
            PopulateRoomB(root, cubePlanet, cubeHalfExtent, Vector3.up, doorsB.plusU, player, enemyPrefab, hpMultiplier);

            var doorsE = BuildRoom(root, "E", cubeCenter, cubeHalfExtent, Vector3.right,
                WallMode.GatedExit, WallMode.Closed, WallMode.Closed, WallMode.Open);
            PopulateRoomE(root, cubePlanet, cubeHalfExtent, Vector3.right, doorsE.minusU, player, enemyPrefab, hpMultiplier);

            var doorsC = BuildRoom(root, "C", cubeCenter, cubeHalfExtent, Vector3.back,
                WallMode.Closed, WallMode.Open, WallMode.GatedExit, WallMode.Closed);
            PopulateRoomC(root, cubePlanet, cubeHalfExtent, Vector3.back, doorsC.minusV, player, enemyPrefab, hpMultiplier);

            var doorsD = BuildRoom(root, "D", cubeCenter, cubeHalfExtent, Vector3.down,
                WallMode.Closed, WallMode.GatedExit, WallMode.Open, WallMode.Closed);
            PopulateRoomD(root, cubePlanet, cubeHalfExtent, Vector3.down, doorsD.plusU, player, enemyPrefab, hpMultiplier);

            BuildRoom(root, "F", cubeCenter, cubeHalfExtent, Vector3.left,
                WallMode.Closed, WallMode.Closed, WallMode.Open, WallMode.Closed);
            return PopulateRoomF(root, cubePlanet, cubeHalfExtent, Vector3.left, player, enemyPrefab, hpMultiplier);
        }
    }
}
