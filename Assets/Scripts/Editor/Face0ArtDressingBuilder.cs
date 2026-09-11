using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// Face0ArtDressingBuilder (1층/정육점 골목 — 건물 모듈 + 발광 오브젝트 첫 배치, 에디터 전용)
    /// [[changelog/2026-09-10_6면건물모듈-전체완료]]/[[changelog/2026-09-10_발광오브젝트-빨강파랑제작]]에서
    /// 만든 FBX들이 아직 어느 씬에도 배치되지 않은 상태였습니다 — 이 스크립트는 "전체 그림"을
    /// 처음 확인하기 위한 파일럿으로, 방A(Ground, 10x10, 서쪽 벽은 문 없이 통째로 막혀있음 —
    /// [[Floor1CorridorBuilder]].BuildRoomABoundaryWalls 참고)의 서쪽 벽 안쪽에 3칸짜리 데모
    /// 건물(Building_StreetUnit_Demo)을 세우고, 처마 아래에 빨강 등불을 겁니다.
    ///
    /// 좌표 계산: Building_StreetUnit_Demo.fbx 로컬 좌표는 전면(창문)이 +Z, 후면(기단 안쪽)이
    /// Z≈0 부근입니다. 방A 서쪽 벽(X=-5, 문 없음)을 향해 정면이 +X를 보도록 Y축 +90도만큼
    /// 추가로 돌립니다.
    ///
    /// ⚠️ 2026-09-11 발견 — 환경 아트 FBX 전체(BuildingModules/Lanterns/Props 18개)가 Blender
    /// Z-up → Unity Y-up 보정 회전이 루트에 안 구워진 채로 임포트돼 있었습니다(기본 로컬 회전이
    /// (270,0,0) 등 0이 아님). `SetPositionAndRotation()`으로 회전을 절대값으로 덮어쓰면 이 보정을
    /// 지워버려서 건물이 옆으로 넘어져 보이는 실제 버그를 겪었습니다 — 모든 FBX의 Model Importer에서
    /// `bakeAxisConversion=true`로 재임포트해서 근본 수정했지만(→ [[changelog/2026-09-11_환경아트FBX-축보정버그]]),
    /// 재임포트 후에도 루트에 (90,0,0) 근처 회전이 남아있는 걸 확인했습니다(자식 메쉬가 이미 그에 맞게
    /// 구워져 있어 최종 결과는 똑바로 섬 — bounds로 실측 확인 완료). 그래서 이 스크립트는 회전을
    /// **덮어쓰지 않고 항상 prefab 기본 회전 위에 추가로 곱해서** 적용합니다.
    /// 이 스크립트는 색/발광 배선(PaintableObject 등)은 건드리지 않습니다 — 그건 별도 작업
    /// ([[11 셰이더 설계 - 색 복원 파동]] 확장, MaterialPropertyBlock 적용)으로 미룹니다.
    ///
    /// 재실행해도 안전합니다(이전 "Face0_ArtDressing_Generated" 그룹을 지우고 다시 만듦).
    /// </summary>
    public static class Face0ArtDressingBuilder
    {
        private const string ScenePath = "Assets/Scenes/SC_Face_0.unity";
        private const string GeneratedRootName = "Face0_ArtDressing_Generated";

        private const string BuildingAssetPath = "Assets/Models/Environment/BuildingModules/Building_StreetUnit_Demo.fbx";
        private const string LanternAssetPath = "Assets/Models/Environment/Lanterns/RedLantern_01.fbx";

        [MenuItem("MG/1층 건물+등불 첫 배치 (Face0 Art Dressing)")]
        public static void BuildFromMenu() => Build();

        public static void BuildFromCLI() => Build();

        private static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject prevRoot = GameObject.Find(GeneratedRootName);
            if (prevRoot != null) Object.DestroyImmediate(prevRoot);

            var root = new GameObject(GeneratedRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            var buildingAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingAssetPath);
            var lanternAsset = AssetDatabase.LoadAssetAtPath<GameObject>(LanternAssetPath);

            if (buildingAsset == null || lanternAsset == null)
            {
                Debug.LogError($"[Face0ArtDressingBuilder] 에셋을 못 찾았습니다. building={buildingAsset != null}, lantern={lanternAsset != null}");
                return;
            }

            // 서쪽 벽(문 없음, X=-5 안쪽)을 향해 정면이 +X를 보도록 배치.
            // ⚠️ 회전은 절대값 대입이 아니라 prefab 기본 회전(축 보정 잔여분) 위에 곱해서 추가합니다 — 위 클래스 설명 참고.
            // ⚠️ 위치도 손으로 어림한 좌표가 아니라, 원점+회전만 적용한 상태에서 실측한 바운드
            // (X:[-0.95,1.30] 깊이, Z:[-6.15,0.15] 길이, 2026-09-11 실측)를 기준으로 역산했습니다 —
            // 뒷면(X=-0.95)을 방A 서쪽 벽(X=-5)에 붙이고, 길이 중심(Z=-3.0)을 방 중심(Z=0)으로 옮김.
            GameObject building = (GameObject)PrefabUtility.InstantiatePrefab(buildingAsset, root.transform);
            building.name = "StreetUnit_Demo_West";
            building.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * building.transform.rotation;
            building.transform.position = new Vector3(-4.05f, 0f, 3.0f);

            // 처마 밑, 건물 전면(위 배치 후 월드 X≈-2.75) 근처에 빨강 등불을 겁니다
            // (정육점 골목 = 빨강, [[색-오브젝트 매칭 최종 확정]]).
            GameObject lantern = (GameObject)PrefabUtility.InstantiatePrefab(lanternAsset, root.transform);
            lantern.name = "RedLantern_West";
            lantern.transform.position = new Vector3(-2.6f, 2.3f, 0f);
            // 회전은 prefab 기본값(축 보정) 그대로 유지 — 추가 회전 없음.

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Face0ArtDressingBuilder] 완료 — StreetUnit_Demo_West + RedLantern_West 배치, " + ScenePath + " 저장함.");
        }
    }
}
