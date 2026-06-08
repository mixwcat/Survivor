using UnityEngine;

public class InputReaderManager : MonoBehaviour
{
    public static InputReaderManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }


    public InputReader inputReader;
}
