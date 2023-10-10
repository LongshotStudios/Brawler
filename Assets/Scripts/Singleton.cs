using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    static protected T _instance;
    static public T instance {
        get {
            if (_instance == null) {
                var instances = FindObjectsOfType<T>();
                if (instances.Length > 0) {
                    if (instances.Length > 1) {
                        Debug.LogError("Found too many instances of type " + typeof(T));
                    }
                    _instance = instances[0];
/* 
                } else if (instances.Length < 1) {
                    var go = new GameObject(typeof(T).ToString());
                    var co = go.AddComponent<T>();
                    _instance = co;
*/
                }
            }
            return _instance;
        }
    }
}
