using System.Linq.Expressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Record Player Properties and Change Properties Value
/// </summary>
public class PlayerProperty : MonoBehaviour
{    
    //健康值
    //Full
    [Header("Full Value")]
    [SerializeField] private float maxHealthValue = GlobalSetting.playerInitFull;
    [SerializeField] public float currentHealthValue;
    [SerializeField] public float hungryHealthValue = GlobalSetting.playerHungryFull;
    [SerializeField] public float agonyHealthValue = GlobalSetting.playerAgonyFull;
    [SerializeField] public float sleepHealthValue = 0;
    [SerializeField] public float onceHealthValue = GlobalSetting.timelyFullConsume;

    //体力值
    [Space(10)]
    [Header("Power Value")]
    [SerializeField] private float maxPowerValue = GlobalSetting.playerInitPower;
    // [SerializeField] private float maxCountValue;
    // [SerializeField] public float currentCounterValue;
    [SerializeField] public float currentPowerValue;
    [SerializeField] public float weakPowerValue;
    [SerializeField] public float timelyPowerValue = GlobalSetting.timelyPowerConsume;
    // [SerializeField] private int initCounterNum;
    // [SerializeField] private int currentCounterNum;
    // Dictionary<int,bool> powerCountState = new Dictionary<int, bool>();
    
    //清潔值
    [Space(10)]
    [Header("Clean Value")]
    [SerializeField] private float maxCleanValue = GlobalSetting.playerInitClean;
    [SerializeField] public float currentCleanValue;
    [SerializeField] public float dirtyCleanValue = GlobalSetting.dirtyClean;
    [SerializeField] public float dirtyTwiceCleanValue = GlobalSetting.dirtyTwiceClean;
    [SerializeField] public float dangerCleanValue = GlobalSetting.dangerClean;
    [SerializeField] public float cleanOnceValue;
    [SerializeField] public float eatOnceCleanValue = GlobalSetting.onceDirtyConsume;
    //[SerializeField] private float 

    //氧氣值
    [Space(10)]
    [Header("Oxygen Value")]
    [SerializeField] private float maxOxygenValue = GlobalSetting.playerInitOxy;
    [SerializeField] public float currentOxygenValue;
    [SerializeField] public float weakOxygenValue;
    [SerializeField] public float timelyOxygenValue = GlobalSetting.timelyOxyConsume;
  
    //不同健康、清洁状态对应的速度速率
    [Space(10)]
    [Header("Different State Responding Speed Ratio")]
    [SerializeField] [Range(0.01f,1)] public float dirtyCleanSpeedRatio = GlobalSetting.playerDirtySpeedRatio;
    [SerializeField] [Range(0.01f,1)] public float dangerCleanSpeedRatio = GlobalSetting.playerDangerCleanSpeedRatio;
    [SerializeField] [Range(0.01f,1)] public float agonyHealthSpeedRatio = GlobalSetting.playerAgonyFullSpeedRatio;
    private PlayerStateController stateController;

    //Exprience
    Dictionary<int,float> maxExperienceValue = new Dictionary<int, float>
    {
        {0, 100},
        {1, 200},
        {2,300},
        {3,400}
    };
    [SerializeField] public float currentExperienceValue ;
    [SerializeField] public int currentLevel ;

    // Dictionary<int, (float health, float clean, float oxygen, float exp)> LevelMap = new Dictionary<int, (float health, float clean, float oxygen, float exp)>
    // {
    //     {1, (100, )}
    // };



    void Start()
    {
        currentLevel = 0;
        currentHealthValue = maxHealthValue;
        currentCleanValue = maxCleanValue;
        cleanOnceValue = maxCleanValue;
        currentOxygenValue = maxOxygenValue;
        currentPowerValue = maxPowerValue;
        currentExperienceValue = maxExperienceValue[currentLevel];
        stateController = GetComponent<PlayerStateController>();

        // if (initCounterNum == 0) 
        // {
        //     Debug.Log("Forget to init Counter Number !");
        //     initCounterNum = 1;
        // }

        // //Init counter state
        // for (int i = 1; i <= initCounterNum; i++)
        // {
        //     powerCountState.Add(i,true);
        // }
        // currentCounterNum = initCounterNum;
        // currentCounterValue = maxCountValue;
    }

    void Update()
    {
        if (GameManager.instance.GetGameAction() && !GameManager.instance.IsGamePause)
        {
            if (!GameManager.instance.IsGuideLevel && stateController.playerAniState != PlayerInteractAniState.Sleep)
            {
                ChangeHealthValue(false, onceHealthValue * Time.deltaTime);
            }
            DetectOxyChange();
            DetectPowerChange();
        }
        //ChangeMaxPowerAndOxy();
        //DetectCounterChange();
    }

    /// <summary>
    /// Update Player Oxygen Value Timely
    /// </summary>
    private void DetectOxyChange() 
    {
        if (stateController.playerPlaceState == PlayerPlaceState.Dive) {
            if (currentOxygenValue <= 0) {
                return;
            }
            ChangeOxygenValue(false, timelyOxygenValue * Time.deltaTime);
        }
        else {
            if (currentOxygenValue >= maxOxygenValue) { 
                if (currentPowerValue >= maxPowerValue) {
                    EventCenter.Broadcast(GameEvents.HideOxygenAndPower);
                }
                return;
            }
            ChangeOxygenValue(true, 2 * timelyOxygenValue * Time.deltaTime);
        }
    }

    /// <summary>
    /// Update PLayer Power Value Timely
    /// </summary>
    private void DetectPowerChange() 
    {
        if (stateController.playerSpeedState == PlayerSpeedState.Fast && currentPowerValue > 0 && stateController.playerPlaceState != PlayerPlaceState.Dive) {
            //If player's level can move cross the waterfall, dont decrease player's power value
            if (!stateController.playerAddSpeedLock && stateController.playerPlaceState == PlayerPlaceState.WaterFall) { return; }
            
            ChangePowerValue(false, timelyPowerValue * Time.deltaTime);
            if (currentPowerValue <= maxPowerValue / 10 && stateController.playerPlaceState == PlayerPlaceState.WaterFall)
            {
                EventCenter.Broadcast(GameEvents.BecomeSleepy);
            }
        }
        else {
            if (currentPowerValue >= maxPowerValue) {
                if (currentOxygenValue > maxOxygenValue) {
                    EventCenter.Broadcast(GameEvents.HideOxygenAndPower);
                }
                return;
            }
            //When Player is not in Waterfall and dont input AddSpeed Keycode, Return Power Value
            if (!Input.GetKey(GlobalSetting.AddSpeedKey) && stateController.playerSpeedState != PlayerSpeedState.Fast 
            && !stateController.playerAddSpeedLock && stateController.playerPlaceState != PlayerPlaceState.Dive) 
            {
                ChangePowerValue(true, 2 * timelyPowerValue * Time.deltaTime);
            }
        }
    }

    // private void DetectCounterChange()
    // {
    //     if (!IfCountOut() && (stateController.playerSpeedState == PlayerSpeedState.Fast || stateController.playerPlaceState == PlayerPlaceState.Dive)) 
    //     {
    //         ChangeCounterValue(false, timelyPowerValue * Time.deltaTime);
    //     }
    //     else {
    //         if (powerCountState[powerCountState.Count] && currentCounterValue >= maxCountValue) {
    //             return;
    //         }
            
    //         if (stateController.playerPlaceState == PlayerPlaceState.Dive && IfCountOut())
    //         {
    //             return;
    //         }
    //         //When Player is not in Waterfall and dont input AddSpeed Keycode, Return Power Value
    //         if (!Input.GetKey(GlobalSetting.AddSpeedKey) && stateController.playerSpeedState != PlayerSpeedState.Fast 
    //         && !stateController.playerAddSpeedLock) 
    //         {
    //             ChangeCounterValue(true, timelyPowerValue * Time.deltaTime);
    //         }
    //     }
    // }

    public void ChangeMaxPowerAndOxy() 
    {
        if (stateController.playerFullState != PlayerFullState.Strong) {
            maxOxygenValue = weakOxygenValue;
            maxPowerValue = weakPowerValue;
            if (currentPowerValue > maxPowerValue) { currentPowerValue = maxPowerValue; }
            if (currentOxygenValue > maxOxygenValue) { currentOxygenValue = maxOxygenValue; }
        }
    }

    public void ChangePowerValue(bool isAdd, float changeNum) 
    {
        if (isAdd) {
            currentPowerValue += changeNum;  
            if (currentPowerValue > maxPowerValue) {currentPowerValue = maxPowerValue;}          
        }
        else {
            currentPowerValue -= changeNum;
            if (currentPowerValue < 0) {currentPowerValue = 0;}
        }
        //EventCenter.Broadcast(GameEvents.UpdatePower, currentPowerValue / maxPowerValue);
    }

    public void ChangeHealthValue(bool isAdd, float changeNum) 
    {
        if (isAdd) {
            currentHealthValue += changeNum;  
            if (currentHealthValue > maxHealthValue) {currentHealthValue = maxHealthValue;}          
        }
        else {
            currentHealthValue -= changeNum;
            if (currentHealthValue < 0) {currentHealthValue = 0;}
        }
        //EventCenter.Broadcast(GameEvents.UpdateHealth, currentHealthValue / maxHealthValue);
    }

    public void ChangeCleanValue(bool isAdd, float changeNum) 
    {
        if (isAdd) {
            currentCleanValue += changeNum;
            if (currentCleanValue > maxCleanValue) {currentCleanValue = maxCleanValue;}
        }
        else {
            currentCleanValue -= changeNum;
            if (currentCleanValue < 0) { currentCleanValue = 0;}
        }
        //EventCenter.Broadcast(GameEvents.UpdateClean, currentCleanValue / maxCleanValue);
    }

    public void ChangeOxygenValue(bool isAdd, float changeNum) 
    {
        if (isAdd) {
            currentOxygenValue += changeNum;
            if (currentOxygenValue > maxOxygenValue) {currentOxygenValue = maxOxygenValue;}
        }
        else {
            currentOxygenValue -= changeNum;
            if (currentOxygenValue < 0) {currentOxygenValue = 0;}
        }
        //EventCenter.Broadcast(GameEvents.UpdateOxygen, currentOxygenValue / maxOxygenValue);
    }

    public void ChangeMaxOxygenValue(float changeNum) 
    {
        maxOxygenValue += changeNum;
        currentOxygenValue = maxOxygenValue;
        //EventCenter.Broadcast(GameEvents.UpdateOxygen, currentOxygenValue / maxOxygenValue);
    }

    public void ChangeExperienceValue(bool isAdd, float changeNum) 
    {
        if (isAdd) {
            currentExperienceValue += changeNum;
            if (currentExperienceValue > maxExperienceValue[currentLevel]) {currentExperienceValue = maxExperienceValue[currentLevel];}
        }
        else {
            currentExperienceValue -= changeNum;
            if (currentExperienceValue < 0) {currentExperienceValue = 0;}
        }

        if (currentExperienceValue >= maxExperienceValue[currentLevel]) {
            currentExperienceValue = maxExperienceValue[currentLevel] - currentExperienceValue;
            currentLevel ++;
        }
    }

    public void ChangeLevelValue(bool isAdd, int changeNum)
    {
        if (isAdd)
        {
            maxPowerValue += changeNum * GlobalSetting.playerUpLevelPower;
            // maxOxygenValue += changeNum * GlobalSetting.playerUpLevelOxy;
            currentPowerValue = maxPowerValue;
            currentOxygenValue = maxOxygenValue;
            if (maxPowerValue == GlobalSetting.playerInitPower + GlobalSetting.playerUpLevelPower){
                currentLevel ++;
            }
            else if (maxPowerValue == GlobalSetting.playerInitPower + 3 * GlobalSetting.playerUpLevelPower)
            {
                currentLevel ++;
            }
            else if (maxPowerValue == GlobalSetting.playerInitPower + 5 * GlobalSetting.playerUpLevelPower)
            {
                currentLevel ++;
            }
        }
        else{
            if (currentLevel <=0 ) { return; }
            currentLevel -= changeNum;
        }
    }

    // public void ChangeCounterValue(bool isAdd, float changeNum) 
    // {
    //     if (isAdd) {
    //         currentCounterValue += changeNum;  
    //         if (currentCounterValue > maxCountValue) {currentCounterValue = maxCountValue;}          
    //     }
    //     else {
    //         currentCounterValue -= changeNum;
    //         if (currentCounterValue < 0) 
    //         {
    //             currentCounterValue = maxCountValue;
    //             powerCountState[currentCounterNum] = false;
    //             currentCounterNum --;
    //         }
    //     }
    // }

    /// <summary>
    /// Judge if player's Counts has gone
    /// </summary>
    /// <returns></returns>
    // public bool IfCountOut()
    // {
    //     return !powerCountState[1];
    // }

    /// <summary>
    /// ReActive Player Empty Counter to full
    /// </summary>
    /// <param name="activeNum"></param>
    // public void ReActiveCounter(int activeNum)
    // {
    //     for (int i = 0; i < activeNum; i++)
    //     {
    //         if (currentCounterNum < powerCountState.Count)
    //         {
    //             powerCountState[currentCounterNum + 1] = true;
    //             currentCounterNum ++;
    //         }
    //         else{
    //             return;
    //         }
    //     }
    // }

    /// <summary>
    /// Add Player Empty Counter
    /// </summary>
    /// <param name="addNum"></param>
    // public void AddCounterNum(int addNum)
    // {
    //     for (int i = 0; i < addNum; i++)
    //     {
    //         powerCountState.Add(powerCountState.Count + 1, false);
    //     }
    // }
}
