using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "SmallCardImage", menuName = "ClubPoker/SmallCardImage")]
public class SmallCardSO : ScriptableObject
{
  public List<SmallCardData> SmallCard = new List<SmallCardData>();
}

[System.Serializable]
public class SmallCardData
{
    public string CardName;
    public Sprite CardImage;
}
