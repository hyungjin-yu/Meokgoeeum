using System.Collections.Generic;
using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeDungeonRoomKit (큐브 면 던전 방 빌드 — 런타임/에디터 공유 순수 로직)
    /// [[CubeFaceRoomBuilder]](에디터, 최초 씬 세팅)와 [[CubeDungeonProgressionManager]](런타임,
    /// 6개 방 전부 클리어 시 다음 층 재생성)가 똑같은 코드를 쓰도록 뽑아낸 것입니다. `UnityEditor`
    /// API를 전혀 안 써서 실제 빌드에도 그대로 포함되고 런타임에서 안전하게 호출할 수 있습니다.
    ///
    /// [[changelog/2026-09-15_실제던전-큐브면포팅-1차]]/[[changelog/2026-09-15_큐브던전-웨이브스포너와장식]]에서
    /// `CubeFaceRoomBuilder` 안에 직접 있던 벽/스폰/장식 빌더들을 그대로 옮긴 것 — 로직 자체는
    /// 안 바뀌었고, `cubeCenter`/`cubeHalfExtent`/`enemyPrefab`을 (전엔 `GameObject.Find`나
    /// `AssetDatabase`로 안에서 직접 구했던 것을) 전부 매개변수로 받도록만 바꿨습니다.
    ///
    /// ⚠️ 2026-09-16 (1차) — 방 6개(A~F)로 확장, 큐브 6면 전부 사용
    /// 기존엔 방 4개(A~D)만 큐브 4개 면(+Z/+Y/-Z/-Y)에 돌아가며 배치되고 ±X 2개 면은 AI
    /// 테스트용으로 안 쓰였는데, 그 두 면도 실제 던전 방(E=+X, F=-X)으로 편입시켜 **한 층이
    /// 큐브 6면 전부를 쓰는** 구조로 확장했습니다 — "이 세계는 큐브 1개" 기획 의도
    /// ([[05 맵 시스템 - 큐브 구조]])에 더 가까움. 이동/면 전환 수학([[CubeSurfaceWalker]],
    /// [[CubeEnemyBase]], `GetFaceBasis`/`BuildFaceWall`)은 전혀 안 바뀌었습니다 — 축이 정렬된
    /// 정육면체 면은 원래도 6개 다 똑같은 방식으로 다뤄지므로, 방 개수/문 배선만 확장하면 됩니다.
    ///
    /// ⚠️ 2026-09-16 (2차) — 순서대로 잠기는 선형 경로(A→B→E→C→D→F) 폐기, 6면 전부 상시 개방
    /// 사용자 요청: "꼭 A~F가 아니라 각 면마다 문을 추가해서 층 클리어 조건을 모든 면의 몹
    /// 처치로 해." 1차 확장에서는 방마다 딱 2개 면(진입 1 + 진출 1 GatedExit)만 열고 나머지
    /// 2면은 막아서 정해진 순서로만 진행하게 했는데, 이제 **모든 방의 4면 전부를 상시 개방
    /// (`WallMode.Open`, 잠금 없음)**해서 인접한 어느 면으로든 자유롭게 오갈 수 있게 바꿨습니다.
    /// 정육면체 각 면은 정확히 4개의 이웃(반대편 면 1개만 빼고)을 가지므로, 방의 4면(±U/±V)이
    /// 그 4개 이웃과 정확히 1:1 대응 — 남는 면도 부족한 면도 없습니다. 층 클리어 조건도
    /// "정해진 마지막 방(F) 클리어"에서 **"6개 방 전부 클리어"**로 바뀌었습니다 —
    /// [[CubeDungeonProgressionManager]]가 6개 [[RoomClearGate]]를 전부 구독해서 전원 클리어된
    /// 순간에만 다음 층으로 넘어갑니다. `doorBlocker`/`GatedExit` 개념 자체가 이제 안 쓰이지만,
    /// 나중에 "특정 방을 잠그고 싶다" 같은 요구가 생기면 재사용할 수 있게 `WallMode`/`BuildRoom`의
    /// 4방향 인터페이스는 그대로 남겨뒀습니다.
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
        // 종 다양화 (2026-09-16 추가)
        // ------------------------------------------------------------------

        /// <summary>5종 프리팹 참조를 한 데 묶은 것. `CubeDungeonProgressionManager`가 들고 있다가
        /// `BuildFullFloor()`에 그대로 넘깁니다 — 아직 이식 안 된 종이 있으면 null로 비워두면 됩니다
        /// (해당 종은 그냥 풀에서 빠짐).</summary>
        [System.Serializable]
        public class EnemySpeciesPrefabs
        {
            public GameObject pyeong;
            public GameObject won;
            public GameObject heup;
            public GameObject bun;
            public GameObject gwang;
        }

        /// <summary>
        /// 층 번호에 따라 그 시점까지 풀린 종 목록(누적)을 돌려줍니다. [[19 층별 상세 설계]]의
        /// 원래 순차 등장 의도(1~2층 평만 → 3층 원 → 4층 흡 → 6층 분 → 7층 광)를 그대로 가져오되,
        /// 그 문서는 "층 하나 = 방 하나" 기준이고 지금은 "층 하나 = 방 6개, 방마다 무작위 조합"
        /// 구조라 정확한 마릿수까지는 맞추지 않고 "이 시점부터 이 종이 나올 수 있다"는 순서만
        /// 유지합니다. 실제 방별 조합은 <see cref="BuildWaveSpawner"/>가 이 풀에서 무작위로 뽑습니다.
        /// </summary>
        public static GameObject[] GetUnlockedPool(int floorNumber, EnemySpeciesPrefabs species)
        {
            var pool = new List<GameObject>();
            if (species == null) return pool.ToArray();

            if (species.pyeong != null) pool.Add(species.pyeong); // 1층부터
            if (floorNumber >= 3 && species.won != null) pool.Add(species.won);
            if (floorNumber >= 4 && species.heup != null) pool.Add(species.heup);
            if (floorNumber >= 6 && species.bun != null) pool.Add(species.bun);
            if (floorNumber >= 7 && species.gwang != null) pool.Add(species.gwang);
            return pool.ToArray();
        }

        /// <summary>
        /// 방 하나(웨이브 전체 합산 마릿수)에 뽑을 종을 무작위로 정합니다. [[RandomEncounterSpawner]]와
        /// 같은 "종류당 최대 N마리" 방식이되, 풀이 작아서 그대로 적용하면 마릿수를 못 채우는 경우
        /// (예: 1층은 풀이 평 하나뿐인데 방F는 4마리 필요)까지 자동으로 감안 — 필요 마릿수 대비 풀이
        /// 작으면 한도를 그만큼 늘려서 항상 요청한 마릿수를 채웁니다.
        /// </summary>
        private static GameObject[] PickSpeciesForRoom(GameObject[] pool, int totalCount)
        {
            var result = new GameObject[totalCount];
            if (pool == null || pool.Length == 0) return result;

            int maxDuplicates = Mathf.Max(2, Mathf.CeilToInt((float)totalCount / pool.Length));
            var pickable = new List<GameObject>(pool);
            var pickedCount = new Dictionary<GameObject, int>();

            for (int i = 0; i < totalCount; i++)
            {
                if (pickable.Count == 0) pickable.AddRange(pool); // 이론상 위 한도 계산 덕에 안 일어나야 하는 방어 코드

                GameObject prefab = pickable[Random.Range(0, pickable.Count)];
                result[i] = prefab;

                pickedCount.TryGetValue(prefab, out int count);
                count++;
                pickedCount[prefab] = count;
                if (count >= maxDuplicates) pickable.Remove(prefab);
            }
            return result;
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
        /// 비활성화 — 2026-09-16 2차 개편으로 지금은 안 쓰지만 인터페이스는 유지). U변이든 V변이든
        /// "고정 축"과 "따라가는 축"만 바뀔 뿐 로직은 동일해서 하나로 통합했습니다.
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

        /// <summary>면 하나에 방 하나(사방 벽)를 짓습니다. 4방향(±U/±V) 전부 각각 `WallMode`를 받습니다.</summary>
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

        /// <summary>모든 방에 공통으로 쓰는 "4면 전부 상시 개방" — 2026-09-16 2차 개편 이후의 기본 형태.</summary>
        public static RoomDoors BuildOpenRoom(Transform parent, string label, Vector3 cubeCenter, float cubeHalfExtent, Vector3 normal)
            => BuildRoom(parent, label, cubeCenter, cubeHalfExtent, normal, WallMode.Open, WallMode.Open, WallMode.Open, WallMode.Open);

        // ------------------------------------------------------------------
        // 콘텐츠 (스폰/장식)
        // ------------------------------------------------------------------

        /// <summary>
        /// 방 중앙에 [[EncounterSpawner]]를 큐브 면 모드로 세웁니다. `enemyPool`에서 방 전체
        /// (웨이브 합산) 마릿수만큼 무작위로 종을 뽑아 배치합니다(<see cref="PickSpeciesForRoom"/>) —
        /// 풀에 하나만 있으면(예: 1층) 자동으로 그 하나로만 채워져서 기존 "평만 등장" 동작과
        /// 동일하게 동작합니다. `EncounterSpawner`가 `cubeCenter`가 설정돼 있으면 스폰 시점에
        /// 자동으로 프리팹에 맞는 CubeEnemy* 종으로 교체합니다.
        /// </summary>
        public static EncounterSpawner BuildWaveSpawner(Transform parent, string name, Transform cubePlanet, float cubeHalfExtent,
            Vector3 normal, Vector3 u, Vector3 v, CubeSurfaceWalker target, GameObject[] enemyPool, int[] enemiesPerWave)
        {
            if (enemyPool == null || enemyPool.Length == 0) { Debug.LogError("[CubeDungeonRoomKit] enemyPool이 비어있습니다."); return null; }

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

            int totalCount = 0;
            foreach (var c in enemiesPerWave) totalCount += c;
            GameObject[] chosenSpecies = PickSpeciesForRoom(enemyPool, totalCount);
            int chosenIndex = 0;

            var waves = new EncounterSpawner.Wave[enemiesPerWave.Length];
            for (int w = 0; w < enemiesPerWave.Length; w++)
            {
                int count = enemiesPerWave[w];
                var prefabs = new GameObject[count];
                var points = new Transform[count];
                for (int i = 0; i < count; i++)
                {
                    prefabs[i] = chosenSpecies[chosenIndex++];
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

        /// <summary>
        /// 2026-09-16 2차 개편 — `doorBlocker` 매개변수 제거(모든 문이 상시 개방이라 게이트가 더는
        /// 문을 잠그지 않음). 게이트는 이제 순수하게 "이 방의 적을 다 잡았는가"만 보고받고
        /// [[CubeDungeonProgressionManager]]가 6개 전부의 `OnCleared`를 취합해서 층 전환을 판단합니다.
        /// </summary>
        public static RoomClearGate BuildRoomGate(Transform parent, string name, Vector3 cubeCenter, float cubeHalfExtent, Vector3 normal)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            GameObject gateGo = new GameObject(name);
            gateGo.transform.SetParent(parent, false);
            gateGo.transform.position = cubeCenter + normal * (cubeHalfExtent + 5f);
            gateGo.transform.rotation = Quaternion.LookRotation(v, normal);

            var gate = gateGo.AddComponent<RoomClearGate>();
            gate.boundsSize = new Vector3(RoomHalf * 2f + 4f, 10f, RoomHalf * 2f + 4f);
            gate.doorsToOpen = new GameObject[0];
            return gate;
        }

        // ------------------------------------------------------------------
        // 방별 콘텐츠 (원본 [[MultiRoomFloor1Builder]] 방A~D + 신규 E/F를 면 기준으로 포팅)
        // 2026-09-16 2차 개편 — 전부 `doorBlocker` 매개변수 제거, 전부 RoomClearGate 반환.
        // ------------------------------------------------------------------

        /// <summary>방A — 튜토리얼 전투(평×1+1, 2웨이브) + 벽화/숨겨진 구슬 곁가지.</summary>
        public static RoomClearGate PopulateRoomA(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            CubeSurfaceWalker target, GameObject[] enemyPool, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomA", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPool, new[] { 1, 1 });
            spawner.showFirstAttackHint = true;
            ApplyHpMultiplier(spawner, hpMultiplier);

            var mural = BuildMural(parent, "Mural_RoomA_Side", cubeCenter, cubeHalfExtent, normal, u, v, 10f, 8f, new Color(0.9f, 0.2f, 0.2f));
            GameObject orbGo = new GameObject("HiddenOrbSpawner_RoomA_Side");
            orbGo.transform.SetParent(parent, false);
            orbGo.transform.position = cubeCenter + normal * (cubeHalfExtent + 0.5f) + u * 10f + v * 7f;
            var orbSpawner = orbGo.AddComponent<HiddenOrbSpawner>();
            orbSpawner.trigger = mural;
            orbSpawner.orbColor = OrbColor.Red;

            return BuildRoomGate(parent, "RoomClearGate_A", cubeCenter, cubeHalfExtent, normal);
        }

        /// <summary>방B — 평×1+1(2웨이브) + 장식 벽화 3개.</summary>
        public static RoomClearGate PopulateRoomB(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            CubeSurfaceWalker target, GameObject[] enemyPool, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomB", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPool, new[] { 1, 1 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildMural(parent, "Mural_RoomB_0", cubeCenter, cubeHalfExtent, normal, u, v, 8f, 8f, new Color(0.6f, 0.2f, 0.8f));
            BuildMural(parent, "Mural_RoomB_1", cubeCenter, cubeHalfExtent, normal, u, v, 0f, -10f, new Color(0.9f, 0.2f, 0.2f));
            BuildMural(parent, "Mural_RoomB_2", cubeCenter, cubeHalfExtent, normal, u, v, -8f, 8f, new Color(0.9f, 0.7f, 0.2f));

            return BuildRoomGate(parent, "RoomClearGate_B", cubeCenter, cubeHalfExtent, normal);
        }

        /// <summary>방C — 평×2+1(2웨이브) + 왕복 장애물 2개.</summary>
        public static RoomClearGate PopulateRoomC(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            CubeSurfaceWalker target, GameObject[] enemyPool, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomC", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPool, new[] { 2, 1 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildRoomObstacle(parent, "MovingObstacle_RoomC_0", cubeCenter, cubeHalfExtent, normal, u, v, 4f, -2f);
            BuildRoomObstacle(parent, "MovingObstacle_RoomC_1", cubeCenter, cubeHalfExtent, normal, u, v, -4f, 2f);

            return BuildRoomGate(parent, "RoomClearGate_C", cubeCenter, cubeHalfExtent, normal);
        }

        /// <summary>방D — 평×2+1(2웨이브) + 왕복 장애물 1개.</summary>
        public static RoomClearGate PopulateRoomD(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            CubeSurfaceWalker target, GameObject[] enemyPool, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomD", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPool, new[] { 2, 1 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildRoomObstacle(parent, "MovingObstacle_RoomD_0", cubeCenter, cubeHalfExtent, normal, u, v, 0f, 3f);

            return BuildRoomGate(parent, "RoomClearGate_D", cubeCenter, cubeHalfExtent, normal);
        }

        /// <summary>방E — 평×1+1(2웨이브) + 장식 벽화 2개.</summary>
        public static RoomClearGate PopulateRoomE(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            CubeSurfaceWalker target, GameObject[] enemyPool, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomE", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPool, new[] { 1, 1 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildMural(parent, "Mural_RoomE_0", cubeCenter, cubeHalfExtent, normal, u, v, 6f, -6f, new Color(0.9f, 0.7f, 0.2f));
            BuildMural(parent, "Mural_RoomE_1", cubeCenter, cubeHalfExtent, normal, u, v, -6f, 6f, new Color(0.6f, 0.2f, 0.8f));

            return BuildRoomGate(parent, "RoomClearGate_E", cubeCenter, cubeHalfExtent, normal);
        }

        /// <summary>방F — 평×2+2(2웨이브, 가장 큰 규모) + 벽화 2개.</summary>
        public static RoomClearGate PopulateRoomF(Transform parent, Transform cubePlanet, float cubeHalfExtent, Vector3 normal,
            CubeSurfaceWalker target, GameObject[] enemyPool, float hpMultiplier)
        {
            GetFaceBasis(normal, out Vector3 u, out Vector3 v);
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;

            var spawner = BuildWaveSpawner(parent, "EncounterSpawner_RoomF", cubePlanet, cubeHalfExtent, normal, u, v, target, enemyPool, new[] { 2, 2 });
            ApplyHpMultiplier(spawner, hpMultiplier);

            BuildMural(parent, "Mural_RoomF_Climax_0", cubeCenter, cubeHalfExtent, normal, u, v, 0f, 12f, new Color(0.9f, 0.2f, 0.2f));
            BuildMural(parent, "Mural_RoomF_Climax_1", cubeCenter, cubeHalfExtent, normal, u, v, 0f, -12f, new Color(0.9f, 0.2f, 0.2f));

            return BuildRoomGate(parent, "RoomClearGate_F", cubeCenter, cubeHalfExtent, normal);
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
        /// 2026-09-16 2차 개편 — 6개 방 전부 4면 상시 개방(어느 순서로든 자유롭게 오갈 수 있음),
        /// 6개의 [[RoomClearGate]]를 배열로 반환합니다 — [[CubeDungeonProgressionManager]]가
        /// 전부 구독해서 "6개 방 전부 클리어"를 층 전환 조건으로 판단합니다.
        ///
        /// 2026-09-16 3차 개편(층별 콘텐츠 다양화) — 프리팹 하나 대신 <see cref="EnemySpeciesPrefabs"/>와
        /// `floorNumber`를 받아서, <see cref="GetUnlockedPool"/>로 이 층까지 풀린 종 풀을 한 번만
        /// 계산해 6개 방 전부에 같은 풀을 넘깁니다 — 방마다 그 풀 안에서 무작위로 조합이 갈립니다
        /// (<see cref="PickSpeciesForRoom"/>).
        /// </summary>
        public static RoomClearGate[] BuildFullFloor(Transform root, Transform cubePlanet, float cubeHalfExtent,
            CubeSurfaceWalker player, EnemySpeciesPrefabs speciesPrefabs, int floorNumber, float hpMultiplier)
        {
            Vector3 cubeCenter = cubePlanet != null ? cubePlanet.position : Vector3.zero;
            GameObject[] pool = GetUnlockedPool(floorNumber, speciesPrefabs);

            BuildOpenRoom(root, "A", cubeCenter, cubeHalfExtent, Vector3.forward);
            var gateA = PopulateRoomA(root, cubePlanet, cubeHalfExtent, Vector3.forward, player, pool, hpMultiplier);

            BuildOpenRoom(root, "B", cubeCenter, cubeHalfExtent, Vector3.up);
            var gateB = PopulateRoomB(root, cubePlanet, cubeHalfExtent, Vector3.up, player, pool, hpMultiplier);

            BuildOpenRoom(root, "E", cubeCenter, cubeHalfExtent, Vector3.right);
            var gateE = PopulateRoomE(root, cubePlanet, cubeHalfExtent, Vector3.right, player, pool, hpMultiplier);

            BuildOpenRoom(root, "C", cubeCenter, cubeHalfExtent, Vector3.back);
            var gateC = PopulateRoomC(root, cubePlanet, cubeHalfExtent, Vector3.back, player, pool, hpMultiplier);

            BuildOpenRoom(root, "D", cubeCenter, cubeHalfExtent, Vector3.down);
            var gateD = PopulateRoomD(root, cubePlanet, cubeHalfExtent, Vector3.down, player, pool, hpMultiplier);

            BuildOpenRoom(root, "F", cubeCenter, cubeHalfExtent, Vector3.left);
            var gateF = PopulateRoomF(root, cubePlanet, cubeHalfExtent, Vector3.left, player, pool, hpMultiplier);

            return new[] { gateA, gateB, gateC, gateD, gateE, gateF };
        }
    }
}
