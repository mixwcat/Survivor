using UnityEngine;
using System.Collections;
public class SpinWeapon : MonoBehaviour
{
    public float rotationSpeed = 360f;
    public Transform fireBallHolder;
    public float fireBallInterval = 5f;
    // Update is called once per frame

    void Start()
    {
        StartCoroutine(GenerateFireball());
    }

    void Update()
    {
        transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + rotationSpeed * Time.deltaTime);
    }

    // 携程生成火球
    IEnumerator GenerateFireball()
    {
        while (PlayerManager.Instance.player != null)
        {
            Instantiate(Resources.Load<GameObject>("Weapon/FireBall"), fireBallHolder.position, Quaternion.identity).transform.parent = fireBallHolder;
            yield return new WaitForSeconds(fireBallInterval);
        }
    }
}
