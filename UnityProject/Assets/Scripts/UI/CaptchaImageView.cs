//
// Shows the captcha from GET /api/auth/captcha on this Image.
// The image arrives as a "data:image/jpeg;base64,..." (or png) data URI.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    [RequireComponent(typeof(Image))]
    public class CaptchaImageView : MonoBehaviour
    {
        private Image _image;
        private Texture2D _texture;
        private Sprite _sprite;

        private Image Img => _image != null ? _image : (_image = GetComponent<Image>());

        private void OnDestroy() => Clear();

        /// Show a "data:image/...;base64,..." captcha. False if it couldn't be decoded.
        public bool Show(string dataUri)
        {
            Clear();
            if (string.IsNullOrEmpty(dataUri)) return false;

            try
            {
                int comma = dataUri.IndexOf(',');
                byte[] bytes = Convert.FromBase64String(dataUri.Substring(comma + 1));

                // LoadImage reads PNG/JPG only.
                _texture = new Texture2D(2, 2);
                if (!_texture.LoadImage(bytes)) return false;

                _sprite = Sprite.Create(_texture,
                    new Rect(0, 0, _texture.width, _texture.height), new Vector2(0.5f, 0.5f));
                Img.sprite = _sprite;
                Img.preserveAspect = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CaptchaImageView] decode failed: {e.Message}");
                return false;
            }
        }

        public void Clear()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
            _sprite = null;
            _texture = null;

            Img.sprite = null;
        }
    }
}
