using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// RandomEncounterBatchTool (재방문 랜덤 인카운터 일괄 배치 — 에디터 전용)
/// [[CubeMapManager]]가 7번째 회전부터 면을 완전 무작위로 고르기 때문에 "몇 층에 어떤 몹"을
/// 미리 정해둘 수 없다는 문제를, [[RandomEncounterSpawner]]를 6개 면 씬 전부에 **항상 켠 채로**
/// 깔아서 해결하기로 함(자세한 배경은 [[changelog/2026-08-24_7층-광배치]] 참고). 6개 씬을
/// 손으로 하나씩 열어서 반복하는 대신 이 메뉴 하나로 한 번에 처리합니다.
///
/// 이미 RandomEncounterSpawner가 있는 씬은 건너뜁니다(재실행해도 중복 추가 안 됨).
/// ⚠️ 씬을 순서대로 열고 저장하므로, 지금 열려있는 씬에 저장 안 한 변경사항이 있으면
/// 사라집니다 — 실행 전에 확인 다이얼로그가 한 번 뜹니다.
/// </summary>
public class RandomEncounterBatchTool
{
    private static readonly string[] FaceScenePaths =
    {
        "Assets/Scenes/SC_Face_0.unity",
        "Assets/Scenes/SC_Face_1.unity",
        "Assets/Scenes/SC_Face_2.unity",
        "Assets/Scenes/SC_Face_3.unity",
        "Assets/Scenes/SC_Face_4.unity",
        "Assets/Scenes/SC_Face_5.unity",
    };

    private static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Scenes/EnemyPyeong.prefab",
        "Assets/Scenes/EnemyWon.prefab",
        "Assets/Scenes/EnemyHeup.prefab",
        "Assets/Scenes/EnemyBun.prefab",
        "Assets/Scenes/EnemyGwang.prefab",
    };

    [MenuItem("MG/재방문 랜덤 인카운터 일괄 추가")]
    public static void AddToAllFaces()
    {
        if (!EditorUtility.DisplayDialog("확인",
            "SC_Face_0~5 씬을 순서대로 열고 저장합니다.\n지금 열려있는 씬에 저장 안 한 변경사항이 있으면 사라집니다.\n\n계속할까요?",
            "계속", "취소"))
        {
            return;
        }

        GameObject[] pool = new GameObject[EnemyPrefabPaths.Length];
        int foundCount = 0;
        for (int i = 0; i < EnemyPrefabPaths.Length; i++)
        {
            pool[i] = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPaths[i]);
            if (pool[i] != null) foundCount++;
            else Debug.LogWarning($"[RandomEncounterBatchTool] 프리팹을 못 찾았습니다: {EnemyPrefabPaths[i]}");
        }

        if (foundCount == 0)
        {
            EditorUtility.DisplayDialog("오류", "Enemy Pool 프리팹을 하나도 못 찾았습니다. 경로를 확인하세요.", "확인");
            return;
        }

        int added = 0, skipped = 0, missing = 0;

        foreach (string scenePath in FaceScenePaths)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[RandomEncounterBatchTool] {scenePath} 없음 — 건너뜁니다.");
                missing++;
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (Object.FindFirstObjectByType<RandomEncounterSpawner>() != null)
            {
                Debug.Log($"[RandomEncounterBatchTool] {scenePath}엔 이미 RandomEncounterSpawner가 있어서 건너뜁니다.");
                skipped++;
                continue;
            }

            // 씬마다 Ground/SpawnPoint 위치가 다를 수 있어서, (0,0,0) 대신 그 씬의 실제
            // SpawnPoint 위치를 기준으로 둡니다 — CubeMapManager.TeleportPlayerToSpawnPoint()와
            // 같은 이름 규칙("SpawnPoint")을 그대로 씁니다.
            GameObject spawnPointObj = GameObject.Find("SpawnPoint");
            Vector3 position = spawnPointObj != null ? spawnPointObj.transform.position : Vector3.zero;
            if (spawnPointObj == null)
                Debug.LogWarning($"[RandomEncounterBatchTool] {scenePath}에서 SpawnPoint를 못 찾아 (0,0,0)에 배치합니다.");

            var go = new GameObject("RandomEncounterSpawner");
            go.transform.position = position;

            var spawner = go.AddComponent<RandomEncounterSpawner>();
            spawner.enemyPool = pool;
            spawner.spawnCount = 3;
            spawner.maxDuplicatesPerType = 2;
            spawner.scatterRadius = 3f;
            spawner.autoStart = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            added++;

            Debug.Log($"[RandomEncounterBatchTool] {scenePath}에 RandomEncounterSpawner 추가 완료 (풀 {foundCount}종).");
        }

        Debug.Log($"[RandomEncounterBatchTool] 완료! 추가 {added}개, 건너뜀(이미 있음) {skipped}개, 없는 씬 {missing}개.");
        EditorUtility.DisplayDialog("완료",
            $"{added}개 씬에 RandomEncounterSpawner 추가함.\n건너뜀(이미 있음): {skipped}개\n\n각 씬 Play 모드로 열어서 스폰 위치가 자연스러운지 확인 필요.",
            "확인");
    }
}
