using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed;
    private Vector2 moveInput;

    [Header("组件")]

    public Rigidbody2D rb;

    [Header("属性")]
    public int pickRange;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        Move();
    }

    void Update()
    {
    }


    /// <summary>
    /// 玩家移动
    /// </summary>
    private void Move()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        moveInput.Normalize();
        rb.linearVelocity = moveInput * moveSpeed;
    }


    private void OnEnable()
    {
        PlayerManager.Instance.FindPlayer(this);
    }

    private void OnDisable()
    {
        PlayerManager.Instance.MissPlayer();
    }
}
