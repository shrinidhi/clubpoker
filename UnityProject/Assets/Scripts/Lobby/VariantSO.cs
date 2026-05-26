using System.Collections.Generic;
using UnityEngine;

namespace ClubPoker.Lobby
{
    [CreateAssetMenu(fileName = "VariantImage", menuName = "ClubPoker/VariantImage")]
    public class VariantSO : ScriptableObject
    {
        public List<VariantData> Variants = new List<VariantData>();

        public Sprite GetVariantSprite(string variantName)
        {
            if (string.IsNullOrEmpty(variantName))
                return null;

            foreach (VariantData data in Variants)
            {
                if (data.VariantName.ToLower() == variantName.ToLower())
                    return data.VariantImage;
            }

            return null;
        }
    }

    [System.Serializable]
    public class VariantData
    {
        public string VariantName;
        public Sprite VariantImage;
    }
}
