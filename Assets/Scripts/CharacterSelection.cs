using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;


public class CharacterSelection : MonoBehaviour
{
    public GameObject[] characters;
    public int selectedCharacter = 0;
    public TextMeshProUGUI subtitle;
    public string levelName;
    public string noHelperLevelName;

    public void NextCharacter()
    {
        characters[selectedCharacter].SetActive(false);
        selectedCharacter = (selectedCharacter + 1) % characters.Length;
        characters[selectedCharacter].SetActive(true);
    }

    public void PreviousCharacter()
    {
        characters[selectedCharacter].SetActive(false);
        selectedCharacter--;
        if(selectedCharacter < 0)
        {
            selectedCharacter += characters.Length;
        }
        characters[selectedCharacter].SetActive(true);
    }

    public void SetCharacter(int charIndex)
    {
        characters[selectedCharacter].SetActive(false);
        selectedCharacter = charIndex;
        characters[selectedCharacter].SetActive(true);
        PlayerPrefs.SetInt("selectedCharacter", selectedCharacter); /////////////
        subtitle.SetText($"{characters[selectedCharacter].name}");
    }

    public void StartGame(string lvlName)
    {
        //PointsManager.Instance.SetHelper(selectedCharacter);
        if (SceneManager.GetActiveScene().name == "NewStarterMenu")
            PlayerPrefs.SetInt("selectedCharacter", selectedCharacter);
        SceneManager.LoadScene(lvlName);
    }
}
