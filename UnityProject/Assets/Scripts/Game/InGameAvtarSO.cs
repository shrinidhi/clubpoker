using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "InGameAvtarImage", menuName = "ClubPoker/InGameAvtarImage")]
public class InGameAvtarSO : ScriptableObject
{
    public List<IngameAvtarData> AvtarBadges = new List<IngameAvtarData>();
}
[System.Serializable]
public class IngameAvtarData
{
    public string AvtarName;
    public Sprite AvtarImage;
}