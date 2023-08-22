using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderOrderManager : Singleton<RenderOrderManager>
{
    private List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    
    public void Subscribe(SpriteRenderer renderer)
    {
        renderers.Add(renderer);
    }

    public void Unsubscribe(SpriteRenderer renderer)
    {
        renderers.Remove(renderer);
    }

    private void LateUpdate()
    {
        renderers.Sort((renderer1, renderer2) => {
            return (int)Mathf.Sign(renderer2.transform.position.y - renderer1.transform.position.y);
        });
        for (int i = 0; i < renderers.Count; i++) {
            renderers[i].sortingOrder = i;
        }
    }
}
