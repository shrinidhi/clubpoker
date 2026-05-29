using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;

public class ClubTablePrefabScript : MonoBehaviour
{
    public Text VariantName;
    public Text SB_BB_Text;
    public Text PlayerSeat_Text;

    private ClubTableData tableData;

    public void Setup(ClubTableData data)
    {
        tableData = data;

        VariantName.text = data.Variant;
        SB_BB_Text.text = data.SmallBlind + "/" + data.BigBlind;
        PlayerSeat_Text.text = data.PlayerCount + "/" + data.MaxSeats;
    }
}