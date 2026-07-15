using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Admin ▸ Club Poster ▸ Add. Pick an image (NativeGallery), preview it, then Save — which
/// hands the base64 data URI back to the poster screen to upload. The screen-1 popup from
/// the prototype. (End Time has no backend, so it's optional/UI-only.)
/// </summary>
public class AdminPosterUploadPopupScript : MonoBehaviour
{
    private const long MaxFileBytes = 800 * 1024;   // "File Size < 800kb"

    [Header("Header")]
    public Button Close_Button;

    [Header("Upload area")]
    public Button UploadArea_Button;         // the dashed "Click to upload image" box
    public Image Preview_Image;               // sits over the placeholder; enabled once picked

    [Header("Submit")]
    public Button Save_Button;

    [Header("Shared")]
    public AlertPopup AlertPopup;             // close-without-save confirm

    // Callback → (dataUri, filename, fileSize). The screen does the actual POST.
    private Action<string, string, long> _onSaved;

    private string _dataUri;
    private string _filename;
    private long _fileSize;
    private Texture2D _tex;
    private Sprite _sprite;

    private void Start()
    {
        if (Close_Button      != null) Close_Button.onClick.AddListener(OnCloseTap);
        if (UploadArea_Button != null) UploadArea_Button.onClick.AddListener(OnPickTap);
        if (Save_Button       != null) Save_Button.onClick.AddListener(OnSaveTap);
    }

    /// Open, routing a successful Save to <paramref name="onSaved"/>.
    public void Open(Action<string, string, long> onSaved)
    {
        _onSaved = onSaved;
        ResetState();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    private void ResetState()
    {
        _dataUri = null;
        _filename = null;
        _fileSize = 0;
        FreeImage();

        if (Preview_Image != null)
        {
            Preview_Image.sprite = null;
            Preview_Image.enabled = false;   // reveals the placeholder underneath
        }
        if (Save_Button != null) Save_Button.interactable = false;
    }

    private void OnPickTap()
    {
        NativeGallery.GetImageFromGallery(OnImagePicked, "Select Poster", "image/*");
    }

    private void OnImagePicked(string path)
    {
        if (string.IsNullOrEmpty(path)) return;   // cancelled

        // Load a downscaled copy (recommended height 960) instead of the raw multi-MB file,
        // so the base64 payload stays small. markTextureNonReadable:false → we can re-encode.
        FreeImage();
        _tex = NativeGallery.LoadImageAtPath(path, maxSize: 960, markTextureNonReadable: false);
        if (_tex == null)
        {
            Debug.LogError("[AdminPosterUploadPopupScript] LoadImageAtPath returned null");
            ShowToast("Could not load image");
            return;
        }

        // Re-encode as JPG, dropping quality until it fits the size cap.
        byte[] jpg = _tex.EncodeToJPG(85);
        int quality = 85;
        while (jpg.Length > MaxFileBytes && quality > 30)
        {
            quality -= 15;
            jpg = _tex.EncodeToJPG(quality);
        }

        if (jpg.Length > MaxFileBytes)
        {
            Debug.LogWarning($"[AdminPosterUploadPopupScript] still {jpg.Length} bytes at q{quality}");
            ShowToast("Image too large");
            FreeImage();
            return;
        }

        _dataUri  = ClubImageUtil.ToDataUri(jpg, "image/jpeg");
        _filename = Path.GetFileNameWithoutExtension(path) + ".jpg";
        _fileSize = jpg.Length;

        _sprite = ClubImageUtil.SpriteFromTexture(_tex);

        if (Preview_Image != null)
        {
            Preview_Image.gameObject.SetActive(true);
            Preview_Image.enabled = true;
            Preview_Image.color   = Color.white;      // ensure not left transparent
            Preview_Image.sprite  = _sprite;
            Debug.Log($"[AdminPosterUploadPopupScript] preview set {_tex.width}x{_tex.height}");
        }
        else
        {
            Debug.LogWarning("[AdminPosterUploadPopupScript] Preview_Image not assigned");
        }

        if (Save_Button != null) Save_Button.interactable = true;
    }

    private void OnSaveTap()
    {
        if (string.IsNullOrEmpty(_dataUri))
        {
            ShowToast("Please select an image");
            return;
        }

        var cb = _onSaved;
        string uri = _dataUri; string name = _filename; long size = _fileSize;
        Close();
        cb?.Invoke(uri, name, size);
    }

    private void OnCloseTap()
    {
        // Picked but not saved → confirm.
        if (!string.IsNullOrEmpty(_dataUri) && AlertPopup != null)
        {
            AlertPopup.Show("Tips", "Current content will not be saved, confirm to close?",
                showCancel: true, onConfirm: Close);
            return;
        }
        Close();
    }

    private void Close()
    {
        _onSaved = null;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        FreeImage();
    }

    private void FreeImage()
    {
        if (_sprite != null) { Destroy(_sprite); _sprite = null; }
        if (_tex    != null) { Destroy(_tex);    _tex = null; }
    }

    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
