using UnityEngine;

public class EnemySizeController : MonoBehaviour
{
    public Transform spriteTransform;
    public float maxSize;
    public float minSize;
    private float targetSize;
    public float sizeChangeSpeed;
    private bool isGrowing = true;

    void Start()
    {
        sizeChangeSpeed = Random.Range(sizeChangeSpeed * 0.7f, sizeChangeSpeed * 1.5f);
    }

    /// <summary>
    /// 在maxSize和minSize之间来回变换敌人大小
    /// </summary>
    void FixedUpdate()
    {
        targetSize = isGrowing ? maxSize : minSize;

        spriteTransform.localScale = Vector3.MoveTowards(spriteTransform.localScale, Vector3.one * targetSize, sizeChangeSpeed * Time.deltaTime);

        if (Mathf.Abs(spriteTransform.localScale.x - targetSize) < 0.1f)
        {
            isGrowing = !isGrowing;
        }
    }
}

