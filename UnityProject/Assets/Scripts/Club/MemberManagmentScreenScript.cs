using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class MemberManagmentScreenScript : MonoBehaviour
{
    public Button Back_Button;
    public Button Member_Button;
    public Button Agent_Button;
    public Button Applicant_Button;
    public GameObject Member_Panel;
    public GameObject Agent_Panel;
    public GameObject Applicant_Panel;
    public string CludID;
    public Sprite Select_BG;
    public Sprite UnSelect_BG;
    // Start is called before the first frame update
    void Start()
    {
        Back_Button.onClick.AddListener(Back_ButtonOnTap);
        Member_Button.onClick.AddListener(Member_ButtonOnTap);
        Agent_Button.onClick.AddListener(Agent_ButtonOnTap);
        Applicant_Button.onClick.AddListener(Applicant_ButtonOnTap);
    }

    private void OnEnable()
    {
        Member_ButtonOnTap();
    }
    void Back_ButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    void Member_ButtonOnTap()
    {
        Member_Panel.SetActive(true);
        Agent_Panel.SetActive(false);
        Applicant_Panel.SetActive(false);
        Member_Button.image.sprite = Select_BG;
        Agent_Button.image.sprite = UnSelect_BG;
        Applicant_Button.image.sprite = UnSelect_BG;

    }

    void Agent_ButtonOnTap()
    {
        Member_Panel.SetActive(false);
        Agent_Panel.SetActive(true);
        Applicant_Panel.SetActive(false);
        Member_Button.image.sprite = UnSelect_BG;
        Agent_Button.image.sprite = Select_BG;
        Applicant_Button.image.sprite = UnSelect_BG;
    }

    void Applicant_ButtonOnTap()
    {
        Member_Panel.SetActive(false);
        Agent_Panel.SetActive(false);
        Applicant_Panel.SetActive(true);
        Member_Button.image.sprite = UnSelect_BG;
        Agent_Button.image.sprite = UnSelect_BG;
        Applicant_Button.image.sprite = Select_BG;
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
