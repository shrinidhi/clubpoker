using System.Text;

namespace ClubPoker.Theme
{
    /// <summary>
    /// Normalizes card identifiers into a single canonical form: RANK + SUIT letter.
    /// Accepts "A♠", "as", "10H", "Th", "AS" → "AS", "TH".
    /// Every deck lookup goes through this so sprite naming stays independent of
    /// whatever format the server or an old inspector list happens to use.
    /// </summary>
    public static class CardKey
    {
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            StringBuilder sb = new StringBuilder(2);

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];

                switch (c)
                {
                    case ' ':
                    case '_':
                    case '-':
                        continue;

                    case '♠': sb.Append('S'); continue;
                    case '♥': sb.Append('H'); continue;
                    case '♦': sb.Append('D'); continue;
                    case '♣': sb.Append('C'); continue;
                }

                // "10" → "T"
                if (c == '1' && i + 1 < raw.Length && raw[i + 1] == '0')
                {
                    sb.Append('T');
                    i++;
                    continue;
                }

                sb.Append(char.ToUpperInvariant(c));
            }

            return sb.ToString();
        }
    }
}
