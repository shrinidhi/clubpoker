using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

public class ApplicantPanelScript : MonoBehaviour
{
    public Transform Request_Content;
    public GameObject RequestPrefab;

    public string ClubId;

    public Button Close_Button;
    public ShowClubTableScreenScript ShowClubTableScreenScript; 

    private List<RequestPrefabScript> requestItems =
        new List<RequestPrefabScript>();

    private void OnEnable()
    {
        ClubId = ShowClubTableScreenScript.CLubID;
        LoadApplications().Forget();
    }

    private void Start()
    {
        if (Close_Button != null)
            Close_Button.onClick.AddListener(() => gameObject.SetActive(false));
    }

    public async UniTaskVoid LoadApplications()
    {
        ClearRequests();

        if (string.IsNullOrEmpty(ClubId))
        {
            Debug.LogError("ClubId missing");
            return;
        }

        List<ClubApplicationData> applications =
            await AuthManager.Instance.GetClubApplicationsAsync(ClubId);

        foreach (ClubApplicationData application in applications)
        {
            if (application.Status != "PENDING")
                continue;

            GameObject obj = Instantiate(RequestPrefab, Request_Content);

            RequestPrefabScript prefab =
                obj.GetComponent<RequestPrefabScript>();

            prefab.Setup(
                application,
                OnAcceptApplication,
                OnRejectApplication
            );

            requestItems.Add(prefab);
        }
    }

    private async void OnAcceptApplication(ClubApplicationData application)
    {
        bool success =
            await AuthManager.Instance.ApproveClubApplicationAsync(
                ClubId,
                application.Id
            );

        if (success)
            LoadApplications().Forget();
    }

    private async void OnRejectApplication(ClubApplicationData application)
    {
        bool success =
            await AuthManager.Instance.RejectClubApplicationAsync(
                ClubId,
                application.Id
            );

        if (success)
            LoadApplications().Forget();
    }

    private void ClearRequests()
    {
        requestItems.Clear();

        for (int i = Request_Content.childCount - 1; i >= 0; i--)
        {
            Destroy(Request_Content.GetChild(i).gameObject);
        }
    }
}