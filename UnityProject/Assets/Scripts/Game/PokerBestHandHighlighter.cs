using System.Collections.Generic;
using System.Linq;

namespace ClubPoker.Game
{
    public static class PokerBestHandHighlighter
    {
        class Card
        {
            public string Raw;
            public int Rank;
            public char Suit;
        }

        public static List<string> GetHighlightCards(List<string> holeCards, List<string> communityCards)
        {
            if (holeCards == null || communityCards == null)
                return new List<string>();

            List<Card> allCards = new List<Card>();

            foreach (string card in holeCards)
                allCards.Add(Parse(card));

            foreach (string card in communityCards)
                allCards.Add(Parse(card));

            List<List<Card>> combos = GetCombinations(allCards, 5);

            HandScore best = null;

            foreach (var combo in combos)
            {
                HandScore score = Evaluate(combo);

                if (best == null || score.CompareTo(best) > 0)
                    best = score;
            }

            if (best == null)
                return new List<string>();

            return best.Cards
                .Where(x => best.ImportantRanks.Contains(x.Rank))
                .Select(x => x.Raw)
                .ToList();
        }

        static Card Parse(string card)
        {
            string rankText = card.Substring(0, card.Length - 1);
            char suit = card[card.Length - 1];

            int rank;

            switch (rankText)
            {
                case "A": rank = 14; break;
                case "K": rank = 13; break;
                case "Q": rank = 12; break;
                case "J": rank = 11; break;
                default: rank = int.Parse(rankText); break;
            }

            return new Card
            {
                Raw = card,
                Rank = rank,
                Suit = suit
            };
        }

        class HandScore
        {
            public int Category;
            public List<int> TieRanks = new List<int>();
            public List<int> ImportantRanks = new List<int>();
            public List<Card> Cards = new List<Card>();

            public int CompareTo(HandScore other)
            {
                if (Category != other.Category)
                    return Category.CompareTo(other.Category);

                for (int i = 0; i < TieRanks.Count; i++)
                {
                    if (TieRanks[i] != other.TieRanks[i])
                        return TieRanks[i].CompareTo(other.TieRanks[i]);
                }

                return 0;
            }
        }

        static HandScore Evaluate(List<Card> cards)
        {
            cards = cards.OrderByDescending(x => x.Rank).ToList();

            bool flush = cards.All(x => x.Suit == cards[0].Suit);
            int straightHigh = GetStraightHigh(cards);

            var groups = cards
                .GroupBy(x => x.Rank)
                .OrderByDescending(x => x.Count())
                .ThenByDescending(x => x.Key)
                .ToList();

            if (flush && straightHigh > 0)
            {
                return new HandScore
                {
                    Category = 8,
                    TieRanks = new List<int> { straightHigh },
                    ImportantRanks = GetStraightRanks(straightHigh),
                    Cards = cards
                };
            }

            if (groups[0].Count() == 4)
            {
                return new HandScore
                {
                    Category = 7,
                    TieRanks = new List<int> { groups[0].Key, groups[1].Key },
                    ImportantRanks = new List<int> { groups[0].Key },
                    Cards = cards
                };
            }

            if (groups[0].Count() == 3 && groups[1].Count() == 2)
            {
                return new HandScore
                {
                    Category = 6,
                    TieRanks = new List<int> { groups[0].Key, groups[1].Key },
                    ImportantRanks = new List<int> { groups[0].Key, groups[1].Key },
                    Cards = cards
                };
            }

            if (flush)
            {
                return new HandScore
                {
                    Category = 5,
                    TieRanks = cards.Select(x => x.Rank).ToList(),
                    ImportantRanks = cards.Select(x => x.Rank).ToList(),
                    Cards = cards
                };
            }

            if (straightHigh > 0)
            {
                return new HandScore
                {
                    Category = 4,
                    TieRanks = new List<int> { straightHigh },
                    ImportantRanks = GetStraightRanks(straightHigh),
                    Cards = cards
                };
            }

            if (groups[0].Count() == 3)
            {
                int three = groups[0].Key;

                return new HandScore
                {
                    Category = 3,
                    TieRanks = new List<int> { three }
                        .Concat(groups.Where(x => x.Key != three).Select(x => x.Key))
                        .ToList(),
                    ImportantRanks = new List<int> { three },
                    Cards = cards
                };
            }

            if (groups[0].Count() == 2 && groups[1].Count() == 2)
            {
                return new HandScore
                {
                    Category = 2,
                    TieRanks = new List<int> { groups[0].Key, groups[1].Key, groups[2].Key },
                    ImportantRanks = new List<int> { groups[0].Key, groups[1].Key },
                    Cards = cards
                };
            }

            if (groups[0].Count() == 2)
            {
                int pair = groups[0].Key;

                return new HandScore
                {
                    Category = 1,
                    TieRanks = new List<int> { pair }
                        .Concat(groups.Where(x => x.Key != pair).Select(x => x.Key))
                        .ToList(),
                    ImportantRanks = new List<int> { pair },
                    Cards = cards
                };
            }

            return new HandScore
            {
                Category = 0,
                TieRanks = cards.Select(x => x.Rank).ToList(),
                ImportantRanks = cards.Select(x => x.Rank).ToList(),
                Cards = cards
            };
        }

        static int GetStraightHigh(List<Card> cards)
        {
            List<int> ranks = cards.Select(x => x.Rank).Distinct().OrderBy(x => x).ToList();

            if (ranks.Contains(14))
                ranks.Insert(0, 1);

            for (int i = 0; i <= ranks.Count - 5; i++)
            {
                bool straight = true;

                for (int j = 1; j < 5; j++)
                {
                    if (ranks[i + j] != ranks[i] + j)
                    {
                        straight = false;
                        break;
                    }
                }

                if (straight)
                    return ranks[i + 4];
            }

            return 0;
        }

        static List<int> GetStraightRanks(int high)
        {
            if (high == 5)
                return new List<int> { 14, 5, 4, 3, 2 };

            return new List<int>
            {
                high,
                high - 1,
                high - 2,
                high - 3,
                high - 4
            };
        }

        static List<List<Card>> GetCombinations(List<Card> cards, int count)
        {
            List<List<Card>> result = new List<List<Card>>();
            Combine(cards, count, 0, new List<Card>(), result);
            return result;
        }

        static void Combine(List<Card> cards, int count, int index, List<Card> current, List<List<Card>> result)
        {
            if (current.Count == count)
            {
                result.Add(new List<Card>(current));
                return;
            }

            for (int i = index; i < cards.Count; i++)
            {
                current.Add(cards[i]);
                Combine(cards, count, i + 1, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }
    }
}