using NUnit.Framework;
using UnityEngine;

namespace Meokgoeeum.Tests
{
    /// <summary>
    /// GameState / CubeFaceData 회귀 테스트. CubeFaceData는 ScriptableObject 에셋이라 플레이가
    /// 끝나도 값이 영구로 남는데, GameState.Awake가 매 세션 시작마다 런타임 상태를 리셋해줘야
    /// 이전 플레이의 방문 기록이 새 세션에 새어 들어가지 않습니다.
    /// </summary>
    public class GameStateTests
    {
        private GameObject go;
        private GameState state;
        private CubeFaceData[] faces;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestGameState");
            state = go.AddComponent<GameState>();

            faces = new CubeFaceData[6];
            for (int i = 0; i < faces.Length; i++)
            {
                faces[i] = ScriptableObject.CreateInstance<CubeFaceData>();
                faces[i].faceIndex = i;
            }
            state.faceData = faces;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            foreach (var f in faces)
                if (f != null) Object.DestroyImmediate(f);
        }

        [Test]
        public void GetFaceData_ValidIndex_ReturnsMatchingFace()
        {
            TestUtil.InvokeAwake(state);

            Assert.AreEqual(faces[3], state.GetFaceData(3));
        }

        [Test]
        public void GetFaceData_NegativeIndex_ReturnsNull()
        {
            TestUtil.InvokeAwake(state);

            Assert.IsNull(state.GetFaceData(-1));
        }

        [Test]
        public void GetFaceData_OutOfRangeIndex_ReturnsNull()
        {
            TestUtil.InvokeAwake(state);

            Assert.IsNull(state.GetFaceData(6)); // 유효 인덱스는 0~5
        }

        [Test]
        public void Awake_ResetsEveryFaceRuntimeState()
        {
            // 지난 플레이의 흔적을 흉내냄 — ScriptableObject 에셋이라 이 값이 실제로 남아있을 수 있음.
            foreach (var f in faces)
            {
                f.isVisited = true;
                f.visitCount = 3;
            }

            TestUtil.InvokeAwake(state);

            foreach (var f in faces)
            {
                Assert.IsFalse(f.isVisited, "Awake 이후에는 지난 세션의 방문 기록이 남아있으면 안 됩니다.");
                Assert.AreEqual(0, f.visitCount);
            }
        }
    }

    /// <summary>CubeFaceData 자체의 리셋 동작 회귀 테스트.</summary>
    public class CubeFaceDataTests
    {
        [Test]
        public void ResetRuntimeState_ClearsVisitedAndCount()
        {
            var face = ScriptableObject.CreateInstance<CubeFaceData>();
            face.isVisited = true;
            face.visitCount = 5;

            face.ResetRuntimeState();

            Assert.IsFalse(face.isVisited);
            Assert.AreEqual(0, face.visitCount);

            Object.DestroyImmediate(face);
        }
    }
}
