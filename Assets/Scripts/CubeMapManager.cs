using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

/// <summary>
/// CubeMapManager (큐브 좌표계 매니저)
/// [[12 큐브 좌표계 설계]] 구현. 색 구슬 획득 시 큐브가 무작위 축으로 90도 회전하면서
/// 보이는 면(Addressable Scene)이 바뀝니다.
///
/// 단순화: 원안은 UniTask를 쓰지만, 이 프로젝트의 다른 비동기 흐름(BrushWeapon/EnemyPyeong/
/// PlayerDodge/ColorSkillController 전부)이 전부 Unity 기본 코루틴으로 짜여 있어서 일관성을
/// 위해 여기도 코루틴으로 구현했습니다. Addressables의 AsyncOperationHandle은 코루틴에서
/// 그대로 yield 가능해서(IEnumerator 구현) UniTask 없이도 문제 없습니다.
///
/// 아직 [[ColorWaveEffect]]에서 자동으로 호출하도록 연결하지 않았습니다 — 색 구슬을 주울
/// 때마다(=적을 잡을 때마다) 매번 화면이 전환되면 전투 흐름을 심하게 끊을 수 있어서,
/// 실제로 연결하기 전에 먼저 눈으로 확인해보고 판단하는 게 좋겠다고 판단했습니다.
/// 지금은 `DebugOrbCheat`의 F4로 수동 테스트만 가능합니다.
/// </summary>
public class CubeMapManager : MonoBehaviour
{
    public static CubeMapManager Instance { get; private set; }

    [Tooltip("게임 시작 시 로드할 첫 면입니다.")]
    public int startingFaceIndex = 0;

    public bool IsRotating { get; private set; }

    private int currentFaceIndex;
    private AsyncOperationHandle<SceneInstance> currentSceneHandle;
    private bool hasLoadedScene;

    // 각 축 90도 CW 회전 시 면 인덱스 재배치 테이블 ([[12 큐브 좌표계 설계]] 원문 그대로)
    // X축 CW: Front→Bottom→Back→Top→Front
    // Y축 CW: Front→Right→Back→Left→Front
    // Z축 CW: Top→Right→Bottom→Left→Top
    private static readonly int[][] RotationTableCW =
    {
        new[] { 3, 2, 0, 1, 4, 5 }, // X축 CW
        new[] { 4, 5, 2, 3, 1, 0 }, // Y축 CW
        new[] { 0, 1, 5, 4, 2, 3 }, // Z축 CW
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[CubeMapManager] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        currentFaceIndex = startingFaceIndex;
        StartCoroutine(LoadInitialFace());
    }

    private IEnumerator LoadInitialFace()
    {
        if (GameState.Instance == null)
        {
            Debug.LogError("[CubeMapManager] GameState가 씬에 없습니다. GameSystems에 GameState를 먼저 부착하세요.");
            yield break;
        }

        var faceData = GameState.Instance.GetFaceData(currentFaceIndex);
        if (faceData == null)
        {
            Debug.LogError($"[CubeMapManager] 시작 면({currentFaceIndex}) 데이터가 비어있습니다. GameState의 Face Data 배열을 확인하세요.");
            yield break;
        }

        yield return LoadFace(faceData);
        MarkVisited(currentFaceIndex);
        TeleportPlayerToSpawnPoint(); // 게임 시작 시에도 SpawnPoint로 옮겨야 함 — 씬 분리 전 원래 위치에 그대로 있으면 새 면의 Ground와 어긋나서 허공에 떨어질 수 있음
    }

    /// <summary>
    /// [[22 핵심 시스템 통합 기획]] ④~⑤ 단계. 나중에 색 복원 파동 완료 시점에 연결하거나,
    /// 지금처럼 디버그 키로 수동 호출할 수 있습니다.
    /// </summary>
    public void RotateRandomAxis()
    {
        if (IsRotating)
        {
            Debug.LogWarning("[CubeMapManager] 이미 회전 중입니다.");
            return;
        }
        StartCoroutine(RotateRoutine());
    }

    private IEnumerator RotateRoutine()
    {
        IsRotating = true;

        int axis = SelectWeightedAxis();
        if (axis < 0)
        {
            Debug.LogWarning("[CubeMapManager] 회전할 수 있는 면이 없습니다 — 인접한 면 중 CubeFaceData가 설정된 곳이 하나도 없습니다. GameState의 Face Data 배열을 확인하세요.");
            IsRotating = false;
            yield break;
        }

        int nextFaceIndex = RotationTableCW[axis][currentFaceIndex];
        Debug.Log($"[CubeMapManager] 회전 시작! 축={axis}, {currentFaceIndex}면 → {nextFaceIndex}면");

        if (FadeManager.Instance != null)
            yield return FadeManager.Instance.FadeOut();

        if (hasLoadedScene && currentSceneHandle.IsValid())
            yield return Addressables.UnloadSceneAsync(currentSceneHandle);

        var nextFaceData = GameState.Instance.GetFaceData(nextFaceIndex);
        yield return LoadFace(nextFaceData);

        currentFaceIndex = nextFaceIndex;
        MarkVisited(currentFaceIndex);
        TeleportPlayerToSpawnPoint();

        GameState.Instance.floorNumber++;
        Debug.Log($"[CubeMapManager] {GameState.Instance.floorNumber}층 도착. 현재 면: {currentFaceIndex}");

        if (FadeManager.Instance != null)
            yield return FadeManager.Instance.FadeIn();

        IsRotating = false;
    }

    private IEnumerator LoadFace(CubeFaceData faceData)
    {
        if (faceData == null || faceData.sceneReference == null || !faceData.sceneReference.RuntimeKeyIsValid())
        {
            Debug.LogError($"[CubeMapManager] 면 데이터 또는 씬 참조가 비어있습니다 (faceIndex={faceData?.faceIndex}). Addressables 설정을 확인하세요.");
            yield break;
        }

        var loadHandle = Addressables.LoadSceneAsync(faceData.sceneReference, UnityEngine.SceneManagement.LoadSceneMode.Additive);
        yield return loadHandle;

        currentSceneHandle = loadHandle;
        hasLoadedScene = true;
    }

    private void MarkVisited(int faceIndex)
    {
        var faceData = GameState.Instance.GetFaceData(faceIndex);
        if (faceData == null) return;

        faceData.visitCount++;
        faceData.isVisited = true;

        // 2번째 방문 = 첫 재방문(약한 데자뷰), 3번째 이상 = 확신 단계 — 나레이션 시스템은 v0.3 몫이라
        // 지금은 로그로만 남겨서 나중에 훅 걸 자리를 표시해둡니다.
        if (faceData.visitCount == 2)
            Debug.Log($"[CubeMapManager] {faceIndex}면 첫 재방문! (나레이션 훅 자리 — v0.3에서 연결)");
        else if (faceData.visitCount >= 3)
            Debug.Log($"[CubeMapManager] {faceIndex}면 재방문 {faceData.visitCount}회째! (확신 나레이션 훅 자리 — v0.3에서 연결)");
    }

    /// <summary>
    /// 새로 로드된 면에서 "SpawnPoint"라는 이름의 오브젝트를 찾아 플레이어를 그 위치로 옮깁니다.
    /// 단순화: 전용 SpawnPoint 컴포넌트 대신 이름 규칙으로 처리 — 면 씬마다 SpawnPoint라는
    /// 이름의 빈 오브젝트 하나만 두면 되므로 씬 설정이 제일 간단합니다.
    /// </summary>
    private void TeleportPlayerToSpawnPoint()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject spawnPoint = GameObject.Find("SpawnPoint");

        if (player == null || spawnPoint == null)
        {
            Debug.LogWarning("[CubeMapManager] Player 또는 SpawnPoint를 찾지 못해 위치 이동을 건너뜁니다.");
            return;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false; // 켜진 채로 강제 이동하면 충돌 이슈가 생길 수 있음

        player.transform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);

        if (cc != null) cc.enabled = true;

        Debug.Log($"[CubeMapManager] 플레이어를 {spawnPoint.transform.position}로 이동.");
    }

    /// <summary>
    /// 6개 면을 전부 최소 1번 방문하기 전까지는, 아직 안 가본 면으로만 이어지는 축을 우선 선택합니다.
    /// 다 가봤으면(7번째 회전부터) 완전 무작위 — 그때부터 재방문이 나올 수 있습니다.
    ///
    /// 개발 중(v0.2) 안전장치: CubeFaceData가 아예 설정 안 된 면(개발 중이라 아직 다 안 만든
    /// 면)은 후보에서 완전히 제외합니다 — 안 그러면 미설정 면으로 회전을 시도했다가 이전 면은
    /// 이미 내렸는데 다음 면을 못 불러와서 허공에 남는 상태가 될 수 있습니다. 유효한 면이
    /// 하나도 없으면 -1을 반환합니다(회전 불가).
    /// </summary>
    private int SelectWeightedAxis()
    {
        var unvisitedAxes = new List<int>();
        var validAxes = new List<int>();

        for (int axis = 0; axis < 3; axis++)
        {
            int candidate = RotationTableCW[axis][currentFaceIndex];
            var data = GameState.Instance.GetFaceData(candidate);
            if (data == null) continue; // 아직 CubeFaceData가 없는 면은 후보에서 제외

            validAxes.Add(axis);
            if (!data.isVisited)
                unvisitedAxes.Add(axis);
        }

        if (unvisitedAxes.Count > 0) return unvisitedAxes[Random.Range(0, unvisitedAxes.Count)];
        if (validAxes.Count > 0) return validAxes[Random.Range(0, validAxes.Count)];

        return -1; // 인접한 면 중 설정된 게 하나도 없음
    }
}
