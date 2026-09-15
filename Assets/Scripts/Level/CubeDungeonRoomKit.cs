using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeDungeonRoomKit (큐브 면 던전 방 빌드 — 런타임/에디터 공유 순수 로직)
    /// [[CubeFaceRoomBuilder]](에디터, 최초 씬 세팅)와 [[CubeDungeonProgressionManager]](런타임,
    /// 방D 클리어 시 다음 층 재생성)가 똑같은 코드를 쓰도록 뽑아낸 것입니다. `UnityEditor` API를
    /// 전혀 안 써서 실제 빌드에도 그대로 포함되고 런타임에서 안전하게 호출할 수 있습니다.
    ///
    /// [[changelog/2026-09-15_실제던전-큐브면포팅-1차]]/[[changelog/2026-09-15_큐브던전-웨이브스포너와장식]]에서
    /// `CubeFaceRoomBuilder` 안에 직접 있던 벽/스폰/장식 빌더들을 그대로 옮긴 것 — 로직 자체는
    /// 안 바뀌었고, `cubeCenter`/`cubeHalfExtent`/`enemyPrefab`을 (전엔 `GameObject.Find`나
    /// `AssetDatabase`로 안에서 직접 구했던 것을) 전부 매개변수로 받도록만 바꿨습니다.
    /// </summary>
    public static class CubeDungeonRoomKit
    {
        public const float RoomHalf = 15f;
        public const float WallHeight = 3f;
        public const float WallThickness = 2f;
        public const float DoorWidth = 4f;

        public enum WallMode { Closed, Open, GatedExit }

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
        /// v = ±(half+t/2) 위치의 벽 한 쌍(문 쪽) — Closed면 통짜, Open/GatedExit면 가운데
        /// DoorWidth만큼 틈을 내고 양옆만 세웁니다. GatedExit면 그 틈을 막는 블로커도 하나
        /// 더 세워서 반환합니다(RoomClearGate.doorsToOpen이 나중에 비활성화).
        /// </summary>
        public static GameObject BuildDoorWall(Transform parent, string label, string side, Vector3 cubeCenter, float cubeHalfExtent,
            Vector3 normal, Vector3 u, Vector3 v, float vSign, WallMode mode)
        {
            float half = RoomHalf, t = WallThickness;
            float vCenter = vSign * (half + t * 0.5f);

            if (mode == WallMode.Closed)
            {
                BuildFaceWall(parent, $"Wall_{label}_{side}", cubeCenter, cubeHalfExtent, normal, u, v, 0f, vCenter, half * 2f, t);
                return null;
            }

            float doorHalf = DoorWidth * 0.5f;
            float segLen = half - doorHalf;
            if (segLen > 0.01f)
            {
                BuildFaceWall(parent, $"Wall_{label}_{side}_Neg", cubeCenter, cubeHalfExtent, normal, u, v, -(doorHalf + segLen * 0.5f), vCenter, segLen, t);
                BuildFaceWall(parent, $"Wall_{label}_{side}_Pos", cubeCenter, cubeHalfExtent, normal, u, v, (doorHalf + segLen * 0.5f), vCenter, segLen, t);
            }

            if (mode == WallMode.GatedExit)
                return BuildFaceWall(parent, $"DoorBlocker_{label}_{side}", cubeCenter, cubeHalfExtent, normal, u, v, 0f, vCenter, DoorWidth - 0.2f, t + 0.5f);

            return null; // Open — 틈만 내고 막음 없음(항상 통과 가능)
        }

        /// <summary>
        /// 면 하나에 방 하나(사방 벽)를 짓습니다. u±15 벽(링에 안 쓰는 면과 맞닿는 쪽)은 항상
        /// 막힘. v±15 벽이 이웃 면과 실제로 맞닿는 모서리라 여기에 문/게이트를 냅니다.
        /// </summary>
        public static (GameObject minus, GameObject plus) BuildRoom(Transform parent, string label, Vector3 cubeCenter, float cubeHalfExtent,
            Vector3 normal, WallMode minusVMode, WallMode plusVMode)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            float half = RoomHalf, t = WallThickness;

            BuildFaceWall(parent, $"Wall_{label}_PlusU", cubeCenter, cubeHalfExtent, normal, u, v, half + t * 0.5f, 0f, t, half * 2f + t * 2f);
            BuildFaceWall(parent, $"Wall_{label}_MinusU", cubeCenter, cubeHalfExtent, normal, u, v, -(half + t * 0.5f), 0f, t, half * 2f + t * 2f);

            GameObject blockerMinus = BuildDoorWall(parent, label, "MinusV", cubeCenter, cubeHalfExtent, normal, u, v, -1f, minusVMode);
            GameObject blockerPlus = BuildDoorWall(parent, label, "PlusV", cubeCenter, cubeHalfExtent, normal, u, v, 1f, plusVMode);

            return (blockerMinus, blockerPlus);
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
        // 방별 콘텐츠 (원본 [[MultiRoomFloor1Builder]] 방A~D를 면 기준으로 포팅)
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

        /// <summary>방D — 평×2+2(2웨이브, 클라이맥스) + 벽화 2개. 게이트를 반환 — 다음 층 전환에 구독해서 씁니다.</summary>
        public static RoomClearGate PopulateRoomD(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            CubeSurfaceWalker target, GameObject enemyPrefab, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomD", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPrefab, new[] { 2, 2 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildMural(parent, "Mural_RoomD_Climax_0", cubeCenter, cubeHalfExtent, normal, u, v, 0f, 12f, new Color(0.9f, 0.2f, 0.2f));
            BuildMural(parent, "Mural_RoomD_Climax_1", cubeCenter, cubeHalfExtent, normal, u, v, 0f, -12f, new Color(0.9f, 0.2f, 0.2f));

            // stairs는 안 씀 — [[CubeDungeonProgressionManager]]가 반환된 게이트의 OnCleared를
            // 구독해서 "다음 층 재생성"을 직접 트리거함(2026-09-15).
            return BuildRoomGate(parent, "RoomClearGate_D", cubeCenter, cubeHalfExtent, normal, null);
        }

        private static void ApplyHpMultiplier(EncounterSpawner spawner, float hpMultiplier)
        {
            if (spawner == null || Mathf.Approximately(hpMultiplier, 1f)) return;
            foreach (var wave in spawner.waves)
                wave.hpMultiplier *= hpMultiplier;
        }

        /// <summary>
        /// 방A~D 전체를 한 번에 짓습니다. `root`는 호출하는 쪽이 만들어서 넘겨야 합니다(이전
        /// 층 잔재 삭제 여부는 호출자 책임 — 에디터는 DestroyImmediate, 런타임은 Destroy를
        /// 써야 해서 여기선 관여하지 않습니다). `hpMultiplier`는 층이 올라갈수록 적을 강하게
        /// 만들 때 씀(1층=1.0 기본).
        /// </summary>
        public static RoomClearGate BuildFullFloor(Transform root, Transform cubePlanet, float cubeHalfExtent,
            CubeSurfaceWalker player, GameObject enemyPrefab, float hpMultiplier)
        {
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var doorsA = BuildRoom(root, "A", cubeCenter, cubeHalfExtent, Vector3.forward, WallMode.Closed, WallMode.GatedExit);
            PopulateRoomA(root, cubePlanet, cubeHalfExtent, Vector3.forward, doorsA.plus, player, enemyPrefab, hpMultiplier);

            var doorsB = BuildRoom(root, "B", cubeCenter, cubeHalfExtent, Vector3.up, WallMode.GatedExit, WallMode.Open);
            PopulateRoomB(root, cubePlanet, cubeHalfExtent, Vector3.up, doorsB.minus, player, enemyPrefab, hpMultiplier);

            var doorsC = BuildRoom(root, "C", cubeCenter, cubeHalfExtent, Vector3.back, WallMode.GatedExit, WallMode.Open);
            PopulateRoomC(root, cubePlanet, cubeHalfExtent, Vector3.back, doorsC.minus, player, enemyPrefab, hpMultiplier);

            BuildRoom(root, "D", cubeCenter, cubeHalfExtent, Vector3.down, WallMode.Open, WallMode.Closed);
            return PopulateRoomD(root, cubePlanet, cubeHalfExtent, Vector3.down, player, enemyPrefab, hpMultiplier);
        }
    }
}
