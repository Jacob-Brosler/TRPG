﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The events that could cause an effect to trigger
/// </summary>
public enum EffectTriggers
{
    FallBelow25Percent,
    FallBelow50Percent,
    RiseAbove25Percent,
    RiseAbove50Percent,
    TakeDamage,
    DealDamage,
    TakePhysicalDamage,
    LeavePhysicalDamage,
    TakeMagicDamage,
    LeaveMagicDamage,
    BasicAttack,
    SpellCast,
    GettingHealed,
    Healing,
    StartOfMatch,
    EndOfMatch
}

/// <summary>
/// Stores a possible enemy move
/// </summary>
public struct EnemyMove
{
    public Vector2Int movePosition;
    public Vector2Int attackPosition;
    public int priority;
    public int reasonPriority;
    public bool xFirst;

    public EnemyMove(int x, int y, int p, int rP, bool xF)
    {
        movePosition = new Vector2Int(x, y);
        attackPosition = new Vector2Int(-1, -1);
        priority = p;
        reasonPriority = rP;
        xFirst = xF;
    }
    public EnemyMove(int x, int y, int aX, int aY, int p, int rP, bool xF)
    {
        movePosition = new Vector2Int(x, y);
        attackPosition = new Vector2Int(aX, aY);
        priority = p;
        reasonPriority = rP;
        xFirst = xF;
    }

    /// <summary>
    /// Determines which move has a higher priority
    /// </summary>
    /// <param name="m">The move to check this one against</param>
    public int CompareTo(EnemyMove m)
    {
        if (priority > m.priority)
        {
            return -1;
        }
        else if (priority < m.priority)
        {
            return 1;
        }
        else
        {
            if (reasonPriority > m.reasonPriority)
            {
                return -1;
            }
            else if (reasonPriority < m.reasonPriority)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }
}

/// <summary>
/// Stores basic animation data
/// </summary>
public struct MoveQueue
{
    public int entityID;
    public Vector2 relativeMove;

    public MoveQueue(int e, Vector2 relativeMoveCoords)
    {
        entityID = e;
        relativeMove = relativeMoveCoords;
    }
}

public enum BattleState
{
    None,
    BattleSetup,
    Swap,
    Player,
    MovePlayer,
    Attack,
    Enemy,
    MoveEnemy,
    ReturnCamera
}

public class Battle : MonoBehaviour
{
    //Prefabs
    public GameObject EnemyBattleModelPrefab;
    public GameObject PlayerBattleModelPrefab;
    public GameObject MoveMarkerPrefab;
    public GameObject BattleUIPrefab;
    public GameObject CameraPrefab;
    public GameObject skillCastConfirmMenu;

    public GameObject battleTile;
    public Vector2Int topLeft;

    public bool showDanger = false;
    public bool showaEther = false;

    //Battle state for the finite state machine
    public static BattleState battleState = BattleState.None;
    //Whether or not the players can swap positions, only true if no one has moved yet
    public bool canSwap;

    //Declares the map size, unchanged post initialization. Default is 20x20, camera will not change view to accomidate larger currently
    int mapSizeX;
    int mapSizeY;
    //Affects what the enemies take into account when making their moves, see MoveEnemies() for more information
    int difficulty;

    //Stores the physical tiles generated in the world to detect and interpret player input
    GameObject[,] tileList;
    //Stores the data representation of the current chunk of the world, dictates where participants can move
    int[,] battleMap;
    //Stores the aEther levels of the area, slot 0 = current level, slot 1 = max level
    int[,,] aEtherMap;
    //Stores the enemy data
    public Enemy[] enemyList;
    //Stores the visual representation of the participants
    public GameObject[] playerModels = new GameObject[4];
    public GameObject[] enemyModels = new GameObject[4];
    //This is a camera
    private GameObject battleCamera;
    public GameObject mapPlayer;

    //-1 means nothing selected
    public int selectedPlayer = -1;
    public int selectedEnemy = -1;
    public int hoveredSpell = -1;
    public int selectedSpell = -1;
    private int turn = 1;
    public Vector2Int selectedMoveSpot = new Vector2Int(-1, -1);
    //This displays how the pawn would move when a move is selected
    public GameObject moveMarker;

    //Stores where the pieces need to be moved to to match up with where they need to be
    private List<MoveQueue> playerAnimMoves = new List<MoveQueue>();
    private List<MoveQueue> enemyAnimMoves = new List<MoveQueue>();
    //How fast the pieces move
    public float animSpeed = 0.1f;

    public int movingEnemy;
    private EnemyMove chosenMove;
    private bool moveXFirst;

    //The initial and final positions for animations involving the camera
    private Vector3 cameraInitPos;
    private Quaternion cameraInitRot;
    private Vector3 cameraFinalPos;
    private Quaternion cameraFinalRot;

    //Whether a change has been made that would affect the states of one or more tiles
    //Keeps updateTiles from being called every frame
    private bool updateTilesThisFrame = false;

    // Use this for initialization
    void Awake()
    {
        Registry.FillRegistry();
        GameStorage.FillStorage();
        Inventory.LoadInventory();
    }

    /// <summary>
    /// Sets up all of the variables and prefabs needed during the battle
    /// </summary>
    /// <param name="centerX">Center x position of the board</param>
    /// <param name="centerY">Center y position of the board</param>
    /// <param name="mainCamera">The current main camera, most likely the over-the-shoulder camera on the player</param>
    /// <param name="xSize">Board width</param>
    /// <param name="ySize">Board height</param>
    public void StartBattle(int centerX, int centerY, Transform mainCamera, int xSize = 20, int ySize = 20)
    {
        //removes whatever is left of the previous battle
        ExpungeAll();
        cameraInitPos = mainCamera.position;
        cameraInitRot = mainCamera.rotation;
        //sets the map size
        mapSizeX = xSize;
        mapSizeY = ySize;
        //generates enemies
        enemyList = new Enemy[4];
        enemyList[0] = new Enemy(5, 5, 3, 5, 5);
        enemyList[1] = new Enemy(10, 5, 2, 5, 5);
        enemyList[2] = new Enemy(12, 5, 2, 5, 5);
        enemyList[3] = new Enemy(14, 5, 5, 5, 5);
        for (int i = 0; i < GameStorage.activePlayerList.Count; i++)
        {
            GameStorage.playerMasterList[GameStorage.activePlayerList[i]].position = new Vector2Int(6 + 2 * i, 10 + i % 2);
            GameStorage.playerMasterList[GameStorage.activePlayerList[i]].moved = false;
        }
        //grabs the map layout
        battleMap = GameStorage.GrabBattleMap(centerX, centerY, xSize, ySize);
        //finds the top left corner of the current map
        topLeft = new Vector2Int(GameStorage.trueBX, GameStorage.trueBY);
        //generates the visible tile map
        tileList = new GameObject[mapSizeX, mapSizeY];
        GenerateTileMap(topLeft.x, topLeft.y);
        //grabs the aEther map
        aEtherMap = GameStorage.GrabaEtherMap(topLeft.x, topLeft.y, xSize, ySize);
        //adds other things
        moveMarker = Instantiate(MoveMarkerPrefab, Vector3.zero, Quaternion.Euler(0, 0, 0));
        moveMarker.SetActive(false);
        //make the camera
        battleCamera = Instantiate(CameraPrefab);
        cameraFinalRot = battleCamera.transform.rotation;
        battleCamera.transform.SetPositionAndRotation(cameraInitPos, cameraInitRot);
        //Calculates where the camera needs to end up to frame the battle correctly
        cameraFinalPos = new Vector3(topLeft.x + (xSize / 2), 19, topLeft.y + (ySize / 2));
        battleCamera.GetComponent<Camera>().tag = "MainCamera";
        //moves the player and enemy models into their correct position
        for (int i = 0; i < GameStorage.activePlayerList.Count; i++)
        {
            playerModels[i] = Instantiate(PlayerBattleModelPrefab);
            playerModels[i].transform.position = new Vector3(GameStorage.playerMasterList[GameStorage.activePlayerList[i]].position.x + topLeft.x, 1, (mapSizeY - 1) - GameStorage.playerMasterList[GameStorage.activePlayerList[i]].position.y + topLeft.y);
        }
        for (int i = 0; i < enemyList.Length; i++)
        {
            enemyModels[i] = Instantiate(EnemyBattleModelPrefab);
            enemyModels[i].transform.position = new Vector3(enemyList[i].position.x + topLeft.x, 1, (mapSizeY - 1) - enemyList[i].position.y + topLeft.y);
        }
        skillCastConfirmMenu.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        battleState = BattleState.BattleSetup;
    }
    
    /// <summary>
    /// Update is called once per frame
    /// Controls the battle's finite state machine and player input
    /// </summary>
    void Update()
    {
        if (skillCastConfirmMenu.activeSelf == false)
        {
            switch (battleState)
            {
                case BattleState.None:
                    break;
                case BattleState.BattleSetup:
                    Debug.Log(battleCamera.transform.position + "|" + battleCamera.transform.rotation.eulerAngles);
                    battleCamera.transform.position = (Vector3.Lerp(battleCamera.transform.position, cameraFinalPos, 0.1f));
                    battleCamera.transform.rotation = (Quaternion.Lerp(battleCamera.transform.rotation, cameraFinalRot, 0.1f));
                    if (GameStorage.Approximately(battleCamera.transform.position, cameraFinalPos) && GameStorage.Approximately(battleCamera.transform.rotation, cameraFinalRot))
                    {
                        battleState = BattleState.Player;
                        battleCamera.transform.position = cameraFinalPos;
                        battleCamera.transform.rotation = cameraFinalRot;
                        //sets up the background variables
                        canSwap = true;
                    }
                    break;
                case BattleState.ReturnCamera:
                    Debug.Log(mapPlayer.GetComponentInChildren<Camera>().transform.position + "|" + mapPlayer.GetComponentInChildren<Camera>().transform.rotation.eulerAngles);
                    mapPlayer.GetComponentInChildren<Camera>().transform.position = (Vector3.Lerp(mapPlayer.GetComponentInChildren<Camera>().transform.position, cameraFinalPos, 0.1f));
                    mapPlayer.GetComponentInChildren<Camera>().transform.rotation = (Quaternion.Lerp(mapPlayer.GetComponentInChildren<Camera>().transform.rotation, cameraFinalRot, 0.1f));
                    if (GameStorage.Approximately(mapPlayer.GetComponentInChildren<Camera>().transform.position, cameraFinalPos) && GameStorage.Approximately(mapPlayer.GetComponentInChildren<Camera>().transform.rotation, cameraFinalRot))
                    {
                        battleState = BattleState.None;
                        mapPlayer.GetComponentInChildren<Camera>().transform.position = cameraFinalPos;
                        mapPlayer.GetComponentInChildren<Camera>().transform.rotation = cameraFinalRot;
                    }
                    break;
                case BattleState.MovePlayer:
                    Vector2 initPlayerPos = new Vector2(GameStorage.playerMasterList[GameStorage.activePlayerList[playerAnimMoves[0].entityID]].position.x + topLeft.x, (mapSizeY - 1) - GameStorage.playerMasterList[GameStorage.activePlayerList[playerAnimMoves[0].entityID]].position.y + topLeft.y);
                    playerModels[playerAnimMoves[0].entityID].transform.Translate(Vector2.Lerp(Vector2.zero, playerAnimMoves[0].relativeMove, animSpeed).x, 0, Vector2.Lerp(Vector2.zero, playerAnimMoves[0].relativeMove, animSpeed).y);
                    if (Mathf.Approximately(playerAnimMoves[0].relativeMove.x + initPlayerPos.x, playerModels[playerAnimMoves[0].entityID].transform.position.x) && Mathf.Approximately(playerAnimMoves[0].relativeMove.y + initPlayerPos.y, playerModels[playerAnimMoves[0].entityID].transform.position.z))
                    {
                        //moves the player and player model
                        GameStorage.playerMasterList[GameStorage.activePlayerList[playerAnimMoves[0].entityID]].position.Set(Mathf.RoundToInt(GameStorage.playerMasterList[GameStorage.activePlayerList[playerAnimMoves[0].entityID]].position.x + playerAnimMoves[0].relativeMove.x), Mathf.RoundToInt(GameStorage.playerMasterList[GameStorage.activePlayerList[playerAnimMoves[0].entityID]].position.y - playerAnimMoves[0].relativeMove.y));
                        playerAnimMoves.Remove(playerAnimMoves[0]);

                        //if the player is done moving
                        if (playerAnimMoves.Count == 0)
                        {
                            updateTilesThisFrame = true;
                            battleState = BattleState.Attack;
                        }
                    }
                    break;
                case BattleState.Enemy:
                    selectedPlayer = -1;
                    if (enemyList[movingEnemy].cHealth > 0)
                    {
                        MoveEnemies();
                        battleState = BattleState.MoveEnemy;
                    }
                    else
                    {
                        movingEnemy++;
                        if (movingEnemy >= enemyList.Length)
                            EndEnemyTurn();
                    }
                    break;
                case BattleState.MoveEnemy:
                    Vector2 initEnemyPos = new Vector2(enemyList[enemyAnimMoves[0].entityID].position.x + topLeft.x, (mapSizeY - 1) - enemyList[enemyAnimMoves[0].entityID].position.y + topLeft.y);
                    enemyModels[enemyAnimMoves[0].entityID].transform.Translate(Vector2.Lerp(Vector2.zero, enemyAnimMoves[0].relativeMove, animSpeed).x, 0, Vector2.Lerp(Vector2.zero, enemyAnimMoves[0].relativeMove, animSpeed).y);
                    if (Mathf.Approximately(enemyAnimMoves[0].relativeMove.x + initEnemyPos.x, enemyModels[enemyAnimMoves[0].entityID].transform.position.x) && Mathf.Approximately(enemyAnimMoves[0].relativeMove.y + initEnemyPos.y, enemyModels[enemyAnimMoves[0].entityID].transform.position.z))
                    {
                        //moves the enemy and enemy model
                        enemyList[enemyAnimMoves[0].entityID].position.Set(Mathf.RoundToInt(enemyList[enemyAnimMoves[0].entityID].position.x + enemyAnimMoves[0].relativeMove.x), Mathf.RoundToInt(enemyList[enemyAnimMoves[0].entityID].position.y - enemyAnimMoves[0].relativeMove.y));
                        enemyAnimMoves.Remove(enemyAnimMoves[0]);
                        if (enemyAnimMoves.Count == 0)
                        {
                            updateTilesThisFrame = true;
                            //attacks if enemy can
                            if (chosenMove.attackPosition.x != -1)
                            {
                                PerformEnemyAttack(movingEnemy, chosenMove.attackPosition.x, chosenMove.attackPosition.y);
                            }
                            if (battleState != BattleState.ReturnCamera)
                            {
                                updateTilesThisFrame = true;
                                movingEnemy++;
                                if (movingEnemy >= enemyList.Length)
                                    EndEnemyTurn();
                                else
                                    battleState = BattleState.Enemy;
                            }
                        }
                    }
                    break;
                case BattleState.Swap:
                case BattleState.Player:
                case BattleState.Attack:
                    if (Input.GetMouseButtonDown(0))
                    {
                        //Debug.Log(Input.mousePosition.x + ", " + Screen.width  + ", " + Input.mousePosition.x / Screen.width + " " + (Screen.height - Input.mousePosition.y) + ", " + Screen.height + ", " + ( 1 - Input.mousePosition.y / Screen.height));
                        Ray ray = Camera.main.ViewportPointToRay(new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, 0));
                        RaycastHit hit;
                        int layerMask = 1 << 8;
                        if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                        {
                            print("I'm looking at " + hit.transform.GetComponent<BattleTile>().arrayID);
                            SpaceInteraction(hit.transform.GetComponent<BattleTile>().arrayID);
                            updateTilesThisFrame = true;
                        }
                        else
                            print("I'm looking at nothing!");
                    }

                    if (selectedSpell != -1)
                    {
                        updateTilesThisFrame = true;
                    }
                    break;
            }

            //If anything happened that could have changed the state of one or more tiles this frame
            if (updateTilesThisFrame)
            {
                UpdateTileMap();
                for (int x = 0; x < mapSizeX; x++)
                {
                    for (int y = 0; y < mapSizeY; y++)
                    {
                        tileList[x, y].GetComponent<BattleTile>().UpdateColors();
                    }
                }
            }
            updateTilesThisFrame = false;
        }
    }

    /// <summary>
    /// Ends the enemy's turn and sets up the player's turn
    /// </summary>
    private void EndEnemyTurn()
    {
        //resets to allow players to move and starts player's turn
        for (int j = 0; j < GameStorage.activePlayerList.Count; j++)
        {
            if (GameStorage.playerMasterList[GameStorage.activePlayerList[j]].cHealth > 0 && !GameStorage.playerMasterList[GameStorage.activePlayerList[j]].statusList.Contains("sleep"))
                GameStorage.playerMasterList[GameStorage.activePlayerList[j]].moved = false;
        }
        movingEnemy = 0;
        turn++;
        foreach (int pID in GameStorage.activePlayerList)
        {
            GameStorage.playerMasterList[pID].EndOfTurn();
        }
        foreach (Enemy e in enemyList)
        {
            e.EndOfTurn();
        }
        if (battleState != BattleState.ReturnCamera)
            battleState = BattleState.Player;
    }
    
    /// <summary>
    /// Resets all variables and clears all visibles at the start and end of each battle
    /// </summary>
    private void ExpungeAll()
    {
        battleState = BattleState.None;
        for (int x = 0; x < mapSizeX; x++)
        {
            for (int y = 0; y < mapSizeY; y++)
            {
                Destroy(tileList[x, y]);
                tileList[x, y] = null;
            }
        }
        for (int x = 0; x < playerModels.Length; x++)
        {
            Destroy(playerModels[x]);
            playerModels[x] = null;
        }
        for (int x = 0; x < enemyModels.Length; x++)
        {
            Destroy(enemyModels[x]);
            enemyModels[x] = null;
        }
        Destroy(battleCamera);
        battleCamera = null;
        Destroy(moveMarker);
        moveMarker = null;
    }

    /// <summary>
    /// Generates all of the tiles at the beginning of the battle
    /// </summary>
    /// <param name="xPos">X position of the left-most tile on the board</param>
    /// <param name="yPos">Y position of the up-most tile on the board</param>
    private void GenerateTileMap(int xPos, int yPos)
    {
        for (int x = 0; x < mapSizeX; x++)
        {
            for (int y = 0; y < mapSizeY; y++)
            {
                tileList[x, y] = Instantiate(battleTile, new Vector3(xPos + x, 0.5f, yPos + y), Quaternion.Euler(0, 0, 0));
                tileList[x, y].GetComponent<BattleTile>().arrayID = new Vector2Int(x, (mapSizeY - 1) - y);
            }
        }
    }

    /// <summary>
    /// Checks to see if all of one team is dead and triggers OnBattleEnd if so
    /// </summary>
    public void CheckForDeath()
    {
        int deadCount = 0;
        for (int pID = 0; pID < GameStorage.activePlayerList.Count; pID++)
        {
            if (GameStorage.playerMasterList[GameStorage.activePlayerList[pID]].cHealth <= 0)
            {
                deadCount++;
                playerModels[pID].SetActive(false);
                GameStorage.playerMasterList[GameStorage.activePlayerList[pID]].position.Set(-200, -200);
            }
        }
        if (deadCount == GameStorage.activePlayerList.Count)
            OnBattleEnd(false);
        deadCount = 0;
        for (int e = 0; e < enemyList.Length; e++)
        {
            if (enemyList[e].cHealth <= 0)
            {
                deadCount++;
                enemyModels[e].SetActive(false);
                enemyList[e].position.Set(-200, -200);
            }
        }
        if (deadCount == enemyList.Length)
            OnBattleEnd(true);
    }

    /// <summary>
    /// Triggered when all of one team is dead
    /// On won: Sets up camera return animation, gives control back to the player and hands out winnings
    /// On loss: Breaks everything
    /// </summary>
    /// <param name="won">If the battle was won by the player or not</param>
    public void OnBattleEnd(bool won)
    {
        if (won)
        {
            foreach (int p in GameStorage.activePlayerList)
            {
                GameStorage.playerMasterList[p].GainExp(200);
            }
            Cursor.lockState = CursorLockMode.Locked;
            mapPlayer.SetActive(true);
            cameraFinalPos = mapPlayer.GetComponentInChildren<Camera>().transform.position;
            cameraFinalRot = mapPlayer.GetComponentInChildren<Camera>().transform.rotation;
            mapPlayer.GetComponentInChildren<Camera>().transform.SetPositionAndRotation(battleCamera.transform.position, battleCamera.transform.rotation);
            ExpungeAll();
            updateTilesThisFrame = false;
            battleState = BattleState.ReturnCamera;
        }
        else
        {
            battleState = BattleState.None;
            ExpungeAll();
        }
    }

    /// <summary>
    /// Toggles whether enemy ranges are shown or not
    /// </summary>
    public void ToggleDangerArea()
    {
        showDanger = !showDanger;
        updateTilesThisFrame = true;
    }

    /// <summary>
    /// Toggles whether the aEther visual representation is shown or not
    /// </summary>
    public void ToggleaEtherView()
    {
        showaEther = !showaEther;
        updateTilesThisFrame = true;
    }

    /// <summary>
    /// Toggles between swap and move at start of battle
    /// </summary>
    public void ToggleSwap()
    {
        if (canSwap)
        {
            if (battleState == BattleState.Swap)
            {
                battleState = BattleState.Player;
            }
            else
            {
                battleState = BattleState.Swap;
            }
            selectedMoveSpot = new Vector2Int(-1, -1);
            selectedEnemy = -1;
            selectedPlayer = -1;
            moveMarker.SetActive(false);
            updateTilesThisFrame = true;
        }
    }
    
    /// <summary>
    /// If player wants to end the turn before all ally pawns have been moved
    /// </summary>
    public void EndPlayerTurnEarly()
    {
        moveMarker.SetActive(false);
        canSwap = false;
        for (int j = 0; j < GameStorage.activePlayerList.Count; j++)
        {
            GameStorage.playerMasterList[GameStorage.activePlayerList[j]].moved = true;
        }
        FinishedMovingPawn();
    }

    /// <summary>
    /// Activates when a spell button starts being hovered
    /// </summary>
    public void HoveringSpell(int buttonID)
    {
        hoveredSpell = buttonID;
        updateTilesThisFrame = true;
    }
    
    /// <summary>
    /// Activates when a spell button is no longer hovered
    /// </summary>
    public void StopHoveringSpell()
    {
        hoveredSpell = -1;
        updateTilesThisFrame = true;
    }
    
    /// <summary>
    /// Called then the player selects a spell they want to try and cast from their quick cast list
    /// </summary>
    /// <param name="buttonID">The place in the spell quick list to grab the spell from</param>
    public void SelectSpell(int buttonID)
    {
        if (selectedSpell == buttonID)
            selectedSpell = -1;
        else
            selectedSpell = buttonID;
        updateTilesThisFrame = true;
    }

    /// <summary>
    /// Deals with all of the possibilities of what the player could want to do when they click on a tile
    /// </summary>
    /// <param name="pos">What tile they clicked on</param>
    private void SpaceInteraction(Vector2Int pos)
    {
        bool actionTaken = false;
        switch (battleState)
        {
            case BattleState.Swap:
                if (selectedPlayer != -1)
                {
                    int n = PlayerAtPos(pos.x, pos.y);
                    GameStorage.playerMasterList[GameStorage.activePlayerList[n]].position = GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position;
                    GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position = pos;
                    playerModels[n].transform.position = new Vector3(GameStorage.playerMasterList[GameStorage.activePlayerList[n]].position.x + topLeft.x, 1, (mapSizeY - 1) - GameStorage.playerMasterList[GameStorage.activePlayerList[n]].position.y + topLeft.y);
                    playerModels[selectedPlayer].transform.position = new Vector3(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x + topLeft.x, 1, (mapSizeY - 1) - GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y + topLeft.y);
                    selectedPlayer = -1;
                    actionTaken = true;
                }
                break;
            case BattleState.Player:
                //if player is moving something
                if (tileList[pos.x, (mapSizeY - 1) - pos.y].GetComponent<BattleTile>().playerMoveRange && !GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].moved)
                {
                    selectedMoveSpot.Set(pos.x, pos.y);
                    moveMarker.transform.position = new Vector3(pos.x + topLeft.x, 1, (mapSizeY - 1) - pos.y + topLeft.y);
                    moveMarker.SetActive(true);

                    //update the line renderer
                    moveMarker.GetComponent<LineRenderer>().SetPosition(0, Vector3.zero);
                    moveXFirst = true;
                    Vector2Int p = new Vector2Int(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x - selectedMoveSpot.x, GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y - selectedMoveSpot.y);

                    Vector2 moveDifference = new Vector2(selectedMoveSpot.x - GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, selectedMoveSpot.y - GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y);

                    for (int x = 0; x <= Mathf.Abs(moveDifference.x); x++)
                    {
                        if (!GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].ValidMoveTile(battleMap[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x + x * Mathf.RoundToInt(Mathf.Sign(moveDifference.x)), GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y]))
                            moveXFirst = false;
                        if (EnemyAtPos(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x + x * Mathf.RoundToInt(Mathf.Sign(moveDifference.x)), GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y) != -1)
                            moveXFirst = false;
                    }

                    for (int y = 0; y <= Mathf.Abs(moveDifference.y); y++)
                    {
                        if (!GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].ValidMoveTile(battleMap[selectedMoveSpot.x, GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y + y * Mathf.RoundToInt(Mathf.Sign(moveDifference.y))]))
                            moveXFirst = false;
                        if (EnemyAtPos(selectedMoveSpot.x, GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y + y * Mathf.RoundToInt(Mathf.Sign(moveDifference.y))) != -1)
                            moveXFirst = false;
                    }

                    Debug.Log("Move X First check 1 " + moveXFirst);
                    if (selectedMoveSpot.x != GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x)
                    {
                        p.x *= 2;
                    }
                    if (selectedMoveSpot.y != GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y)
                    {
                        p.y *= 2;
                    }

                    if (moveXFirst)
                    {
                        moveMarker.GetComponent<LineRenderer>().SetPosition(1, new Vector3(p.x, 0, 0));
                        moveMarker.GetComponent<LineRenderer>().SetPosition(2, new Vector3(p.x, 0, -p.y));
                    }
                    else
                    {
                        moveMarker.GetComponent<LineRenderer>().SetPosition(1, new Vector3(0, 0, -p.y));
                        moveMarker.GetComponent<LineRenderer>().SetPosition(2, new Vector3(p.x, 0, -p.y));
                    }
                    actionTaken = true;
                }
                break;
        }
        //if player tries to cast a spell
        if (selectedSpell != -1)
        {
            actionTaken = true;
            if (BattleTile.skillLegitTarget)
            {
                selectedEnemy = EnemyAtPos(pos.x, pos.y);
                //generates the choice menu
                if (selectedMoveSpot.x != -1)
                    skillCastConfirmMenu.GetComponentInChildren<Text>().text = "You have a move selected. Move and cast?";
                else
                    skillCastConfirmMenu.GetComponentInChildren<Text>().text = "Are you sure you want to cast there?";
                skillCastConfirmMenu.SetActive(true);
            }
        }
        if (!actionTaken && hoveredSpell == -1)
        {
            //selecting a different player
            if (battleState != BattleState.Attack)
            {
                selectedPlayer = PlayerAtPos(pos.x, pos.y);
                selectedMoveSpot = new Vector2Int(-1, -1);
                moveMarker.SetActive(false);
                //selectedSpell = -1;
            }

            //selecting an enemy
            selectedEnemy = EnemyAtPos(pos.x, pos.y);
            if (tileList[pos.x, (mapSizeY - 1) - pos.y].GetComponent<BattleTile>().playerAttackRange)
            {
                if (selectedEnemy == -1)
                    selectedEnemy = enemyList.Length + PlayerAtPos(pos.x, pos.y);
            }
        }
    }
    
    /// <summary>
    /// Sets up the movement animation to the position they want to go to for the player
    /// </summary>
    public void ConfirmPlayerMove()
    {
        if (selectedMoveSpot.x != -1)
        {
            if (moveXFirst)
            {
                if (!Mathf.Approximately(((mapSizeY - 1) - selectedMoveSpot.y) - playerModels[selectedPlayer].transform.position.z, 0))
                    playerAnimMoves.Add(new MoveQueue(selectedPlayer, new Vector2(0, ((mapSizeY - 1) - selectedMoveSpot.y) - playerModels[selectedPlayer].transform.position.z + topLeft.y)));
                if (!Mathf.Approximately(selectedMoveSpot.x - playerModels[selectedPlayer].transform.position.x, 0))
                    playerAnimMoves.Add(new MoveQueue(selectedPlayer, new Vector2(selectedMoveSpot.x - playerModels[selectedPlayer].transform.position.x + topLeft.x, 0)));
            }
            else
            {
                if (!Mathf.Approximately(selectedMoveSpot.x - playerModels[selectedPlayer].transform.position.x, 0))
                    playerAnimMoves.Add(new MoveQueue(selectedPlayer, new Vector2(selectedMoveSpot.x - playerModels[selectedPlayer].transform.position.x + topLeft.x, 0)));
                if (!Mathf.Approximately(((mapSizeY - 1) - selectedMoveSpot.y) - playerModels[selectedPlayer].transform.position.z, 0))
                    playerAnimMoves.Add(new MoveQueue(selectedPlayer, new Vector2(0, ((mapSizeY - 1) - selectedMoveSpot.y) - playerModels[selectedPlayer].transform.position.z + topLeft.y)));
            }
            selectedMoveSpot = new Vector2Int(-1, -1);
            moveMarker.SetActive(false);
            canSwap = false;
            battleState = BattleState.MovePlayer;
        }
    }

    /// <summary>
    /// Checks to see if the given enemy can move to the given position this turn
    /// </summary>
    /// <param name="enemyID">The ID of the enemy to check for</param>
    /// <param name="x">The X of the position to check for</param>
    /// <param name="y">The Y of the position to check for</param>
    private bool ValidEnemyMove(int enemyID, int x, int y)
    {
        //Makes sure the ending position does not overlap with an existing character
        if (PlayerAtPos(x + enemyList[enemyID].position.x, y + enemyList[enemyID].position.y) != -1)
            return false;
        if (EnemyAtPos(x + enemyList[enemyID].position.x, y + enemyList[enemyID].position.y) != -1 && !(x == 0 && y == 0))
            return false;

        bool validPath = true;
        for (int cX = 1; cX <= Mathf.Abs(x); cX++)
        {
            if (!enemyList[enemyID].ValidMoveTile(battleMap[cX * Mathf.RoundToInt(Mathf.Sign(x)) + enemyList[enemyID].position.x, enemyList[enemyID].position.y]) || PlayerAtPos(cX + enemyList[enemyID].position.x, enemyList[enemyID].position.y) != -1)
                validPath = false;
        }

        if (validPath)
        {
            for (int cY = 1; cY <= Mathf.Abs(y); cY++)
            {
                if (!enemyList[enemyID].ValidMoveTile(battleMap[x + enemyList[enemyID].position.x, cY * Mathf.RoundToInt(Mathf.Sign(y)) + enemyList[enemyID].position.y]) || PlayerAtPos(x + enemyList[enemyID].position.x, cY + enemyList[enemyID].position.y) != -1)
                    validPath = false;
            }
        }

        if (validPath)
        {
            moveXFirst = true;
        }
        else
        {
            //if invalid by x, y, check y, x
            for (int cY = 1; cY <= Mathf.Abs(y); cY++)
            {
                if (!enemyList[enemyID].ValidMoveTile(battleMap[enemyList[enemyID].position.x, cY * Mathf.RoundToInt(Mathf.Sign(y)) + enemyList[enemyID].position.y]) || PlayerAtPos(enemyList[enemyID].position.x, cY + enemyList[enemyID].position.y) != -1)
                    return false;
            }
            if (validPath)
            {
                for (int cX = 1; cX <= Mathf.Abs(x); cX++)
                {
                    if (!enemyList[enemyID].ValidMoveTile(battleMap[cX * Mathf.RoundToInt(Mathf.Sign(x)) + enemyList[enemyID].position.x, y + enemyList[enemyID].position.y]) || PlayerAtPos(cX + enemyList[enemyID].position.x, y + enemyList[enemyID].position.y) != -1)
                        return false;
                }
            }
            if (validPath)
                moveXFirst = false;
        }
        return true;
    }
    
    /// <summary>
    /// Finds the optimal move for the enemy currently moving
    /// </summary>
    private void MoveEnemies()
    {
        /*
         * Difficulty of the battles determines what variables will be taken into account (higher difficulties will also take into account lower difficulty variables):
         * 1: super easy - just moves towards and attacks nearest player
         * 2: easy - takes into account how much damage they can do, approx damage they'll take in return, and ability to be counterattacked
         * 3: medium - checks who their final position would put them near, both enemy and ally (raw number version), whether they will be near an ally healer, and whether the opponent target is a healer
         * 4: hard - starts knowing attacking can be the wrong move, takes into account blocking for more important allies(healers, ranged carries)
         * 5: why? - checks who their final position would put them near, both enemy and ally (including damage each enemy can do to them, return damage if possible)
         * 
         * Each enemy also has an aggression and a ppa(pack play value) value. These values determine the weight of the previous checks:
         * 
         * Aggression determines their likelyhood to take fights and whether they are scared of being outnumbered
         * 1-3 = cowardly: very unlikely to take fights stacked against them, prefering to hang back and pick off low health enemies
         * 4-7 = balanced: open to any situation, has qualities of both sides
         * 8-10 = aggressive: likely to challenge any enemy no matter their heath or backup
         * 
         * PPA determines how likely they are to play with the rest of their team
         * 1-3 = lone wolf: prefers to fight on their own
         * 4-7 = meh: could care less
         * 8-10 = social animal: finds strength in numbers, usually playing around and protecting others
        */

        int n = movingEnemy;
        List<EnemyMove> possibleMoves = new List<EnemyMove>();
        EnemyMove fallbackMove = new EnemyMove(0, 0, 100, 0, true);
        int maxMove = enemyList[n].GetMoveSpeed();
        WeaponType weapon;
        if (!Registry.WeaponTypeRegistry.TryGetValue(((EquippableBase)Registry.ItemRegistry[enemyList[n].equippedWeapon]).subType, out weapon))
            Debug.Log("Weapon Type does not exist in the Registry.");
        for (int x = -maxMove; x <= maxMove; x++)
        {
            for (int y = -maxMove; y <= maxMove; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= maxMove && x + enemyList[n].position.x >= 0 && x + enemyList[n].position.x < mapSizeX && y + enemyList[n].position.y >= 0 && y + enemyList[n].position.y < mapSizeY)
                {
                    //if enemy can move to this tile
                    if (ValidEnemyMove(n, x, y))
                    {
                        int priority = 0;
                        int reasonPriority = 0;

                        //checks weapon ranges
                        for (int i = 1; i <= weapon.range; i++)
                        {
                            if (PlayerAtPos(x + enemyList[n].position.x + weapon.sRange + i, y + enemyList[n].position.y) != -1)
                            {
                                if (!weapon.ranged && i > 1)
                                {
                                    bool validMelee = true;
                                    for (int j = 1; j < i; j++)
                                    {
                                        if (!enemyList[n].ValidMoveTile(battleMap[x + enemyList[n].position.x + weapon.sRange + j, y + enemyList[n].position.y]))
                                            validMelee = false;
                                    }
                                    if (validMelee)
                                        possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x + weapon.sRange + i, y + enemyList[n].position.y, maxMove * 3 - (Mathf.Abs(x + weapon.sRange + i) + Mathf.Abs(y)), 1, moveXFirst));
                                }
                                else
                                {
                                    possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x + weapon.sRange + i, y + enemyList[n].position.y, maxMove * 3 - (Mathf.Abs(x + weapon.sRange + i) + Mathf.Abs(y)), 1, moveXFirst));
                                }
                            }
                            if (PlayerAtPos(x + enemyList[n].position.x - weapon.sRange - i, y + enemyList[n].position.y) != -1)
                            {
                                if (!weapon.ranged && i > 1)
                                {
                                    bool validMelee = true;
                                    for (int j = 1; j < i; j++)
                                    {
                                        if (enemyList[n].ValidMoveTile(battleMap[x + enemyList[n].position.x - weapon.sRange - j, y + enemyList[n].position.y]))
                                            validMelee = false;
                                    }
                                    if (validMelee)
                                        possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x - weapon.sRange - i, y + enemyList[n].position.y, maxMove * 3 - (Mathf.Abs(x - weapon.sRange - i) + Mathf.Abs(y)), 1, moveXFirst));
                                }
                                else
                                {
                                    possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x - weapon.sRange - i, y + enemyList[n].position.y, maxMove * 3 - (Mathf.Abs(x - weapon.sRange - i) + Mathf.Abs(y)), 1, moveXFirst));
                                }
                            }
                            if (PlayerAtPos(x + enemyList[n].position.x, y + enemyList[n].position.y + weapon.sRange + i) != -1)
                            {
                                if (!weapon.ranged && i > 1)
                                {
                                    bool validMelee = true;
                                    for (int j = 1; j < i; j++)
                                    {
                                        if (enemyList[n].ValidMoveTile(battleMap[x + enemyList[n].position.x, y + enemyList[n].position.y + weapon.sRange + j]))
                                            validMelee = false;
                                    }
                                    if (validMelee)
                                        possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x, y + enemyList[n].position.y + weapon.sRange + i, maxMove * 3 - (Mathf.Abs(x) + Mathf.Abs(y + weapon.sRange + i)), 1, moveXFirst));
                                }
                                else
                                {
                                    possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x, y + enemyList[n].position.y + weapon.sRange + i, maxMove * 3 - (Mathf.Abs(x) + Mathf.Abs(y + weapon.sRange + i)), 1, moveXFirst));
                                }
                            }
                            if (PlayerAtPos(x + enemyList[n].position.x, y + enemyList[n].position.y - weapon.sRange - i) != -1)
                            {
                                if (!weapon.ranged && i > 1)
                                {
                                    bool validMelee = true;
                                    for (int j = 1; j < i; j++)
                                    {
                                        if (enemyList[n].ValidMoveTile(battleMap[x + enemyList[n].position.x, y + enemyList[n].position.y - weapon.sRange - j]))
                                            validMelee = false;
                                    }
                                    if (validMelee)
                                        possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x, y + enemyList[n].position.y - weapon.sRange - i, maxMove * 3 - (Mathf.Abs(x) + Mathf.Abs(y - weapon.sRange - i)), 1, moveXFirst));
                                }
                                else
                                {
                                    possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x, y + enemyList[n].position.y - weapon.sRange - i, maxMove * 3 - (Mathf.Abs(x) + Mathf.Abs(y - weapon.sRange - i)), 1, moveXFirst));
                                }
                            }
                        }
                        for (int i = 0; i <= weapon.diagCut; i++)
                        {
                            if (PlayerAtPos(x + enemyList[n].position.x - i, y + enemyList[n].position.y + i) != -1)
                            {
                                possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x - i, y + enemyList[n].position.y + i, maxMove * 3 - (Mathf.Abs(x - i) + Mathf.Abs(y + i)), 1, moveXFirst));
                            }
                            if (PlayerAtPos(x + enemyList[n].position.x + i, y + enemyList[n].position.y + i) != -1)
                            {
                                possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x + i, y + enemyList[n].position.y + i, maxMove * 3 - (Mathf.Abs(x + i) + Mathf.Abs(y + i)), 1, moveXFirst));
                            }
                            if (PlayerAtPos(x + enemyList[n].position.x - i, y + enemyList[n].position.y - i) != -1)
                            {
                                possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x - i, y + enemyList[n].position.y - i, maxMove * 3 - (Mathf.Abs(x - i) + Mathf.Abs(y - i)), 1, moveXFirst));
                            }
                            if (PlayerAtPos(x + enemyList[n].position.x + i, y + enemyList[n].position.y - i) != -1)
                            {
                                possibleMoves.Add(new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, x + enemyList[n].position.x + i, y + enemyList[n].position.y - i, maxMove * 3 - (Mathf.Abs(x + i) + Mathf.Abs(y - i)), 1, moveXFirst));
                            }
                        }

                        foreach (int pID in GameStorage.activePlayerList)
                        {
                            if (Mathf.Abs(GameStorage.playerMasterList[pID].position.x - (enemyList[n].position.x + x)) + Mathf.Abs(GameStorage.playerMasterList[pID].position.y - (enemyList[n].position.y + y)) < fallbackMove.priority)
                            {
                                fallbackMove = new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, Mathf.Abs(GameStorage.playerMasterList[pID].position.x - (enemyList[n].position.x + x)) + Mathf.Abs(GameStorage.playerMasterList[pID].position.y - (enemyList[n].position.y + y)), 0, moveXFirst);
                            }
                            else if (Mathf.Approximately(Mathf.Abs(GameStorage.playerMasterList[pID].position.x - (enemyList[n].position.x + x)) + Mathf.Abs(GameStorage.playerMasterList[pID].position.y - (enemyList[n].position.y + y)), fallbackMove.priority))
                            {
                                if (Random.Range(0, 2) == 1)
                                {
                                    fallbackMove = new EnemyMove(x + enemyList[n].position.x, y + enemyList[n].position.y, Mathf.Abs(GameStorage.playerMasterList[pID].position.x - (enemyList[n].position.x + x)) + Mathf.Abs(GameStorage.playerMasterList[pID].position.y - (enemyList[n].position.y + y)), 0, moveXFirst);
                                }
                            }
                        }
                    }
                }
            }
        }
        //if the enemy can't attack anyone, adds the move that would get them closest to the nearest player
        if (possibleMoves.Count == 0)
        {
            possibleMoves.Add(fallbackMove);
        }
        //sorts the possible moves in order of priority
        possibleMoves.Sort(delegate (EnemyMove c1, EnemyMove c2) { return c1.CompareTo(c2); });

        //chooses between moves with equal priority
        while (possibleMoves.Count > 1 && possibleMoves[0].CompareTo(possibleMoves[1]) == 0)
        {
            possibleMoves.RemoveAt(Random.Range(0, 2));
        }
        if (possibleMoves[0].xFirst)
        {
            if (!Mathf.Approximately(((mapSizeY - 1) - possibleMoves[0].movePosition.y) - enemyModels[n].transform.position.z, 0))
                enemyAnimMoves.Add(new MoveQueue(n, new Vector2(0, ((mapSizeY - 1) - possibleMoves[0].movePosition.y) - enemyModels[n].transform.position.z + topLeft.y)));
            if (!Mathf.Approximately(possibleMoves[0].movePosition.x - enemyModels[n].transform.position.x, 0))
                enemyAnimMoves.Add(new MoveQueue(n, new Vector2(possibleMoves[0].movePosition.x - enemyModels[n].transform.position.x + topLeft.x, 0)));
        }
        else
        {
            if (!Mathf.Approximately(possibleMoves[0].movePosition.x - enemyModels[n].transform.position.x, 0))
                enemyAnimMoves.Add(new MoveQueue(n, new Vector2(possibleMoves[0].movePosition.x - enemyModels[n].transform.position.x + topLeft.x, 0)));
            if (!Mathf.Approximately(((mapSizeY - 1) - possibleMoves[0].movePosition.y) - enemyModels[n].transform.position.z, 0))
                enemyAnimMoves.Add(new MoveQueue(n, new Vector2(0, ((mapSizeY - 1) - possibleMoves[0].movePosition.y) - enemyModels[n].transform.position.z + topLeft.y)));
        }

        enemyList[n].moved = true;
        chosenMove = possibleMoves[0];
    }
    
    /// <summary>
    /// Gets how much damage p1 would do when attacking p2
    /// </summary>
    /// <param name="p1">The pawn doing the attacking</param>
    /// <param name="p2">The pawn getting attacked</param>
    /// <returns></returns>
    public int GetDamageValues(BattleParticipant p1, BattleParticipant p2)
    {
        //gets the distance between the player and enemy
        int dist = Mathf.Abs(p2.position.x - p1.position.x);
        if (Mathf.Abs(p2.position.y - p1.position.y) > dist)
            dist = Mathf.Abs(p2.position.y - p1.position.y);

        int type = ((EquippableBase)Registry.ItemRegistry[p1.equippedWeapon]).statType;
        foreach (RangeDependentAttack r in Registry.WeaponTypeRegistry[((EquippableBase)Registry.ItemRegistry[p1.equippedWeapon]).subType].specialRanges)
        {
            if (r.atDistance == dist)
            {
                type = r.damageType;
            }
        }
        if (type == 0)
            return Mathf.RoundToInt((p1.GetEffectiveAtk(dist) * 3.0f) / p2.GetEffectiveDef(dist));
        else
            return Mathf.RoundToInt((p1.GetEffectiveMAtk(dist) * 3.0f) / p2.GetEffectiveMDef(dist));
    }
    
    /// <summary>
    /// Performs attack, then checks for an executes possible counterattack if both pawns are still alive
    /// </summary>
    public void PerformPlayerAttack()
    {
        WeaponType pweapon;
        if (!Registry.WeaponTypeRegistry.TryGetValue(((EquippableBase)Registry.ItemRegistry[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].equippedWeapon]).subType, out pweapon))
            Debug.Log("Weapon Type does not exist in the Registry.");
        //if healing a player
        if (selectedEnemy >= enemyList.Length)
        {
            GameStorage.playerMasterList[GameStorage.activePlayerList[selectedEnemy - enemyList.Length]].Heal(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].GetEffectiveAtk() / 2);
        }
        //if attacking an enemy
        else
        {
            float mod = 1.0f;
            if (Random.Range(0.0f, 100.0f) < GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].critChance + ((EquippableBase)Registry.ItemRegistry[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].equippedWeapon]).critChanceMod) { mod = 1.5f; }
            enemyList[selectedEnemy].Damage(Mathf.RoundToInt(GetDamageValues(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]], enemyList[selectedEnemy]) * mod));
            if (enemyList[selectedEnemy].cHealth <= 0)
            {
                CheckForDeath();
            }
            else
            {
                WeaponType eweapon;
                if (!Registry.WeaponTypeRegistry.TryGetValue(((EquippableBase)Registry.ItemRegistry[enemyList[selectedEnemy].equippedWeapon]).subType, out eweapon))
                    Debug.Log("Weapon Type does not exist in the Registry.");
                if (pweapon.ranged == eweapon.ranged)
                {
                    mod = 1.0f;
                    if (Random.Range(0.0f, 100.0f) < enemyList[selectedEnemy].critChance + ((EquippableBase)Registry.ItemRegistry[enemyList[selectedEnemy].equippedWeapon]).critChanceMod) { mod = 1.5f; }
                    GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].Damage(Mathf.RoundToInt(GetDamageValues(enemyList[selectedEnemy], GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]]) * mod));
                    if (GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].cHealth <= 0)
                    {
                        playerModels[selectedPlayer].SetActive(false);
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.Set(-200, -200);
                        CheckForDeath();
                    }
                }
            }
        }
        EndPlayerAttack();
    }

    /// <summary>
    /// Performs attack, then checks for an executes possible counterattack if both pawns are still alive
    /// </summary>
    /// <param name="enemy">The ID of the attacking enemy</param>
    /// <param name="pX">X coordinate of the tile Where the player being attacked is</param>
    /// <param name="pY">Y coordinate of the tile where the player being attacked is</param>
    public void PerformEnemyAttack(int enemy, int pX, int pY)
    {
        int player = PlayerAtPos(pX, pY);

        float mod = 1.0f;
        if (Random.Range(0.0f, 100.0f) < enemyList[enemy].critChance + ((EquippableBase)Registry.ItemRegistry[enemyList[enemy].equippedWeapon]).critChanceMod) { mod = 1.5f; }
        GameStorage.playerMasterList[GameStorage.activePlayerList[player]].Damage(Mathf.RoundToInt(GetDamageValues(enemyList[enemy], GameStorage.playerMasterList[GameStorage.activePlayerList[player]]) * mod));
        if (GameStorage.playerMasterList[GameStorage.activePlayerList[player]].cHealth <= 0)
        {
            playerModels[player].SetActive(false);
            GameStorage.playerMasterList[GameStorage.activePlayerList[player]].position.Set(-200, -200);
            CheckForDeath();
        }
        else
        {
            WeaponType pweapon;
            WeaponType eweapon;
            if (!Registry.WeaponTypeRegistry.TryGetValue(((EquippableBase)Registry.ItemRegistry[enemyList[enemy].equippedWeapon]).subType, out eweapon))
                Debug.Log("Weapon Type does not exist in the Registry.");
            if (!Registry.WeaponTypeRegistry.TryGetValue(((EquippableBase)Registry.ItemRegistry[GameStorage.playerMasterList[GameStorage.activePlayerList[player]].equippedWeapon]).subType, out pweapon))
                Debug.Log("Weapon Type does not exist in the Registry.");
            if (pweapon.ranged == eweapon.ranged)
            {
                mod = 1.0f;
                if (Random.Range(0.0f, 100.0f) < GameStorage.playerMasterList[GameStorage.activePlayerList[player]].critChance + ((EquippableBase)Registry.ItemRegistry[GameStorage.playerMasterList[GameStorage.activePlayerList[player]].equippedWeapon]).critChanceMod) { mod = 1.5f; }
                enemyList[enemy].Damage(Mathf.RoundToInt(GetDamageValues(GameStorage.playerMasterList[GameStorage.activePlayerList[player]], enemyList[enemy]) * mod));
                if (enemyList[enemy].cHealth <= 0)
                {
                    CheckForDeath();
                    Debug.Log(battleState);
                }
            }
        }
    }

    /// <summary>
    /// If the player decides not to do anything after moving
    /// </summary>
    public void EndPlayerAttack()
    {
        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].moved = true;
        FinishedMovingPawn();
    }

    /// <summary>
    /// Sets up for the next pawn to be moved
    /// or
    /// If all player pawns have finished moving, set up for enemies to move
    /// </summary>
    public void FinishedMovingPawn()
    {
        if (battleState != BattleState.ReturnCamera)
        {
            battleState = BattleState.Player;
            selectedMoveSpot = new Vector2Int(-1, -1);
            selectedPlayer = -1;
            selectedEnemy = -1;
            selectedSpell = -1;
            updateTilesThisFrame = true;

            //checks if all players are done moving
            bool playersDone = true;
            foreach (int pID in GameStorage.activePlayerList)
            {
                if (!GameStorage.playerMasterList[pID].moved)
                    playersDone = false;
            }
            if (playersDone)
            {
                //resets to start enemy moves
                for (int j = 0; j < enemyList.Length; j++)
                {
                    if (enemyList[j].cHealth > 0)
                        enemyList[j].moved = false;
                }
                battleState = BattleState.Enemy;
            }
        }
    }

    /// <summary>
    /// Checks if there is an player at the given x and y values
    /// </summary>
    private int PlayerAtPos(int x, int y)
    {
        foreach (int pID in GameStorage.activePlayerList)
        {
            if (GameStorage.playerMasterList[pID].position.x == x && GameStorage.playerMasterList[pID].position.y == y)
                return pID;
        }
        return -1;
    }
    
    /// <summary>
    /// Checks if there is an enemy at the given x and y values
    /// </summary>
    private int EnemyAtPos(int x, int y)
    {
        for (int e = 0; e < enemyList.Length; e++)
        {
            if (enemyList[e].position.x == x && enemyList[e].position.y == y)
                return e;
        }
        return -1;
    }
    
    /// <summary>
    /// Updates the data of each tile in the battlefield with its significance at the current moment
    /// </summary>
    private void UpdateTileMap()
    {
        //resets the tiles
        for (int x = 0; x < mapSizeX; x++)
        {
            for (int y = 0; y < mapSizeY; y++)
            {
                tileList[x, y].GetComponent<BattleTile>().Reset();

                //updates the aEther viewer
                tileList[x, y].GetComponentsInChildren<Renderer>()[1].enabled = showaEther;
                if (showaEther)
                    tileList[x, y].GetComponentsInChildren<Transform>()[1].localScale = new Vector3(0.1f * aEtherMap[x, y, 0], 0.01f, 0.1f * aEtherMap[x, y, 0]);
            }
        }

        //shows skill range and what is targettable within that range if a spell is selected or hovered
        int skillToShow = selectedSpell;
        if (hoveredSpell != -1)
            skillToShow = hoveredSpell;
        if (skillToShow != -1)
        {
            Vector2Int skillPos = GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position;
            if (selectedMoveSpot.x != -1)
                skillPos = selectedMoveSpot;
            Skill displaySkill = GameStorage.skillTreeList[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].skillQuickList[skillToShow - 1].x][GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].skillQuickList[skillToShow - 1].y];

            if (displaySkill.targetType == 1)
            {
                tileList[skillPos.x, (mapSizeY - 1) - skillPos.y].GetComponent<BattleTile>().skillTargettable = true;
            }
            else
            {
                for (int x = -displaySkill.targettingRange; x <= displaySkill.targettingRange; x++)
                {
                    for (int y = -displaySkill.targettingRange; y <= displaySkill.targettingRange; y++)
                    {
                        if (Mathf.Abs(x) + Mathf.Abs(y) <= displaySkill.targettingRange && x + skillPos.x >= 0 && x + skillPos.x < mapSizeX && y + skillPos.y >= 0 && y + skillPos.y < mapSizeY)
                        {
                            if (displaySkill.targetType == 5)
                            {
                                tileList[x + skillPos.x, (mapSizeY - 1) - (y + skillPos.y)].GetComponent<BattleTile>().skillTargettable = true;
                            }
                            else
                            {
                                tileList[x + skillPos.x, (mapSizeY - 1) - (y + skillPos.y)].GetComponent<BattleTile>().skillRange = true;
                            }

                            if (displaySkill.targetType == 2 && EnemyAtPos(x + skillPos.x, y + skillPos.y) != -1)
                            {
                                tileList[x + skillPos.x, (mapSizeY - 1) - (y + skillPos.y)].GetComponent<BattleTile>().skillTargettable = true;
                            }
                            else if (displaySkill.targetType == 3 && PlayerAtPos(x + skillPos.x, y + skillPos.y) != -1)
                            {
                                tileList[x + skillPos.x, (mapSizeY - 1) - (y + skillPos.y)].GetComponent<BattleTile>().skillTargettable = true;
                            }
                        }
                    }
                }
            }
            if (selectedSpell != -1)
            {
                Ray ray = Camera.main.ViewportPointToRay(new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, 0));
                RaycastHit hit;
                int layerMask = 1 << 8;
                if (Physics.Raycast(ray, out hit, 30.0f, layerMask))
                {
                    int iX = hit.transform.GetComponent<BattleTile>().arrayID.x;
                    int iY = hit.transform.GetComponent<BattleTile>().arrayID.y;
                    if (tileList[iX, (mapSizeY - 1) - iY].GetComponent<BattleTile>().skillTargettable)
                        BattleTile.skillLegitTarget = true;
                    else
                        BattleTile.skillLegitTarget = false;
                    for (int x = -Mathf.FloorToInt((displaySkill.xRange - 1) / 2.0f); x <= Mathf.CeilToInt((displaySkill.xRange - 1) / 2.0f); x++)
                    {
                        for (int y = -Mathf.FloorToInt((displaySkill.yRange - 1) / 2.0f); y <= Mathf.CeilToInt((displaySkill.yRange - 1) / 2.0f); y++)
                        {
                            if (x + iX >= 0 && x + iX < mapSizeX && y + iY >= 0 && y + iY < mapSizeY)
                            {
                                tileList[x + iX, (mapSizeY - 1) - (y + iY)].GetComponent<BattleTile>().skillTargetting = true;
                            }
                        }
                    }
                }
            }
        }
        if (battleState == BattleState.Swap)
        {
            for (int p = 0; p < GameStorage.activePlayerList.Count; p++)
            {
                tileList[GameStorage.playerMasterList[GameStorage.activePlayerList[p]].position.x, (mapSizeY - 1) - GameStorage.playerMasterList[GameStorage.activePlayerList[p]].position.y].GetComponent<BattleTile>().playerMoveRange = true;
            }
            return;
        }

        //if we need to render player moves
        else if (battleState == BattleState.Player && selectedPlayer != -1)
        {
            int maxMove = GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].GetMoveSpeed();
            WeaponType weapon;
            if (!Registry.WeaponTypeRegistry.TryGetValue(((EquippableBase)Registry.ItemRegistry[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].equippedWeapon]).subType, out weapon))
                Debug.Log("Weapon Type does not exist in the Registry.");
            for (int x = -maxMove; x <= maxMove; x++)
            {
                for (int y = -maxMove; y <= maxMove; y++)
                {
                    if (Mathf.Abs(x) + Mathf.Abs(y) <= maxMove && x + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x >= 0 && x + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x < mapSizeX && y + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y >= 0 && y + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y < mapSizeY)
                    {
                        bool goodTile = true;
                        if (PlayerAtPos(x + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, y + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y) != -1)
                            goodTile = false;
                        if (EnemyAtPos(x + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, y + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y) != -1)
                            goodTile = false;
                        if (x == 0 && y == 0)
                            goodTile = true;
                        if (goodTile)
                        {
                            for (int cX = 1; cX <= Mathf.Abs(x); cX++)
                            {
                                if (!GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].ValidMoveTile(battleMap[cX * Mathf.RoundToInt(Mathf.Sign(x)) + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y]))
                                {
                                    goodTile = false;
                                }
                                if (EnemyAtPos(cX * Mathf.RoundToInt(Mathf.Sign(y)) + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y) != -1)
                                    goodTile = false;
                            }
                            if (goodTile)
                            {
                                for (int cY = 1; cY <= Mathf.Abs(y); cY++)
                                {
                                    if (!GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].ValidMoveTile(battleMap[x + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, cY * Mathf.RoundToInt(Mathf.Sign(y)) + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y]))
                                    {
                                        goodTile = false;
                                    }
                                    if (EnemyAtPos(x + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, cY * Mathf.RoundToInt(Mathf.Sign(y)) + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y) != -1)
                                        goodTile = false;
                                }
                            }
                            //if invalid by x, y, check y, x
                            if (!goodTile)
                            {
                                goodTile = true;
                                for (int cY = 1; cY <= Mathf.Abs(y); cY++)
                                {
                                    if (!GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].ValidMoveTile(battleMap[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, cY * Mathf.RoundToInt(Mathf.Sign(y)) + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y]))
                                    {
                                        goodTile = false;
                                    }
                                    if (EnemyAtPos(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, cY * Mathf.RoundToInt(Mathf.Sign(y)) + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y) != -1)
                                        goodTile = false;
                                }
                                if (goodTile)
                                {
                                    for (int cX = 1; cX <= Mathf.Abs(x); cX++)
                                    {
                                        if (!GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].ValidMoveTile(battleMap[cX * Mathf.RoundToInt(Mathf.Sign(x)) + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, y + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y]))
                                        {
                                            goodTile = false;
                                        }
                                        if (EnemyAtPos(cX * Mathf.RoundToInt(Mathf.Sign(x)) + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, y + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y) != -1)
                                            goodTile = false;
                                    }
                                }
                            }
                            if (goodTile)
                            {
                                //marks the tile as a moveable spot
                                tileList[x + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, (mapSizeY - 1) - (y + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y)].GetComponent<BattleTile>().playerMoveRange = true;
                                RenderWeaponRanges(x + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, y + GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y, weapon, "attack area");
                            }
                        }
                    }
                }
            }
        }
        if (showDanger)
        {
            for (int n = 0; n < enemyList.Length; n++)
            {
                int maxMove = enemyList[n].GetMoveSpeed();
                WeaponType weapon;
                if (!Registry.WeaponTypeRegistry.TryGetValue(((EquippableBase)Registry.ItemRegistry[enemyList[n].equippedWeapon]).subType, out weapon))
                    Debug.Log("Weapon Type does not exist in the Registry.");
                for (int x = -maxMove; x <= maxMove; x++)
                {
                    for (int y = -maxMove; y <= maxMove; y++)
                    {
                        if (Mathf.Abs(x) + Mathf.Abs(y) <= maxMove && x + enemyList[n].position.x >= 0 && x + enemyList[n].position.x < mapSizeX && y + enemyList[n].position.y >= 0 && y + enemyList[n].position.y < mapSizeY)
                        {
                            if (ValidEnemyMove(n, x, y))
                            {
                                tileList[x + enemyList[n].position.x, (mapSizeY - 1) - (y + enemyList[n].position.y)].GetComponent<BattleTile>().enemyDanger = true;
                                RenderWeaponRanges(x + enemyList[n].position.x, y + enemyList[n].position.y, weapon, "danger area");
                            }
                        }
                    }
                }
            }
        }
        if (battleState == BattleState.Attack)
        {
            WeaponType weapon;
            if (!Registry.WeaponTypeRegistry.TryGetValue(((EquippableBase)Registry.ItemRegistry[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].equippedWeapon]).subType, out weapon))
                Debug.Log("Weapon Type does not exist in the Registry.");
            RenderWeaponRanges(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.x, GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].position.y, weapon, "attack area");
            for (int x = 0; x < mapSizeX; x++)
            {
                for (int y = 0; y < mapSizeY; y++)
                {
                    if ((EnemyAtPos(x, y) == -1 || ((EquippableBase)Registry.ItemRegistry[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].equippedWeapon]).strength == 0) && (PlayerAtPos(x, y) == -1 || !weapon.heals))
                        tileList[x, (mapSizeY - 1) - y].GetComponent<BattleTile>().playerAttackRange = false;
                }
            }
        }
    }

    /// <summary>
    /// Changes the tiles around the specified position to show the weapon range from that point
    /// </summary>
    /// <param name="x">The grid x position of the spot to check around</param>
    /// <param name="y">The grid y position of the spot to check around</param>
    /// <param name="weapon">What weapon type is being checked. Contains the range and if it is ranged or not</param>
    /// <param name="tileValue">Whether this check is for an enemy (danger area) or a player (attack area)</param>
    public void RenderWeaponRanges(int x, int y, WeaponType weapon, string tileValue)
    {
        for (int i = 1; i <= weapon.range; i++)
        {
            if (!weapon.ranged && i > 1)
            {
                if (ViableMeleeSpot(x + weapon.sRange + i, y, tileValue))
                    tileList[x + weapon.sRange + i, (mapSizeY - 1) - y].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
            }
            else
            {
                tileList[x + weapon.sRange + i, (mapSizeY - 1) - y].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
            }
            if (!weapon.ranged && i > 1)
            {
                if (ViableMeleeSpot(x - weapon.sRange - i, y, tileValue))
                    tileList[x - weapon.sRange - i, (mapSizeY - 1) - y].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
            }
            else
            {
                tileList[x - weapon.sRange - i, (mapSizeY - 1) - y].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
            }
            if (!weapon.ranged && i > 1)
            {
                if (ViableMeleeSpot(x, y + weapon.sRange + i, tileValue))
                    tileList[x, (mapSizeY - 1) - (y + weapon.sRange + i)].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
            }
            else
            {
                tileList[x, (mapSizeY - 1) - (y + weapon.sRange + i)].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
            }
            if (!weapon.ranged && i > 1)
            {
                if (ViableMeleeSpot(x, y - weapon.sRange - i, tileValue))
                    tileList[x, (mapSizeY - 1) - (y - weapon.sRange - i)].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
            }
            else
            {
                tileList[x, (mapSizeY - 1) - (y - weapon.sRange - i)].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
            }
        }
        if (weapon.diagCut > 0)
        {
            for (int i = 1; i <= weapon.diagCut; i++)
            {
                if (y + i < mapSizeY)
                {
                    if (x - i >= 0)
                    {
                        tileList[x - i, (mapSizeY - 1) - (y + i)].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
                    }
                    if (x + i < mapSizeX)
                    {
                        tileList[x + i, (mapSizeY - 1) - (y + i)].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
                    }
                }
                if (y - i >= 0)
                {
                    if (x - i >= 0)
                    {
                        tileList[x - i, (mapSizeY - 1) - (y - i)].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
                    }
                    if (x + i < mapSizeX)
                    {
                        tileList[x + i, (mapSizeY - 1) - (y - i)].GetComponent<BattleTile>().ChangeValueByKey(tileValue);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks to see whether a tile would be a viable melee attack target
    /// </summary>
    /// <param name="x">The grid x position of the spot to check</param>
    /// <param name="y">The grid y position of the spot to check</param>
    /// <param name="tileValue">Whether this check is for an enemy (danger area) or a player (attack area)</param>
    public bool ViableMeleeSpot(int x, int y, string tileValue)
    {
        if (battleMap[x, y] == 1 || battleMap[x, y] == 3 || battleMap[x, y] == 4 || battleMap[x, y] == 5)
            if ((tileValue == "danger area" && tileList[x - 1, (mapSizeY - 1) - (y - 1)].GetComponent<BattleTile>().enemyDanger) || (tileValue == "attack area" && tileList[x - 1, (mapSizeY - 1) - (y - 1)].GetComponent<BattleTile>().playerAttackRange))
                return true;
        return false;
    }

    /// <summary>
    /// Enacts all of a spell's effects at a single coordinate space
    /// </summary>
    /// <param name="castedSpell">What spell is being cast</param>
    /// <param name="castedX">The x coordinate of the space to affect</param>
    /// <param name="castedY">The y coordinate of the space to affect</param>
    /// <param name="enemyUsingMove">If it is an enemy casting the spell</param>
    public void CastSkill(Skill castedSpell, int castedX, int castedY, int enemyUsingMove = -1)
    {
        foreach (SkillPartBase s in castedSpell.partList)
        {
            //flat damage, then calculated damage, then remaining hp, then max hp
            if (s.skillPartType.CompareTo("damage") == 0)
            {
                if (s.targetType == 1)
                {
                    if (enemyUsingMove != -1)
                    {
                        enemyList[enemyUsingMove].Damage((s as DamagePart).flatDamage);
                        enemyList[enemyUsingMove].Damage(Mathf.RoundToInt(((s as DamagePart).damage * enemyList[enemyUsingMove].mAttack * 3.0f) / enemyList[enemyUsingMove].GetEffectiveDef()));
                        enemyList[enemyUsingMove].Damage((int)(enemyList[enemyUsingMove].cHealth * (s as DamagePart).remainingHpPercent / 100.0f));
                        enemyList[enemyUsingMove].Damage((int)(enemyList[enemyUsingMove].mHealth * (s as DamagePart).maxHpPercent / 100.0f));
                    }
                    else
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].Damage((s as DamagePart).flatDamage);
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].Damage(Mathf.RoundToInt(((s as DamagePart).damage * GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].mAttack * 3.0f) / GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].GetEffectiveDef()));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].Damage((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].cHealth * (s as DamagePart).remainingHpPercent / 100.0f));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].Damage((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].mHealth * (s as DamagePart).maxHpPercent / 100.0f));
                    }
                }
                else if (s.targetType == 2 || s.targetType == 5)
                {
                    int i = PlayerAtPos(castedX, castedY);
                    if (enemyUsingMove != -1 && i != -1)
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Damage((s as DamagePart).flatDamage);
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Damage(Mathf.RoundToInt(((s as DamagePart).damage * GameStorage.playerMasterList[GameStorage.activePlayerList[i]].mAttack * 3.0f) / GameStorage.playerMasterList[GameStorage.activePlayerList[i]].GetEffectiveDef()));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Damage((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[i]].cHealth * (s as DamagePart).remainingHpPercent / 100.0f));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Damage((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[i]].mHealth * (s as DamagePart).maxHpPercent / 100.0f));
                    }
                    else if (enemyUsingMove == -1 && EnemyAtPos(castedX, castedY) != -1)
                    {
                        i = EnemyAtPos(castedX, castedY);
                        enemyList[i].Damage((s as DamagePart).flatDamage);
                        enemyList[i].Damage(Mathf.RoundToInt(((s as DamagePart).damage * enemyList[i].mAttack * 3.0f) / enemyList[i].GetEffectiveDef()));
                        enemyList[i].Damage((int)(enemyList[i].cHealth * (s as DamagePart).remainingHpPercent / 100.0f));
                        enemyList[i].Damage((int)(enemyList[i].mHealth * (s as DamagePart).maxHpPercent / 100.0f));
                    }
                }
                else if (s.targetType == 3 || s.targetType == 5)
                {
                    int i = PlayerAtPos(castedX, castedY);
                    if (enemyUsingMove == -1 && i != -1)
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Damage((s as DamagePart).flatDamage);
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Damage(Mathf.RoundToInt(((s as DamagePart).damage * GameStorage.playerMasterList[GameStorage.activePlayerList[i]].mAttack * 3.0f) / GameStorage.playerMasterList[GameStorage.activePlayerList[i]].GetEffectiveDef()));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Damage((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[i]].cHealth * (s as DamagePart).remainingHpPercent / 100.0f));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Damage((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[i]].mHealth * (s as DamagePart).maxHpPercent / 100.0f));
                    }
                    else if (enemyUsingMove != -1 && EnemyAtPos(castedX, castedY) != -1)
                    {
                        i = EnemyAtPos(castedX, castedY);
                        enemyList[i].Damage((s as DamagePart).flatDamage);
                        enemyList[i].Damage(Mathf.RoundToInt(((s as DamagePart).damage * enemyList[i].mAttack * 3.0f) / enemyList[i].GetEffectiveDef()));
                        enemyList[i].Damage((int)(enemyList[i].cHealth * (s as DamagePart).remainingHpPercent / 100.0f));
                        enemyList[i].Damage((int)(enemyList[i].mHealth * (s as DamagePart).maxHpPercent / 100.0f));
                    }
                }
            }

            //flat healing, then calculated healing, then max hp
            else if (s.skillPartType.CompareTo("healing") == 0)
            {
                if (s.targetType == 1)
                {
                    if (enemyUsingMove != -1)
                    {
                        enemyList[enemyUsingMove].Heal((s as HealingPart).flatHealing);
                        enemyList[enemyUsingMove].Heal(Mathf.RoundToInt(((s as HealingPart).healing * enemyList[enemyUsingMove].mAttack * 3.0f) / enemyList[enemyUsingMove].GetEffectiveDef()));
                        enemyList[enemyUsingMove].Heal((int)(enemyList[enemyUsingMove].mHealth * (s as HealingPart).maxHpPercent / 100.0f));
                    }
                    else
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].Heal((s as HealingPart).flatHealing);
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].Heal(Mathf.RoundToInt(((s as HealingPart).healing * GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].mAttack * 3.0f) / GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].GetEffectiveDef()));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].Heal((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].mHealth * (s as HealingPart).maxHpPercent / 100.0f));
                    }
                }
                else if (s.targetType == 2 || s.targetType == 5)
                {
                    int i = PlayerAtPos(castedX, castedY);
                    if (enemyUsingMove != -1 && i != -1)
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Heal((s as HealingPart).flatHealing);
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Heal(Mathf.RoundToInt(((s as HealingPart).healing * GameStorage.playerMasterList[GameStorage.activePlayerList[i]].mAttack * 3.0f) / GameStorage.playerMasterList[GameStorage.activePlayerList[i]].GetEffectiveDef()));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Heal((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[i]].mHealth * (s as HealingPart).maxHpPercent / 100.0f));
                    }
                    else if (enemyUsingMove == -1 && EnemyAtPos(castedX, castedY) != -1)
                    {
                        i = EnemyAtPos(castedX, castedY);
                        enemyList[i].Heal((s as HealingPart).flatHealing);
                        enemyList[i].Heal(Mathf.RoundToInt(((s as HealingPart).healing * enemyList[i].mAttack * 3.0f) / enemyList[i].GetEffectiveDef()));
                        enemyList[i].Heal((int)(enemyList[i].mHealth * (s as HealingPart).maxHpPercent / 100.0f));
                    }
                }
                else if (s.targetType == 3 || s.targetType == 5)
                {
                    int i = PlayerAtPos(castedX, castedY);
                    if (enemyUsingMove == -1 && i != -1)
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Heal((s as HealingPart).flatHealing);
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Heal(Mathf.RoundToInt(((s as HealingPart).healing * GameStorage.playerMasterList[GameStorage.activePlayerList[i]].mAttack * 3.0f) / GameStorage.playerMasterList[GameStorage.activePlayerList[i]].GetEffectiveDef()));
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].Heal((int)(GameStorage.playerMasterList[GameStorage.activePlayerList[i]].mHealth * (s as HealingPart).maxHpPercent / 100.0f));
                    }
                    else if (enemyUsingMove != -1 && EnemyAtPos(castedX, castedY) != -1)
                    {
                        i = EnemyAtPos(castedX, castedY);
                        enemyList[i].Heal((s as HealingPart).flatHealing);
                        enemyList[i].Heal(Mathf.RoundToInt(((s as HealingPart).healing * enemyList[i].mAttack * 3.0f) / enemyList[i].GetEffectiveDef()));
                        enemyList[i].Heal((int)(enemyList[i].mHealth * (s as HealingPart).maxHpPercent / 100.0f));
                    }
                }
            }

            //stat changes, self explanitory
            else if (s.skillPartType.CompareTo("statChange") == 0)
            {
                if (s.targetType == 1)
                {
                    if (enemyUsingMove != -1)
                    {
                        enemyList[enemyUsingMove].AddMod((s as StatChangePart).StatMod);
                    }
                    else
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].AddMod((s as StatChangePart).StatMod);
                    }
                }
                else if (s.targetType == 2 || s.targetType == 5)
                {
                    int i = PlayerAtPos(castedX, castedY);
                    if (enemyUsingMove != -1 && i != -1)
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].AddMod((s as StatChangePart).StatMod);
                    }
                    else if (enemyUsingMove == -1 && EnemyAtPos(castedX, castedY) != -1)
                    {
                        i = EnemyAtPos(castedX, castedY);
                        enemyList[i].AddMod((s as StatChangePart).StatMod);
                    }
                }
                else if (s.targetType == 3 || s.targetType == 5)
                {
                    int i = PlayerAtPos(castedX, castedY);
                    if (enemyUsingMove == -1 && i != -1)
                    {
                        GameStorage.playerMasterList[GameStorage.activePlayerList[i]].AddMod((s as StatChangePart).StatMod);
                    }
                    else if (enemyUsingMove != -1 && EnemyAtPos(castedX, castedY) != -1)
                    {
                        i = EnemyAtPos(castedX, castedY);
                        enemyList[i].AddMod((s as StatChangePart).StatMod);
                    }
                }
            }

            //adds status effects
            else if (s.skillPartType.CompareTo("statusEffect") == 0)
            {
                if (s.targetType == 1)
                {
                    if (enemyUsingMove != -1)
                    {
                        if ((s as StatusEffectPart).remove)
                            enemyList[enemyUsingMove].RemoveStatusEffect((s as StatusEffectPart).status);
                        else
                            enemyList[enemyUsingMove].AddStatusEffect((s as StatusEffectPart).status);
                    }
                    else
                    {
                        if ((s as StatusEffectPart).remove)
                            GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].RemoveStatusEffect((s as StatusEffectPart).status);
                        else
                            GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].AddStatusEffect((s as StatusEffectPart).status);
                    }
                }
                else if (s.targetType == 2 || s.targetType == 5)
                {
                    int i = PlayerAtPos(castedX, castedY);
                    if (enemyUsingMove != -1 && i != -1)
                    {
                        if ((s as StatusEffectPart).remove)
                            GameStorage.playerMasterList[GameStorage.activePlayerList[i]].RemoveStatusEffect((s as StatusEffectPart).status);
                        else
                            GameStorage.playerMasterList[GameStorage.activePlayerList[i]].AddStatusEffect((s as StatusEffectPart).status);
                    }
                    else if (enemyUsingMove == -1 && EnemyAtPos(castedX, castedY) != -1)
                    {
                        i = EnemyAtPos(castedX, castedY);
                        if ((s as StatusEffectPart).remove)
                            enemyList[i].RemoveStatusEffect((s as StatusEffectPart).status);
                        else
                            enemyList[i].AddStatusEffect((s as StatusEffectPart).status);
                    }
                }
                else if (s.targetType == 3 || s.targetType == 5)
                {
                    int i = PlayerAtPos(castedX, castedY);
                    if (enemyUsingMove == -1 && i != -1)
                    {
                        if ((s as StatusEffectPart).remove)
                            GameStorage.playerMasterList[GameStorage.activePlayerList[i]].RemoveStatusEffect((s as StatusEffectPart).status);
                        else
                            GameStorage.playerMasterList[GameStorage.activePlayerList[i]].AddStatusEffect((s as StatusEffectPart).status);
                    }
                    else if (enemyUsingMove != -1 && EnemyAtPos(castedX, castedY) != -1)
                    {
                        i = EnemyAtPos(castedX, castedY);
                        if ((s as StatusEffectPart).remove)
                            enemyList[i].RemoveStatusEffect((s as StatusEffectPart).status);
                        else
                            enemyList[i].AddStatusEffect((s as StatusEffectPart).status);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Confirms the spell cast, casts the spell effects at every point in its AOE and checks for deaths
    /// </summary>
    public void ConfirmSkillCast()
    {
        skillCastConfirmMenu.SetActive(false);
        if (selectedMoveSpot.x != -1)
            ConfirmPlayerMove();
        for (int x = 0; x < mapSizeX; x++)
        {
            for (int y = 0; y < mapSizeY; y++)
            {
                if (tileList[x, (mapSizeY - 1) - y].GetComponent<BattleTile>().skillTargetting)
                {
                    Debug.Log("Casting Skill at " + x + "|" + y);
                    Skill displaySkill = GameStorage.skillTreeList[GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].skillQuickList[selectedSpell - 1].x][GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].skillQuickList[selectedSpell - 1].y];
                    CastSkill(displaySkill, x, y);
                }
            }
        }
        GameStorage.playerMasterList[GameStorage.activePlayerList[selectedPlayer]].moved = true;
        FinishedMovingPawn();
        CheckForDeath();
    }

    /// <summary>
    /// Cancels casting the spell through the spell cast confirm menu
    /// </summary>
    public void CancelSkillCast()
    {
        selectedEnemy = -1;
        skillCastConfirmMenu.SetActive(false);
    }
}
