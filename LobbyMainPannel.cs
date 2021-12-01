using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Collections;


using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class LobbyMainPannel : MonoBehaviourPunCallbacks
{
    private byte _faceSelection = 0;
    private byte _headupSlection = 0;

    // The sprites of skins provided
    private List<Sprite> _faceSprites;
    private List<Sprite> _headupSprites;

    [Header("Visual references to skin selection")]
    public Image FaceSelectedDisplay;
    public Image HeadupSelectedDisplay;

    public static LobbyMainPannel Instance;

    [Header("Login Panel")]
    //登录面板包含玩家取名、登录、退出
    public GameObject LoginPanel;

    public TMP_InputField PlayerNameInput;
    
    [Header("Selection Panel")]
    // 选择面板包含“创建房间”、“进入随机房间”、“房间列表”、“返回”选项
    public GameObject SelectionPanel;

    [Header("Create Room Panel")]
    public GameObject CreateRoomPanel;

    public TMP_InputField RoomNameInputField;

    [Header("Join Random Room Panel")]
    public GameObject JoinRandomRoomPanel;

    [Header("Room List Panel")]
    public GameObject RoomListPanel;

    public GameObject RoomListContent;
    public GameObject RoomListEntryPrefab;

    [Header("Inside Room Panel")]
    public GameObject InsideRoomPanel;
    public TextMeshProUGUI RoomNameTMP;
    public GameObject PlayerListBoardImage;
    public TextMeshProUGUI WarningTMP;

    public Button StartGameButton;
    public GameObject PlayerListEntryPrefab;

    private Dictionary<string, RoomInfo> _cachedRoomList;
    private Dictionary<string, GameObject> _roomListEntries;
    private Dictionary<int, GameObject> _playerListEntries;

    [Header("Characters")]
    public Image[] CharacterImage;
    public Image[] UserImgae;
    public Button[] ChooseButton;
    public Image[] JudgeIfCanChoose;
    public Sprite[] CharacterAndBotany;
    #region UNITY

    public void Awake()
    {
        Debug.LogFormat("Send rate = {0}, SerializationRate = {1}", PhotonNetwork.SendRate, PhotonNetwork.SerializationRate);

        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 20;

        if (Instance == null)
        {
            Instance = this;
        }

        _cachedRoomList = new Dictionary<string, RoomInfo>();
        _roomListEntries = new Dictionary<string, GameObject>();

        PlayerNameInput.text = "玩家" + Random.Range(1000, 10000);

        _faceSprites = GlobalSettings.Instance.HumanFaceSprites;
       // _headupSprites = GlobalSettings.Instance.HeadupSprites;
}

    public override void OnEnable()
    {
        base.OnEnable();
        if (PhotonNetwork.InRoom)
        {
            OnJoinedRoom();
            //LobbyMainPannel.Instance.SetActivePanel(LobbyMainPannel.Instance.InsideRoomPanel.name);
        }
        Debug.Log(this.name + "OnEnable");
    }

    #endregion


    #region PUN CALLBACKS

    public override void OnConnectedToMaster()
    {
        this.SetActivePanel(SelectionPanel.name);
        Debug.Log("ping: " + PhotonNetwork.GetPing());
        Debug.LogFormat("Send rate = {0}, SerializationRate = {1}", PhotonNetwork.SendRate, PhotonNetwork.SerializationRate);

    }


    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        ClearRoomListView();

        UpdateCachedRoomList(roomList);
        UpdateRoomListView();
    }


    public override void OnLeftLobby()
    {
        _cachedRoomList.Clear();

        ClearRoomListView();
    }


    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetActivePanel(SelectionPanel.name);
    }


    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SetActivePanel(SelectionPanel.name);
    }


    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        string roomName = "Room" + Random.Range(1000, 10000);

        RoomOptions options = new RoomOptions { MaxPlayers = GlobalSettings.MAX_PLAYERS };

        PhotonNetwork.CreateRoom(roomName, options, null);
    }


    public override void OnJoinedRoom()
    {
        Debug.Log(PhotonNetwork.LocalPlayer.NickName + " joined room.");

        if (PhotonNetwork.PlayerList.Length < GlobalSettings.MAX_PLAYERS)
        {
            PhotonNetwork.CurrentRoom.IsOpen = true;
        }

        SetActivePanel(InsideRoomPanel.name);

        RoomNameTMP.text = "房名: " + PhotonNetwork.CurrentRoom.Name; 

        if (_playerListEntries == null)
        {
            _playerListEntries = new Dictionary<int, GameObject>();
        }
        int[] tempSkinArr = new int[8];

        foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
        {

            GameObject entry = Instantiate(PlayerListEntryPrefab);
            //entry.transform.SetParent(InsideRoomPanel.transform);
            entry.transform.SetParent(PlayerListBoardImage.transform);
            entry.transform.localScale = Vector3.one;
            entry.GetComponent<PlayerListEntry>().Initialize(player.ActorNumber, player.NickName);

            // 进房获取房内已有玩家的准备状态
            object isPlayerReady;
            if (player.CustomProperties.TryGetValue(GlobalSettings.PLAYER_READY, out isPlayerReady))
            {
                entry.GetComponent<PlayerListEntry>().SetPlayerReady((bool)isPlayerReady);
            }

            // 进房获取房内已有玩家的皮肤选择
            if (player.CustomProperties.TryGetValue(GlobalSettings.FACE_SELECTION, out object selection))
            {
                entry.GetComponent<PlayerListEntry>().SetColorImage((byte)selection);
                tempSkinArr[(byte)selection] = 1;
            }

            _playerListEntries.Add(player.ActorNumber, entry);
        }

        StartGameButton.gameObject.SetActive(CheckPlayersReady());

        Hashtable props = new Hashtable
        {
            { GlobalSettings.PLAYER_LOADED_LEVEL, false}
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // read the previous record of skin selection
        //if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(GlobalSettings.FACE_SELECTION, out object faceSelection ))
        //{
        //    _faceSelection = (byte)faceSelection;
        //}
        //else
        //{
            for (int i = 0; i < tempSkinArr.Length; i++)
            {
                if (tempSkinArr[i] != 1)
                {
                    _faceSelection = (byte)i;
                    tempSkinArr[i] = 1;
                    break;
                }
            }
            
        //}
        // display selected visual
        FaceSelectedDisplay.sprite = CharacterAndBotany[_faceSelection];

        //if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(GlobalSettings.HEADUP_SELECTION, out object headupSelection)) 
        //{
        //    _headupSlection = (byte)headupSelection;

        //}
        //else
        //{
        //    _headupSlection = 0;
        //}
        // display selected visual
        //HeadupSelectedDisplay.sprite = _headupSprites[_headupSlection];
        // write to custom props
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { GlobalSettings.FACE_SELECTION, _faceSelection } });
        //PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { GlobalSettings.HEADUP_SELECTION, _headupSlection} });
    }


    public override void OnLeftRoom()
    {
        SetActivePanel(SelectionPanel.name);

        foreach (GameObject entry in _playerListEntries.Values)
        {
            Destroy(entry.gameObject);
        }

        _playerListEntries.Clear();
        _playerListEntries = null;
    }


    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Debug.Log(PhotonNetwork.LocalPlayer.NickName + " entered room.");

        GameObject entry = Instantiate(PlayerListEntryPrefab);
        //entry.transform.SetParent(InsideRoomPanel.transform);
        entry.transform.SetParent(PlayerListBoardImage.transform);
        entry.transform.localScale = Vector3.one;
        entry.GetComponent<PlayerListEntry>().Initialize(newPlayer.ActorNumber, newPlayer.NickName);

        _playerListEntries.Add(newPlayer.ActorNumber, entry);

        StartGameButton.gameObject.SetActive(CheckPlayersReady());
    }


    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Destroy(_playerListEntries[otherPlayer.ActorNumber].gameObject);
        _playerListEntries.Remove(otherPlayer.ActorNumber);

        StartGameButton.gameObject.SetActive(CheckPlayersReady());

        if (PhotonNetwork.PlayerList.Length < GlobalSettings.MAX_PLAYERS)
        {
            PhotonNetwork.CurrentRoom.IsOpen = true;
        }
    }


    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber == newMasterClient.ActorNumber)
        {
            StartGameButton.gameObject.SetActive(CheckPlayersReady());
        }
    }


    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
    {
        Debug.Log("changedPropertis" + changedProps.ToString());
        object isLocalPlayerReady;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(GlobalSettings.PLAYER_READY, out isLocalPlayerReady))
        {
            if ((bool)isLocalPlayerReady)
            {
                PhotonNetwork.AutomaticallySyncScene = true;
            }
        }

        if (_playerListEntries == null)
        {
            _playerListEntries = new Dictionary<int, GameObject>();
        }

        GameObject entry;
        if (_playerListEntries.TryGetValue(targetPlayer.ActorNumber, out entry))
        {
            object isPlayerReady;
            if (changedProps.TryGetValue(GlobalSettings.PLAYER_READY, out isPlayerReady))
            {
                if (isPlayerReady != null)
                {
                    entry.GetComponent<PlayerListEntry>().SetPlayerReady((bool)isPlayerReady);
                }
            }

            // 皮肤选择发生变化时，更新房间中PlayerListEntry的颜色
            if (changedProps.TryGetValue(GlobalSettings.FACE_SELECTION, out object faceSelection))
            {
                if (faceSelection != null)
                {
                    Debug.Log("Entry颜色更新：Selection " + faceSelection);
                    entry.GetComponent<PlayerListEntry>().SetColorImage((byte)faceSelection);
                    foreach (Button btn in ChooseButton)
                    {
                        btn.enabled = true;
                    }

                    foreach (Image img in JudgeIfCanChoose)
                    {
                        img.enabled = false;
                    }

                    foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
                    {
                        object index;
                        if (player.CustomProperties.TryGetValue(GlobalSettings.FACE_SELECTION, out index)){ 
                            ChooseButton[(byte)index].enabled = false;
                            JudgeIfCanChoose[(byte)index].enabled = true;
                        }
                    }
                }
            }
        }

        StartGameButton.gameObject.SetActive(CheckPlayersReady());
    }

    #endregion


    #region UI CALLBACKS

    public void OnBackToLoginPanelButtonClicked()
    {
        PhotonNetwork.Disconnect();
        SetActivePanel(LoginPanel.name);
    }


    public void OnBackToSelectionPanelButtonClicked()
    {
        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.LeaveLobby();
        }

        SetActivePanel(SelectionPanel.name);
    }


    public void OnCreateRoomButtonClicked()
    {
        string roomName = RoomNameInputField.text;
        // give a random roomname if it is empty.
        roomName = (roomName.Equals(string.Empty)) ? "Room " + Random.Range(1000, 10000) : roomName;

        byte maxPlayers = GlobalSettings.MAX_PLAYERS;

        //RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers, PlayerTtl = 10000 };
        //RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers, PlayerTtl = 1000 };
        //RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers, PlayerTtl = -1 };
        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers, PlayerTtl = 0 };

        PhotonNetwork.CreateRoom(roomName, options, null);
    }


    public void OnExitButtonClicked()
    {
        Application.Quit();
    }


    public void OnJoinRandomRoomButtonClicked()
    {
        SetActivePanel(JoinRandomRoomPanel.name);

        PhotonNetwork.JoinRandomRoom();
    }


    public void OnLeaveGameButtonClicked()
    {
        PhotonNetwork.LeaveRoom();
    }


    public void OnLoginButtonClicked()
    {
        string playerName = PlayerNameInput.text;

        if (!playerName.Equals(""))
        {
            PhotonNetwork.LocalPlayer.NickName = playerName;
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            Debug.LogError("Player Name is invalid.");
        }
    }


    public void OnRoomListButtonClicked()
    {
        if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
        }

        SetActivePanel(RoomListPanel.name);
    }


    /// <summary>
    /// 开始游戏
    /// </summary>
    public void OnStartGameButtonClicked()
    {
        if (PhotonNetwork.PlayerList.Length < GlobalSettings.MAX_PLAYERS && !GMTool.Instance.IsUnlimitedPlayerToggle.isOn)
        {
            Debug.Log("Players numbers not enough. Need " + GlobalSettings.MAX_PLAYERS + " players.");
            StartCoroutine(ShowWarningInRoom(3.0f, "未满" + GlobalSettings.MAX_PLAYERS + "人无法开始"));
            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = false;
        // TODO: room filter to hide or display game started room
        //PhotonNetwork.CurrentRoom.IsVisible = false;

        // N.B. 1 SCENE FOR LOBBY, the others for level
        int levelScenesAmount = SceneManager.sceneCountInBuildSettings - 2;
        System.Random rnd = new System.Random();
        int levelIndex = rnd.Next(1, levelScenesAmount);

        // This game level should be different from the last one.
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GlobalSettings.PREVIOUS_GAME_LEVEL, out object previousGameLevelIndex))
        {
            while ((int)previousGameLevelIndex == levelIndex)
            {
                levelIndex = rnd.Next(1, levelScenesAmount + 1);
            }
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { GlobalSettings.PREVIOUS_GAME_LEVEL, levelIndex } });
        PhotonNetwork.LoadLevel("BigLevelScene " + levelIndex.ToString());
        // TEST ONLY!
        //PhotonNetwork.LoadLevel("BigLevelScene 1");
        //PhotonNetwork.LoadLevel("BigLevelScene 2");
    }


    /// <summary>
    /// 点击颜色按钮选择皮肤
    /// </summary>
    public void OnFaceButtonClicked()
    {
          //读点击到的Button的名字，所得到的数字减1即为_faceSelection
          byte.TryParse(EventSystem.current.currentSelectedGameObject.name, out byte originalIndex);
          _faceSelection = (byte)(originalIndex - 1);
          FaceSelectedDisplay.sprite = CharacterAndBotany[_faceSelection];
          
          PhotonNetwork.LocalPlayer.SetCustomProperties
            (new Hashtable { { GlobalSettings.FACE_SELECTION, _faceSelection } });
    }


    public void OnFaceChooseNextButtonClicked()
    {
        // _faceSelection from 0 to the amount of faces provided.
        if (_faceSelection < _faceSprites.Count - 1)
        {
            _faceSelection++;
        }
        else
        {
            _faceSelection = 0;
        }

        // display selected visual
        FaceSelectedDisplay.sprite = CharacterAndBotany[_faceSelection];

        PhotonNetwork.LocalPlayer.SetCustomProperties
            (new Hashtable { { GlobalSettings.FACE_SELECTION, _faceSelection } });
    }


    public void OnFaceChoosePreviousButtonClicked()
    {
        if (_faceSelection > 0)
        {
            _faceSelection--;
        }
        else
        {
            _faceSelection = (byte)(_faceSprites.Count - 1);
        }

        // display selected visual
        FaceSelectedDisplay.sprite = CharacterAndBotany[_faceSelection];

        PhotonNetwork.LocalPlayer.SetCustomProperties
            (new Hashtable { { GlobalSettings.FACE_SELECTION, _faceSelection } });
    }


    public void OnHeadupChooseNextButtonClicked()
    {
        if (_headupSlection < _headupSprites.Count - 1)
        {
            _headupSlection++;
        }
        else
        {
            _headupSlection = 0;
        }

        // display selected visual
        HeadupSelectedDisplay.sprite = _headupSprites[_headupSlection];

        PhotonNetwork.LocalPlayer.SetCustomProperties
            (new Hashtable { { GlobalSettings.HEADUP_SELECTION, _headupSlection } });
    }


    public void OnHeadupChoosePreviousButtonClicked()
    {
        if (_headupSlection > 0)
        {
            _headupSlection--;
        }
        else
        {
            _headupSlection = (byte)(_headupSprites.Count - 1);
        }

        // display selected visual
        HeadupSelectedDisplay.sprite = _headupSprites[_headupSlection];

        PhotonNetwork.LocalPlayer.SetCustomProperties
            (new Hashtable { { GlobalSettings.HEADUP_SELECTION, _headupSlection } });
    }

    #endregion


    /// <summary>
    /// Check whether every player in the room is ready.
    /// </summary>
    /// <returns></returns>
    private bool CheckPlayersReady()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return false;
        }

        foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
        {
            object isPlayerReady;
            if (player.CustomProperties.TryGetValue(GlobalSettings.PLAYER_READY, out isPlayerReady))
            {
                if (!(bool)isPlayerReady)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private void ClearRoomListView()
    {
        foreach (GameObject entry in _roomListEntries.Values)
        {
            Destroy(entry.gameObject);
        }

        _roomListEntries.Clear();
    }


    public void LocalPlayerPropertiesUpdated()
    {
        StartGameButton.gameObject.SetActive(CheckPlayersReady());
    }

    //private void SetActivePanel(string activePanel)
    public void SetActivePanel(string activePanel)
    {
        LoginPanel.SetActive(activePanel.Equals(LoginPanel.name));
        SelectionPanel.SetActive(activePanel.Equals(SelectionPanel.name));
        CreateRoomPanel.SetActive(activePanel.Equals(CreateRoomPanel.name));
        JoinRandomRoomPanel.SetActive(activePanel.Equals(JoinRandomRoomPanel.name));
        RoomListPanel.SetActive(activePanel.Equals(RoomListPanel.name));
        InsideRoomPanel.SetActive(activePanel.Equals(InsideRoomPanel.name));
    }


    private void UpdateCachedRoomList(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            // Remove room from cached room list if it got closed, invisible or marked as removed.
            if (!info.IsOpen || !info.IsVisible || info.RemovedFromList)
            {
                if (_cachedRoomList.ContainsKey(info.Name))
                {
                    _cachedRoomList.Remove(info.Name);
                }

                continue;
            }

            // Update cached room info
            if (_cachedRoomList.ContainsKey(info.Name))
            {
                _cachedRoomList[info.Name] = info;
            }
            // Add new room info to cache
            else
            {
                _cachedRoomList.Add(info.Name, info);
            }
        }
    }


    private void UpdateRoomListView()
    {
        foreach (RoomInfo info in _cachedRoomList.Values)
        {
            GameObject entry = Instantiate(RoomListEntryPrefab);
            entry.transform.SetParent(RoomListContent.transform);
            entry.transform.localScale = Vector3.one;
            entry.GetComponent<RoomListEntry>().Initialize(info.Name, (byte)info.PlayerCount, info.MaxPlayers);

            _roomListEntries.Add(info.Name, entry);
        }
    }


    /// <summary>
    /// Show warnning text.Only used in room.
    /// </summary>
    /// <param name="remainingTime"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    IEnumerator ShowWarningInRoom(float remainingTime, string content)
    {
        WarningTMP.text = content;
        Debug.Log("content is:" + content);
        yield return new WaitForSeconds(remainingTime);
        WarningTMP.text = string.Empty;
    }

}