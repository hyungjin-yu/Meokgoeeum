using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// LockOnCameraInstaller (에디터 전용)
    /// [[02 플레이어 시스템]]/[[16 조작 설계]] "Lock-On 시 CinemachineTargetGroup" 스펙대로,
    /// 락온 중 플레이어+타겟을 함께 잡는 카메라를 씬에 배치합니다. [[LockOnController]](간소화
    /// 버전, 카메라는 안 건드림)에 이어 카메라 프레이밍만 추가하는 2단계 작업입니다.
    ///
    /// 기존 카메라(orbit 반경/높이/FOV 등 이미 튜닝된 값)를 손으로 다시 만드는 대신
    /// `Object.Instantiate`로 통째로 복제해서 만듭니다 — Cinemachine 컴포넌트들의 내부 구조체
    /// 필드를 하나하나 손으로 옮기다 실수하는 것보다 훨씬 안전합니다. 복제본에서 바뀌는 건
    /// 딱 세 가지: (1) 마우스 입력으로 도는 CinemachineInputAxisController를 꺼서 락온 중엔
    /// 카메라가 임의로 안 돌게 함, (2) Follow 타겟을 Player 대신 새로 만든 TargetGroup으로,
    /// (3) Priority를 기본 카메라보다 낮게(-10) 둬서 평소엔 안 뜨다가 LockOnController가 락온
    /// 시 Priority만 올림.
    ///
    /// ⚠️ 2026-08-31 실측: 처음엔 평소에 SetActive(false)로 꺼뒀는데, `GameObject.Find`가
    /// 비활성 오브젝트를 못 찾는다는 걸 몰라서(런타임/에디터 스크립트 둘 다 마찬가지) 재실행할
    /// 때마다 "기존 걸 못 찾음 → 새로 또 만듦"이 반복돼 중복 오브젝트가 쌓이는 버그가 있었음.
    /// 그래서 (a) 오브젝트는 항상 활성 상태로 유지(Priority로만 화면 노출 제어), (b) 이 설치
    /// 스크립트도 "찾아서 재사용" 대신 매번 파괴 후 재생성하는 방식으로 바꿔서, Find가 비활성
    /// 오브젝트를 못 보는 것과 무관하게 항상 정확히 하나만 존재하도록 만들었습니다
    /// ([[Floor1CorridorBuilder]]가 자기 생성물을 매번 지우고 다시 만드는 것과 같은 패턴).
    ///
    /// ⚠️ 카메라 프레이밍(궤도 반경/높이/그룹 프레이밍 정도)은 실제로 플레이하면서 눈으로 봐야
    /// 제대로 튜닝되는 부분이라, 지금 값은 "기존 카메라를 그대로 복제한 첫 시도"일 뿐입니다.
    /// 실측 후 `LockOnCamera`의 `CinemachineOrbitalFollow`를 인스펙터에서 직접 조정하세요.
    /// </summary>
    public static class LockOnCameraInstaller
    {
        private const string ScenePath = "Assets/Scenes/SC_Game.unity";
        private const string TargetGroupName = "LockOnTargetGroup";
        private const string LockOnCameraName = "LockOnCamera";

        [MenuItem("MG/LockOn 카메라 설치 (TargetGroup + LockOnCamera)")]
        public static void InstallFromMenu() => Install();

        public static void InstallFromCLI() => Install();

        private static void Install()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[LockOnCameraInstaller] Player를 씬에서 못 찾았습니다.");
                return;
            }

            // 활성/비활성 여부와 무관하게 이전에 만든 걸 전부 지우고 새로 만듭니다(위 클래스 주석 참고).
            DestroyAllNamed(LockOnCameraName);
            DestroyAllNamed(TargetGroupName);

            // --- 1. LockOnTargetGroup ---
            var targetGroupObj = new GameObject(TargetGroupName);
            SceneManager.MoveGameObjectToScene(targetGroupObj, scene);
            CinemachineTargetGroup group = targetGroupObj.AddComponent<CinemachineTargetGroup>();
            group.AddMember(player.transform, 1f, 1f); // 플레이어는 항상 그룹 멤버 — 타겟은 런타임에 LockOnController가 추가/제거

            // --- 2. LockOnCamera (기존 CinemachineCamera를 복제) ---
            GameObject sourceCamObj = GameObject.Find("CinemachineCamera");
            if (sourceCamObj == null)
            {
                Debug.LogError("[LockOnCameraInstaller] 기존 CinemachineCamera를 못 찾아 LockOnCamera를 만들 수 없습니다.");
                return;
            }

            GameObject lockOnCamObj = Object.Instantiate(sourceCamObj);
            lockOnCamObj.name = LockOnCameraName;
            SceneManager.MoveGameObjectToScene(lockOnCamObj, scene);

            // 2026-08-31 실측: 마우스 입력을 껐다(벽에 낀 채로 못 빠져나옴) → 켰다(그럼 마우스로
            // 다 덮어써서 "고정"이 사실상 무의미) 두 번 시행착오. 진짜 문제는 마우스 On/Off가
            // 아니라 카메라 충돌 회피가 아예 없었다는 것 — 그래서 CinemachineDeoccluder를 추가해
            // 근본 원인을 고치고, 기획서 스펙대로 "적 중심으로 카메라 고정"을 다시 켬(마우스 입력
            // 끔). 이제 벽 쪽으로 궤도가 잡혀도 Deoccluder가 카메라를 자동으로 벽 앞으로 당겨서
            // 시야를 확보합니다.
            var axisController = lockOnCamObj.GetComponent<CinemachineInputAxisController>();
            if (axisController != null) axisController.enabled = false;

            if (lockOnCamObj.GetComponent<CinemachineDeoccluder>() == null)
                lockOnCamObj.AddComponent<CinemachineDeoccluder>();

            var cam = lockOnCamObj.GetComponent<CinemachineCamera>();
            if (cam != null)
            {
                cam.Follow = targetGroupObj.transform;
                cam.Priority.Value = -10; // 기본 카메라(우선순위 0)보다 항상 아래 — 평소엔 안 뜸
            }
            else
            {
                Debug.LogWarning("[LockOnCameraInstaller] LockOnCamera에서 CinemachineCamera 컴포넌트를 못 찾았습니다.");
            }

            // GameObject는 항상 활성 상태로 둡니다 — 비활성이면 GameObject.Find로 못 찾습니다(위 주석 참고).
            lockOnCamObj.SetActive(true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[LockOnCameraInstaller] LockOnTargetGroup + LockOnCamera 재생성 완료(Follow=LockOnTargetGroup, Priority=-10).");
        }

        /// <summary>주어진 이름의 오브젝트를 활성/비활성 상관없이 전부 찾아 파괴합니다. `GameObject.Find`는 비활성 오브젝트를 못 찾아서 안 씁니다.</summary>
        private static void DestroyAllNamed(string name)
        {
            var roots = new List<GameObject>();
            SceneManager.GetActiveScene().GetRootGameObjects(roots);

            foreach (var root in roots)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t.gameObject.name == name)
                        Object.DestroyImmediate(t.gameObject);
                }
            }
        }
    }
}
