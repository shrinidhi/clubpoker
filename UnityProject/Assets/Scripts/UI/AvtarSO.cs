using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AvtarImage", menuName = "ClubPoker/AvtarImage")]
public class AvtarSO : ScriptableObject
{
    public List<AvtarData> AvtarBadges = new List<AvtarData>();
}
[System.Serializable]
public class AvtarData
{
    public string AvtarName;
    public Sprite AvtarImage;
}