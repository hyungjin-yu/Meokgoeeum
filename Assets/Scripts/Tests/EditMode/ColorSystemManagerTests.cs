using NUnit.Framework;
using UnityEngine;

namespace Meokgoeeum.Tests
{
    /// <summary>
    /// ColorSystemManager 회귀 테스트. 보유 구슬 5개 상한의 FIFO 교체와, 스킬 소모 후에도
    /// acquisitionOrder가 heldOrbs와 어긋나지 않는지를 중점적으로 검증합니다 — 코드 주석에
    /// 명시된 "유령 항목" 버그 클래스(소모 순서가 꼬이면 나중에 음수가 나는 문제)를 겨냥합니다.
    /// </summary>
    public class ColorSystemManagerTests
    {
        private GameObject go;
        private ColorSystemManager mgr;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestColorSystemManager");
            mgr = go.AddComponent<ColorSystemManager>();
            TestUtil.InvokeAwake(mgr); // EditMode에서는 Awake가 즉시 안 불려서 직접 호출 (TestUtil 주석 참고)
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void AddOrb_IncreasesHeldAndLifetime()
        {
            mgr.AddOrb(OrbColor.Red);

            Assert.AreEqual(1, mgr.GetHeld(OrbColor.Red));
            Assert.AreEqual(1, mgr.GetLifetimeCollected(OrbColor.Red));
        }

        [Test]
        public void AddOrb_LifetimeNeverDecreasesOnConsume()
        {
            mgr.AddOrb(OrbColor.Red);
            mgr.TryConsumeOrb(OrbColor.Red);

            Assert.AreEqual(0, mgr.GetHeld(OrbColor.Red));
            Assert.AreEqual(1, mgr.GetLifetimeCollected(OrbColor.Red), "누적 획득 기록은 소모해도 줄면 안 됩니다.");
        }

        [Test]
        public void AddOrb_BeyondMaxHeld_EvictsOldestByFifo()
        {
            // 서로 다른 색으로 5개를 채운 뒤(상한), 6번째를 추가하면 가장 먼저 넣은 색이 1개 빠져야 함.
            mgr.AddOrb(OrbColor.Red);
            mgr.AddOrb(OrbColor.Blue);
            mgr.AddOrb(OrbColor.Yellow);
            mgr.AddOrb(OrbColor.Green);
            mgr.AddOrb(OrbColor.Purple);
            Assert.AreEqual(5, mgr.TotalHeld());

            mgr.AddOrb(OrbColor.Black);

            Assert.AreEqual(5, mgr.TotalHeld(), "합산 상한(5개)을 넘으면 안 됩니다.");
            Assert.AreEqual(0, mgr.GetHeld(OrbColor.Red), "가장 먼저 넣은 색(Red)이 빠져야 합니다.");
            Assert.AreEqual(1, mgr.GetHeld(OrbColor.Black), "새로 넣은 색은 그대로 보유돼야 합니다.");
        }

        [Test]
        public void TryConsumeOrb_WithNoStock_ReturnsFalseAndDoesNotUnderflow()
        {
            bool result = mgr.TryConsumeOrb(OrbColor.Red);

            Assert.IsFalse(result);
            Assert.AreEqual(0, mgr.GetHeld(OrbColor.Red));
        }

        [Test]
        public void TryConsumeOrb_ReducesHeldAndReturnsTrue()
        {
            mgr.AddOrb(OrbColor.Blue);

            bool result = mgr.TryConsumeOrb(OrbColor.Blue);

            Assert.IsTrue(result);
            Assert.AreEqual(0, mgr.GetHeld(OrbColor.Blue));
        }

        [Test]
        public void MixedAddAndConsume_KeepsAcquisitionOrderConsistent()
        {
            // ColorSystemManager.cs의 RemoveOneFromAcquisitionOrder 주석이 경고하는 "유령 항목" 시나리오:
            // 소모 순서가 heldOrbs와 어긋나면 나중에 FIFO 교체가 이미 없는 항목을 지우려다 음수가 남.
            mgr.AddOrb(OrbColor.Red);
            mgr.AddOrb(OrbColor.Blue);
            mgr.TryConsumeOrb(OrbColor.Red); // 큐 중간(가장 오래된) 항목을 소모
            mgr.AddOrb(OrbColor.Yellow);
            mgr.AddOrb(OrbColor.Green);
            mgr.AddOrb(OrbColor.Purple);
            mgr.AddOrb(OrbColor.Black); // 이 시점에 5개 상한 — 다음 추가 시 FIFO 교체 발생

            Assert.AreEqual(5, mgr.TotalHeld());
            Assert.GreaterOrEqual(mgr.GetHeld(OrbColor.Blue), 0, "소모 순서가 꼬이면 음수가 될 수 있는 지점입니다.");

            mgr.AddOrb(OrbColor.Red); // 6번째 — FIFO로 가장 오래된 것(Blue)이 빠져야 함
            Assert.AreEqual(5, mgr.TotalHeld());
            Assert.AreEqual(0, mgr.GetHeld(OrbColor.Blue));
        }

        [Test]
        public void RestoreState_OverwritesHeldAndLifetimeArrays()
        {
            mgr.AddOrb(OrbColor.Red); // 복원 전 상태 — 덮어써져야 함

            var held = new int[6];
            var lifetime = new int[6];
            held[(int)OrbColor.Blue] = 2;
            lifetime[(int)OrbColor.Blue] = 5;

            mgr.RestoreState(held, lifetime);

            Assert.AreEqual(0, mgr.GetHeld(OrbColor.Red), "복원 전 상태는 남아있으면 안 됩니다.");
            Assert.AreEqual(2, mgr.GetHeld(OrbColor.Blue));
            Assert.AreEqual(5, mgr.GetLifetimeCollected(OrbColor.Blue));
            Assert.AreEqual(2, mgr.TotalHeld());
        }

        [Test]
        public void RestoreState_ThenConsume_WorksNormally()
        {
            // acquisitionOrder는 세이브 데이터에 없어서 색 인덱스 순서로 근사 복원됨(클래스 주석 참고).
            // 복원 직후에도 소모가 정상 동작해야 함.
            var held = new int[6];
            held[(int)OrbColor.Red] = 1;
            mgr.RestoreState(held, new int[6]);

            bool result = mgr.TryConsumeOrb(OrbColor.Red);

            Assert.IsTrue(result);
            Assert.AreEqual(0, mgr.GetHeld(OrbColor.Red));
        }
    }
}
