using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NodeCanvas.Framework;

public class PursuitSteer : ActionTask
{
    public BBParameter<float> maxSpeed;
    public BBParameter<Vector2> inTargetPosition;
    public BBParameter<Vector2> inTargetVelocity;
    public BBParameter<bool> inFlipped;
    public BBParameter<Vector2> outTargetSteer;

    protected override void OnUpdate()
    {
        // estimate pursuit position
        var dir = inTargetPosition.value - (Vector2)agent.transform.position;
        var dis = dir.magnitude;
        if (dis < Mathf.Epsilon) {
            if (inFlipped.value == agent.GetComponent<SpriteRenderer>().flipX) {
                outTargetSteer.value = Vector2.zero;
            } else {
                float x = inFlipped.value ? -0.1f : 0.1f;
                outTargetSteer.value = new Vector2(x, 0);
            }
        } else {
            dir /= dis;
            var maxDist = maxSpeed.value * Time.fixedDeltaTime;
            var steerRatio = Mathf.Clamp(dis / maxDist, 0f, 1f);
            outTargetSteer.value = steerRatio * dir;
        }
        EndAction(true);
    }
}
