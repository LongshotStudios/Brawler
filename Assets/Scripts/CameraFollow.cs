using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Vector2 scrollMin;
    public Vector2 scrollMax;
    public Transform[] followTargets;
    public float followSpeed;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }
    

    private void Update()
    {
        var position = followTargets[0].position;
        for (int i = 1; i < followTargets.Length; i++) {
            position += followTargets[i].position;
        }
        position /= followTargets.Length;
        
        var screenTarget = cam.WorldToViewportPoint(position);
        var move = screenTarget - new Vector3(0.5f, 0.5f, screenTarget.z);
        if (move.x > 0.25f) {
            move.x = 1;
        } else if (move.x < -.25f) {
            move.x = -1;
        } else {
            move.x = 0;
        }
        
        var pos = transform.position + move * followSpeed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, scrollMin.x, scrollMax.x);
        pos.y = Mathf.Clamp(pos.y, scrollMin.y, scrollMax.y);
        transform.position = pos;
    }
}
