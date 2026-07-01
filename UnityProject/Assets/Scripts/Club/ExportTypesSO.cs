using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Config for the export data types shown in the Export Data modal. Edit this
/// single asset to add/remove a type, rename it, change its API key, or its cost.
/// </summary>
[CreateAssetMenu(fileName = "ExportTypes", menuName = "ClubPoker/ExportTypes")]
public class ExportTypesSO : ScriptableObject
{
    public List<ExportTypeData> Types = new List<ExportTypeData>();
}

[System.Serializable]
public class ExportTypeData
{
    public string DisplayName;   // e.g. "Club Data"
    public string TypeKey;       // server key, e.g. "clubData"
    public int    Cost;          // diamond cost
    public bool   DefaultOn = true;
}
