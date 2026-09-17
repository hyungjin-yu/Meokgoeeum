using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyConverter (평지 몹 프리팹 → 큐브 면 버전 즉석 변환 — 공용 유틸리티)
    /// 원래 [[EncounterSpawner]].SpawnWave()에만 있던 변환 로직을 [[DebugSpawnPanel]]도 그대로
    /// 재사용할 수 있도록 뽑아냈습니다 — 동작은 100% 동일(어떤 flat 종 스크립트가 붙어있는지 보고
    /// 대응하는 CubeEnemy*를 자동으로 골라 붙임, SpeciesMap 참고, 5종 전부 지원).
    /// </summary>
    public static class CubeEnemyConverter
    {
        // 2026-09-16 — 큐브 모드 변환용 flat 종 스크립트 → CubeEnemy* 매핑. 새 종이 추가되면
        // 여기에 한 줄만 추가하면 됨(다른 데는 안 건드림).
        private static readonly Dictionary<Type, Type> SpeciesMap = new Dictionary<Type, Type>
        {
            { typeof(EnemyPyeong), typeof(CubeEnemyPyeong) },
            { typeof(EnemyWon), typeof(CubeEnemyWon) },
            { typeof(EnemyHeup), typeof(CubeEnemyHeup) },
            { typeof(EnemyBun), typeof(CubeEnemyBun) },
            { typeof(EnemyGwang), typeof(CubeEnemyGwang) },
        };

        private static Type GetFlatSpeciesType(GameObject instance)
        {
            foreach (var flatType in SpeciesMap.Keys)
                if (instance.GetComponent(flatType) != null)
                    return flatType;
            return null;
        }

        /// <summary>
        /// instance(평지용 프리팹 인스턴스, 아직 Start()가 한 번도 안 돈 상태여야 함)를 큐브 면
        /// 버전으로 즉석 변환합니다. 알려진 종 스크립트를 못 찾으면 instance를 파괴하고 null을
        /// 반환합니다. 변환 후 위치를 faceNormal 기준 수학적 표면(+surfaceOffset)에 정확히
        /// 재보정합니다 — 호출하는 쪽이 스폰 좌표를 완벽히 안 맞춰도 안전하게 면 위에 붙습니다.
        /// </summary>
        public static CubeEnemyBase ConvertToCubeMode(GameObject instance, Transform cubeCenter, Vector3 faceNormal, float cubeHalfExtent, CubeSurfaceWalker target, float hpMultiplier = 1f)
        {
            var flatSpeciesType = GetFlatSpeciesType(instance);
            if (flatSpeciesType == null || !SpeciesMap.TryGetValue(flatSpeciesType, out var cubeType))
            {
                Debug.LogWarning($"[CubeEnemyConverter] {instance.name}에서 알려진 먹괴음 종 스크립트를 못 찾아 큐브 모드로 변환하지 못했습니다 — 그대로 파괴합니다.");
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            // ⚠️ 순서 중요 — flat 종 스크립트(EnemyPyeong 등)가 EnemyHealth/NavMeshAgent를
            // [RequireComponent]로 요구하므로, 그 스크립트부터 먼저 지워야 나머지 둘을 지울 수
            // 있습니다. 반대로 하면 Unity가 "OO가 의존하니 못 지운다" 에러를 내고 조용히 지우기를
            // 실패시켜서, NavMeshAgent/EnemyHealth/원본 스크립트가 CubeEnemy*와 같이 남아있는
            // 상태가 됩니다(2026-09-16, 종 다양화 작업 중 실제로 재현된 버그).
            UnityEngine.Object.Destroy(instance.GetComponent(flatSpeciesType));
            var oldHealth = instance.GetComponent<EnemyHealth>();
            if (oldHealth != null) UnityEngine.Object.Destroy(oldHealth);
            var oldAgent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (oldAgent != null) UnityEngine.Object.Destroy(oldAgent);

            var cubeEnemy = (CubeEnemyBase)instance.AddComponent(cubeType);
            cubeEnemy.cubeCenter = cubeCenter;
            cubeEnemy.faceNormal = faceNormal;
            cubeEnemy.cubeHalfExtent = cubeHalfExtent;
            cubeEnemy.target = target;
            if (!Mathf.Approximately(hpMultiplier, 1f))
                cubeEnemy.maxHP *= hpMultiplier;

            // 2026-09-17 추가 — 호출하는 쪽(특히 디버그 스폰 도구처럼 정확한 면 위 좌표를 미리
            // 계산해두지 않은 경우)이 대충 준 위치를 수학적 표면에 정확히 재보정합니다.
            // [[CubeSurfaceWalker]].ClampToSurface()와 동일한 공식.
            int axis = Mathf.Abs(faceNormal.x) > 0.5f ? 0 : (Mathf.Abs(faceNormal.y) > 0.5f ? 1 : 2);
            Vector3 local = instance.transform.position - (cubeCenter != null ? cubeCenter.position : Vector3.zero);
            for (int a = 0; a < 3; a++)
            {
                if (a == axis) continue;
                local[a] = Mathf.Clamp(local[a], -cubeHalfExtent, cubeHalfExtent);
            }
            local[axis] = (cubeHalfExtent + cubeEnemy.surfaceOffset) * Mathf.Sign(faceNormal[axis]);
            instance.transform.position = (cubeCenter != null ? cubeCenter.position : Vector3.zero) + local;

            return cubeEnemy;
        }
    }
}
