using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NodeCanvas.Framework;

public class PursuitSteer : ActionTask
{
    public BBParameter<float> maxSpeed;
    public BBParameter<Vector2> inTargetPosition;
    public BBParameter<Vector2> inTargetVelocity;
    public BBParameter<Vector2> outTargetSteer;

    protected override void OnUpdate()
    {
        // estimate pursuit position
        var dir = inTargetPosition.value - (Vector2)agent.transform.position;
        var dis = dir.magnitude;
        if (dis < 0.1f)
        {
            EndAction(false);
            return;
        }
        dir /= dis;
        var maxDist = maxSpeed.value * Time.fixedDeltaTime;
        var steerRatio = Mathf.Clamp(dis / maxDist, 0f, 1f);
        Debug.Log("SteerRatio " + steerRatio + " dir " + dir);
        outTargetSteer.value = steerRatio * dir;
        EndAction(true);
    }
}
