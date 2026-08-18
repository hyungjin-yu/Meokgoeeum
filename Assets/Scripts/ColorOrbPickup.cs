using UnityEngine;

/// <summary>
/// ColorOrbPickup (색 구슬 — 월드 오브젝트)
/// 먹괴음 처치 시 드랍되어 놓이고, 플레이어가 닿으면 자동 획득됩니다.
/// [[15 튜토리얼 설계]] 4단계: "드랍된 구슬 위로 이동 시 자동 획득 (F키 불필요)".
///
/// 풀링 대상이라 스스로 Destroy하지 않고 ColorOrbPool로 반납합니다.
/// </summary>
public class ColorOrbPickup : MonoBehaviour
{
    public OrbColor Color { get; private set; }
    private ColorOrbPool pool;
    private Renderer cachedRenderer;

    // 23 UI 디자인 기획서의 색 구슬 팔레트 HEX를 그대로 사용
    private static readonly Color[] DisplayColors =
    {
        new Color(0.898f, 0.224f, 0.208f), // 빨강 #E53935
        new Color(0.118f, 0.533f, 0.898f), // 파랑 #1E88E5
        new Color(0.992f, 0.847f, 0.208f), // 노랑 #FDD835
        new Color(0.263f, 0.627f, 0.278f), // 초록 #43A047
        new Color(0.557f, 0.141f, 0.667f), // 보라 #8E24AA
        new Color(0.129f, 0.129f, 0.129f), // 검정 #212121
    };

    /// <summary>
    /// 풀에서 꺼내질 때마다 호출됩니다. 색을 다시 칠하고 소유 풀을 기억해둡니다.
    /// </summary>
    public void Init(OrbColor color, ColorOrbPool ownerPool)
    {
        Color = color;
        pool = ownerPool;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<Renderer>();

        // 풀링되는 오브젝트라 material 인스턴스가 재사용 개체당 1번만 생기고,
        // 이후로는 색만 덮어씁니다 (풀 크기만큼만 인스턴스가 생기므로 배칭 부담이 적음).
        if (cachedRenderer != null)
            cachedRenderer.material.color = DisplayColors[(int)color];
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        ColorSystemManager.Instance?.AddOrb(Color);
        pool.Release(this);
    }
}
