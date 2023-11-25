using NodeCanvas.BehaviourTrees;
using Unity.Netcode;

public class BehaviorTreesOnServerOnly : NetworkBehaviour
{
    private BehaviourTreeOwner treeOwner; 
    
    public override void OnNetworkSpawn()
    {
        treeOwner = GetComponent<BehaviourTreeOwner>();
        treeOwner.enabled = IsServer;
    }
}
