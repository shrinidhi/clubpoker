using ClubPoker.Auth;
using ClubPoker.Game;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TopUpPanel : MonoBehaviour
{
    public Button CloseButton;
    public Button Ok_Button;

    public InputField AmountInputField;

    public TextMeshProUGUI ErrorMsg;

    void Start()
    {
        CloseButton.onClick.AddListener(CloseButtononTap);
        Ok_Button.onClick.AddListener(Ok_ButtonOnTap);
    }

    void CloseButtononTap()
    {
        gameObject.SetActive(false);
    }

    void Ok_ButtonOnTap()
    {
        ErrorMsg.text = "";

        if (string.IsNullOrEmpty(AmountInputField.text))
        {
            ErrorMsg.text = "Enter Amount";
            return;
        }

        int amount;

        if (!int.TryParse(AmountInputField.text, out amount))
        {
            ErrorMsg.text = "Invalid Amount";
            return;
        }

        BuyIn(amount).Forget();
    }

    async UniTaskVoid BuyIn(int amount)
    {
        Ok_Button.interactable = false;
        ErrorMsg.text = "";

        try
        {
            if (GameStateManager.Instance == null)
            {
                ErrorMsg.text = "GameStateManager not found.";
                return;
            }

            string tableId = GameStateManager.Instance.TableId;

            if (string.IsNullOrEmpty(tableId))
            {
                ErrorMsg.text = "Invalid Table Id.";
                return;
            }

            BuyInResponse response =
                await AuthManager.Instance.BuyInAsync(
                    tableId,
                    amount);

           
            
            Debug.Log("Table Chips : " + response.Data?.TableChips);
            Debug.Log("Wallet Chips : " + response.Data?.WalletChips);

            // UI Update
            // Example:
            // GameStateManager.Instance.TableChips = response.Data.TableChips;
            // WalletManager.Instance.SetBalance(response.Data.WalletChips);

            gameObject.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            ErrorMsg.text = "Something went wrong.";
        }
        finally
        {
            Ok_Button.interactable = true;
        }
    }
}
