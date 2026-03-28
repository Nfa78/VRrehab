using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadCharacter : MonoBehaviour
{
    public GameObject[] characterPrefabs;
    public Transform spawnPoint;
    public GameObject[] dialogues;
    public GameObject player;
    public GameObject checklist;
    public GameObject writtenDialogue;
    public GameObject superfluousObjects;

    private GameObject clone;
    private int selectedCharacter;
    private string chosenEnv;

    private string currentSceneName;

    void Start()
    {
        var pointsManager = EnsurePointsManager();

        selectedCharacter = PlayerPrefs.GetInt("selectedCharacter", 0); 
        chosenEnv = pointsManager != null ? pointsManager.GetEnvironment() : string.Empty;
        currentSceneName = SceneManager.GetActiveScene().name;

        if (selectedCharacter == 0) // Difficult mode (no help)
        {
            if (dialogues != null && dialogues.Length >= 2)
            {
                dialogues[0].SetActive(false);
                dialogues[1].SetActive(false);
            }
            if (checklist != null)
                checklist.SetActive(false);
            if (writtenDialogue != null)
                writtenDialogue.SetActive(false);
        }
        else
        {
            if(clone == null)
            {
                if (characterPrefabs != null && selectedCharacter >= 0 && selectedCharacter < characterPrefabs.Length && characterPrefabs[selectedCharacter] != null)
                {
                    var spawn = spawnPoint != null ? spawnPoint : transform;
                    GameObject prefab = characterPrefabs[selectedCharacter];
                    clone = Instantiate(prefab, spawn.position, spawn.rotation); // Instantiate the helper npc chosen by the player
                }
                else
                {
                    Debug.LogWarning($"{nameof(LoadCharacter)}: Invalid character prefab selection.", this);
                }
            }
            if (selectedCharacter == 1 || selectedCharacter == 2) // Choose the right dialogue option
            {
                if (dialogues != null && dialogues.Length >= 2)
                {
                    dialogues[0].SetActive(true);
                    dialogues[1].SetActive(false);
                }
            }
            else
            {
                if (dialogues != null && dialogues.Length >= 2)
                {
                    dialogues[0].SetActive(false);
                    dialogues[1].SetActive(true);
                }
            }
        }

        if(chosenEnv == "Simple")
        {
            if (superfluousObjects != null)
                superfluousObjects.SetActive(false);
        }

        if (pointsManager != null && !string.IsNullOrEmpty(pointsManager.CurrentUsername))
        {
            if (currentSceneName == "ApartmentScene")
                pointsManager.ResetPoints();
            else if (currentSceneName == "SecondLevel")
                pointsManager.ResetSecondLevelPoints();
            else if (currentSceneName == "TestScene")
                pointsManager.ResetThirdLevelPoints();
        }
        else
        {
            Debug.LogWarning("PointsManager is not initialized or no username is set. Using defaults.");
            if (pointsManager != null)
            {
                pointsManager.SetCurrentUser("DefaultUser");
                pointsManager.Points = 0;
                pointsManager.SetAffectedArm("Right");
                pointsManager.SetEnvironment("Simple");
            }
        }
}
    public GameObject GetClone()
    {
        return clone;
    }

    /*void Update()
    {
        if ((player != null) && (clone != null))
        {
            // Make the NPC face the player while keeping its up direction unchanged
            clone.transform.LookAt(player.transform);
            clone.transform.eulerAngles = new Vector3(clone.transform.eulerAngles.x, 0, clone.transform.eulerAngles.z);
        }
    }*/

    private PointsManager EnsurePointsManager()
    {
        if (PointsManager.Instance != null)
            return PointsManager.Instance;

        GameObject pmGO = new GameObject("PointsManager");
        PointsManager pm = pmGO.AddComponent<PointsManager>();
        PointsManager.Instance = pm;
        DontDestroyOnLoad(pmGO);
        pm.SetCurrentUser("DefaultUser");
        pm.Points = 0;
        pm.SetAffectedArm("Right");
        pm.SetEnvironment("Simple");
        return pm;
    }
}
