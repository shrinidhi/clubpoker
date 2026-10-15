using System.Collections.Generic;
using ClubPoker.Core;

namespace ClubPoker.Audio
{
    /// <summary>
    /// Per-id rate limit: a minimum gap between plays and a cap on overlapping
    /// instances. Keeps bursts (several players acting, pot + chip events landing
    /// together) from stacking into noise.
    /// </summary>
    public class SoundThrottle
    {
        private readonly Dictionary<SoundId, float> _lastPlay = new Dictionary<SoundId, float>();

        public bool CanPlay(SoundId id, float minInterval, int maxConcurrent, int activeCount, float now)
        {
            if (maxConcurrent > 0 && activeCount >= maxConcurrent)
                return false;

            if (_lastPlay.TryGetValue(id, out float last) && now - last < minInterval)
                return false;

            return true;
        }

        public void Record(SoundId id, float now) => _lastPlay[id] = now;
    }
}
