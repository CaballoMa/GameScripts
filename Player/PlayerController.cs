using System;
using System.Security.AccessControl;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manager Player Input (Except Movement) and Responding Logic
/// </summary>
[RequireComponent(typeof(PlayerStateController))]
[RequireComponent(typeof(PlayerHand))]
[RequireComponent(typeof(PlayerProperty))]
public class PlayerController : MonoBehaviour
{
    public bool haveGGItem;

    [SerializeField]
    private List<ItemProperties> canTakeList = new List<ItemProperties>();
    //private List<IPullable> canPullList = new List<IPullable>();
    [SerializeField]
    private List<ItemProperties> canKnockList = new List<ItemProperties>();
    [SerializeField]
    private List<Env_SeaWeed> seaWeedsList = new List<Env_SeaWeed>();
    [SerializeField]
    public Material playerMaterial;
    private PlayerStateController stateController;
    private PlayerProperty playerProperty;
    private PlayerHand hand;
    //Use to set lerp value with player material
    private float m_materialFloat;
    //When player level up, set true
    private bool m_isGrow;
    //When player knock and rotate, we need to record player origial rotation and rotate back
    private Quaternion m_PlayerOriRotation;
    //When Player Grab Weapon, its become true and open Forecast Point
    private bool m_ForeCast;

    #region Cast param

    private bool m_isCast;
    [SerializeField] private float m_castTime;
    [SerializeField] private float castMaxTime;
    [SerializeField] private ParticleSystem knockParticle;
    [SerializeField] private Transform castPoint;

    #endregion

    CompareItem compareItem;
    // List<Renderer> rendererList;

    void Start()
    {
        hand = GetComponent<PlayerHand>();
        //playerMaterial = transform.Find("backup").GetComponent<SkinnedMeshRenderer>().materials[0];
        stateController = GetComponent<PlayerStateController>();
        playerProperty = GetComponent<PlayerProperty>();
        compareItem = new CompareItem(this.transform);
    }

    void Update()
    {
        if (GameManager.instance.GetGameAction() && !GameManager.instance.IsGamePause)
        {
            Interact();
            EatOrKnockInteract();
            PlayerGrowUp();
            if (m_isCast)
            {
               Accumulate();
            }

            if (m_ForeCast)
            {
                Cast.instance.CastForeCastOn(castPoint, m_castTime);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
       if (other.GetComponent<ItemProperties>() && other.GetComponent<ItemProperties>().CanCatch ) 
       {
           ItemProperties itemBase = other.GetComponent<ItemProperties>();
           Debug.Log("Takable Found");
           if (!canTakeList.Contains(itemBase) && hand.grabItemInHand != itemBase)
                canTakeList.Add(itemBase);
                EventCenter.Broadcast(GameEvents.ShowButtonHint, ButtonHintType.Button_Z);
                canTakeList.Sort(compareItem);
       }


       if (other.GetComponent<ItemProperties>() && other.GetComponent<ItemProperties>().CanKnock
       && stateController.playerPlaceState == PlayerPlaceState.Float) {
           ItemProperties item = other.GetComponent<ItemProperties>();
           if (!canKnockList.Contains(item) && hand.grabItemInHand != item) {
                canKnockList.Add(item);
                canKnockList.Sort(compareItem);
                if (hand.grabItemInHand != null && !hand.grabItemInHand.IsBroken &&
                    stateController.playerPlaceState == PlayerPlaceState.Float)
                {
                    //Hint Player Knock food
                    EventCenter.Broadcast(GameEvents.ShowButtonHint, ButtonHintType.Button_X);
                }
           }
       }

       if (other.GetComponent<Env_SeaWeed>() && stateController.playerPlaceState == PlayerPlaceState.Float) {
           Env_SeaWeed item = other.GetComponent<Env_SeaWeed>();
           if (!seaWeedsList.Contains(item)) {
                seaWeedsList.Add(item);                
           }
       }

    }

    void OnTriggerExit(Collider other)
    {
       if (other.GetComponent<ItemProperties>() && other.GetComponent<ItemProperties>().CanCatch) {
           ItemProperties itemBase = other.GetComponent<ItemProperties>();
           if (canTakeList.Contains(itemBase))
               canTakeList.Remove(itemBase);
       }

       if (other.GetComponent<ItemProperties>() && other.GetComponent<ItemProperties>().CanKnock) {
           ItemProperties item = other.GetComponent<ItemProperties>();
           if (canKnockList.Contains(item)) {
                Debug.Log("Remove item because I quit !");
               canKnockList.Remove(item);
           }
       } 

        if (other.GetComponent<Env_SeaWeed>()) {
           Env_SeaWeed item = other.GetComponent<Env_SeaWeed>();
           if (seaWeedsList.Contains(item)) {
                seaWeedsList.Remove(item);
           }
        }        
    }

    #region Interact Detect

    /// <summary>
    /// When Player input Interact Keycode
    /// Responding logic
    /// </summary>
    private void Interact() 
    {
        if (Input.GetKeyDown(GlobalSetting.InterectKey)) {

            if (hand.grabItemInHand != null) {
                //地面状态：水面/水下
                //手部状态：手中有物品     
                //动作：放开物品    
                PlayerReleaseItem();
            }
            else if (stateController.playerCanClean 
            && stateController.playerPlaceState == PlayerPlaceState.Float
            && canTakeList.Count <= 0) 
            {
                if (GameManager.instance.GetDayState() == DayState.Night) {
                        //水面、环境可睡觉
                        //时间：夜晚
                        //手部状态：手中无物品
                        //动作：睡觉
                        PlayerSleep();
                        return;
                    }    
                    //地面状态：水面、环境可清洁
                    //手部状态：手中无物品
                    //动作：清洁                         
                    PlayerClean();
            }
            else 
            {
                //地面状态：水面/水下、可抓取物体
                //手部状态：手中无物品     
                //动作：抓取                
                PlayerGrabItemInHand();
            }

        }
    }

    /// <summary>
    /// Player Eat Food
    /// Improve Health Value and Experience Value
    /// </summary>
    private void EatOrKnockInteract()
    {
        if (Input.GetKeyDown(GlobalSetting.EatOrKnockKey) && hand.grabItemInHand != null && stateController.playerPlaceState == PlayerPlaceState.Float)
        {
            if (canKnockList.Count == 0 && hand.grabItemInHand.GetComponent<Weaponable>())
            {
                PrepareCast();
            }
        }

        if (Input.GetKeyUp(GlobalSetting.EatOrKnockKey) && hand.grabItemInHand != null)
        {
            if (hand.grabItemInHand.GetComponent<ItemProperties>().CanEat
            && hand.grabItemInHand.GetComponent<ItemProperties>().IsBroken)
            {
                //Eat
                EatFoodAniPlay();
            }
            else if (canKnockList.Count > 0 && hand.grabItemInHand.GetComponent<ItemProperties>().CanKnock)
            {
                Knock();
            }
            else if (hand.grabItemInHand.GetComponent<Weaponable>())
            {
                hand.grabItemInHand.UseCaughtItemOn(null);
                StartCast(hand.grabItemInHand);                
            }
        }
    }

    #endregion

    #region Player Interaction Logic

    /// <summary>
    /// Improve Experience Value
    /// </summary>
    private void PlayerSleep() 
    {
        if (stateController.playerStateLock) return;
        stateController.ChangeAniState(PlayerInteractAniState.Sleep);
        int lastLevel = playerProperty.currentLevel;
        playerProperty.ChangeLevelValue(true, 1);
        if (playerProperty.currentLevel > lastLevel)
        {
            m_isGrow = true;
            m_materialFloat = 0;
        }
    }

    /// <summary>
    /// When Player Level up, Show Player Material Changing 
    /// </summary>
    private void PlayerGrowUp()
    {
        if (m_isGrow)
        {
            m_materialFloat += Time.deltaTime / 5;
            if (playerProperty.currentLevel == 2 && playerMaterial.GetFloat("Step1To2") < 0.99f)
            {
                Debug.Log("Grow UP !");
                playerMaterial.SetFloat("Step1To2",m_materialFloat);
            }
            else if (playerProperty.currentLevel == 3 && playerMaterial.GetFloat("Step2To3") < 0.99f)
            {
                playerMaterial.SetFloat("Step2To3",m_materialFloat);
            }
            else
            {
                m_isGrow = false;
            }
        }
    }

    /// <summary>
    /// Improve Clean Value
    /// </summary>
    private void PlayerClean() 
    {
        Debug.Log("Clean !");
        if (stateController.playerStateLock) return;
        playerProperty.ChangeCleanValue(true,playerProperty.cleanOnceValue);
        stateController.ChangeAniState(PlayerInteractAniState.Clean);
        EventCenter.Broadcast(GameEvents.BecomeGrowth);
    }

    private void PlayerGrabItemInHand() 
    {
       if (stateController.playerStateLock) return;
       stateController.ChangeAniState(PlayerInteractAniState.Grab);
    }

    //Unity Animation Event
    //When Player Grab Animation Finished, Handle the Grab Logic
    private void GrabItemInHandLogic() 
    {
       if (canTakeList.Count <= 0) return;
       ItemProperties item = canTakeList[0];
       hand.GrabItem(item);
       canTakeList.Remove(item);
       if (canKnockList.Contains(item)) {
           canKnockList.Remove(item);
       }
       item.Catch(hand.playerHandModel);

        //When Grab Weapon, Open Forecast Point
        if (hand.grabItemInHand.GetComponent<Weaponable>())
        {
            m_ForeCast = true;
        }
    }


    private void PlayerReleaseItem() {
       stateController.ChangeAniState(PlayerInteractAniState.Release);
       if (hand.grabItemInHand == null) return;
       canTakeList.Add(hand.grabItemInHand);
        //Close Forecast Point
        if (hand.grabItemInHand.GetComponent<Weaponable>())
        {
            m_ForeCast = false;
            Cast.instance.CastForeCastOff(castPoint);            
        }
        hand.grabItemInHand.Release();
        hand.ReleaseGrabItem();
    }
    
    private void Knock() 
    {
        if (stateController.playerStateLock) return;
        stateController.ChangeAniState(PlayerInteractAniState.Knock);
        m_PlayerOriRotation = transform.rotation;
        transform.rotation = Quaternion.LookRotation(-(canKnockList[0].transform.position - transform.position).Y(transform.position.y).normalized);
        // transform.LookAt(canKnockList[0].transform);
    }

    //Unity Animation Event
    private void KnockLogic() 
    {
        //knockParticle.Play();
        transform.rotation = m_PlayerOriRotation;
        if (canKnockList.Count == 0) return;

        hand.grabItemInHand.KnockWith(canKnockList[0]);
        if (canKnockList[0].IsBroken)
        {
            canKnockList.Remove(canKnockList[0]);
        }

        GetComponent<Rigidbody>().AddForce(transform.forward.normalized * 2
            //* canKnockList[0].force
            , ForceMode.VelocityChange);

        if (hand.grabItemInHand.IsBroken && !hand.grabItemInHand.CanEat) {
            hand.grabItemInHand = null;
        }
    }

    private void EatFoodAniPlay() 
    {
        if (stateController.playerStateLock) return;
        stateController.ChangeAniState(PlayerInteractAniState.Eat);
    }
    

    //Unity Animation Event
    //When Player Eat Animation Finished, Handle the Eat food Logic
    private void EatFood() 
    {
        //if (! hand.grabItemInHand.CanEat) return;
        (float Oxygen, float health) foodAdd = hand.grabItemInHand.Eat();
        
        if (hand.grabItemInHand.GetComponent<Item_Urchin>())
        {
            AnimatorManager.instance.PlayerCelebrate();
        }
        hand.grabItemInHand.transform.parent = null;
        // hand.grabItemInHand.Release();
        hand.grabItemInHand = null;
        playerProperty.ChangeHealthValue(true, foodAdd.health);
        playerProperty.ChangeMaxOxygenValue(foodAdd.Oxygen);
        //playerProperty.ReActiveCounter((int)foodAdd.health);
        playerProperty.ChangeCleanValue(false,playerProperty.eatOnceCleanValue / 2);
        // rendererList.Clear();
        //UI Emotion
        EventCenter.Broadcast(GameEvents.BecomeGrowth);
    }

    /// <summary>
    /// When Player GetDown Cast
    /// </summary>
    private void PrepareCast()
    {
        if (stateController.playerStateLock) return;
        //if (!GameManager.instance.IsGuideLevel)
        //{
        //    m_isCast = true;
        //    Cast.instance.CastForeCastOn(castPoint, m_castTime);
        //}
        stateController.ChangeAniState(PlayerInteractAniState.Cast);
    }

    /// <summary>
    /// Improve cast power when player get down cast
    /// </summary>
    private void Accumulate()
    {
        if (m_castTime < castMaxTime)
        {
            m_castTime += Time.deltaTime;
        }
    }

    /// <summary>
    /// When Player release Cast then throw the obj
    /// </summary>
    /// <param name="castObj"></param>
    private void StartCast(ItemProperties castObj)
    {
        //if (!GameManager.instance.IsGuideLevel)
        //{
        //    Cast.instance.CastForeCastOff(castPoint);
        //}

        m_ForeCast = false;
        Cast.instance.CastForeCastOff(castPoint);

        m_isCast = false;
        PlayerReleaseItem();

        Action objLandCallback = castObj.GetComponent<Weaponable>().NotifyAfterLanded;
        Action objThrowingCallback = castObj.GetComponent<Weaponable>().CheckHitOnThrowing;

        Debug.Log("Cast Obj : " + castObj.name);
        Cast.instance.CastObj(castObj, m_castTime, objLandCallback, objThrowingCallback);
        m_castTime = 0;
    }

    #endregion


    #region items render setting

    public List<Renderer> GetTargetItemRendererList(OutlineTarget target)
    {
        switch (target)
        {
            case OutlineTarget.TakeAndEat:
                return GetPlayerCanTakeItemRendererList();                
            case OutlineTarget.SeaGrass:
                return GetPlayerCanCleanRendererList();                
            default:
                return null;
        }
    }

    public List<Renderer> GetPlayerCanCleanRendererList()
    {
        if (seaWeedsList.Count <= 0) { return null; }
        List<Renderer> rendererList = new List<Renderer>();
        GameObject seaWeedTarget = seaWeedsList[0].gameObject;
        if (seaWeedTarget.GetComponent<Renderer>())
        {
            rendererList.Add(seaWeedTarget.GetComponent<Renderer>());
        }

        Renderer[] renderInChild = seaWeedTarget.GetComponentsInChildren<Renderer>();
        foreach (var render in renderInChild)
        {
            rendererList.Add(render);
        }
        return rendererList;
    }

    public List<Renderer> GetPlayerCanTakeItemRendererList()
    {        
        if((canTakeList.Count <= 0 && hand.grabItemInHand == null)) { return null; }
        rendererList = new List<Renderer>();        

        if (canTakeList.Count > 0) {
            GameObject closestTarget = canTakeList[0].gameObject;
            Debug.Log(closestTarget.name);
            if (closestTarget.GetComponent<Renderer>())
            {
                rendererList.Add(closestTarget.GetComponent<Renderer>());
            }

            Renderer[] renderInChild = closestTarget.GetComponentsInChildren<Renderer>();
            foreach (var render in renderInChild)
            {
                rendererList.Add(render);
            }
        }
        if (hand.grabItemInHand != null) {
            if (hand.grabItemInHand.GetComponent<ItemProperties>()
                &&hand.grabItemInHand.GetComponent<ItemProperties>().CanEat
                && hand.grabItemInHand.GetComponent<ItemProperties>().IsBroken)
                {
                    if (hand.grabItemInHand.GetComponent<Renderer>()) {
                        rendererList.Add(hand.grabItemInHand.GetComponent<Renderer>());
                    }
                    Renderer[] renderInChild2 = hand.grabItemInHand.GetComponentsInChildren<Renderer>();
                    foreach (var render in renderInChild2)
                    {
                        rendererList.Add(render);
                    }
                }
        }

        Debug.Log($"Render List Count: {rendererList.Count}");

        return rendererList;
    }

    #endregion

    //When Get Hurt by Shark, Back to BackPosition
    public void GetHurt(Vector3 backPosition)
    {
        transform.position = new Vector3(backPosition.x, transform.position.y, backPosition.z);
        EventCenter.Broadcast(GameEvents.BecomeHurt);
    }

    public class CompareItem : IComparer<ItemProperties>
    {
        Transform player;
        public CompareItem(Transform player) {
            this.player = player;
        }

        public int Compare(ItemProperties itemA, ItemProperties itemB) {
            if (Vector3.Distance(itemA.transform.position, player.position) > Vector3.Distance(itemB.transform.position,player.position))
            {
                return 1;
            }
            else {
                return -1;
            }
        }
    }

    

}
