
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

namespace ClubPoker.Auth
{
    /// <summary>
    /// AES-256-CBC encrypted JWT storage using PlayerPrefs.
    /// Plain text is never written to disk — every value is encrypted
    /// with a device-specific key before storage.
    ///
    /// Rule: only AuthManager reads and writes TokenStore.
    /// No other class should call these methods directly.
    /// </summary>
    public class GuestProfile
    {
        public string Id          { get; set; }
        public string Username    { get; set; }
        public int    WalletChips { get; set; }
    }

    public static class TokenStore
    {
        // ── PlayerPrefs keys ──────────────────────────────────────────────────
        private const string KEY_ACCESS_TOKEN  = "cp_at";
        private const string KEY_REFRESH_TOKEN = "cp_rt";
        private const string KEY_REMEMBER_ME   = "cp_rm";
        private const string KEY_GUEST_TOKEN   = "cp_gt";
        private const string KEY_GUEST_EXPIRY  = "cp_ge";
        private const string KEY_GUEST_PROFILE = "cp_gp";

        // Salt scopes the derived key to ClubPoker specifically
        private const string DERIVATION_SALT   = "ClubPoker_v1_TokenStore";

        // ── Encryption key — lazy, cached for the session ─────────────────────
        private static byte[] _cachedKey;

        private static byte[] EncryptionKey
        {
            get
            {
                if (_cachedKey != null) return _cachedKey;

                // SHA-256( deviceUniqueIdentifier + salt ) → 32-byte AES-256 key
                // Consistent across app restarts on the same device.
                // Different on every device so tokens can't be decrypted elsewhere.
                string raw = SystemInfo.deviceUniqueIdentifier + DERIVATION_SALT;
                using var sha = SHA256.Create();
                _cachedKey = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return _cachedKey;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Store access and refresh tokens atomically.
        /// Called by AuthManager after register, login, and token refresh.
        /// </summary>
        public static void SaveTokens(string accessToken, string refreshToken, bool rememberMe = true)
        {
            WriteEncrypted(KEY_ACCESS_TOKEN,  accessToken);
            WriteEncrypted(KEY_REFRESH_TOKEN, refreshToken);
            PlayerPrefs.SetInt(KEY_REMEMBER_ME, rememberMe ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Store a guest token with a server-derived UTC expiry timestamp.
        /// Called by AuthManager after a successful guest login.
        /// </summary>
        public static void SaveGuestToken(string token, DateTime expiresAt)
        {
            WriteEncrypted(KEY_GUEST_TOKEN, token);
            PlayerPrefs.SetString(KEY_GUEST_EXPIRY, expiresAt.ToString("O")); // ISO 8601
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Persist the guest player's server-assigned id and username so they
        /// survive a cold restart. Not encrypted — neither value is sensitive.
        /// Called by AuthManager immediately after SaveGuestToken.
        /// </summary>
        public static void SaveGuestProfile(string id, string username, int walletChips)
        {
            var profile = new GuestProfile { Id = id, Username = username, WalletChips = walletChips };
            WriteEncrypted(KEY_GUEST_PROFILE, JsonConvert.SerializeObject(profile));
            PlayerPrefs.Save();
        }

        public static GuestProfile LoadGuestProfile()
        {
            string json = ReadEncrypted(KEY_GUEST_PROFILE);
            if (string.IsNullOrEmpty(json)) return null;
            try   { return JsonConvert.DeserializeObject<GuestProfile>(json); }
            catch { return null; }
        }

        /// <summary>
        /// Returns the decrypted access token, or null if:
        ///   - Remember Me is off (user chose not to persist session)
        ///   - No token is stored
        ///   - Stored data is corrupted
        /// </summary>
        public static string LoadAccessToken()
        {
            if (PlayerPrefs.GetInt(KEY_REMEMBER_ME, 0) == 0) return null;
            return ReadEncrypted(KEY_ACCESS_TOKEN);
        }

        /// <summary>
        /// Returns the decrypted refresh token, or null if not stored.
        /// Deliberately ignores Remember Me — the refresh token must always
        /// be available so AuthManager can attempt silent re-auth.
        /// </summary>
        public static string LoadRefreshToken()
        {
            return ReadEncrypted(KEY_REFRESH_TOKEN);
        }

        /// <summary>
        /// Returns the decrypted guest token if the session has not expired.
        /// Returns null and clears storage if the token is expired.
        /// </summary>
        public static string LoadGuestToken()
        {
            string expiryRaw = PlayerPrefs.GetString(KEY_GUEST_EXPIRY, null);
            if (string.IsNullOrEmpty(expiryRaw)) return null;

            if (!DateTime.TryParse(expiryRaw, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiry))
                return null;

            if (DateTime.UtcNow >= expiry)
            {
                ClearGuestToken();
                return null;
            }

            return ReadEncrypted(KEY_GUEST_TOKEN);
        }

        /// <summary>
        /// Returns time remaining on the guest session.
        /// Returns TimeSpan.Zero if expired or no guest token exists.
        /// Used by AuthManager.GuestTimeRemaining() for the countdown UI.
        /// </summary>
        public static TimeSpan GuestTimeRemaining()
        {
            string expiryRaw = PlayerPrefs.GetString(KEY_GUEST_EXPIRY, null);
            if (string.IsNullOrEmpty(expiryRaw)) return TimeSpan.Zero;

            if (!DateTime.TryParse(expiryRaw, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiry))
                return TimeSpan.Zero;

            TimeSpan remaining = expiry - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>Whether the user ticked Remember Me on their last login.</summary>
        public static bool HasRememberMe() => PlayerPrefs.GetInt(KEY_REMEMBER_ME, 0) == 1;

        /// <summary>
        /// Clear every stored auth value — tokens, expiry, Remember Me flag.
        /// Called by AuthManager.LogoutAsync() as part of the full logout sequence.
        /// </summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(KEY_ACCESS_TOKEN);
            PlayerPrefs.DeleteKey(KEY_REFRESH_TOKEN);
            PlayerPrefs.DeleteKey(KEY_REMEMBER_ME);
            ClearGuestToken();
            PlayerPrefs.Save();
            Debug.Log("[TokenStore] All tokens cleared.");
        }

        /// <summary>Clear only the guest token and its expiry timestamp.</summary>
        public static void ClearGuestToken()
        {
            PlayerPrefs.DeleteKey(KEY_GUEST_TOKEN);
            PlayerPrefs.DeleteKey(KEY_GUEST_EXPIRY);
            PlayerPrefs.DeleteKey(KEY_GUEST_PROFILE);
            PlayerPrefs.Save();
        }

        // ── AES-256-CBC Encrypt / Decrypt ─────────────────────────────────────

        private static void WriteEncrypted(string prefsKey, string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                PlayerPrefs.DeleteKey(prefsKey);
                return;
            }

            try
            {
                PlayerPrefs.SetString(prefsKey, Encrypt(plainText, EncryptionKey));
            }
            catch (Exception e)
            {
                Debug.LogError($"[TokenStore] Encrypt failed for '{prefsKey}': {e.Message}");
            }
        }

        private static string ReadEncrypted(string prefsKey)
        {
            string cipher = PlayerPrefs.GetString(prefsKey, null);
            if (string.IsNullOrEmpty(cipher)) return null;

            try
            {
                return Decrypt(cipher, EncryptionKey);
            }
            catch (Exception e)
            {
                // Corrupted or tampered — remove silently and return null
                // AuthManager will treat null as a missing token and go to login
                Debug.LogWarning($"[TokenStore] Decrypt failed for '{prefsKey}': {e.Message}. Clearing.");
                PlayerPrefs.DeleteKey(prefsKey);
                return null;
            }
        }

        /// <summary>
        /// AES-256-CBC encryption with a fresh random IV per call.
        /// Output format: Base64( IV[16 bytes] + CipherBytes )
        /// The random IV means the same plaintext produces a different
        /// ciphertext every time, preventing comparison attacks on PlayerPrefs.
        /// </summary>
        private static string Encrypt(string plainText, byte[] key)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key     = key;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            byte[] plain  = Encoding.UTF8.GetBytes(plainText);
            byte[] cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);

            // Prepend IV so Decrypt can extract it
            byte[] result = new byte[16 + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0,  16);
            Buffer.BlockCopy(cipher, 0, result, 16, cipher.Length);
            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// AES-256-CBC decryption.
        /// Splits the first 16 bytes as IV, decrypts the remainder.
        /// </summary>
        private static string Decrypt(string cipherText, byte[] key)
        {
            byte[] full = Convert.FromBase64String(cipherText);

            if (full.Length < 32)
                throw new CryptographicException("Cipher data too short.");

            byte[] iv     = new byte[16];
            byte[] cipher = new byte[full.Length - 16];
            Buffer.BlockCopy(full, 0,  iv,     0, 16);
            Buffer.BlockCopy(full, 16, cipher, 0, cipher.Length);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key     = key;
            aes.IV      = iv;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }
    }
}