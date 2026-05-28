using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

public class AddChipsModalScript : MonoBehaviour
{
    [Header("Input")]
    public TMP_InputField Amount_InputField;

    [Header("Footer")]
    public Button Confirm_Button;
    public Button Cancel_Button;

    [Header("Parent")]
    public TradeViewScript TradeView;

    private void Start()
    {
        Confirm_Button.onClick.AddListener(OnConfirmTap);
        Cancel_Button.onClick.AddListener(OnCancelTap);
    }

    public void Show()
    {
        Amount_InputField.text = "";
        gameObject.SetActive(true);
    }

    private void OnConfirmTap()
    {
        if (!long.TryParse(Amount_InputField.text, out long amount) || amount <= 0) return;
        Confirm_Button.interactable = false;
        AddChips(amount).Forget();
    }

    private async UniTaskVoid AddChips(long amount)
    {
        try
        {
            var res = await ClubChipManager.Instance.AddChipsAsync(ClubContext.ClubId, amount);

            if (res != null && res.Added)
            {
                // update pool total directly from response, then sync full summary
                ClubContext.UpdatePoolChips(res.NewPoolTotal, ClubContext.MembersChips, ClubContext.AgentsCredit);
                await ClubChipManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);

                gameObject.SetActive(false);

                if (TradeView != null)
                {
                    TradeView.RefreshStatsBar();
                    TradeView.ShowAddChipsSuccess(res.Amount);
                    TradeView.ReloadAfterTrade();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AddChipsModalScript] error: {e.Message}");
        }
        finally
        {
            Confirm_Button.interactable = true;
        }
    }

    private void OnCancelTap()
    {
        gameObject.SetActive(false);
    }
}
