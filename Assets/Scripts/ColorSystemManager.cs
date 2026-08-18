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
/// 색 스킬로 구슬을 "소모"하는 기능은 아직 없습니다 (스킬 시스템 자체가 없어서) —
/// 나중에 스킬을 만들 때 이 매니저에 소모 API를 추가하면 됩니다.
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
