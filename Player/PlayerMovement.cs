using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ValueShortcut;

/// <summary>
/// Player Action and Movement
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float playerOriSpeed = GlobalSetting.playerInitSpeed;
    [SerializeField]
    private float addSpeedRatio = GlobalSetting.playerAddSpeedRatio;
    [SerializeField]
    private float slowSpeedRatio = 0.75f;
    [SerializeField]
    private float swimTimelyRatio = 2.5f;
    [SerializeField]
    private float playerdiveDepth = 1.5f;
    [SerializeField]
    private float playerdiveSpeed = 3;
    [SerializeField]
    private float colliderRebackSpeed = 5;
    private float m_playerCurSpeed;
    private bool m_isFloat;
    private float playerDiveAim;
    private float playerFloatAim;
    private Rigidbody m_rigidbody;
    private Vector3 m_preVelocity;
    private PlayerStateController m_stateController;
    private Vector3 m_lastFloatPos;//When some collider are above player, translate player to last float position and float up.
    public delegate void PlayerSpeedChangeHandle(PlayerSpeedState speedState);
    public PlayerSpeedChangeHandle playerSpeedChangeHandle;
    public bool isMoveStop;


    [SerializeField] float rotSpeed = 3;  
    [SerializeField] float accelerationSpeed = 0.8f;  
    float h;
    float v;    
    Animator m_Animator;    
    Quaternion rot;
    Vector3 Dir;

    //Just for test
    private float _timer;

    void Start()
    {
        playerFloatAim = transform.position.y;
        playerDiveAim = transform.position.y - playerdiveDepth;
        m_isFloat = true;
        m_playerCurSpeed = playerOriSpeed;
        playerSpeedChangeHandle = PlayerSpeedChange;
        m_rigidbody = GetComponent<Rigidbody>();
        m_stateController = GetComponent<PlayerStateController>();
        m_Animator = GetComponent<Animator>();

        _timer = Time.fixedDeltaTime * 20;        
    }

    void FixedUpdate()
    {
        if (GameManager.instance.GetGameAction() && !GameManager.instance.IsGamePause)
        {
            Movement(m_playerCurSpeed);               
            m_preVelocity = m_rigidbody.velocity;
        }
    }

    void Update()
    {
        Shader.SetGlobalVector("PlayerPosition", transform.position);
        if (h == 0 && v == 0) {
            _timer -= Time.deltaTime;
            if (_timer <= 0) {
                isMoveStop = true;
            }
        }
        else {
            isMoveStop = false;
            _timer = Time.fixedDeltaTime * 20;
        }
    }

    /// <summary>
    /// Change Player's Current Speed When Player Move to Different Place
    /// Change Player's Current Speed When Player Transfer Different Speed State
    /// </summary>
    /// <param name="placeState"></param>
    private void PlayerSpeedChange(PlayerSpeedState speedState) {
        switch(speedState) {
            case PlayerSpeedState.Fast:
                m_playerCurSpeed = playerOriSpeed * addSpeedRatio;
                break;
            case PlayerSpeedState.Stop:
                //m_playerCurSpeed = 0;
                break;
            case PlayerSpeedState.Normal:
                m_playerCurSpeed = playerOriSpeed;
                break;
            default:
                break;          
        }
    }
    
    /// <summary>
    ///   W
    /// A S D  to Controll Player Movement with Current Speed
    /// Space bar to Let Player Dive in the Sea with Dive Speed
    /// </summary>
    /// <param name="speed"></param>
    private void Movement(float speed) 
    {
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");
        Vector3 world2Screen = Camera.main.WorldToScreenPoint(transform.position);
        Vector3 screenXOffset = Camera.main.ScreenToWorldPoint(new Vector3(world2Screen.x + 1, world2Screen.y, world2Screen.z));
        Vector3 screenYOffset = Camera.main.ScreenToWorldPoint(new Vector3(world2Screen.x, world2Screen.y + 1, world2Screen.z));
        if (!m_stateController.playerStateLock || m_stateController.playerAniState == PlayerInteractAniState.Grab) 
        {
            Dir = ((screenXOffset - transform.position) * h + (screenYOffset - transform.position) * v).normalized * speed;
            Dir = new Vector3(Dir.x, 0, Dir.z);		
            //m_rigidbody.velocity = Dir;
            m_rigidbody.velocity = Vector3.Lerp(m_rigidbody.velocity, Dir, Mathf.Clamp01(accelerationSpeed * Time.deltaTime));			
            
            if (Dir.x != 0 || Dir.z != 0)
            {									
                rot = Quaternion.LookRotation(Dir.X(Dir.x * -1).Z(Dir.z * -1));	
                // AudioManager.instance.PlayObjectSFX(m_stateController.playerAudio, (SFX_Name)UnityEngine.Random.Range((int)SFX_Name.Swim_A, (int)SFX_Name.Swim_D + 1) , 1);
                if (m_stateController.playerSpeedState == PlayerSpeedState.Stop && m_stateController.playerPlaceState == PlayerPlaceState.Float)
                {
                    transform.rotation = rot;
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                    rot, Mathf.Clamp01(rotSpeed * Time.deltaTime));
                }
                //transform.rotation = Quaternion.Slerp(transform.rotation,
                //rot, Mathf.Clamp01(rotSpeed * Time.deltaTime));
            }	

            if (m_stateController.playerPlaceState == PlayerPlaceState.Dive) 
            {
                PlayerDiveOrFloat (true , playerDiveAim);
            }
            else if (m_stateController.playerPlaceState == PlayerPlaceState.Float) 
            {
                RaycastHit hit;
                if (Physics.Raycast(this.transform.position,this.transform.up, out hit, 30))
                {
                    if (hit.collider.GetComponent<ItemProperties>())
                    {
                        this.transform.position += (new Vector3(m_lastFloatPos.x,transform.position.y, m_lastFloatPos.z) - transform.position).normalized * Time.deltaTime;
                    }
                }
                else
                {
                    PlayerDiveOrFloat (false , playerFloatAim);
                }
            }       
        }
        else {
            m_rigidbody.velocity = Vector3.zero;
        }
        // transform.LookAt(transform.position + new Vector3(h, 0f, v).normalized);
    }

    void OnTriggerExit(Collider other)
    {
        if (m_stateController.playerPlaceState == PlayerPlaceState.Float 
        && other.gameObject.layer == LayerIndex_WaterSurface) {
            m_isFloat = false; 
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.gameObject.layer == LayerIndex_WaterSurface) {
            m_isFloat = true; 
        }
    }

    void PlayerDiveOrFloat(bool isDive,float aimDepth) {
        if (isDive)
        {
            if (transform.position.y > aimDepth)
            {
                transform.position += Vector3.down * Time.deltaTime * playerdiveSpeed;
            }
            
        }
        else if (transform.position.y < aimDepth)
        {
            transform.position += Vector3.up * Time.deltaTime * playerdiveSpeed;
        }
        else
        {
            m_lastFloatPos = transform.position;
        }
    }

    public float GetCurrentSpeed() {
        return m_playerCurSpeed;
    }

    /// <summary>
    /// When PLayer Move to Different Water Area,
    /// Reset Dive or Float Height.
    /// </summary>
    /// <param name="isAdd"></param>
    /// <param name="height"></param>
    public void SetDiveOrFloatHeight(bool isAdd, float height) {
        if (isAdd) {
            playerDiveAim += height;
            playerFloatAim += height;
        }
        else {
            playerDiveAim -= height;
            playerFloatAim -= height;
        }
    }

    /// <summary>
    /// Correct Player Direction When Player Hit the Collider
    /// </summary>
    /// <param name="collision"></param>
    public void OnCollisionEnter(Collision collision)
    {  
        ContactPoint contactPoint = collision.contacts[0];
        Vector3 newDir = Vector3.zero;
        Vector3 curDir = transform.TransformDirection(Vector3.forward);
        newDir = Vector3.Reflect(Dir, contactPoint.normal);
        rot = Quaternion.FromToRotation(Vector3.forward, newDir);
        m_rigidbody.velocity = ( newDir.normalized * m_preVelocity.x / (m_preVelocity.normalized.x + 0.01f) ).normalized * colliderRebackSpeed;
    }

    /// <summary>
    /// Decrease Speed ratio
    /// Remember Add Speed after Slow Down
    /// </summary>
    /// <param name="ratio"></param>
    public void ReturnCurSpeed(float ratio) {
        m_playerCurSpeed = m_playerCurSpeed / ratio;
        DetectSpeedRange();
    }

    /// <summary>
    /// Add Speed ratio
    /// Remember Decrease Speed after Add Speed
    /// </summary>
    /// <param name="ratio"></param>
    public void EffectCurSpeed(float ratio) {
        m_playerCurSpeed = m_playerCurSpeed * ratio;
        DetectSpeedRange();
    }  

    /// <summary>
    /// Avoid Player's Speed being so huge
    /// When Player's Speed exceeds Max Speed, Currect the Speed
    /// </summary>
    private void DetectSpeedRange() 
    {
        if (m_stateController.playerSpeedState != PlayerSpeedState.Fast) {
            if (m_playerCurSpeed > playerOriSpeed * swimTimelyRatio) 
            {
                m_playerCurSpeed = playerOriSpeed * addSpeedRatio;
            }
        }
        else {
            if (m_playerCurSpeed > playerOriSpeed * swimTimelyRatio * addSpeedRatio) 
            {
                m_playerCurSpeed = playerOriSpeed * addSpeedRatio * addSpeedRatio;
            }
        }
    }

    //Unity Animation Event
    private void SwimFastSpeed() {
        EffectCurSpeed(swimTimelyRatio);
    }

    //Unity Animation Event
    private void SwimSlowSpeed() {
        ReturnCurSpeed(swimTimelyRatio);
    }  
}
