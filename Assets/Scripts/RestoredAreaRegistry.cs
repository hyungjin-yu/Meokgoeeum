using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RestoredAreaRegistry (색 복원 구역 등록소)
/// [[PaintableObject]]가 칠해질 때마다 자기 위치를 등록해두면, [[EnemyHeup]] 같은 "색 복원
/// 구역을 찾아가는" AI가 가장 가까운 곳을 조회할 수 있습니다. [[13 먹괴음 AI 설계]]의
/// `ColorAreaQuery.FindNearestColorRestoredArea`(UE5 EQS 대체)를 정적 유틸로 구현했습니다.
///
/// static이라 씬을 새로 로드해도 안 비워지는데, Unity 에디터는 Play 모드를 시작할 때마다
/// 도메인 리로드로 static 필드를 초기화해주기 때문에(기본 설정 기준) 지금은 별도 초기화
/// 코드 없이도 안전합니다. 나중에 "Enter Play Mode Options"에서 도메인 리로드를 끄고 쓰게
/// 되면 이 가정이 깨지니 그때 다시 볼 것.
/// </summary>
public static class RestoredAreaRegistry
{
    private static readonly List<Vector3> restoredPositions = new List<Vector3>();

    public static void Register(Vector3 position) => restoredPositions.Add(position);

    /// <summary>
    /// 등록된 위치 중 from에서 가장 가까운 곳을 찾습니다. 하나도 없으면 false.
    /// </summary>
    public static bool TryFindNearest(Vector3 from, out Vector3 nearest)
    {
        nearest = default;
        if (restoredPositions.Count == 0) return false;

        float bestDistSqr = float.MaxValue;
        foreach (var pos in restoredPositions)
        {
            float distSqr = (pos - from).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = pos;
            }
        }
        return true;
    }
}
