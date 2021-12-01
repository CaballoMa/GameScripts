using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerProperty))]
[RequireComponent(typeof(PlayerHand))]
public class PlayerStateController : MonoBehaviour
{
    [SerializeField]
    public PlayerPlaceState playerPlaceState; //角色所在地型狀態
    [SerializeField]
    public PlayerPlaceState playerTempPlaceState;// 临时角色所在地形状态 
    [SerializeField]
    public PlayerSpeedState playerSpeedState; //角色速度狀態
    public PlayerFullState playerFullState; //角色饱腹状态  
    public PlayerInteractAniState playerAniState; //角色动画状态
    public PlayerCleanState playerCleanState;
    private PlayerMovement playerMovement; //角色移動組件
    private PlayerProperty playerProperty; //角色屬性組件  
    private PlayerHand playerHand; //角色手部组件
    public AudioSource playerAudio { get; private set;} //角色AudioSource组件
    public bool playerCanClean; //角色是否可清潔
    public bool playerCanSleep;//角色是否可睡觉
    public bool playerStateLock;//角色状态锁，用于防止角色同一时间处于多个状态
    public bool playerAddSpeedLock {get; private set; }//角色加速锁，用于处理玩家在不同等级瀑布中的逻辑
    public bool onKnock {get; private set;}
    public bool canPassGame;
    [SerializeField]
    private ParticleSystem playerFloatParticle;
    [SerializeField]
    private Material playerMaterial;

   void Start()
   {
       if (playerFloatParticle == null)
        {
            Debug.LogError("Require Player Float Particle System !");
        }
       playerMovement = GetComponent<PlayerMovement>();
       playerProperty = GetComponent<PlayerProperty>();
       playerHand = GetComponent<PlayerHand>();
       playerPlaceState = PlayerPlaceState.Float; //默認為飄浮狀態     
       playerFullState = PlayerFullState.Strong; //默认为健康状态 
       playerCleanState = PlayerCleanState.Clean; //默认为干净状态    
       playerAudio = GetComponent<AudioSource>(); 
   }
    void Update()
    {
        if (GameManager.instance.GetGameAction() && !GameManager.instance.IsGamePause)
        {
            PlayerSpeedStateChange();
            PlayerPlaceStateChange();
            PlayerFullStateChange();
            PlayerCleanStateChange();
            //PlayerKnockStateChange();
            PlayerSleepStateChange();
        }
    }

    /// <summary>
    /// When Player's Health Value Change,
    /// Update Player Healthy State and Responding Speed
    /// </summary>
    private void PlayerFullStateChange() 
    {
        // if (playerProperty.currentHealthValue <= 0) {
        //     playerHealthState = PlayerHealthState.Dead;
        //     return;
        // }
        //Change to Strong
        if (playerFullState != PlayerFullState.Strong 
        && playerProperty.currentHealthValue > playerProperty.hungryHealthValue) 
        {
            if (playerFullState == PlayerFullState.Agony) {
                //If update more elements in Game, change these scripts, because they cannot do in State Controller
                playerMovement.ReturnCurSpeed(1 - playerProperty.agonyHealthSpeedRatio);
            }
            playerFullState = PlayerFullState.Strong;
        }

        //Change to Hungry
        if (playerFullState != PlayerFullState.Hungry
        && (playerProperty.currentHealthValue > playerProperty.agonyHealthValue
        && playerProperty.currentHealthValue < playerProperty.hungryHealthValue))
        {
            if (playerFullState == PlayerFullState.Agony) {
                //If update more elements in Game, change these scripts, because they cannot do in State Controller
                playerMovement.ReturnCurSpeed(1 - playerProperty.agonyHealthSpeedRatio);
            }
            playerFullState = PlayerFullState.Hungry;
            EventCenter.Broadcast(GameEvents.BecomeHungry);
        }

        //Change to Agony
        else if (playerFullState != PlayerFullState.Agony 
        && playerProperty.currentHealthValue <= playerProperty.agonyHealthValue) 
        {
            playerMovement.EffectCurSpeed(1 - playerProperty.agonyHealthSpeedRatio);
            playerFullState = PlayerFullState.Agony;
            EventCenter.Broadcast(GameEvents.BecomeHungry);
        }
    }

    /// <summary>
    /// When Player's Health Value Change,
    /// Update Player Healthy State and Responding Speed
    /// </summary>
    private void PlayerCleanStateChange()
    {
        if (playerCleanState != PlayerCleanState.Clean && playerProperty.currentCleanValue > playerProperty.dirtyCleanValue)
        {
            switch (playerCleanState)
            {
                case PlayerCleanState.Dirty:
                    playerMovement.ReturnCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio);
                    break;
                case PlayerCleanState.TwiceDirty:
                    playerMovement.ReturnCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio * 2);
                    break;
                case PlayerCleanState.Weak:
                    playerMovement.ReturnCurSpeed(1 - playerProperty.dangerCleanSpeedRatio);
                    break;
                default:
                    break;
            }
            playerCleanState = PlayerCleanState.Clean;
            //If update more elements in Game, change these scripts, because they cannot do in State Controller
            playerMaterial.SetFloat("Dirt1_Lerp",0);
            playerMaterial.SetFloat("Dirt2_Lerp",0);
            playerMaterial.SetFloat("Dirt3_Lerp",0);
            //
        }

        if (playerCleanState != PlayerCleanState.Dirty 
        && playerProperty.currentCleanValue <= playerProperty.dirtyCleanValue
        && playerProperty.currentCleanValue > playerProperty.dirtyTwiceCleanValue)
        {
            switch (playerCleanState)
            {
                case PlayerCleanState.Clean:
                    playerMovement.EffectCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio);
                    break;
                case PlayerCleanState.TwiceDirty:
                    playerMovement.ReturnCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio);
                    break;
                case PlayerCleanState.Weak:
                    playerMovement.ReturnCurSpeed(1 - playerProperty.dangerCleanSpeedRatio);
                    playerMovement.EffectCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio);
                    break;
                default:
                    break;
            }
            playerCleanState = PlayerCleanState.Dirty;
            //If update more elements in Game, change these scripts, because they cannot do in State Controller
            playerMaterial.SetFloat("Dirt1_Lerp",1);
            playerMaterial.SetFloat("Dirt2_Lerp",0);
            playerMaterial.SetFloat("Dirt3_Lerp",0);
            //
        }

        if (playerCleanState != PlayerCleanState.TwiceDirty
        && playerProperty.currentCleanValue <= playerProperty.dirtyTwiceCleanValue
        && playerProperty.currentCleanValue > playerProperty.dangerCleanValue)
        {
            switch (playerCleanState)
            {
                case PlayerCleanState.Clean:
                    playerMovement.EffectCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio * 2);
                    break;
                case PlayerCleanState.Dirty:
                    playerMovement.EffectCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio);
                    break;
                case PlayerCleanState.Weak:
                    playerMovement.ReturnCurSpeed(1 - playerProperty.dangerCleanSpeedRatio);
                    playerMovement.EffectCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio * 2);
                    break;
                default:
                    break;
            }
            playerCleanState = PlayerCleanState.TwiceDirty;
            //If update more elements in Game, change these scripts, because they cannot do in State Controller
            playerMaterial.SetFloat("Dirt1_Lerp",1);
            playerMaterial.SetFloat("Dirt2_Lerp",1);
            playerMaterial.SetFloat("Dirt3_Lerp",0);
            //
        }

        if (playerCleanState != PlayerCleanState.Weak
        && playerProperty.currentCleanValue <= playerProperty.dangerCleanValue)
        {
            switch (playerCleanState)
            {
                case PlayerCleanState.Clean:
                    playerMovement.EffectCurSpeed(1- playerProperty.dangerCleanSpeedRatio);
                    break;
                case PlayerCleanState.Dirty:
                    playerMovement.ReturnCurSpeed(1- playerProperty.dirtyCleanSpeedRatio);
                    playerMovement.EffectCurSpeed(1- playerProperty.dangerCleanSpeedRatio);
                    break;
                case PlayerCleanState.TwiceDirty:
                    playerMovement.ReturnCurSpeed(1 - playerProperty.dirtyCleanSpeedRatio * 2);
                    playerMovement.EffectCurSpeed(1 - playerProperty.dangerCleanSpeedRatio);
                    break;
                default:
                    break;
            }
            playerCleanState = PlayerCleanState.Weak;
            //If update more elements in Game, change these scripts, because they cannot do in State Controller
            playerMaterial.SetFloat("Dirt1_Lerp",1);
            playerMaterial.SetFloat("Dirt2_Lerp",1);
            playerMaterial.SetFloat("Dirt3_Lerp",1);
            //
        }
    }

    /// <summary>
    /// When Player Input Add Speed Keycode Or Decrease Speed by something,
    /// Call Event to Change Responding Current Speed;
    /// </summary>
    private void PlayerSpeedStateChange() 
    {
        if (playerMovement.isMoveStop) {
            playerSpeedState = PlayerSpeedState.Stop;
            playerMovement.playerSpeedChangeHandle.Invoke(playerSpeedState);
        }
        else {
            if (playerProperty.currentPowerValue <= 0 && playerPlaceState != PlayerPlaceState.Dive)
            //if (playerProperty.IfCountOut() && playerPlaceState != PlayerPlaceState.Dive)
            {
                playerSpeedState = PlayerSpeedState.Normal;
                //If update more elements in Game, change these scripts, because they cannot do in State Controller
                playerMovement.playerSpeedChangeHandle.Invoke(playerSpeedState);
                EventCenter.Broadcast(GameEvents.BecomeTired);
                return;
            }
            
            if (playerSpeedState != PlayerSpeedState.Fast && (
                (playerPlaceState == PlayerPlaceState.Dive || Input.GetKey(GlobalSetting.AddSpeedKey)) &&
                playerHand.grabItemInHand == null)) 
            {
                playerSpeedState = PlayerSpeedState.Fast;
                //If update more elements in Game, change these scripts, because they cannot do in State Controller
                playerMovement.playerSpeedChangeHandle.Invoke(playerSpeedState);
            }
            
            else if (playerSpeedState != PlayerSpeedState.Normal && (
            (!Input.GetKey(GlobalSetting.AddSpeedKey) && playerPlaceState != PlayerPlaceState.Dive) ||
            playerHand.grabItemInHand != null) )
            {
                playerSpeedState = PlayerSpeedState.Normal;
                //If update more elements in Game, change these scripts, because they cannot do in State Controller
                playerMovement.playerSpeedChangeHandle.Invoke(playerSpeedState);            
            }
        }
        
    }

    /// <summary>
    /// Change Player Dive or Float State
    /// </summary>
    private void PlayerPlaceStateChange() 
    {
        if (playerStateLock) { return; } 
        if (playerProperty.currentOxygenValue <= GlobalSetting.playerOutOxyHint && playerPlaceState == PlayerPlaceState.Dive)
        {
            if (playerProperty.currentOxygenValue <= 0)
            {
                playerPlaceState = PlayerPlaceState.Float;
                AudioManager.instance.ChangeAudioLowpassCutoff(false);
                PostProcessingManager.instance.ChangeSeaAlpha(false);
                AnimatorManager.instance.DetectDiveOrFloatAniPlay();
                playerFloatParticle.startSize = 0.4f;
                playerFloatParticle.loop = false;
            }
            else
            {
                playerFloatParticle.startSize = 0.2f;
                playerFloatParticle.loop = true;
                PlayerOutOxygenParticlePlay();
            }

            //AudioManager.instance.PlayLocalSFX(SFX_Name.DiveAndFloat, transform.position, 1);
        }
        else if (playerPlaceState != PlayerPlaceState.WaterFall && (Input.GetKeyDown(GlobalSetting.DiveKey))) {    
            if (playerPlaceState == PlayerPlaceState.Dive) {           
                playerPlaceState = PlayerPlaceState.Float;

                //If update more elements in Game, change these scripts, because they cannot do in State Controller
                AudioManager.instance.ChangeAudioLowpassCutoff(false);
                PostProcessingManager.instance.ChangeSeaAlpha(false);
                AnimatorManager.instance.DetectDiveOrFloatAniPlay();
                //AudioManager.instance.PlayLocalSFX(SFX_Name.DiveAndFloat, transform.position, 1);
                //

            }

            else if (playerPlaceState == PlayerPlaceState.Float && playerProperty.currentOxygenValue > 0)
            //else if (playerPlaceState == PlayerPlaceState.Float && !playerProperty.IfCountOut()) 
            {       
                playerPlaceState = PlayerPlaceState.Dive;
                
                //If update more elements in Game, change these scripts, because they cannot do in State Controller
                AudioManager.instance.ChangeAudioLowpassCutoff(true);
                PostProcessingManager.instance.ChangeSeaAlpha(true);
                AnimatorManager.instance.DetectDiveOrFloatAniPlay();
                AudioManager.instance.PlayLocalSFX(SFX_Name.DiveAndFloat, transform.position, 1);
                //
            }
        }
    }

    /// <summary>
    /// When Player's Anim is in Knock and the Knock Animation has done,
    /// When Player Input KeyCode Without Knock again, Finish Knock State.
    /// </summary>
    private void PlayerKnockStateChange() 
    {
        if (!onKnock && playerAniState == PlayerInteractAniState.Knock) {
            onKnock = true;
        }
        else if (onKnock && Input.anyKeyDown) {
            //AnimatorManager.instance.OffLockState();
            StateOffLock();
            onKnock = false;
        }       
    }

    private void PlayerSleepStateChange() 
    {
        if (playerAniState != PlayerInteractAniState.Sleep) { return; }
        if (GameManager.instance.GetCurTime() >= 0
        && GameManager.instance.GetDayState() == DayState.Day)
        {
            //AnimatorManager.instance.OffLockState();
            StateOffLock();
        }
    }

    public void GetPassGame()
    {
        canPassGame = true;
    }

    //Our Leader Like a Shit
    void OnTriggerEnter(Collider other)
    {
        TerrainBase terrain;
        if (other.GetComponent<TerrainBase>() && playerPlaceState != PlayerPlaceState.Dive){
            terrain = other.GetComponent<TerrainBase>();
            if (terrain.GetComponent<Env_WaterFall>()) {
                playerPlaceState = PlayerPlaceState.WaterFall;
                //Add Lock to Avoid PLayer's Power Return
                if(playerProperty.currentLevel < terrain.GetComponent<RampTerrainBase>().waterfallLevel)
                {
                    playerAddSpeedLock = true;
                }
            }
        }
        else {
            return;
        }

        if (terrain.canClean) {
            playerCanClean = true;
            
        } 
        
        if (terrain.canSleep) {
            playerCanSleep = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        TerrainBase terrain;
        if (other.GetComponent<TerrainBase>()){
            terrain = other.GetComponent<TerrainBase>();
            if (playerPlaceState == PlayerPlaceState.WaterFall 
            && terrain.GetComponent<Env_WaterFall>()) {
                playerPlaceState = PlayerPlaceState.Float;
                playerAddSpeedLock = false;
            }
        }
        else {
            return;
        }

        if (terrain.canClean) {
            playerCanClean = false;
        } 
        
        if (terrain.canSleep) {
            playerCanSleep = false;
        }      
    }

    public void ChangeAniState(PlayerInteractAniState aniState) 
    {
        playerAniState = aniState;
    }

    //Unity Animation Event
    public void StateOnLock() 
    {
        //Debug.Log("OnLock !");
        playerStateLock = true;
    }

    //Unity Animation Event
    public void StateOffLock() 
    {
        //Debug.Log("OffLock !");
        playerStateLock = false;
        ChangeAniState(PlayerInteractAniState.Idle);
    }
    /// <summary>
    // TODO : When Add Particle System Manager, Transfer the Function to Manager
    /// </summary>
    public void PlayerOutOxygenParticlePlay()
    {
        if (playerFloatParticle.isPlaying) { return; }
        playerFloatParticle.Play();
    }
}
