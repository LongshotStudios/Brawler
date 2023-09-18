using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NodeCanvas.Framework;

public class SensePlayer : ActionTask
{
    public BBParameter<float> inSenseRadius = 8.0f;
    public BBParameter<List<Vector2>> inAttackOffsets;
    public BBParameter<bool> outHasTarget;
    public BBParameter<Vector2> outTargetPosition;
    public BBParameter<Vector2> outTargetVelocity;
    public BBParameter<bool> outFlipped;
    
    protected override void OnUpdate()
    {
        Vector2 target = Vector2.zero;
        float targetDistance = Mathf.Infinity;
        bool flipped = false;
        
        var colliders = Physics2D.OverlapCircleAll(agent.transform.position, inSenseRadius.value);
        foreach (var collider in colliders) {
            if (collider.gameObject.tag != "Player")
            {
                continue;
            }

            foreach (var offset in inAttackOffsets.value)
            {
                var targetCandidate = (Vector2)collider.transform.position + offset;
                float dist = Vector3.Distance(targetCandidate, agent.transform.position);
                if (dist < targetDistance)
                {
                    target = targetCandidate;
                    targetDistance = dist;
                    flipped = collider.transform.position.x < agent.transform.position.x;
                }
            }
        }

        if (targetDistance < Mathf.Infinity) {
            outHasTarget.value = true;
            outTargetPosition.value = target;
            outTargetVelocity.value = Vector2.zero;
            outFlipped.value = flipped;
            EndAction(true);
        } else {
            outHasTarget.value = false;
            EndAction(false);
        }
    }
}
