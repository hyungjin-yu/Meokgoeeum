using UnityEngine;

/// <summary>
/// RoomClearGate (방 클리어 게이트)
/// 지정한 박스 범위 안에 살아있는 적이 하나도 없으면 [[Stairs]]를 활성화합니다.
/// [[19 층별 상세 설계]]에서 가장 흔한 계단 조건인 "전투 클리어"의 기본형 구현입니다.
///
/// 적을 미리 손으로 목록에 등록하는 대신 "이 범위 안에 EnemyHealth가 남아있는가"를
/// 주기적으로 검사하는 방식을 택했습니다 — [[EnemyBun]]처럼 처치 시 미니언이 새로
/// 생겨나는 경우에도(손으로 등록해둔 목록에는 없는 새 오브젝트) 자동으로 걸러집니다.
///
/// 다른 계단 조건(예: "색 구슬 획득 후 자동 등장", "특정 순서로 처치")이 필요한 층은
/// 이 스크립트 대신 새로 만들고 Stairs.Activate()만 그대로 호출하면 됩니다 — Stairs는
/// "누가 왜 열었는지" 모릅니다(단일 책임 분리).
/// </summary>
public class RoomClearGate : MonoBehaviour
{
    [Tooltip("이 범위(중심 = 이 오브젝트 위치) 안의 적을 전부 처치해야 계단이 열립니다.")]
    public Vector3 boundsSize = new Vector3(20f, 5f, 20f);

    [Tooltip("클리어되면 활성화할 계단입니다.")]
    public Stairs stairs;

    [Tooltip("검사 주기입니다. 매 프레임 안 하고 이 간격으로 확인합니다 (최적화 원칙 — EnemyPyeong의 perceptionInterval과 같은 이유).")]
    public float checkInterval = 0.5f;

    private bool cleared;
    private float timer;

    private void Update()
    {
        if (cleared) return;

        timer += Time.deltaTime;
        if (timer < checkInterval) return;
        timer = 0f;

        if (!AnyEnemyAlive())
        {
            cleared = true;
            Debug.Log($"[RoomClearGate] {name} 클리어! 계단 활성화.");
            if (stairs != null)
                stairs.Activate();
            else
                Debug.LogWarning($"[RoomClearGate] {name}에 Stairs가 연결 안 되어 있습니다.");
        }
    }

    private bool AnyEnemyAlive()
    {
        Collider[] hits = Physics.OverlapBox(transform.position, boundsSize / 2f, transform.rotation);
        foreach (var hit in hits)
        {
            if (hit.GetComponent<EnemyHealth>() != null)
                return true;
        }
        return false;
    }

    // 에디터에서 방 범위를 눈으로 확인하기 위한 기즈모입니다.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boundsSize);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
