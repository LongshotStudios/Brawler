using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionLayerManager : MonoBehaviour
{
    static private InteractionLayerManager _instance;
    static public InteractionLayerManager instance {
        get {
            if (_instance == null) {
                _instance = FindObjectOfType<InteractionLayerManager>();
            }
            return _instance;
        }
    }

    private int[] layers;
    public int orderToLayer(int order)
    {
        return layers[order];
    }

    private void Awake()
    {
        layers = new int[6];
        layers[0] = LayerMask.NameToLayer("Interaction 0");
        layers[1] = LayerMask.NameToLayer("Interaction 1");
        layers[2] = LayerMask.NameToLayer("Interaction 2");
        layers[3] = LayerMask.NameToLayer("Interaction 3");
        layers[4] = LayerMask.NameToLayer("Interaction 4");
        layers[5] = LayerMask.NameToLayer("Interaction 5");
    }
    
    public int DetermineOrder(Vector3 position)
    {
        var offset = Mathf.Clamp(position.y - transform.position.y, 0f, 5.9f);
        return Mathf.FloorToInt(offset);
    }
}
