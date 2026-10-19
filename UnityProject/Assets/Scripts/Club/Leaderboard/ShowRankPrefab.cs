using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class ShowRankPrefab : MonoBehaviour
{
    public Image RankImage;
    public Text Rank_Text;
    public Text Player_Name;
    public Text RewardText;

    public Sprite[] RankSprite = new Sprite[4];

    public void SetData(int rank,ClubLeaderboardStanding player,decimal? reward)
    {
        int spriteIndex = rank <= 3 ? rank - 1 : 3;

        Sprite sprite =
            RankSprite != null &&
            spriteIndex >= 0 &&
            spriteIndex < RankSprite.Length
                ? RankSprite[spriteIndex]
                : null;

        if (RankImage != null)
        {
            RankImage.sprite = sprite;
            RankImage.enabled = sprite != null;
        }

        if (Rank_Text != null)
        {
            Rank_Text.supportRichText = false;
            Rank_Text.text = rank.ToString(
                CultureInfo.InvariantCulture);

            Rank_Text.gameObject.SetActive(rank > 3);
        }

        if (Player_Name != null)
        {
            Player_Name.supportRichText = false;
            Player_Name.text = player != null
                ? player.Username ?? ""
                : "";
        }

        if (RewardText != null)
        {
            RewardText.supportRichText = false;
            RewardText.text = reward.HasValue
                ? reward.Value.ToString(
                    "#,0.##", CultureInfo.InvariantCulture)
                : "";
        }
    }
}