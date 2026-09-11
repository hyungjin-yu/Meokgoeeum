using UnityEditor;
using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// ArtDressingUtils (환경 아트 배치 공용 유틸 — 에디터 전용)
    /// [[changelog/2026-09-11_환경아트-1층첫배치]]에서 만든 "회전은 덮어쓰지 않고 곱해서 추가한다"
    /// 패턴과, 그 뒤([[changelog/2026-09-11_6면환경아트-전체배치]])에서 5개 면에 반복 적용한
    /// "실측 후 역산" 배치 방식을 공용 함수로 뽑았습니다. 새 면을 꾸밀 때마다 같은 실수(회전 덮어쓰기,
    /// 좌표 어림짐작)를 반복하지 않기 위한 것입니다.
    /// </summary>
    public static class ArtDressingUtils
    {
        /// <summary>
        /// prefab을 인스턴스화합니다. 환경 아트 FBX 전체가 Blender→Unity 축 보정 회전을
        /// 루트에 남긴 채 임포트돼 있어서([[changelog/2026-09-11_환경아트-1층첫배치]]),
        /// 이 기본 회전을 지우지 않고 그대로 유지한 채로 돌려줍니다 — 추가 회전이 필요하면
        /// AddYaw()를 따로 호출하세요.
        /// </summary>
        public static GameObject Instantiate(GameObject prefabAsset, Transform parent, string name, Vector3 position)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent);
            go.name = name;
            go.transform.position = position; // rotation은 prefab 기본값(축 보정) 그대로 유지
            return go;
        }

        /// <summary>
        /// 기존 회전(축 보정 포함) 위에 월드 Y축 추가 회전만 곱합니다.
        /// ⚠️ transform.rotation을 절대값으로 덮어쓰지 마세요 — 축 보정이 지워집니다.
        /// </summary>
        public static void AddYaw(GameObject go, float yawDegrees)
        {
            go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f) * go.transform.rotation;
        }

        /// <summary>
        /// prefab을 원점에 기본 회전으로 임시 배치해 실제 월드 바운드를 측정하고 즉시 파괴합니다.
        /// 좌표를 손으로 어림하지 않고, 실측값으로 배치를 역산하기 위한 용도입니다.
        /// </summary>
        public static Bounds MeasureBounds(GameObject prefabAsset)
        {
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            temp.transform.position = Vector3.zero;
            var renderers = temp.GetComponentsInChildren<Renderer>(true);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(temp);
            return b;
        }

        /// <summary>
        /// 10x10 Ground(원점 중심, half=5 기본)를 가진 면 하나를 사방 벽으로 완전히 밀폐합니다.
        /// [[Floor1CorridorBuilder]].BuildRoomABoundaryWalls와 같은 이유(터널링 방지)로 벽을
        /// 두껍게 만듭니다. 2~6층 면은(1층과 달리) 복도로 안 이어져 있고 계단으로만 다음 면으로
        /// 넘어가므로 문 없이 통째로 막습니다 — 지금까지 경계벽 자체가 없어서 전투 중 넓게
        /// 밀리면 맵 밖으로 떨어질 수 있던 잠재 버그([[changelog/2026-08-31_1층콘텐츠-입장복도]]
        /// "방A 경계벽 부재" 버그와 동일 클래스)를 덤으로 같이 막습니다.
        /// </summary>
        public static void BuildPerimeterWalls(Transform parent, float halfExtent = 5f, float wallHeight = 3f, float wallThickness = 2f)
        {
            float half = halfExtent;
            float t = wallThickness;

            BuildBoxWall(parent, "Wall_PlusZ", new Vector3(0f, wallHeight * 0.5f, half + t * 0.5f), new Vector3(half * 2f + t * 2f, wallHeight, t));
            BuildBoxWall(parent, "Wall_MinusZ", new Vector3(0f, wallHeight * 0.5f, -half - t * 0.5f), new Vector3(half * 2f + t * 2f, wallHeight, t));
            BuildBoxWall(parent, "Wall_PlusX", new Vector3(half + t * 0.5f, wallHeight * 0.5f, 0f), new Vector3(t, wallHeight, half * 2f));
            BuildBoxWall(parent, "Wall_MinusX", new Vector3(-half - t * 0.5f, wallHeight * 0.5f, 0f), new Vector3(t, wallHeight, half * 2f));
        }

        /// <summary>
        /// 서쪽(-X) 벽 한 곳에만 폭 doorWidth의 문(개구부)을 남기고 사방을 막습니다.
        /// 목욕탕 아치처럼 "실제 문틀에 끼워 넣는" 구조물을 위한 것 — 장식으로 벽에 걸어두는 대신
        /// 진짜 개구부를 만들어서 건축적으로 맞물리게 합니다([[Floor1CorridorBuilder]].
        /// BuildRoomABoundaryWalls의 문 패턴을 서쪽 벽에 적용한 것과 동일).
        /// </summary>
        public static void BuildPerimeterWallsWithWestDoor(Transform parent, float doorCenterZ, float doorWidth, float halfExtent = 5f, float wallHeight = 3f, float wallThickness = 2f)
        {
            float half = halfExtent;
            float t = wallThickness;

            BuildBoxWall(parent, "Wall_PlusZ", new Vector3(0f, wallHeight * 0.5f, half + t * 0.5f), new Vector3(half * 2f + t * 2f, wallHeight, t));
            BuildBoxWall(parent, "Wall_MinusZ", new Vector3(0f, wallHeight * 0.5f, -half - t * 0.5f), new Vector3(half * 2f + t * 2f, wallHeight, t));
            BuildBoxWall(parent, "Wall_PlusX", new Vector3(half + t * 0.5f, wallHeight * 0.5f, 0f), new Vector3(t, wallHeight, half * 2f));

            float doorHalf = doorWidth * 0.5f;
            float doorMinZ = doorCenterZ - doorHalf;
            float doorMaxZ = doorCenterZ + doorHalf;

            float southLen = doorMinZ - (-half);
            if (southLen > 0.01f)
            {
                float southZ = (-half + doorMinZ) * 0.5f;
                BuildBoxWall(parent, "Wall_MinusX_South", new Vector3(-half - t * 0.5f, wallHeight * 0.5f, southZ), new Vector3(t, wallHeight, southLen));
            }

            float northLen = half - doorMaxZ;
            if (northLen > 0.01f)
            {
                float northZ = (doorMaxZ + half) * 0.5f;
                BuildBoxWall(parent, "Wall_MinusX_North", new Vector3(-half - t * 0.5f, wallHeight * 0.5f, northZ), new Vector3(t, wallHeight, northLen));
            }
        }

        private static void BuildBoxWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, worldPositionStays: false);
            wall.transform.position = center;
            wall.transform.localScale = size;
        }

        /// <summary>
        /// 시드 고정 결정론적 지터입니다. 완전한 격자/등간격 배치는 인공적으로 보인다는 절차적
        /// 도시 생성 문헌의 공통 지적(Kelly &amp; McCabe, "A Survey of Procedural Techniques for
        /// City Generation", 2006 — 반복 배치에 소량의 노이즈를 섞는 관행)을 반영한 것으로,
        /// 매번 같은 시드+인덱스면 항상 같은 결과가 나와 재실행해도 안전합니다(Floor1CorridorBuilder류
        /// 스크립트의 "재실행해도 안전" 원칙과 동일).
        /// </summary>
        public static float DeterministicJitter(int seed, int index, float magnitude)
        {
            unchecked
            {
                int h = seed * 486187739 + index * 2654435761u.GetHashCode();
                h = (h ^ (h >> 13)) * 1274126177;
                float t = (h & 0x00FFFFFF) / (float)0x01000000; // 0~1
                return (t * 2f - 1f) * magnitude; // -magnitude ~ +magnitude
            }
        }
    }
}
