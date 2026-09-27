using UnityEngine;
using UnityEngine.UI;

public class PosterImageDotPrefab : MonoBehaviour
{
    public Image DotImage;

    public Color NormalColor = new Color(1f, 1f, 1f, 0.4f);
    public Color SelectedColor = Color.white;

    public float NormalScale = 1f;
    public float SelectedScale = 1.25f;

    public void SetSelected(bool selected)
    {
        if (DotImage == null) return;

        DotImage.color = selected ? SelectedColor : NormalColor;
        transform.localScale = Vector3.one * (selected ? SelectedScale : NormalScale);
    }
}