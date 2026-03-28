using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PointsManager : MonoBehaviour
{
    public static PointsManager Instance;

    public int Points = 0; // Kitchen level points
    public int SecondLevelPoints = 0; // Coffee level points
    public int ThirdLevelPoints = 0; // Living room level points

    public string Arm = "Right"; // Default to right
    public string Environment = "Simple"; // Default to simplified environment
    public int Character = 0;
    public string CurrentUsername { get; private set; } // Track active user

    public event Action OnPointsChanged; // Event for text update
    public CharacterSelection charSel;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadPoints(CurrentUsername);
    }

    // Class needed for the persistence of Username and Points between different sessions
    // It needs to be serializable in order to work with JsonUtility
    [System.Serializable]
    class SaveData
    {
        public string Username;
        public int CurrentPoints;
        public int CoffeeLevelPoints;
        public int LivingRoomLevelPoints;
        public string AffectedArm;
        public string ChosenEnvironment;
        public int SelectedHelper;
    }

    public void AddPoints(string username, int amount)
    {
        Points += amount;
        OnPointsChanged?.Invoke(); // Trigger event when points change
        SavePoints(username);
    }

    public void SavePoints(string username)
    {
        SaveData data = new SaveData();
        data.Username = username;
        data.CurrentPoints = Points;
        data.CoffeeLevelPoints = SecondLevelPoints;
        data.LivingRoomLevelPoints = ThirdLevelPoints;
        data.AffectedArm = Arm;
        data.ChosenEnvironment = Environment;
        data.SelectedHelper = Character;

        string json = JsonUtility.ToJson(data);

        // Let's write the json string to a file
        // Application.persistentDataPath will give you a folder where you can save data
        // that will survive between application reinstall or update
        string path = Application.persistentDataPath + $"/savefile_{username}.json"; // Corrected here
        File.WriteAllText(path, json);
    }

    public void LoadPoints(string username)
    {
        string path = Application.persistentDataPath + $"/savefile_{username}.json";

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            Points = data.CurrentPoints;
            SecondLevelPoints = data.CoffeeLevelPoints;
            ThirdLevelPoints = data.LivingRoomLevelPoints;
            Arm = data.AffectedArm;
            Environment = data.ChosenEnvironment;
            Character = data.SelectedHelper;
            PlayerPrefs.SetInt("selectedCharacter", Character); /////////////
            charSel.selectedCharacter = Character;

        }
        else
        {
            Debug.LogWarning($"Save file for {username} not found. Initializing new save.");
            Points = 0; // Default points for new users
            SecondLevelPoints = 0;
            ThirdLevelPoints = 0;
            //Arm = "Right";
        }
    }

    public void ResetPoints()
    {
        if (string.IsNullOrEmpty(CurrentUsername))
        {
            Debug.LogError("No username set! Cannot reset points.");
            return;
        }

        Points = 0;
        SavePoints(CurrentUsername);

        // Update the UI
        FindAnyObjectByType<TextUpdater>()?.UpdateText();
    }

    public void ResetSecondLevelPoints()
    {
        if (string.IsNullOrEmpty(CurrentUsername))
        {
            Debug.LogError("No username set! Cannot reset points.");
            return;
        }

        SecondLevelPoints = 0;
        SavePoints(CurrentUsername);

        // Update the UI
        FindAnyObjectByType<TextUpdater>()?.UpdateText();
    }

    public void ResetThirdLevelPoints()
    {
        if (string.IsNullOrEmpty(CurrentUsername))
        {
            Debug.LogError("No username set! Cannot reset points.");
            return;
        }

        ThirdLevelPoints = 0;
        SavePoints(CurrentUsername);

        // Update the UI
        FindAnyObjectByType<TextUpdater>()?.UpdateText();
    }

    public void SetCurrentUser(string username)
    {
        CurrentUsername = username;
        LoadPoints(CurrentUsername);
    }

    public void SaveCurrentUserPoints()
    {
        if (!string.IsNullOrEmpty(CurrentUsername))
        {
            SavePoints(CurrentUsername);
        }
        else
        {
            Debug.LogError("No username set! Cannot save points.");
        }
    }


    public List<string> GetSavedUsernames()
    {
        List<string> usernames = new List<string>();
        string[] files = Directory.GetFiles(Application.persistentDataPath, "savefile_*.json");

        foreach (string file in files)
        {
            string filename = Path.GetFileNameWithoutExtension(file);
            string username = filename.Replace("savefile_", "");
            usernames.Add(username);
        }

        return usernames;
    }

    public void SetAffectedArm(string arm)
    {
        if (arm != "Left" && arm != "Right")
        {
            Debug.LogWarning("Invalid affected arm value! Must be 'Left' or 'Right'.");
            return;
        }

        Arm = arm;
    }


    public string GetAffectedArm()
    {
        return Arm; // Return the current arm
    }

    public void SetEnvironment(string env)
    {
        Environment = env;
    }

    public string GetEnvironment()
    {
        return Environment;
    }

    public void SetHelper()
    {
        Character = PlayerPrefs.GetInt("selectedCharacter");
        SavePoints(CurrentUsername);
    }

    public int GetHelper()
    {
        return Character;
    }

    public void AddSecondLevelPoints(string username, int amount)
    {
        SecondLevelPoints += amount;
        SavePoints(username);
    }

    public int GetSecondLevelPoints()
    {
        return SecondLevelPoints;
    }

    public void AddThirdLevelPoints(string username, int amount)
    {
        ThirdLevelPoints += amount;
        SavePoints(username);
    }

    public int GetThirdLevelPoints()
    {
        return ThirdLevelPoints;
    }

}
