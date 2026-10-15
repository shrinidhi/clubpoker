using ClubPoker.Audio;
using ClubPoker.Core;
using NUnit.Framework;
using UnityEngine;

namespace ClubPoker.Tests
{
    public class AudioPoolTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("AudioPoolTest");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void Prewarms_InitialVoices()
        {
            var pool = new AudioPool(_root.transform, 4, 8);
            Assert.AreEqual(4, pool.Count);
            Assert.AreEqual(4, _root.transform.childCount);
        }

        [Test]
        public void Rent_ReusesFreeVoice_BeforeGrowing()
        {
            var pool = new AudioPool(_root.transform, 2, 4);

            var a = pool.Rent(0f);
            pool.MarkPlaying(a, SoundId.Fold, 0f, 1f, false);

            var b = pool.Rent(0f);
            Assert.AreNotSame(a, b);
            Assert.AreEqual(2, pool.Count);

            // a finishes at t=1 → reused, no growth.
            var c = pool.Rent(1.5f);
            Assert.AreSame(a, c);
            Assert.AreEqual(2, pool.Count);
        }

        [Test]
        public void Rent_GrowsUpToCap_ThenStealsOldestOneShot()
        {
            var pool = new AudioPool(_root.transform, 1, 3);

            var v1 = pool.Rent(0f); pool.MarkPlaying(v1, SoundId.Check, 0.0f, 10f, false);
            var v2 = pool.Rent(0f); pool.MarkPlaying(v2, SoundId.Call,  0.1f, 10f, false);
            var v3 = pool.Rent(0f); pool.MarkPlaying(v3, SoundId.Fold,  0.2f, 10f, false);
            Assert.AreEqual(3, pool.Count);

            var stolen = pool.Rent(0.5f);
            Assert.AreSame(v1, stolen);
            Assert.AreEqual(3, pool.Count);
        }

        [Test]
        public void Rent_NeverStealsLoop()
        {
            var pool = new AudioPool(_root.transform, 2, 2);

            var loop = pool.Rent(0f);  pool.MarkPlaying(loop, SoundId.TimerWarning, 0f, 1f, true);
            var shot = pool.Rent(0f);  pool.MarkPlaying(shot, SoundId.Fold, 1f, 10f, false);

            Assert.AreSame(shot, pool.Rent(2f));
        }

        [Test]
        public void Rent_ReturnsNull_WhenAllVoicesLoop()
        {
            var pool = new AudioPool(_root.transform, 1, 1);
            var loop = pool.Rent(0f);
            pool.MarkPlaying(loop, SoundId.TimerWarning, 0f, 1f, true);

            Assert.IsNull(pool.Rent(100f));
        }

        [Test]
        public void CountActive_And_Stop()
        {
            var pool = new AudioPool(_root.transform, 3, 3);
            pool.MarkPlaying(pool.Rent(0f), SoundId.CardFlip, 0f, 1f, false);
            pool.MarkPlaying(pool.Rent(0f), SoundId.CardFlip, 0f, 1f, false);
            pool.MarkPlaying(pool.Rent(0f), SoundId.Fold, 0f, 1f, false);

            Assert.AreEqual(2, pool.CountActive(SoundId.CardFlip, 0.5f));
            Assert.AreEqual(0, pool.CountActive(SoundId.CardFlip, 2f));

            pool.Stop(SoundId.CardFlip);
            Assert.AreEqual(0, pool.CountActive(SoundId.CardFlip, 0.5f));
            Assert.AreEqual(1, pool.CountActive(SoundId.Fold, 0.5f));
        }
    }

    public class SoundThrottleTests
    {
        [Test]
        public void Blocks_WithinMinInterval()
        {
            var t = new SoundThrottle();
            Assert.IsTrue(t.CanPlay(SoundId.Call, 0.1f, 3, 0, 0f));
            t.Record(SoundId.Call, 0f);

            Assert.IsFalse(t.CanPlay(SoundId.Call, 0.1f, 3, 0, 0.05f));
            Assert.IsTrue(t.CanPlay(SoundId.Call, 0.1f, 3, 0, 0.1f));
        }

        [Test]
        public void Ids_AreIndependent()
        {
            var t = new SoundThrottle();
            t.Record(SoundId.Call, 0f);
            Assert.IsTrue(t.CanPlay(SoundId.Fold, 0.1f, 3, 0, 0.01f));
        }

        [Test]
        public void Blocks_AtMaxConcurrent()
        {
            var t = new SoundThrottle();
            Assert.IsFalse(t.CanPlay(SoundId.CardFlip, 0f, 2, 2, 0f));
            Assert.IsTrue(t.CanPlay(SoundId.CardFlip, 0f, 2, 1, 0f));
        }
    }

    public class GameAudioTests
    {
        [Test]
        public void Play_SwallowsListenerExceptions()
        {
            System.Action<SoundId> boom = _ => throw new System.Exception("listener failed");
            GameAudio.OnPlay += boom;
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, "Exception: listener failed");
                Assert.DoesNotThrow(() => GameAudio.Play(SoundId.Fold));
            }
            finally
            {
                GameAudio.OnPlay -= boom;
            }
        }

        [Test]
        public void SoundLibrary_Lookup_SkipsNoneAndDuplicates()
        {
            var lib = ScriptableObject.CreateInstance<SoundLibrary>();
            lib.entries.Add(new SoundEntry { id = SoundId.None });
            lib.entries.Add(new SoundEntry { id = SoundId.Fold, volume = 0.5f });
            lib.entries.Add(new SoundEntry { id = SoundId.Fold, volume = 0.9f });

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("Duplicate SoundLibrary entry"));

            var lookup = lib.BuildLookup();
            Assert.AreEqual(1, lookup.Count);
            Assert.AreEqual(0.5f, lookup[SoundId.Fold].volume);

            Object.DestroyImmediate(lib);
        }
    }
}
