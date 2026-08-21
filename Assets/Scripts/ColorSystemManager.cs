using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// ColorSystemManager (색 시스템 관리자)
/// [[10 씬 구조 & 기술 스펙]]에 명시된 이름을 그대로 사용한 싱글톤입니다.
///
/// [[04 색 구슬 시스템]] "두 개의 다른 숫자"를 그대로 구현합니다:
/// - 보유 구슬(Held): 전투 자원, 합산 상한 5개. 상한 초과 시 가장 오래 들고 있던
///   색부터 자동으로 빠집니다(FIFO) — [[02 플레이어 시스템]] "5슬롯 vs 6색" 절 참고.
/// - 누적 색 기록(Lifetime): 절대 감소하지 않음. 보스 등장 조건/색 도감용.
///
/// 색 스킬로 구슬을 "소모"하는 기능은 [[ColorSkillController]]가 `TryConsumeOrb()`로 씁니다.
/// </summary>
public class ColorSystemManager : MonoBehaviour
{
    public static ColorSystemManager Instance { get; private set; }

    [Tooltip("보유 구슬 합산 상한입니다. (04 색 구슬 시스템)")]
    public int maxHeldOrbs = 5;

    private readonly int[] heldOrbs = new int[6];
    private readonly int[] lifetimeCollected = new int[6];
    private readonly Queue<OrbColor> acquisitionOrder = new Queue<OrbColor>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[ColorSystemManager] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
        }
    }

    public int GetHeld(OrbColor color) => heldOrbs[(int)color];
    public int GetLifetimeCollected(OrbColor color) => lifetimeCollected[(int)color];

    public int TotalHeld()
    {
        int sum = 0;
        foreach (var count in heldOrbs) sum += count;
        return sum;
    }

    /// <summary>
    /// 구슬 1개를 획득 처리합니다. (드랍된 구슬을 주웠을 때 호출)
    /// </summary>
    public void AddOrb(OrbColor color)
    {
        lifetimeCollected[(int)color]++; // 누적은 상한 없이 무조건 증가

        if (TotalHeld() >= maxHeldOrbs && acquisitionOrder.Count > 0)
        {
            OrbColor oldest = acquisitionOrder.Dequeue();
            heldOrbs[(int)oldest]--;
        }

        heldOrbs[(int)color]++;
        acquisitionOrder.Enqueue(color);

        Debug.Log($"색 구슬 획득: {color} | 보유 현황: {DescribeHeld()} | {color} 누적 획득: {lifetimeCollected[(int)color]}회");
    }

    /// <summary>
    /// 색 스킬 사용 시 구슬 1개를 소모합니다. 보유량이 없으면 아무 일도 안 하고 false를 반환합니다.
    /// (호출하는 쪽인 [[ColorSkillController]]가 이 반환값으로 "구슬 부족이라 스킬 실패" 처리를 합니다)
    /// </summary>
    public bool TryConsumeOrb(OrbColor color)
    {
        if (heldOrbs[(int)color] <= 0) return false;

        heldOrbs[(int)color]--;
        RemoveOneFromAcquisitionOrder(color);

        Debug.Log($"색 구슬 소모: {color} | 보유 현황: {DescribeHeld()}");
        return true;
    }

    /// <summary>
    /// acquisitionOrder 큐에서 해당 색 1개를 제거합니다(가장 오래된 것부터).
    /// Queue&lt;T&gt;는 임의 위치 삭제를 지원하지 않아서 전부 꺼냈다가 다시 쌓는 방식으로 처리합니다 —
    /// 보유 구슬이 최대 5개뿐이라(maxHeldOrbs) 비용은 신경 쓸 수준이 아닙니다.
    /// 이걸 안 하면 heldOrbs와 acquisitionOrder.Count가 어긋나서, 나중에 AddOrb()의 FIFO 교체가
    /// 이미 스킬로 소모된 "유령 항목"을 또 지우려다 음수가 나는 버그로 이어집니다.
    /// </summary>
    private void RemoveOneFromAcquisitionOrder(OrbColor color)
    {
        int count = acquisitionOrder.Count;
        bool removed = false;

        for (int i = 0; i < count; i++)
        {
            OrbColor item = acquisitionOrder.Dequeue();
            if (!removed && item == color)
            {
                removed = true; // 이번 한 번만 건너뛰고, 나머지는 그대로 다시 큐에 넣음
                continue;
            }
            acquisitionOrder.Enqueue(item);
        }
    }

    /// <summary>
    /// [[SaveManager]]가 로드 시 호출합니다. 보유/누적 배열을 통째로 덮어씁니다.
    /// acquisitionOrder(FIFO 소모 순서)는 세이브 데이터에 없어서, 색 인덱스 순서대로
    /// 다시 채워서 근사합니다 — 로드 직후 딱 한 번 스킬을 썼을 때 "어느 색이 먼저 빠지는지"가
    /// 저장 시점과 정확히 같지 않을 수 있지만, 보유 구슬이 최대 5개뿐이라 체감상 무시할 수준입니다.
    /// </summary>
    public void RestoreState(int[] heldCounts, int[] lifetimeCounts)
    {
        acquisitionOrder.Clear();

        for (int i = 0; i < heldOrbs.Length; i++)
        {
            heldOrbs[i] = i < heldCounts.Length ? heldCounts[i] : 0;
            lifetimeCollected[i] = i < lifetimeCounts.Length ? lifetimeCounts[i] : 0;

            for (int n = 0; n < heldOrbs[i]; n++)
                acquisitionOrder.Enqueue((OrbColor)i);
        }

        Debug.Log($"[ColorSystemManager] 세이브에서 복원됨: {DescribeHeld()}");
    }

    private string DescribeHeld()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < heldOrbs.Length; i++)
        {
            if (heldOrbs[i] > 0)
                sb.Append($"{(OrbColor)i}x{heldOrbs[i]} ");
        }
        return sb.Length > 0 ? sb.ToString().TrimEnd() : "(없음)";
    }
}
