using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player Hand with Left and Right
/// </summary>
public class PlayerHand : MonoBehaviour
{
    // [SerializeField]
    // public FragileTakableItemBase leftHand {get; private set;}
    // [SerializeField]
    // public FragileTakableItemBase rightHand {get; private set;}
    // [SerializeField]
    // public int itemNumInHand {get; private set;}
    [SerializeField]
    public ItemProperties grabItemInHand;
    [SerializeField]
    public Transform playerHandModel;
    [SerializeField]
    private Transform playerFanShellPos;
    private Transform lastParent;
    void Start()
    {
        // leftHand = null;
        // rightHand = null;
        if (playerHandModel == null) {
            Debug.LogError("Dont Add PlayerHand Model On the Script !");
        }

        if (playerFanShellPos == null) {
            Debug.LogError("Dont Add FanShell_Position On the Script !");
        }          
    }

    public void GrabItem(ItemProperties item) {
        if (grabItemInHand != null) return;
        lastParent = item.transform.parent; 
        if (!item.GetComponent<FanShell>())
        {       
            grabItemInHand = item;        
            grabItemInHand.transform.SetParent(playerHandModel.transform);        
            grabItemInHand.transform.localPosition = Vector3.zero;            
        }
        else
        {
            item.transform.SetParent(playerFanShellPos.transform);
            item.transform.localPosition = Vector3.zero;
            GetComponent<PlayerStateController>().GetPassGame();
        }
        //this.transform.LookAt(item.transform);
    }

    public void ReleaseGrabItem() {
        if (grabItemInHand == null) return;
        grabItemInHand.transform.parent = lastParent;
        grabItemInHand = null;
    }
}
