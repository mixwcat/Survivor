using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    // 单例模式
    private static PlayerManager instance;
    public static PlayerManager Instance => instance;

    [Header("玩家对象")]
    private PlayerController _player;
    public PlayerController player
    {
        get
        {
            if (_player == null)
            {
                Debug.Log("Manager中玩家丢失！请确保玩家对象已被正确查找或赋值。");
                return null;
            }
            return _player;
        }
        private set => _player = value;
    }

    void Awake()
    {
        instance = this;
    }


    /// <summary>
    /// 查找玩家对象
    /// </summary>
    public void FindPlayer(PlayerController playerController = null)
    {
        _player = playerController ?? FindFirstObjectByType<PlayerController>();
    }


    public void MissPlayer()
    {
        _player = null;
    }
}
