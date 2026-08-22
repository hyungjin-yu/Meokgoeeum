using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

/// <summary>
/// FaceBootstrapWindow (새 면 만들기 자동화 — 에디터 전용)
/// 지금까지 1~6층 만들 때마다 매번 손으로 반복했던 절차([[logic/큐브 좌표계 씬 설정]] 4~7단계,
/// [[logic/계단 & 방 클리어 게이트 씬 설정]] 전체)를 버튼 하나로 처리합니다:
///
/// 새 씬 생성 → Ground/SpawnPoint 배치 → Stairs(Box Collider Is Trigger 켜짐,
/// Start Active 꺼짐) → RoomClearGate(Stairs 연결까지) → 씬 저장 → Addressable 등록 →
/// CubeFaceData 에셋 생성(Face Index/Scene Reference 채움) → GameState.faceData 배열에 연결.
///
/// 이 다음에 손으로 할 일은 "적/장식 배치"뿐입니다 — 그건 층마다 내용이 달라서 자동화 대상이
/// 아닙니다.
///
/// ⚠️ Face Index(0~5)는 회전 테이블(CubeMapManager.RotationTableCW) 계산 결과와 일치해야
/// 실제로 그 면으로 갈 수 있습니다 — 헷갈리면 먼저 물어보고 진행하세요.
/// </summary>
public class FaceBootstrapWindow : EditorWindow
{
    private int fileNumber;
    private int faceIndex;

    [MenuItem("MG/새 면 만들기 (Face Bootstrap)")]
    public static void ShowWindow()
    {
        GetWindow<FaceBootstrapWindow>("새 면 만들기");
    }

    private void OnGUI()
    {
        GUILayout.Label("새 면(Face) 씬을 한 번에 만듭니다.", EditorStyles.boldLabel);
        GUILayout.Space(6);

        fileNumber = EditorGUILayout.IntField(
            new GUIContent("씬 파일 번호", "SC_Face_N의 N입니다. 아직 안 쓴 번호로 아무거나 정하면 됩니다 — Face Index와 같을 필요 없습니다."),
            fileNumber);

        faceIndex = EditorGUILayout.IntField(
            new GUIContent("Face Index (0~5)", "GameState의 Face Data 배열에서 실제로 들어갈 Element 번호입니다. 회전 테이블 계산 결과와 일치해야 하니 헷갈리면 먼저 확인하세요."),
            faceIndex);

        GUILayout.Space(10);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("만들기", GUILayout.Height(30)))
                TryCreateFace();
        }

        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Play 모드 중에는 실행할 수 없습니다. 먼저 정지하세요.", MessageType.Warning);

        GUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "만들어지는 것: Ground, SpawnPoint, Stairs(트리거+비활성), RoomClearGate(Stairs 연결됨), " +
            "Addressable 등록, CubeFaceData 에셋, GameState 연결까지 전부.\n\n" +
            "이 다음에 손으로 할 일: 이 씬을 열어서 적/장식 배치 → RoomClearGate의 Bounds Size를 " +
            "실제 배치에 맞게 조정.",
            MessageType.Info);
    }

    private void TryCreateFace()
    {
        if (faceIndex < 0 || faceIndex > 5)
        {
            EditorUtility.DisplayDialog("오류", "Face Index는 0~5 사이여야 합니다.", "확인");
            return;
        }

        string sceneName = $"SC_Face_{fileNumber}";
        string scenePath = $"Assets/Scenes/{sceneName}.unity";

        if (File.Exists(scenePath))
        {
            EditorUtility.DisplayDialog("오류", $"{scenePath}가 이미 존재합니다. 다른 번호를 쓰세요.", "확인");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");

        // 1. 새 씬 생성(추가 모드) — 지금 열려있는 씬들은 안 건드립니다.
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(newScene); // 이후 생성하는 오브젝트가 이 씬에 들어가도록

        // 2. Ground
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);

        // 3. SpawnPoint (이름이 정확히 "SpawnPoint"여야 CubeMapManager가 찾습니다)
        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = new Vector3(0f, 0.3f, 0f);

        // 4. Stairs — 3D Object > Cube로 만드는 것과 동일(BoxCollider 자동 포함)
        GameObject stairsObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stairsObj.name = "Stairs";
        stairsObj.transform.position = new Vector3(5f, 0.5f, 5f);
        stairsObj.transform.localScale = new Vector3(2f, 1f, 2f);
        stairsObj.GetComponent<BoxCollider>().isTrigger = true;
        Stairs stairs = stairsObj.AddComponent<Stairs>();
        stairs.startActive = false;

        // 5. RoomClearGate — Stairs까지 미리 연결
        var gateObj = new GameObject("RoomClearGate");
        RoomClearGate gate = gateObj.AddComponent<RoomClearGate>();
        gate.boundsSize = new Vector3(20f, 5f, 20f);
        gate.stairs = stairs;

        // 6. 저장
        EditorSceneManager.MarkSceneDirty(newScene);
        EditorSceneManager.SaveScene(newScene, scenePath);

        // 7. Addressable 등록
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[FaceBootstrap] Addressables 설정이 아직 없습니다 — Window > Asset Management > " +
                              "Addressables > Groups를 한 번 열어서 초기화한 뒤, Addressable 등록/CubeFaceData/" +
                              "GameState 연결은 기존 절차대로 수동으로 해주세요. 씬 자체는 정상 생성됐습니다.");
            EditorUtility.DisplayDialog("일부 완료", $"{sceneName}은 만들어졌지만, Addressables가 아직 초기화 " +
                "안 되어 있어서 그 이후 단계는 못 했습니다. Addressables Groups 창을 한 번 열고 다시 시도하세요.", "확인");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup("Default Local Group") ?? settings.DefaultGroup;
        string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(sceneGuid, group);
        entry.address = sceneName;

        // 8. CubeFaceData 에셋 생성
        var data = ScriptableObject.CreateInstance<CubeFaceData>();
        data.faceIndex = faceIndex;
        data.sceneReference = new AssetReference(sceneGuid);

        string dataPath = $"Assets/Data/CubeFaceData_{fileNumber}.asset";
        AssetDatabase.CreateAsset(data, dataPath);
        AssetDatabase.SaveAssets();

        // 9. GameState.faceData 배열에 연결
        GameState gameState = FindGameStateAcrossOpenScenes();
        bool linkedToGameState = false;

        if (gameState == null)
        {
            Debug.LogWarning("[FaceBootstrap] 열려있는 씬에서 GameState를 못 찾았습니다 — SC_Game을 열어둔 상태에서 " +
                              "다시 실행하거나, GameState.Face Data 배열에 수동으로 연결하세요.");
        }
        else if (gameState.faceData == null || gameState.faceData.Length <= faceIndex)
        {
            Debug.LogWarning($"[FaceBootstrap] GameState.faceData 배열 크기가 {faceIndex + 1}보다 작습니다. " +
                              "Inspector에서 Size를 6으로 맞추고 수동으로 연결하세요.");
        }
        else
        {
            gameState.faceData[faceIndex] = data;
            EditorUtility.SetDirty(gameState);
            EditorSceneManager.MarkSceneDirty(gameState.gameObject.scene);
            linkedToGameState = true;
        }

        Debug.Log($"[FaceBootstrap] {sceneName} 생성 완료! (Face Index={faceIndex}, GameState 연결={(linkedToGameState ? "완료" : "수동 필요")})");

        EditorUtility.DisplayDialog("완료",
            $"{sceneName} 생성 완료!\nFace Index = {faceIndex}\nGameState 연결: {(linkedToGameState ? "완료" : "수동으로 해야 함 — 콘솔 경고 확인")}\n\n" +
            "이제 이 씬을 열어서 적/장식만 배치하면 됩니다.", "확인");
    }

    private static GameState FindGameStateAcrossOpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var gs = root.GetComponentInChildren<GameState>(true);
                if (gs != null) return gs;
            }
        }
        return null;
    }
}
