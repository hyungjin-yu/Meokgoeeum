using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeStreetDressingBuilder (큐브 표면 보행 프로토타입 — 골목 환경 에셋 실배치)
    /// [[changelog/2026-09-12_골목환경에셋-도로나무경계석]]에서 만든 도로 타일/경계석/가로수 3종을
    /// 실제로 [[CubeSurfaceWalker]]가 걷는 큐브 윗면(Y+)에 깔아서 스케일감·보행감을 확인하는 용도.
    /// "옵션 B"(문으로 던전 연결)는 아직 없음 — 순수하게 에셋 배치 자체만 검증하는 단계.
    ///
    /// 도로를 Z축 방향으로 가로지르는 8m 폭 스트립으로 깔고, 양옆에 경계석을 이어붙이고,
    /// 그 바깥에 가로수 3종을 [[ArtDressingUtils]].DeterministicJitter로 살짝 흐트려 심었습니다.
    /// 재실행해도 안전합니다(기존 StreetDressing 루트를 지우고 다시 만듦).
    /// </summary>
    public static class CubeStreetDressingBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_CubePrototype.unity";

        // ⚠️ CubePrototypeBuilder.CubeHalfExtent와 반드시 같은 값이어야 합니다 — 둘이 따로 놀면
        // 도로/나무가 큐브 표면이 아닌 허공이나 땅속에 배치됩니다.
        private const float CubeHalfExtent = 10f;

        private const string RoadPrefabPath = "Assets/Models/Environment/Roads/AlleyRoad_01.fbx";
        private const string CurbPrefabPath = "Assets/Models/Environment/Roads/AlleyCurb_01.fbx";

        private static readonly string[] TreePrefabPaths =
        {
            "Assets/Models/Environment/Vegetation/StreetTree_01.fbx",
            "Assets/Models/Environment/Vegetation/StreetTree_02.fbx",
            "Assets/Models/Environment/Vegetation/StreetTree_03.fbx",
        };

        [MenuItem("MG/큐브 골목 환경 배치")]
        public static void BuildFromCLI()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[CubeStreetDressingBuilder] {ScenePath}가 없습니다. 먼저 CubePrototypeBuilder를 실행하세요.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 재실행 안전 — 기존 배치 루트만 제거하고 나머지(큐브/플레이어/카메라/존)는 그대로 둠.
            GameObject old = GameObject.Find("StreetDressing");
            if (old != null) Object.DestroyImmediate(old);

            GameObject roadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoadPrefabPath);
            GameObject curbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CurbPrefabPath);
            var treePrefabs = new GameObject[TreePrefabPaths.Length];
            for (int i = 0; i < TreePrefabPaths.Length; i++)
                treePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPaths[i]);

            if (roadPrefab == null || curbPrefab == null)
            {
                Debug.LogError("[CubeStreetDressingBuilder] 도로/경계석 프리팹을 찾을 수 없습니다 — 경로를 확인하세요.");
                return;
            }
            foreach (var t in treePrefabs)
            {
                if (t == null)
                {
                    Debug.LogError("[CubeStreetDressingBuilder] 가로수 프리팹 중 못 찾은 게 있습니다.");
                    return;
                }
            }

            // 실측 후 역산 — 어림짐작 좌표 대신 실제 바운드로 타일 간격/경계석 길이를 계산합니다
            // ([[ArtDressingUtils]] 관행, [[changelog/2026-09-11_환경아트-1층첫배치]] 교훈).
            Bounds roadBounds = ArtDressingUtils.MeasureBounds(roadPrefab);
            Bounds curbBounds = ArtDressingUtils.MeasureBounds(curbPrefab);
            float tileSize = roadBounds.size.x;
            float curbLength = curbBounds.size.x; // 경계석 기본 배치는 로컬 X축 방향으로 김
            float curbHeight = curbBounds.size.y;

            var root = new GameObject("StreetDressing");
            root.transform.position = Vector3.zero;

            const float surfaceY = CubeHalfExtent; // 큐브 윗면
            const float roadY = surfaceY + 0.01f; // z-fighting 방지용으로 살짝 띄움
            const int roadWidthTiles = 2;
            float roadHalfWidth = tileSize * roadWidthTiles * 0.5f;

            // 1. 도로 — Z축을 가로지르는 스트립
            var roadParent = new GameObject("Road");
            roadParent.transform.SetParent(root.transform, worldPositionStays: false);

            int tilesAlongZ = Mathf.Max(1, Mathf.FloorToInt((CubeHalfExtent * 2f) / tileSize));
            float roadStartZ = -tileSize * tilesAlongZ * 0.5f + tileSize * 0.5f;

            int tileCount = 0;
            for (int col = 0; col < roadWidthTiles; col++)
            {
                float x = -roadHalfWidth + tileSize * col + tileSize * 0.5f;
                for (int row = 0; row < tilesAlongZ; row++)
                {
                    float z = roadStartZ + tileSize * row;
                    ArtDressingUtils.Instantiate(roadPrefab, roadParent.transform, $"RoadTile_{tileCount++}",
                        new Vector3(x, roadY, z));
                }
            }

            // 2. 경계석 — 도로 양옆(X = ±roadHalfWidth)을 따라 Z축 방향으로 이어붙임
            //    (경계석 기본 배치가 X축 방향으로 길어서 90도 돌려서 씁니다)
            var curbParent = new GameObject("Curbs");
            curbParent.transform.SetParent(root.transform, worldPositionStays: false);

            int curbSegments = Mathf.Max(1, Mathf.FloorToInt((CubeHalfExtent * 2f) / curbLength));
            float curbStartZ = -curbLength * curbSegments * 0.5f + curbLength * 0.5f;
            float curbY = surfaceY + curbHeight * 0.5f;

            int curbCount = 0;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -roadHalfWidth : roadHalfWidth;
                string sideLabel = side == 0 ? "West" : "East";
                for (int i = 0; i < curbSegments; i++)
                {
                    float z = curbStartZ + curbLength * i;
                    var inst = ArtDressingUtils.Instantiate(curbPrefab, curbParent.transform,
                        $"Curb_{sideLabel}_{i}", new Vector3(x, curbY, z));
                    ArtDressingUtils.AddYaw(inst, 90f);
                    curbCount++;
                }
            }

            // 3. 가로수 — 경계석 바깥쪽에 일정 간격 + 결정론적 지터로 3종을 순환 배치
            var treeParent = new GameObject("Trees");
            treeParent.transform.SetParent(root.transform, worldPositionStays: false);

            const float treeSpacing = 4.5f;
            const float treeOffsetFromCurb = 1.2f;
            const int JitterSeed = 4201;

            int treeCountPerSide = Mathf.Max(1, Mathf.FloorToInt((CubeHalfExtent * 2f) / treeSpacing));
            float treeStartZ = -treeSpacing * treeCountPerSide * 0.5f + treeSpacing * 0.5f;

            int treeIndex = 0;
            for (int side = 0; side < 2; side++)
            {
                float sideSign = side == 0 ? -1f : 1f;
                string sideLabel = side == 0 ? "West" : "East";
                float baseX = sideSign * (roadHalfWidth + treeOffsetFromCurb);
                for (int i = 0; i < treeCountPerSide; i++)
                {
                    float z = treeStartZ + treeSpacing * i
                        + ArtDressingUtils.DeterministicJitter(JitterSeed, treeIndex, 0.6f);
                    float x = baseX + ArtDressingUtils.DeterministicJitter(JitterSeed, treeIndex + 500, 0.3f);

                    GameObject prefab = treePrefabs[treeIndex % treePrefabs.Length];
                    var inst = ArtDressingUtils.Instantiate(prefab, treeParent.transform,
                        $"Tree_{sideLabel}_{i}", new Vector3(x, surfaceY, z));
                    ArtDressingUtils.AddYaw(inst, ArtDressingUtils.DeterministicJitter(JitterSeed, treeIndex + 1000, 180f));
                    treeIndex++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[CubeStreetDressingBuilder] 완료 — 도로 타일 {tileCount}개, 경계석 {curbCount}개, 나무 {treeIndex}개 배치함. " +
                      $"tileSize={tileSize:F2}, curbLength={curbLength:F2}, roadHalfWidth={roadHalfWidth:F2}");
        }
    }
}
