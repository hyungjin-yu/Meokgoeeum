using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Floor1CorridorBuilder (1층 입장 복도 & 방B 절차적 생성 — 에디터 전용)
/// [[logic/1층 콘텐츠 - 입장 복도 & 방B 씬 설정]] 문서의 0~3단계(바닥 보강, 복도, 장애물
/// 구간, 방B 간판)를 코드로 자동 생성합니다. 손으로 큐브 늘리고 좌표 맞추는 대신, "복도
/// 세그먼트 목록을 방 경계에서부터 바깥으로 이어붙이는" 마칭(marching) 알고리즘 하나로
/// 바닥/벽을 한 번에 뽑아냅니다 — 세그먼트마다 폭이 달라도(장애물 구간에서 좁아짐)
/// 자동으로 이어집니다.
///
/// 실행 방법:
///   에디터: 메뉴 MG > 1층 입장 복도 생성 (Corridor Builder)
///   CLI:    Unity.exe -batchmode -quit -projectPath &lt;프로젝트 경로&gt;
///           -executeMethod Floor1CorridorBuilder.BuildFromCLI -logFile -
///
/// 재실행해도 안전합니다 — 이전에 만든 "Floor1_Corridor_Generated" 그룹을 지우고 다시
/// 만듭니다(SpawnPoint 위치 이동은 매번 같은 값으로 재계산되므로 누적 오차 없음).
/// </summary>
public static class Floor1CorridorBuilder
{
    private const string ScenePath = "Assets/Scenes/SC_Face_0.unity";
    private const string GeneratedRootName = "Floor1_Corridor_Generated";

    // 기존 Ground(10x10 기본 Plane, 원점 중심)의 +X쪽 경계 — 복도가 여기서부터 바깥으로 붙습니다.
    private const float RoomEdgeX = 5f;
    // 기존 SpawnPoint(3.57, 0.3, 0.47)의 z에 맞춰 복도 중심선을 잡습니다.
    private const float CorridorCenterZ = 0.47f;

    private const float WallHeight = 3f;
    private const float WallThickness = 0.3f;

    // 2026-08-25 실측: 장애물과 벽 사이에 낀 순간 CharacterController가 바닥을 뚫고 떨어지는
    // 버그 발견. 장애물(Transform로 직접 이동, Rigidbody 없음)이 플레이어를 밀어내지 못하고
    // 겹쳐버릴 수 있는데, 그 상태에서 중력으로 인한 cc.Move() 보정이 크게 튀면서 얇은(0.1)
    // 바닥 콜라이더를 한 프레임에 그냥 통과해버림(Unity CharacterController는 연속 충돌
    // 감지가 아니라 매 Move() 호출마다 스윕 검사라서, 순간 이동급 보정에는 못 잡힘). 바닥을
    // 두껍게 만들면(콜라이더에 실제 부피를 줘서) 이런 순간적인 큰 보정에도 뚫리지 않음 —
    // 근본 원인(장애물 겹침 자체)을 없애기보다, 결과(바닥 뚫림)를 물리적으로 불가능하게 막는 방식.
    private const float FloorThickness = 2f;

    private struct SegmentDef
    {
        public string Label;
        public float Length;
        public float Width;
        public bool HasObstacle;
    }

    private struct PlacedSegment
    {
        public SegmentDef Def;
        public float StartX;
        public float EndX;
        public float MidX;
    }

    // 플레이어가 실제로 걷는 순서(입구 → 방A)대로 적었습니다. 아래 Build()에서는 방 경계에서
    // 바깥쪽으로 쌓아야 해서 이 배열을 "역순"으로 순회합니다 — 목록 자체는 기획 문서
    // 순서 그대로 두는 게 더 읽기 쉬워서 이렇게 나눴습니다.
    private static readonly SegmentDef[] GameplayOrderSegments =
    {
        new SegmentDef { Label = "1단계-직선복도", Length = 6f, Width = 4f, HasObstacle = false },
        new SegmentDef { Label = "2단계-장애물구간", Length = 4f, Width = 3f, HasObstacle = true },
    };

    // 장애물의 "가로 막는 방향(월드 Z)" 두께. 플레이어 CharacterController 반지름이 0.5(지름 1m,
    // [[Assets/Scenes/SC_Game.unity]] 확인)라서, 장애물이 가장 비켜난 순간 열리는 틈이
    // 최소 1.4m는 되도록 아래 BuildObstacle()에서 이 값과 장애물구간 Width로 travelDistance를
    // 역산합니다. (2026-08-25 실측: 원래 값(폭1.6+장애물1.0+진폭1.2)은 최대 틈이 0.9m뿐이라
    // 플레이어 지름 1.0m보다 항상 좁아서 통과가 물리적으로 불가능했던 실제 버그였음)
    private const float ObstacleBlockWidth = 1.2f;

    [MenuItem("MG/1층 입장 복도 생성 (Corridor Builder)")]
    public static void BuildFromMenu() => Build();

    // CLI(batchmode)에서 -executeMethod로 부르는 진입점. 대화상자 없이 로그만 남기고 저장까지 합니다.
    public static void BuildFromCLI() => Build();

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject prevRoot = GameObject.Find(GeneratedRootName);
        if (prevRoot != null) Object.DestroyImmediate(prevRoot);

        var root = new GameObject(GeneratedRootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        // --- 마칭 알고리즘 ---
        // cursorX를 방 경계(RoomEdgeX)에서 시작해, 세그먼트 길이만큼씩 +X로 밀어내며
        // 세그먼트마다 바닥 하나 + 좌우 벽 두 개를 만듭니다. 폭이 바뀌는 지점에서는 벽이
        // 자연스럽게 "턱"으로 이어져 시각적으로도 통로가 좁아지는 게 보입니다.
        float cursorX = RoomEdgeX;
        var placed = new List<PlacedSegment>(GameplayOrderSegments.Length);

        for (int i = GameplayOrderSegments.Length - 1; i >= 0; i--)
        {
            SegmentDef def = GameplayOrderSegments[i];
            float startX = cursorX;
            float endX = cursorX + def.Length;
            float midX = (startX + endX) * 0.5f;

            BuildFloor(root.transform, def.Label, midX, def.Length, def.Width);
            BuildWall(root.transform, def.Label, midX, def.Length, def.Width, +1f);
            BuildWall(root.transform, def.Label, midX, def.Length, def.Width, -1f);

            placed.Add(new PlacedSegment { Def = def, StartX = startX, EndX = endX, MidX = midX });
            cursorX = endX;
        }

        float farEndX = cursorX; // 복도 맨 끝 = 플레이어 스폰 지점

        PlacedSegment straight = placed.Find(p => !p.Def.HasObstacle);
        PlacedSegment obstacleSeg = placed.Find(p => p.Def.HasObstacle);

        // 1단계 끝(직선 복도 → 장애물 구간 진입) = 두 세그먼트의 경계
        BuildCheckpoint(root.transform, "Checkpoint_CorridorEnd", "1단계 - 복도 끝",
            straight.StartX, Mathf.Max(straight.Def.Width, obstacleSeg.Def.Width));

        // 2단계 끝(장애물 통과 직후, 방A 진입 직전) = 장애물 구간의 방 쪽 끝에서 살짝 안쪽
        BuildCheckpoint(root.transform, "Checkpoint_ObstaclePassed", "2단계 - 장애물 통과",
            obstacleSeg.StartX + 0.5f, obstacleSeg.Def.Width);

        BuildObstacle(root.transform, obstacleSeg.MidX, obstacleSeg.Def.Width);

        // 플레이어 시작 위치를 복도 입구로. 방(=-X)을 바라보도록 회전도 같이 맞춥니다.
        GameObject spawnPoint = GameObject.Find("SpawnPoint");
        if (spawnPoint != null)
        {
            Vector3 newPos = new Vector3(farEndX - 1.5f, 0.3f, CorridorCenterZ);
            spawnPoint.transform.SetPositionAndRotation(newPos, Quaternion.Euler(0f, -90f, 0f));
            Debug.Log($"[Floor1CorridorBuilder] SpawnPoint를 복도 입구({newPos})로 이동했습니다.");
        }
        else
        {
            Debug.LogWarning("[Floor1CorridorBuilder] SpawnPoint를 찾지 못해 위치 이동을 건너뛰었습니다.");
        }

        BuildSignboard(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[Floor1CorridorBuilder] 완료 — 복도 {RoomEdgeX}~{farEndX} (총 {farEndX - RoomEdgeX}m), " +
                  $"장애물 중심 X={obstacleSeg.MidX}, {ScenePath} 저장함.");
    }

    private static void BuildFloor(Transform parent, string label, float midX, float length, float width)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = $"Floor_{label}";
        floor.transform.SetParent(parent, worldPositionStays: false);
        floor.transform.position = new Vector3(midX, -FloorThickness * 0.5f, CorridorCenterZ);
        floor.transform.localScale = new Vector3(length, FloorThickness, width);
    }

    private static void BuildWall(Transform parent, string label, float midX, float length, float width, float side)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"Wall_{label}_{(side > 0 ? "Plus" : "Minus")}Z";
        wall.transform.SetParent(parent, worldPositionStays: false);
        float z = CorridorCenterZ + side * (width * 0.5f + WallThickness * 0.5f);
        wall.transform.position = new Vector3(midX, WallHeight * 0.5f, z);
        wall.transform.localScale = new Vector3(length, WallHeight, WallThickness);
    }

    private static void BuildCheckpoint(Transform parent, string objectName, string checkpointName, float x, float width)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = new Vector3(x, 1.5f, CorridorCenterZ);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.5f, 3f, width);

        TutorialCheckpoint checkpoint = go.AddComponent<TutorialCheckpoint>();
        checkpoint.checkpointName = checkpointName;
    }

    private static void BuildObstacle(Transform parent, float midX, float narrowWidth)
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "Obstacle_Slow";
        obstacle.transform.SetParent(parent, worldPositionStays: false);
        obstacle.transform.position = new Vector3(midX, 0.8f, CorridorCenterZ);

        // MovingObstacle은 transform.right 방향으로 왕복합니다(스크립트 참고). 복도가 X축
        // 방향이라 "좌우로 오가며 통로를 막는" 느낌을 내려면 오브젝트를 Y축으로 90도 돌려서
        // local right가 world Z(복도 폭 방향)를 향하게 해야 합니다.
        obstacle.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        // 90도 회전 후: local X(오른쪽, MovingObstacle이 오가는 축) → world Z, local Z → world X.
        // 즉 x=폭 방향(Z) 크기, z=복도 방향(X) 두께.
        obstacle.transform.localScale = new Vector3(ObstacleBlockWidth, 1.8f, 1.5f);

        MovingObstacle mover = obstacle.AddComponent<MovingObstacle>();
        // travelDistance(진폭)를 "장애물이 벽에 닿기 직전까지"로 역산: 벽 안쪽 면
        // (narrowWidth/2)에서 여유 0.2m를 남기고 왕복하게 하면, 가장 비켜난 순간
        // 반대쪽에 (narrowWidth - ObstacleBlockWidth - 0.2)만큼의 틈이 열립니다.
        // 플레이어 CharacterController 지름이 1.0m라서 이 틈이 항상 1.4m 이상 나오도록
        // narrowWidth/ObstacleBlockWidth를 같이 골랐습니다(1.6m 이상 여유).
        mover.travelDistance = Mathf.Max(0.5f, narrowWidth - ObstacleBlockWidth - 0.2f);
        mover.period = 4f;
    }

    private static void BuildSignboard(Transform parent)
    {
        if (GameObject.Find("Signboard_Floor1") != null) return; // 이미 있으면 손대지 않음(색 진행 상태 보존)

        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Signboard_Floor1";
        sign.transform.SetParent(parent, worldPositionStays: false);
        // 방A 안쪽, 기존 웨이브 스폰 지점들(-X쪽)과 겹치지 않는 +X쪽 벽 근처에 배치.
        sign.transform.position = new Vector3(3f, 1f, -3f);
        sign.transform.localScale = new Vector3(1.5f, 2f, 0.2f);

        PaintableObject paintable = sign.AddComponent<PaintableObject>();
        paintable.trueColor = new Color(1f, 0.85f, 0.3f); // 노란빛 간판색
        paintable.startGray = true;
    }
}
