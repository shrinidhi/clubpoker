using System;
using UnityEngine;
using UnityEngine.UI;

public class PosterImageShowPrefab : MonoBehaviour
{
    public Image posterImage;

    private Texture2D loadedTexture;
    private Sprite loadedSprite;

    public void Setup(string dataUri)
    {
        ClearImage();

        if (posterImage == null) return;

        if (string.IsNullOrEmpty(dataUri))
        {
            posterImage.enabled = false;
            return;
        }

        try
        {
            string base64 = dataUri;

            int commaIndex = dataUri.IndexOf(',');

            if (commaIndex >= 0) base64 = dataUri.Substring(commaIndex + 1);

            byte[] bytes = Convert.FromBase64String(base64);

            loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            if (!loadedTexture.LoadImage(bytes))
            {
                ClearImage();
                posterImage.enabled = false;
                return;
            }

            loadedSprite = Sprite.Create(
                loadedTexture,
                new Rect(0, 0, loadedTexture.width, loadedTexture.height),
                new Vector2(0.5f, 0.5f)
            );

            posterImage.sprite = loadedSprite;
            posterImage.color = Color.white;
            posterImage.enabled = true;
            posterImage.preserveAspect = true;
        }
        catch (Exception e)
        {
            Debug.LogError("[PosterImageShowPrefab] " + e.Message);

            if (posterImage != null) posterImage.enabled = false;

            ClearImage();
        }
    }

    private void OnDestroy()
    {
        ClearImage();
    }

    private void ClearImage()
    {
        if (posterImage != null) posterImage.sprite = null;

        if (loadedSprite != null)
        {
            Destroy(loadedSprite);
            loadedSprite = null;
        }

        if (loadedTexture != null)
        {
            Destroy(loadedTexture);
            loadedTexture = null;
        }
    }
}