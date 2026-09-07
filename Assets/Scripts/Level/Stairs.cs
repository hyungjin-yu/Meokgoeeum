using UnityEngine;

/// <summary>
/// Stairs (계단)
/// [[05 맵 시스템 - 큐브 구조]] "계단 & 층 전환" 구현. 방의 클리어 조건이 채워지면
/// [[RoomClearGate]] 등이 `Activate()`를 호출해서 계단을 켜고, 플레이어가 밟으면
/// [[CubeMapManager]]의 면 회전을 발동시켜 "다음 층"으로 넘어갑니다.
///
/// 어제(2026-08-19) 보류해뒀던 "색 구슬 획득 시 자동 회전" 질문의 답이 이겁니다 —
/// 기획서를 다시 보니 회전의 진짜 트리거는 구슬 획득이 아니라 **계단**이고, 구슬 획득은
/// (대부분의 층에서) 계단이 열리기 위한 선행 조건 중 하나일 뿐입니다. 그래서 [[ColorWaveEffect]]에
/// 자동 연결하는 대신, 이 계단을 실제 게임플레이 트리거로 씁니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Stairs : MonoBehaviour
{
    [Tooltip("시작할 때부터 활성화되어 있는지 여부입니다. 보통은 꺼놓고 RoomClearGate 등이 켭니다.")]
    public bool startActive = false;

    private bool isActive;
    private bool used;

    private Collider col;
    private Renderer[] renderers;

    private void Awake()
    {
        col = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
        SetActive(startActive);
    }

    /// <summary>
    /// 계단을 켭니다. "이 방을 클리어했다"고 판단하는 쪽([[RoomClearGate]] 등)에서 호출합니다.
    /// </summary>
    public void Activate()
    {
        if (isActive) return;
        Debug.Log($"[Stairs] {name} 활성화!");
        SetActive(true);
    }

    private void SetActive(bool value)
    {
        isActive = value;
        if (col != null) col.enabled = value;
        foreach (var r in renderers) r.enabled = value; // 꺼져있다가 등장하는 연출
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || used) return;
        if (!other.CompareTag("Player")) return;

        used = true;
        Debug.Log("[Stairs] 계단 사용! 다음 층으로 이동합니다.");
        CubeMapManager.Instance?.RotateRandomAxis();
    }
}
