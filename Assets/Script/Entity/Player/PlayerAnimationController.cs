using System.Collections;
using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private IInputHandle _inputHandle;
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

        _inputHandle = InputHandleFactory.CreateLocalInput();

        if (_inputHandle == null)
        {
            Debug.LogError("PlayerAnimationController: Failed to create IInputHandle!");
        }
    }

    private void Start()
    {
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
        if (_inputHandle == null) return;

        rightWalking = _inputHandle.MoveInput.x > 0;
        leftWalking = _inputHandle.MoveInput.x < 0;
        backWalking = _inputHandle.MoveInput.y > 0;
        towardWalking = _inputHandle.MoveInput.y < 0;
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
