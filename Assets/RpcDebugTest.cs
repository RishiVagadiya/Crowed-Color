using Unity.Netcode;
using UnityEngine;

public class RpcDebugTest : NetworkBehaviour
{
    [ServerRpc]
    public void MyTestServerRpc(int a, string b, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[SERVER] RPC Received. Param Count: 2 | Values: a={a}, b={b}");
        // यहां पर parameter count manually match करा सकते हो
        if (CheckParamCount(2, new object[] { a, b }) == false)
        {
            Debug.LogError("[SERVER] ❌ Parameter count mismatch detected!");
        }
    }

    [ClientRpc]
    public void MyTestClientRpc(int a, string b, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[CLIENT] RPC Received. Param Count: 2 | Values: a={a}, b={b}");
        if (CheckParamCount(2, new object[] { a, b }) == false)
        {
            Debug.LogError("[CLIENT] ❌ Parameter count mismatch detected!");
        }
    }

    private bool CheckParamCount(int expected, object[] parameters)
    {
        return parameters.Length == expected;
    }

    // Example: RPC को ट्रिगर करने के लिए
    private void Update()
    {
        if (IsOwner && Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("[CLIENT] Sending ServerRpc with 2 params...");
            MyTestServerRpc(10, "Hello");
        }
    }
}
