using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// MultiFaceArtDressingBuilder (2~6층 환경 아트 첫 배치 — 에디터 전용)
    /// [[changelog/2026-09-11_환경아트-1층첫배치]]에 이어, 아직 벽조차 없는 나머지 5개 면
    /// (SC_Face_1~5)에 각 면의 대표 구조물 + 5색 발광 오브젝트를 배치합니다.
    ///
    /// 면-테마 배정(코드 어디에도 고정되어 있지 않아서 이번에 확정, [[changelog/2026-09-10_환경다양성-6면테마확정]]
    /// 문서 순서를 그대로 따름):
    ///   Face 0 = 정육점 골목(빨강) — 이미 완료
    ///   Face 1 = 뒷골목(보라)      Face 2 = 전봇대 거리(노랑)
    ///   Face 3 = 약국 상가(초록)   Face 4 = 목욕탕 내부(파랑)
    ///   Face 5 = 지하상가(무채색, 발광 오브젝트 없음)
    ///
    /// 설계 원칙 3가지(자세한 근거는 [[changelog/2026-09-11_6면환경아트-전체배치]]):
    ///  1. 건축선(space syntax) — 구조물은 방 한가운데가 아니라 항상 벽선에 붙임
    ///  2. 그리드 모듈 체이닝(shape grammar) — 약국 아케이드/지하상가 볼트처럼 1m급 그리드
    ///     모듈은 실측한 셀 크기만큼 이어붙여 반복
    ///  3. 결정론적 지터 — 완전 등간격은 인공적으로 보이므로 시드 고정 난수로 살짝 어긋나게
    ///
    /// 다섯 면 전부 재실행해도 안전합니다(각 면의 "FaceN_ArtDressing_Generated" 그룹을 지우고 재생성).
    /// </summary>
    public static class MultiFaceArtDressingBuilder
    {
        private const string RootName = "ArtDressing_Generated";
        private const int JitterSeed = 20260911;

        [MenuItem("MG/2~6층 환경 아트 배치 (All Faces)")]
        public static void BuildAllFromMenu() => BuildAllFromCLI();

        public static void BuildAllFromCLI()
        {
            BuildFace1();
            BuildFace2();
            BuildFace3();
            BuildFace4();
            BuildFace5();
        }

        // ------------------------------------------------------------------
        // Face 1 — 뒷골목 (보라): 노출 배관+계량기(벽 모서리) + 청사초롱(벽 걸이)
        // ------------------------------------------------------------------
        [MenuItem("MG/면별 환경 아트/Face1 뒷골목 (보라)")]
        public static void BuildFace1()
        {
            Scene scene = OpenFace(1);
            Transform root = ResetRoot(scene);

            ArtDressingUtils.BuildPerimeterWalls(root);

            var pipeAsset = Load("Assets/Models/Environment/Props/AlleyPipe_01.fbx");
            var lanternAsset = Load("Assets/Models/Environment/Lanterns/AlleyLantern_01.fbx");

            // 남쪽 벽 모서리 근처(기존 EnemyPyeong(-3.04,-2.00) 등에서 3m 이상 떨어진 자리)에
            // 배관을 세웁니다. 실측 결과 바닥 기준 0.315m 떠 있어서 접지 보정.
            Bounds pipeBounds = ArtDressingUtils.MeasureBounds(pipeAsset);
            float pipeGroundOffset = -pipeBounds.min.y;
            var pipe = ArtDressingUtils.Instantiate(pipeAsset, root, "AlleyPipe_Corner",
                new Vector3(-4f, pipeGroundOffset, -4.85f));

            // 청사초롱은 같은 벽을 따라 중앙 쪽, 사람 눈높이보다 약간 위(벽 고리에 걸린 느낌)에 배치.
            var lantern = ArtDressingUtils.Instantiate(lanternAsset, root, "AlleyLantern_Wall",
                new Vector3(0f, 2.15f, -4.75f));

            SaveFace(scene, 1, "AlleyPipe_Corner + AlleyLantern_Wall");
        }

        // ------------------------------------------------------------------
        // Face 2 — 전봇대 거리 (노랑): 전봇대 + 가로등, 같은 "보도선"을 따라 이격 배치
        // ------------------------------------------------------------------
        [MenuItem("MG/면별 환경 아트/Face2 전봇대 거리 (노랑)")]
        public static void BuildFace2()
        {
            Scene scene = OpenFace(2);
            Transform root = ResetRoot(scene);

            ArtDressingUtils.BuildPerimeterWalls(root);

            var poleAsset = Load("Assets/Models/Environment/Props/UtilityPole_01.fbx");
            var lampAsset = Load("Assets/Models/Environment/Lanterns/StreetLamp_01.fbx");

            // 북쪽 벽(기존 Stairs X=1.01과 EnemyPyeong들에서 떨어진 자리)을 "보도선"으로 삼아
            // 전봇대와 가로등을 서로 다른 위치에 배치 — 실제 거리처럼 한 줄에 늘어서되 붙어있지 않게.
            ArtDressingUtils.Instantiate(poleAsset, root, "UtilityPole_North",
                new Vector3(3f + ArtDressingUtils.DeterministicJitter(JitterSeed, 0, 0.3f), 0f, 4.7f));

            ArtDressingUtils.Instantiate(lampAsset, root, "StreetLamp_North",
                new Vector3(-3.5f + ArtDressingUtils.DeterministicJitter(JitterSeed, 1, 0.3f), 0f, 4.7f));

            SaveFace(scene, 2, "UtilityPole_North + StreetLamp_North");
        }

        // ------------------------------------------------------------------
        // Face 3 — 약국 상가 (초록): 아케이드 유리 지붕 3칸 체이닝(그리드 모듈) + 네온 사인
        // ------------------------------------------------------------------
        [MenuItem("MG/면별 환경 아트/Face3 약국 상가 (초록)")]
        public static void BuildFace3()
        {
            Scene scene = OpenFace(3);
            Transform root = ResetRoot(scene);

            ArtDressingUtils.BuildPerimeterWalls(root);

            var roofAsset = Load("Assets/Models/Environment/BuildingModules/Roofs/ArcadeRoof_01.fbx");
            var signAsset = Load("Assets/Models/Environment/Lanterns/PharmacySign_01.fbx");

            // 셀 깊이(체이닝 축)를 실측해서 이어붙입니다 — 어림수 대신 실측값 사용.
            Bounds roofBounds = ArtDressingUtils.MeasureBounds(roofAsset);
            float cellDepth = roofBounds.size.z; // 로컬 Z가 체이닝 축(실측 약 1.04m)
            float halfWidth = roofBounds.size.x * 0.5f; // 로컬 X가 지붕 폭(실측 약 3.09m)

            // 동쪽 벽(기존 ShopSign X=0.69, WallExplosionHazard X=-1.51에서 떨어진 자리)에
            // 벽 쪽 가장자리를 붙이고 통로 쪽으로 폭만큼 뻗어나가게 배치.
            float wallSideX = 5f - 0.1f; // 벽 안쪽 살짝 여유
            float posX = wallSideX - halfWidth;
            float startZ = -1.5f;

            for (int i = 0; i < 3; i++)
            {
                float z = startZ + i * cellDepth;
                ArtDressingUtils.Instantiate(roofAsset, root, $"ArcadeRoof_Cell{i}", new Vector3(posX, 0f, z));
            }

            // 네온 간판은 아케이드 밑, 동쪽 벽면에 부착 — 정면(로컬 +Z)이 방 안쪽(-X)을 보도록 -90도 추가 회전.
            var sign = ArtDressingUtils.Instantiate(signAsset, root, "PharmacySign_East",
                new Vector3(4.85f, 1.7f, startZ + (3 * cellDepth) * 0.5f));
            ArtDressingUtils.AddYaw(sign, -90f);

            SaveFace(scene, 3, $"ArcadeRoof x3 (cellDepth={cellDepth:F2}) + PharmacySign_East");
        }

        // ------------------------------------------------------------------
        // Face 4 — 목욕탕 내부 (파랑): 서쪽 벽에 실제 개구부를 뚫고 타일 아치를 문틀로 배치
        // ------------------------------------------------------------------
        [MenuItem("MG/면별 환경 아트/Face4 목욕탕 내부 (파랑)")]
        public static void BuildFace4()
        {
            Scene scene = OpenFace(4);
            Transform root = ResetRoot(scene);

            var archAsset = Load("Assets/Models/Environment/BuildingModules/Windows/BathhouseArch_01.fbx");
            var signAsset = Load("Assets/Models/Environment/Lanterns/BathhouseSign_01.fbx");

            Bounds archBounds = ArtDressingUtils.MeasureBounds(archAsset);
            float doorWidth = archBounds.size.x + 0.2f; // 아치 폭 + 여유
            float doorCenterZ = 1.5f; // 기존 EnemyPyeong(-3.58,-2.14)에서 3.6m+ 떨어진 자리

            // 장식으로 벽에 거는 대신, 진짜 서쪽 벽에 문을 뚫고 그 자리에 아치를 끼워 넣습니다.
            ArtDressingUtils.BuildPerimeterWallsWithWestDoor(root, doorCenterZ, doorWidth);

            // ⚠️ 파츠 실측(Bathhouse_PierLeft/Right가 로컬 Z=-1.2, X=±0.9)으로 확인한 결과 —
            // 기본 방향은 "통과축(로컬 Z)"이 문 폭 방향(월드 Z)에, "기둥 간격(로컬 X)"이 벽
            // 두께 방향(월드 X)에 잘못 물려있었습니다. Y축 90도를 추가로 곱해 통과축을 벽
            // 두께 방향(월드 X)으로, 기둥 간격을 문 폭 방향(월드 Z)으로 맞바꿉니다.
            var arch = ArtDressingUtils.Instantiate(archAsset, root, "BathhouseArch_Doorway",
                new Vector3(-5.4f, 0f, doorCenterZ));
            ArtDressingUtils.AddYaw(arch, 90f);

            // 네온 굴뚝 사인은 문 옆(북쪽), 벽 안쪽 면에 배치.
            ArtDressingUtils.Instantiate(signAsset, root, "BathhouseSign_BesideDoor",
                new Vector3(-4.7f, 0f, doorCenterZ + doorWidth * 0.5f + 0.6f));

            SaveFace(scene, 4, $"BathhouseArch_Doorway(door width={doorWidth:F2}) + BathhouseSign_BesideDoor");
        }

        // ------------------------------------------------------------------
        // Face 5 — 지하상가 (무채색): 콘크리트 볼트 2칸 체이닝. 발광 오브젝트 없음(설계 확정 사항).
        // ------------------------------------------------------------------
        [MenuItem("MG/면별 환경 아트/Face5 지하상가 (무채색)")]
        public static void BuildFace5()
        {
            Scene scene = OpenFace(5);
            Transform root = ResetRoot(scene);

            ArtDressingUtils.BuildPerimeterWalls(root);

            var vaultAsset = Load("Assets/Models/Environment/BuildingModules/Roofs/UndergroundVault_01.fbx");

            Bounds vaultBounds = ArtDressingUtils.MeasureBounds(vaultAsset);
            float cellDepth = vaultBounds.size.z;
            float halfWidth = vaultBounds.size.x * 0.5f;

            // 서쪽 벽(기존 EnemyPyeong(1) Z=-2.20에서 떨어진 북쪽 자리)에 붙여서 2칸 터널 구간 조성.
            float wallSideX = -5f + 0.1f;
            float posX = wallSideX + halfWidth;
            float startZ = 1.0f;

            for (int i = 0; i < 2; i++)
            {
                float z = startZ + i * cellDepth;
                ArtDressingUtils.Instantiate(vaultAsset, root, $"UndergroundVault_Cell{i}", new Vector3(posX, 0f, z));
            }

            SaveFace(scene, 5, $"UndergroundVault x2 (cellDepth={cellDepth:F2}), 발광 오브젝트 없음(검정=무채색 설계 확정)");
        }

        // ------------------------------------------------------------------
        // 공용 헬퍼
        // ------------------------------------------------------------------

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogError($"[MultiFaceArtDressingBuilder] 에셋을 못 찾았습니다: {path}");
            return go;
        }

        private static Scene OpenFace(int faceIndex) =>
            EditorSceneManager.OpenScene($"Assets/Scenes/SC_Face_{faceIndex}.unity", OpenSceneMode.Single);

        private static Transform ResetRoot(Scene scene)
        {
            GameObject prev = GameObject.Find(RootName);
            if (prev != null) Object.DestroyImmediate(prev);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private static void SaveFace(Scene scene, int faceIndex, string summary)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MultiFaceArtDressingBuilder] Face {faceIndex} 완료 — {summary}");
        }
    }
}
