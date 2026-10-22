using ClubPoker.Core;        // Loc — expected messages come from the language files
using ClubPoker.UI;          // the code under test
using NUnit.Framework;       // [Test], [TestCase], Assert

namespace ClubPoker.Tests
{
    /// <summary>
    /// Tests for NicknameRules.Validate — returns null when the nickname is OK,
    /// or an error message when it isn't.
    ///
    /// Rules being checked:
    ///   - 3 to 20 characters (spaces count)
    ///   - letters, numbers, underscore only
    ///   - single spaces between words (no double spaces, none at the ends)
    ///
    /// Run: Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
    /// Reference example for writing tests in this project — see README §7.
    /// </summary>
    public class NicknameRulesTests
    {
        // ── 1. The simplest form: one [Test], Arrange → Act → Assert ──────────

        [Test]
        public void Valid_Nickname_ReturnsNull()
        {
            // Arrange: prepare the input
            string nickname = "Arun_99";

            // Act: run the code being tested
            string error = NicknameRules.Validate(nickname);

            // Assert: check the result (null = no error = valid)
            Assert.IsNull(error);
        }

        // ── 2. Checking the exact message the player will see ─────────────────
        // Compared with Loc.Get, not fixed English: the text depends on the
        // language, the key doesn't.

        [Test]
        public void Empty_ShowsEmptyMessage()
        {
            Assert.AreEqual(Loc.Get("nickname_empty"), NicknameRules.Validate(""));
        }

        [Test]
        public void Null_ShowsEmptyMessage()
        {
            // Not just "", null too — a field that was never filled in.
            Assert.AreEqual(Loc.Get("nickname_empty"), NicknameRules.Validate(null));
        }

        // ── 3. Many inputs, one test: [TestCase] ──────────────────────────────
        // Each [TestCase] line shows up as its own test in the Test Runner.
        // Adding a case = adding one line.

        [TestCase("Arun")]                    // normal
        [TestCase("Arun 99")]                 // single space between words
        [TestCase("Big_Blind King")]          // underscore + space
        [TestCase("Ace High Club")]           // several words
        [TestCase("12345")]                   // numbers only
        public void Allowed(string nickname)
        {
            Assert.IsNull(NicknameRules.Validate(nickname));
        }

        [TestCase("Arun!")]                   // symbol
        [TestCase("Arun-99")]                 // dash
        [TestCase("Arun.99")]                 // dot
        [TestCase("Arun  99")]                // double space
        [TestCase(" Arun")]                   // leading space (callers should trim first)
        [TestCase("Arun ")]                   // trailing space
        [TestCase("Arun\t99")]                // tab is not a space
        [TestCase("Arün")]                    // accented letter
        public void Rejected_BadCharacters(string nickname)
        {
            Assert.IsNotNull(NicknameRules.Validate(nickname));
        }

        // ── 4. Edges: exactly on the limit and one past it ────────────────────
        // Bugs live here (< vs <=), so always test both sides of every limit.

        [TestCase("abc")]                     // 3  = MinLength  → allowed
        [TestCase("abcdefghijklmnopqrst")]    // 20 = MaxLength  → allowed
        [TestCase("Arun Arun Arun Arun1")]    // 20 incl. spaces → allowed
        public void Length_AtLimit_IsAllowed(string nickname)
        {
            Assert.IsNull(NicknameRules.Validate(nickname));
        }

        [TestCase("ab")]                      // 2  = one under min
        [TestCase("abcdefghijklmnopqrstu")]   // 21 = one over max
        [TestCase("Arun Arun Arun Arun12")]   // 21 because spaces count
        public void Length_PastLimit_IsRejected(string nickname)
        {
            Assert.IsNotNull(NicknameRules.Validate(nickname));
        }

        // ── 5. Tie the test to the constants, not to magic numbers ────────────
        // If someone changes MaxLength to 25, this still tests the real limit.

        [Test]
        public void Length_UsesConstants()
        {
            string atMax   = new string('a', NicknameRules.MaxLength);
            string overMax = new string('a', NicknameRules.MaxLength + 1);
            string underMin = new string('a', NicknameRules.MinLength - 1);

            Assert.IsNull(NicknameRules.Validate(atMax));
            Assert.IsNotNull(NicknameRules.Validate(overMax));
            Assert.IsNotNull(NicknameRules.Validate(underMin));
        }

        // ── 6. When a check fails, which message wins? ────────────────────────
        // "a!" is both too short (2 chars) AND has a symbol. Validate checks length
        // first, so the player sees the length message. This test pins that order.

        [Test]
        public void TooShort_IsReportedBeforeBadCharacters()
        {
            Assert.AreEqual(Loc.Get("nickname_too_short", ("min", NicknameRules.MinLength)),
                            NicknameRules.Validate("a!"));
        }
    }
}
