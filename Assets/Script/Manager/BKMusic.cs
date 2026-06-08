using UnityEngine;

public class BKMusic
{
    public static BKMusic Instance = new BKMusic();
    public AudioSource audioSource;

    public float soundValue = .5f;
    public bool soundOpen = true;

    BKMusic()
    {
        CreateBKMusic();
    }


    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="resName"></param>
    public void PlaySound(ResourceEnum resName)
    {
        GameObject soundObj = new GameObject();
        AudioSource audioSource = soundObj.AddComponent<AudioSource>();

        audioSource.clip = Resources.Load<AudioClip>("Music/" + resName.ToString());
        audioSource.volume = soundValue;
        audioSource.mute = !soundOpen;
        audioSource.Play();

        GameObject.Destroy(soundObj, 1);
    }


    public void CreateBKMusic()
    {
        GameObject bkMusicObj = new GameObject("BKMusic");
        audioSource = bkMusicObj.AddComponent<AudioSource>();

        audioSource.clip = Resources.Load<AudioClip>("Music/" + ResourceEnum.bgm.ToString());
        audioSource.loop = true;
        audioSource.volume = .5f;
        audioSource.Play();

        GameObject.DontDestroyOnLoad(bkMusicObj);
    }

    public void Wake()
    {

    }
}
