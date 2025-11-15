using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed;
    private Vector2 moveInput;

    [Header("组件")]
    public Animator animator;
    public Rigidbody2D rb;

    [Header("属性")]
    public int pickRange;


    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        Move();
    }

    void Update()
    {
        ChangeAnimParameters();
        //MoveWithMouse();
    }


    /// <summary>
    /// 更改动画参数
    /// </summary>
    private void ChangeAnimParameters()
    {
        if (moveInput != Vector2.zero)
        {
            animator.SetBool("Moving", true);
        }
        else
        {
            animator.SetBool("Moving", false);
        }
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


    float mouseX;
    float mouseY;
    Vector3 mouseMove;
    //public float mouseSpeed = 100f;
    // private void MoveWithMouse()
    // {
    //     mouseX = Input.GetAxis("Mouse X");
    //     mouseY = Input.GetAxis("Mouse Y");
    //     mouseMove = new Vector3(mouseX, mouseY, 0) * mouseSpeed;
    //     transform.position += mouseMove * Time.deltaTime;
    // }

    private void OnEnable()
    {
        PlayerManager.Instance.FindPlayer(this);
    }

    private void OnDisable()
    {
        PlayerManager.Instance.MissPlayer();
    }
}
