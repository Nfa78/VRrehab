using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UsernameSelection : MonoBehaviour
{
    public TMP_InputField usernameInput;

    public void SetUsername()
    {
        string username = usernameInput.text.Trim();

        if (!string.IsNullOrEmpty(username))
        {
            PointsManager.Instance.SetCurrentUser(username);
            Debug.Log($"User {username} selected. Loading points...");
        }
        else
        {
            PointsManager.Instance.SetCurrentUser("Gino");
            Debug.LogWarning("Username cannot be empty!");
        }
    }
}

