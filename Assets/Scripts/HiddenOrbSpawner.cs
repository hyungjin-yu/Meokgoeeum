using UnityEngine;

/// <summary>
/// HiddenOrbSpawner (숨겨진 색 구슬 스포너)
/// [[19 층별 상세 설계]] 2층 "안개 골목" 패턴 구현:
/// "바닥 타일에 패턴 복원 시, 숨겨진 구슬 1개 등장 → 그 구슬 획득 시 계단 자동 등장".
///
/// 적 처치 드랍 구슬과 달리, 특정 [[PaintableObject]]가 색 복원되는 "그 순간"에만
/// 정해진 위치에 구슬 하나가 나타나는 연출용 트리거입니다. 실제 구슬 오브젝트는
/// 기존 [[ColorOrbPool]]을 그대로 재사용해서 시각/획득 로직을 새로 만들지 않았습니다.
/// </summary>
public class HiddenOrbSpawner : MonoBehaviour
{
    [Tooltip("이 오브젝트가 색 복원되는 순간 구슬이 등장합니다. (예: 바닥 타일)")]
    public PaintableObject trigger;

    [Tooltip("구슬이 나타날 위치입니다. 비워두면 이 스포너 자신의 위치를 씁니다.")]
    public Transform spawnPoint;

    [Tooltip("위 위치에서 얼마나 띄워서 등장시킬지입니다. 구슬(반지름 약 0.2)이 바닥에 파묻혀 보이지 않도록 기본으로 살짝 띄워둡니다.")]
    public float spawnHeightOffset = 0.3f;

    [Tooltip("등장할 구슬의 색입니다.")]
    public OrbColor orbColor;

    [Tooltip("이 구슬을 획득하면 자동으로 열릴 계단입니다. 비워두면 계단 연동 없이 구슬만 등장합니다.")]
    public Stairs stairsToActivate;

    private bool spawned;

    private void Start()
    {
        if (trigger == null)
        {
            Debug.LogWarning($"[HiddenOrbSpawner] {name}: Trigger(PaintableObject)가 비어있어 구슬이 절대 등장하지 않습니다. Inspector에서 확인하세요.");
            return;
        }

        trigger.OnPainted += HandlePainted;
    }

    private void HandlePainted()
    {
        if (spawned) return; // 중복 색칠 파동 등으로 두 번 발동돼도 구슬은 한 번만
        spawned = true;

        Vector3 position = (spawnPoint != null ? spawnPoint.position : transform.position) + Vector3.up * spawnHeightOffset;

        if (ColorOrbPool.Instance == null)
        {
            Debug.LogWarning($"[HiddenOrbSpawner] {name}: ColorOrbPool.Instance가 없어 구슬을 등장시킬 수 없습니다.");
            return;
        }

        Debug.Log($"[HiddenOrbSpawner] {name}: 숨겨진 구슬 등장! ({orbColor})");
        ColorOrbPickup orb = ColorOrbPool.Instance.Get(position, orbColor);
        orb.OnPickedUp += HandlePickedUp;
    }

    private void HandlePickedUp()
    {
        Debug.Log($"[HiddenOrbSpawner] {name}: 숨겨진 구슬 획득!");
        stairsToActivate?.Activate();
    }
}
