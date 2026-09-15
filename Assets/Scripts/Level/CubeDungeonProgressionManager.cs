using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeDungeonProgressionManager (큐브 던전 — 방D 클리어 시 다음 층 재생성)
    /// 2026-09-15, 사용자 결정: "같은 4개 면을 새 콘텐츠로 덮어씌우기" — 기존 [[CubeMapManager]]는
    /// 층 하나당 씬 하나를 통째로 불러오는 방식이라, 물리적으로 하나뿐인 큐브를 쓰는 이
    /// 프로토타입과는 안 맞습니다. 대신 방D의 [[RoomClearGate]].OnCleared를 구독해서, 같은
    /// 4개 면(+Z/+Y/-Z/-Y)의 방 콘텐츠를 통째로 지우고 [[CubeDungeonRoomKit]]으로 다시
    /// 짓습니다 — "같은 큐브, 다시 돌면 같은 면이 다른 층처럼 보인다"는 원래 기획 의도
    /// ([[05 맵 시스템 - 큐브 구조]])와 가장 가까운 해석.
    ///
    /// `UnityEditor` API를 전혀 안 써서(벽/스폰 로직은 [[CubeDungeonRoomKit]]에 있음) 실제
    /// 빌드에도 포함되고 게임플레이 중 안전하게 동작합니다 — [[CubeFaceRoomBuilder]](에디터)는
    /// 최초 1층 세팅만 하고 이 스크립트를 씬에 심어둔 뒤 물러납니다.
    /// </summary>
    public class CubeDungeonProgressionManager : MonoBehaviour
    {
        [Header("참조 (CubeFaceRoomBuilder가 최초 세팅 시 자동으로 채움)")]
        public Transform cubePlanet;
        public CubeSurfaceWalker player;
        public GameObject enemyPyeongPrefab;
        public string dungeonRootName = "DungeonRooms_Generated";
        public float cubeHalfExtent = 17f;

        [Header("난이도")]
        [Tooltip("층마다 적 최대HP에 곱해지는 배율의 증가폭입니다. 2층=1+0.15, 3층=1+0.30 ... 식으로 누적됩니다.")]
        public float hpMultiplierPerFloor = 0.15f;

        public int currentFloor = 1;

        private RoomClearGate hookedGate;

        /// <summary>
        /// ⚠️ 2026-09-15 발견 — C# 이벤트(`+=` 구독)는 Unity가 씬 파일에 저장 안 함(직렬화
        /// 대상이 아님). 그래서 에디터 스크립트([[CubeFaceRoomBuilder]])가 씬 저장 "전"에
        /// `HookGate()`를 불러 구독을 걸어놔도, 실제로 Play를 누르는 순간 씬이 저장된 파일
        /// 기준으로 새로 구성되면서 그 구독은 통째로 사라짐 — 방A~D를 전부 클리어해도 아무
        /// 반응이 없던 진짜 원인이었음(사용자가 실제 플레이로 재현). 그래서 Start()에서
        /// 런타임에 직접 "RoomClearGate_D"를 찾아 다시 구독하도록 함 — 이러면 씬이 어떻게
        /// 저장/로드됐는지와 무관하게 Play를 누를 때마다 항상 스스로 연결됩니다.
        /// </summary>
        private void Start()
        {
            var gateGo = GameObject.Find("RoomClearGate_D");
            var gate = gateGo != null ? gateGo.GetComponent<RoomClearGate>() : null;
            if (gate == null)
            {
                Debug.LogWarning("[CubeDungeonProgressionManager] RoomClearGate_D를 못 찾아서 다음 층 전환을 못 겁니다.");
                return;
            }
            HookGate(gate);
        }

        /// <summary>방D 게이트가 클리어되면 다음 층 전환을 시작하도록 구독합니다. 재생성될 때마다 새로 불러야 합니다.</summary>
        public void HookGate(RoomClearGate gate)
        {
            if (hookedGate != null) hookedGate.OnCleared -= HandleRoomDCleared;
            hookedGate = gate;
            if (hookedGate != null) hookedGate.OnCleared += HandleRoomDCleared;
        }

        private void HandleRoomDCleared()
        {
            StartCoroutine(AdvanceToNextFloorRoutine());
        }

        private IEnumerator AdvanceToNextFloorRoutine()
        {
            Debug.Log($"[CubeDungeonProgressionManager] {currentFloor}층 클리어! 다음 층을 준비합니다...");

            if (FadeManager.Instance != null)
                yield return FadeManager.Instance.FadeOut();

            var oldRoot = GameObject.Find(dungeonRootName);
            if (oldRoot != null) Destroy(oldRoot);

            currentFloor++;
            float hpMultiplier = 1f + hpMultiplierPerFloor * (currentFloor - 1);

            var root = new GameObject(dungeonRootName);
            var newGate = CubeDungeonRoomKit.BuildFullFloor(root.transform, cubePlanet, cubeHalfExtent, player, enemyPyeongPrefab, hpMultiplier);
            HookGate(newGate);

            // 플레이어를 새로 지어진 방A(+Z)로 되돌립니다 — CubeMapManager.TeleportPlayerToSpawnPoint()와
            // 같은 이유로 Cinemachine에 "순간이동"임을 알려줘야 카메라가 안 멀어집니다.
            Vector3 oldPos = player.transform.position;
            Vector3 newPos = cubePlanet.position + Vector3.forward * (cubeHalfExtent + 1f);
            player.transform.SetPositionAndRotation(newPos, Quaternion.identity);
            player.SetCurrentFaceNormal(Vector3.forward);
            CinemachineCore.OnTargetObjectWarped(player.transform, newPos - oldPos);

            if (FadeManager.Instance != null)
                yield return FadeManager.Instance.FadeIn();

            Debug.Log($"[CubeDungeonProgressionManager] {currentFloor}층 시작! (적 HP 배율 x{hpMultiplier:0.00})");
        }
    }
}
