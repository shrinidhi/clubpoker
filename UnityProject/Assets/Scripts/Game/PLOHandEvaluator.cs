using System.Collections.Generic;
using System.Linq;

namespace ClubPoker.Game
{
    // Port of HandEvaluator.js + HandComparator.js with PLO enforcement.
    // PLO rule: exactly 2 hole cards + exactly 3 community cards = best 5-card hand.
    // Returns the 2 hole card strings that produced the best result.
    public static class PLOHandEvaluator
    {
        // ── Hand rank constants (higher = better) ────────────────────────────
        const int HIGH_CARD       = 1;
        const int ONE_PAIR        = 2;
        const int TWO_PAIR        = 3;
        const int THREE_OF_A_KIND = 4;
        const int STRAIGHT        = 5;
        const int FLUSH           = 6;
        const int FULL_HOUSE      = 7;
        const int FOUR_OF_A_KIND  = 8;
        const int STRAIGHT_FLUSH  = 9;
        const int ROYAL_FLUSH     = 10;

        // ── Card ─────────────────────────────────────────────────────────────
        class Card
        {
            public string Raw;
            public int    NumericValue;
            public char   Suit;
        }

        // ── HandResult ───────────────────────────────────────────────────────
        class HandResult
        {
            public int         Rank;
            public string      Name;
            public List<Card>  PrimaryCards;
            public List<int>   TieValues;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// PLO4 / PLO6: returns the 2 hole card strings that form the best hand.
        /// Requires at least 3 community cards (flop or later).
        /// </summary>
        public static List<string> GetBestHoleCards(List<string> holeCards, List<string> communityCards)
        {
            if (holeCards == null || communityCards == null || communityCards.Count < 3)
                return new List<string>();

            List<Card> hole      = holeCards.Select(Parse).ToList();
            List<Card> community = communityCards.Select(Parse).ToList();

            List<List<Card>> holePairs        = Combinations(hole, 2);
            List<List<Card>> communityTriples = Combinations(community, 3);

            HandResult   best         = null;
            List<Card>   bestHolePair = null;

            foreach (var holePair in holePairs)
            {
                foreach (var commTriple in communityTriples)
                {
                    List<Card>  five  = holePair.Concat(commTriple).ToList();
                    HandResult  score = Evaluate(five);

                    if (best == null || Compare(score, best) > 0)
                    {
                        best         = score;
                        bestHolePair = holePair;
                    }
                }
            }

            return bestHolePair == null
                ? new List<string>()
                : bestHolePair.Select(c => c.Raw).ToList();
        }

        /// <summary>
        /// PLO4 / PLO6: returns all 5 cards (2 hole + 3 community) that form the best hand.
        /// Use this for round-end highlighting so community cards are also highlighted.
        /// </summary>
        public static List<string> GetBestFiveCards(List<string> holeCards, List<string> communityCards)
        {
            if (holeCards == null || communityCards == null || communityCards.Count < 3)
                return new List<string>();

            List<Card> hole      = holeCards.Select(Parse).ToList();
            List<Card> community = communityCards.Select(Parse).ToList();

            List<List<Card>> holePairs        = Combinations(hole, 2);
            List<List<Card>> communityTriples = Combinations(community, 3);

            HandResult   best            = null;
            List<Card>   bestHolePair    = null;
            List<Card>   bestCommTriple  = null;

            foreach (var holePair in holePairs)
            {
                foreach (var commTriple in communityTriples)
                {
                    List<Card>  five  = holePair.Concat(commTriple).ToList();
                    HandResult  score = Evaluate(five);

                    if (best == null || Compare(score, best) > 0)
                    {
                        best            = score;
                        bestHolePair    = holePair;
                        bestCommTriple  = commTriple;
                    }
                }
            }

            if (bestHolePair == null) return new List<string>();
            return bestHolePair.Select(c => c.Raw)
                .Concat(bestCommTriple.Select(c => c.Raw))
                .ToList();
        }

        // ── Comparator ───────────────────────────────────────────────────────

        static int Compare(HandResult a, HandResult b)
        {
            if (a.Rank != b.Rank)
                return a.Rank.CompareTo(b.Rank);

            int len = System.Math.Max(a.TieValues.Count, b.TieValues.Count);
            for (int i = 0; i < len; i++)
            {
                int av = i < a.TieValues.Count ? a.TieValues[i] : 0;
                int bv = i < b.TieValues.Count ? b.TieValues[i] : 0;
                if (av != bv) return av.CompareTo(bv);
            }

            return 0;
        }

        // ── Evaluator ────────────────────────────────────────────────────────

        static HandResult Evaluate(List<Card> cards)
        {
            List<Card> sorted = cards.OrderByDescending(c => c.NumericValue).ToList();

            bool flush = sorted.All(c => c.Suit == sorted[0].Suit);
            var (isStraight, straightHigh, aceLow) = DetectStraight(sorted);

            var freqMap = sorted
                .GroupBy(c => c.NumericValue)
                .ToDictionary(g => g.Key, g => g.ToList());

            return
                RoyalFlush(sorted, flush, isStraight)                          ??
                StraightFlush(sorted, flush, isStraight, straightHigh, aceLow) ??
                FourOfAKind(sorted, freqMap)                                    ??
                FullHouse(sorted, freqMap)                                      ??
                Flush(sorted, flush)                                            ??
                Straight(sorted, isStraight, straightHigh)                     ??
                ThreeOfAKind(sorted, freqMap)                                   ??
                TwoPair(sorted, freqMap)                                        ??
                OnePair(sorted, freqMap)                                        ??
                HighCard(sorted);
        }

        // ── Hand detectors ───────────────────────────────────────────────────

        static HandResult RoyalFlush(List<Card> s, bool flush, bool straight)
        {
            if (!flush || !straight) return null;
            if (s[0].NumericValue != 14 || s[4].NumericValue != 10) return null;
            return new HandResult
            {
                Rank = ROYAL_FLUSH, Name = "Royal Flush",
                PrimaryCards = s,
                TieValues = new List<int> { 14 }
            };
        }

        static HandResult StraightFlush(List<Card> s, bool flush, bool straight, int high, bool aceLow)
        {
            if (!flush || !straight) return null;
            if (s[0].NumericValue == 14 && !aceLow) return null; // royal handled above
            return new HandResult
            {
                Rank = STRAIGHT_FLUSH, Name = "Straight Flush",
                PrimaryCards = s,
                TieValues = new List<int> { high }
            };
        }

        static HandResult FourOfAKind(List<Card> s, Dictionary<int, List<Card>> freq)
        {
            var quads = Groups(freq, 4);
            if (quads.Count == 0) return null;
            var kicker = s.Where(c => c.NumericValue != quads[0][0].NumericValue).ToList();
            return new HandResult
            {
                Rank = FOUR_OF_A_KIND, Name = "Four of a Kind",
                PrimaryCards = quads[0],
                TieValues = new List<int> { quads[0][0].NumericValue }
                    .Concat(kicker.Select(c => c.NumericValue)).ToList()
            };
        }

        static HandResult FullHouse(List<Card> s, Dictionary<int, List<Card>> freq)
        {
            var trips = Groups(freq, 3);
            var pairs = Groups(freq, 2);
            if (trips.Count == 0 || pairs.Count == 0) return null;
            return new HandResult
            {
                Rank = FULL_HOUSE, Name = "Full House",
                PrimaryCards = trips[0].Concat(pairs[0]).ToList(),
                TieValues = new List<int> { trips[0][0].NumericValue, pairs[0][0].NumericValue }
            };
        }

        static HandResult Flush(List<Card> s, bool flush)
        {
            if (!flush) return null;
            return new HandResult
            {
                Rank = FLUSH, Name = "Flush",
                PrimaryCards = s,
                TieValues = s.Select(c => c.NumericValue).ToList()
            };
        }

        static HandResult Straight(List<Card> s, bool isStraight, int high)
        {
            if (!isStraight) return null;
            return new HandResult
            {
                Rank = STRAIGHT, Name = "Straight",
                PrimaryCards = s,
                TieValues = new List<int> { high }
            };
        }

        static HandResult ThreeOfAKind(List<Card> s, Dictionary<int, List<Card>> freq)
        {
            var trips = Groups(freq, 3);
            if (trips.Count == 0) return null;
            var kickers = s.Where(c => c.NumericValue != trips[0][0].NumericValue).Take(2).ToList();
            return new HandResult
            {
                Rank = THREE_OF_A_KIND, Name = "Three of a Kind",
                PrimaryCards = trips[0],
                TieValues = new List<int> { trips[0][0].NumericValue }
                    .Concat(kickers.Select(c => c.NumericValue)).ToList()
            };
        }

        static HandResult TwoPair(List<Card> s, Dictionary<int, List<Card>> freq)
        {
            var pairs = Groups(freq, 2);
            if (pairs.Count < 2) return null;
            var kicker = s
                .Where(c => c.NumericValue != pairs[0][0].NumericValue && c.NumericValue != pairs[1][0].NumericValue)
                .Take(1).ToList();
            return new HandResult
            {
                Rank = TWO_PAIR, Name = "Two Pair",
                PrimaryCards = pairs[0].Concat(pairs[1]).ToList(),
                TieValues = new List<int> { pairs[0][0].NumericValue, pairs[1][0].NumericValue }
                    .Concat(kicker.Select(c => c.NumericValue)).ToList()
            };
        }

        static HandResult OnePair(List<Card> s, Dictionary<int, List<Card>> freq)
        {
            var pairs = Groups(freq, 2);
            if (pairs.Count == 0) return null;
            var kickers = s.Where(c => c.NumericValue != pairs[0][0].NumericValue).Take(3).ToList();
            return new HandResult
            {
                Rank = ONE_PAIR, Name = "One Pair",
                PrimaryCards = pairs[0],
                TieValues = new List<int> { pairs[0][0].NumericValue }
                    .Concat(kickers.Select(c => c.NumericValue)).ToList()
            };
        }

        static HandResult HighCard(List<Card> s)
        {
            return new HandResult
            {
                Rank = HIGH_CARD, Name = "High Card",
                PrimaryCards = new List<Card> { s[0] },
                TieValues = s.Select(c => c.NumericValue).ToList()
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static (bool isStraight, int high, bool aceLow) DetectStraight(List<Card> sorted)
        {
            var vals = sorted.Select(c => c.NumericValue).ToList();
            bool normal = true;
            for (int i = 1; i < vals.Count; i++)
                if (vals[i - 1] - vals[i] != 1) { normal = false; break; }
            if (normal) return (true, vals[0], false);

            // Ace-low wheel: A 2 3 4 5
            if (vals[0] == 14 && vals[1] == 5 && vals[2] == 4 && vals[3] == 3 && vals[4] == 2)
                return (true, 5, true);

            return (false, 0, false);
        }

        static List<List<Card>> Groups(Dictionary<int, List<Card>> freq, int size)
        {
            return freq
                .Where(kv => kv.Value.Count == size)
                .OrderByDescending(kv => kv.Key)
                .Select(kv => kv.Value)
                .ToList();
        }

        static List<List<Card>> Combinations(List<Card> cards, int k)
        {
            var result = new List<List<Card>>();
            Pick(cards, k, 0, new List<Card>(), result);
            return result;
        }

        static void Pick(List<Card> cards, int k, int start, List<Card> current, List<List<Card>> result)
        {
            if (current.Count == k) { result.Add(new List<Card>(current)); return; }
            for (int i = start; i < cards.Count; i++)
            {
                current.Add(cards[i]);
                Pick(cards, k, i + 1, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }

        static Card Parse(string card)
        {
            string rankText = card.Substring(0, card.Length - 1);
            char suit = char.ToUpper(card[card.Length - 1]);

            int rank;
            switch (rankText.ToUpper())
            {
                case "A": rank = 14; break;
                case "K": rank = 13; break;
                case "Q": rank = 12; break;
                case "J": rank = 11;  break;
                case "T": rank = 10;  break;
                default:  rank = int.Parse(rankText); break;
            }

            return new Card { Raw = card, NumericValue = rank, Suit = suit };
        }
    }
}
