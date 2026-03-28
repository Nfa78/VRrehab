using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CustomDropdown : MonoBehaviour
{
    public GameObject optionPrefab; // Assign your "dropdown option" prefab
    public Transform optionsParent; // Parent object where options will be instantiated
    public TMP_Text header;

    private CanvasGroupAlphaToggle toggly;
    private List<GameObject> spawnedOptions = new List<GameObject>();

    void Start()
    {
        PopulateDropdown();
        toggly = GetComponent<CanvasGroupAlphaToggle>();
    }

    public void PopulateDropdown()
    {
        // Clear existing options
        foreach (GameObject option in spawnedOptions)
        {
            Destroy(option);
        }
        spawnedOptions.Clear();

        List<string> usernames = PointsManager.Instance.GetSavedUsernames();

        if (usernames.Count == 0)
        {
            Debug.Log("No usernames found, dropdown is empty.");
            return;
        }

        // Create an option for each username
        foreach (string username in usernames)
        {
            GameObject newOption = Instantiate(optionPrefab, optionsParent);
            newOption.SetActive(true);

            TMP_Text text = newOption.GetComponentInChildren<TMP_Text>();
            Toggle toggle = newOption.GetComponentInChildren<Toggle>();

            text.text = username;
            toggle.onValueChanged.AddListener((isSelected) =>
            {
                if (isSelected)
                {
                    OnUsernameSelected(username);
                }
            });

            spawnedOptions.Add(newOption);
        }
    }

    private void OnUsernameSelected(string username)
    {
        PointsManager.Instance.SetCurrentUser(username);
        header.text = username;
        toggly.ToggleVisible();
        Debug.Log($"User {username} selected.");
    }
}
