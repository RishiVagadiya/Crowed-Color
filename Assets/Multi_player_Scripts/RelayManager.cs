
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using System.Collections.Generic;
using System.IO;

public class RelayManager : MonoBehaviour
{
    [Header("Relay Manager UI Panels")]
    public GameObject[] panels; 
    public GameObject Main_Obj, Code_Text, PlayerObject;
    [SerializeField] Button HostButton;
    [SerializeField] Button JoinButton, Ready_Button, Start_button;
    [SerializeField] TMP_InputField joinInput;
    [SerializeField] TextMeshProUGUI CodeText;
    [SerializeField] TextMeshProUGUI playerListText;
    [SerializeField] TextMeshProUGUI messageText;

    [Header("Player Name UI")]
    public GameObject namePanel;            
    public TMP_InputField nameInput;        
    public Button confirmButton;            

    private static List<string> playerNames = new List<string>();
    private string savedPlayerName = "";
    private string saveFilePath;

    [System.Serializable]
    public class PlayerData
    {
        public string playerName;
    }

    async void Start()
    {
        PlayerObject.GetComponent<PLayer_Controller>().enabled = false;
        saveFilePath = Path.Combine(Application.persistentDataPath, "playerdata.json");

        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            PlayerData data = JsonUtility.FromJson<PlayerData>(json);
            savedPlayerName = data.playerName;
            Debug.Log("Loaded Player Name: " + savedPlayerName);
            StartRelaySetup();
        }
        else
        {
            namePanel.SetActive(true);
            Main_Obj.SetActive(false);
            confirmButton.onClick.AddListener(SaveNameAndStartRelay);
        }
    }

    public PLayer_Controller PLayer_Controller;

    void SaveNameAndStartRelay()
    {
        if (!string.IsNullOrEmpty(nameInput.text))
        {
            savedPlayerName = nameInput.text;
            PlayerData data = new PlayerData { playerName = savedPlayerName };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(saveFilePath, json);

            Debug.Log("Player Name Saved: " + savedPlayerName);

            namePanel.SetActive(false);
            StartRelaySetup();
        }
        else
        {
            Debug.LogWarning("Name field is empty!");
        }
        Start_button.GetComponent<Button>().interactable = false;
    }

    async void StartRelaySetup()
    {
        ShowPanel(0);
        Main_Obj.SetActive(true);
        Code_Text.SetActive(false);



        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        HostButton.onClick.AddListener(CreatRelay);
        JoinButton.onClick.AddListener(() => JoinRelay(joinInput.text));

        JoinButton.interactable = false;

        if (Ready_Button != null)
        {
            Ready_Button.interactable = false;
            Ready_Button.gameObject.SetActive(false);
            Ready_Button.onClick.AddListener(OnReadyClicked);
        }

        joinInput.onValueChanged.AddListener(OnJoinCodeChanged);
        
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnJoinCodeChanged(string input)
    {
        if (input.Length == 6)
        {
            JoinButton.interactable = true;
            messageText.text = "";
        }
        else
        {
            JoinButton.interactable = false;
            if (input.Length > 0)
                messageText.text = "Please, Enter Invalid Code";
            else
                messageText.text = "";
        }
    }

    public async void CreatRelay()
    {
        ShowPanel(3);
        Code_Text.SetActive(true);
        Code_Text.GetComponent<Animation>().Play("Code_text");

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(2);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        CodeText.text = "Code: " + joinCode;

        var relayServerData = new RelayServerData(allocation, "dtls");
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        NetworkManager.Singleton.StartHost();

        AddPlayer(savedPlayerName);
    }

    public async void JoinRelay(string joinCode)
    {
        if (joinCode.Length != 6)
        {
            messageText.text = "Please, Enter Invalid Code";
            return;
        }

        var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        var relayServerData = new RelayServerData(joinAllocation, "dtls");

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
        NetworkManager.Singleton.StartClient();
    }

    public void ShowPanel(int index)
    {
        for (int i = 0; i < panels.Length; i++)
            panels[i].SetActive(i == index);
    }
    public void Hide_Panel()
    {
        for (int i = 0; i < panels.Length; i++)
            panels[i].SetActive(false);
    }

    public void OnClientConnected(ulong clientId)
    {
        UpdatePlayerListUI();
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SendNameToServerRpc(savedPlayerName);
        }
        Hide_Panel();
    }

    public void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            playerNames.RemoveAll(name => name.Contains($"(ID:{clientId})"));
            UpdatePlayerListClientRpc(playerNames.ToArray());
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendNameToServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        string nameWithId = $"{playerName} (ID:{rpcParams.Receive.SenderClientId})";

        if (!playerNames.Contains(nameWithId))
        {
            playerNames.Add(nameWithId);
        }

        UpdatePlayerListClientRpc(playerNames.ToArray());
    }

    [ClientRpc]
    public void UpdatePlayerListClientRpc(string[] names)
    {
        playerNames = new List<string>(names);
        UpdatePlayerListUI();
    }

    public void UpdatePlayerListUI()
    {
        if (playerListText != null)
            playerListText.text = "Connected Devices:\\n" + string.Join("\\n", playerNames);
    }

    public void AddPlayer(string name)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            string nameWithId = $"{name} (ID:{NetworkManager.Singleton.LocalClientId})";
            if (!playerNames.Contains(nameWithId))
            {
                playerNames.Add(nameWithId);
                UpdatePlayerListClientRpc(playerNames.ToArray());
            }
        }
    }

    public void OnReadyClicked()
    {
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            SendReadyToHostServerRpc();
            Ready_Button.interactable = false;
             Start_button.GetComponent<Button>().interactable = true;
        }
    }

    public void OnStartClicked() => Main_Obj.SetActive(false);

    [ServerRpc(RequireOwnership = false)]
    private void SendReadyToHostServerRpc(ServerRpcParams rpcParams = default)
    {
        Hide_Panel();
        if (NetworkManager.Singleton.IsServer)
        {
            Start_button.GetComponent<Button>().interactable = true;
        }
    }

    public void Click_Multi_p_Button() => ShowPanel(1); 
    public void Click_join_With_BTN() => ShowPanel(2);
    public void Tap_Cancel() => ShowPanel(0);
    public void Tap_Quit() => Application.Quit();
    public void ClickHost_ST_BTN() => ShowPanel(4);
    public void StartButtonClick()
    {
    if (PlayerObject != null && PlayerObject.GetComponent<NetworkObject>().IsOwner)
    {
        PlayerObject.GetComponent<PLayer_Controller>().enabled = true;
    }
}
}


