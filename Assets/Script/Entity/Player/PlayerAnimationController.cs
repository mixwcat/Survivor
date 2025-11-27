using System.Collections;
using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private AudioSource moveAudioSource;
    public float soundInterval = 0.5f;

    private bool isMoving;
    private bool rightWalking;
    private bool leftWalking;
    private bool backWalking;
    private bool towardWalking;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        moveAudioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        //StartCoroutine(PlayMoveSound());
    }

    // Update is called once per frame
    void Update()
    {
        GetWalkingState();
        SetAnimationParameters();
    }


    /// <summary>
    /// 获取行走状态
    /// </summary>
    private void GetWalkingState()
    {
        rightWalking = Input.GetAxisRaw("Horizontal") > 0;
        leftWalking = Input.GetAxisRaw("Horizontal") < 0;
        backWalking = Input.GetAxisRaw("Vertical") > 0;
        towardWalking = Input.GetAxisRaw("Vertical") < 0;
        isMoving = rightWalking || leftWalking || backWalking || towardWalking;
    }

    /// <summary>
    /// 设置动画参数
    /// </summary>
    private void SetAnimationParameters()
    {
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("rightWalking", rightWalking);
        animator.SetBool("leftWalking", leftWalking);
        animator.SetBool("backWalking", backWalking);
        animator.SetBool("towardWalking", towardWalking);
    }


    IEnumerator PlayMoveSound()
    {
        while (PlayerManager.Instance.player != null)
        {
            if (isMoving)
            {
                moveAudioSource.volume = BKMusic.Instance.soundValue;
                moveAudioSource.Play();
            }
            else
            {
                moveAudioSource.Pause();
            }

            yield return new WaitForSeconds(soundInterval);
        }
    }
}
