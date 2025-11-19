using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform playerTransform;
    private Vector3 target;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerTransform = PlayerManager.Instance.player.transform;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // Lerp towards player but keep camera z at -10
        target = Vector3.Lerp(transform.position, playerTransform.position, 5f * Time.deltaTime);
        target.z = -10f;
        transform.position = target;
    }
}
