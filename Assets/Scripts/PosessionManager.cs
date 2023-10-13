using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PosessionManager : MonoBehaviour
{
    static private PosessionManager _instance;

    static public PosessionManager instance
    {
        get {
            if (_instance == null) {
                _instance = FindObjectOfType<PosessionManager>();
            }
            return _instance;
        }
    }
    
    public PlayerControl2D[] posessables;
    private int nextPosessable = 0;

    public int GetNextPosessable()
    {
        if (nextPosessable < posessables.Length)
        {
            nextPosessable++;
            return nextPosessable - 1;
        }
        else
        {
            return -1;
        }
    }
}
