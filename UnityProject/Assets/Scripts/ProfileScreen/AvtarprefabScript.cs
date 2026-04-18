using UnityEngine;
using UnityEngine.UI;
using System;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;

public class AvtarprefabScript : MonoBehaviour
{
    public Image avatarImage;
    public Button selectButton;
    public GameObject selectedBorder;

    public AvatarData Data { get; private set; }

    private Action<AvatarData> onClick;

    public void Setup(AvatarData data, Action<AvatarData> callback)
    {
        Data = data;
        onClick = callback;

        LoadImage(data.ImageUrl);

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() =>
        {
            if (data.Unlocked)
                onClick?.Invoke(data);
            else
                Debug.Log("Locked Avatar");
        });
    }

    public void SetSelected(bool value)
    {
        if (selectedBorder != null)
            selectedBorder.SetActive(value);
    }

    async void LoadImage(string url)
    {
        var tex = await Download(url);
        if (tex != null)
        {
            avatarImage.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                Vector2.one * 0.5f
            );
        }
    }

    async UniTask<Texture2D> Download(string url)
    {
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            await req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                return UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            }
        }
        return null;
    }
}