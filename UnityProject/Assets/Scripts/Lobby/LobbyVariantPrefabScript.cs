using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Lobby
{
    public class LobbyVariantPrefabScript : MonoBehaviour
    {
        public Button Variant_Button;
        public GameObject Lock_BG;
        public Image Variant_Image;

        private LobbyVariantData variantData;
        private LobbyController lobbyController;

        public void Setup(
            LobbyVariantData data,
            Sprite variantSprite,
            LobbyController controller)
        {
            variantData = data;
            lobbyController = controller;

            if (Variant_Image != null)
                Variant_Image.sprite = variantSprite;

            if (Lock_BG != null)
                Lock_BG.SetActive(data.IsLocked);

            if (Variant_Button != null)
            {
                Variant_Button.onClick.RemoveAllListeners();
                Variant_Button.onClick.AddListener(OnClickVariant);
                Variant_Button.interactable = !data.IsLocked;
            }
        }

        private void OnClickVariant()
        {
            if (variantData == null || variantData.IsLocked)
                return;

            lobbyController.OnVariantSelected(variantData);
        }
    }
}