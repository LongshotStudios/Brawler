using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class RenderOrderSubscriber : MonoBehaviour
{
    private void Start()
    {
        RenderOrderManager.instance.Subscribe(GetComponent<SpriteRenderer>());
    }

    private void OnDestroy()
    {
        if (RenderOrderManager.instance) {
            RenderOrderManager.instance.Unsubscribe(GetComponent<SpriteRenderer>());
        }
    }
}
