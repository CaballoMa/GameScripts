using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player Bag with two room to put items in
/// </summary>
public class PlayerBag : MonoBehaviour
{
    [SerializeField]
    private ItemProperties leftBag;
    [SerializeField]
    private ItemProperties rightBag;
    private int m_bagCount;

    public void StoreLeftBag(ItemProperties item) {
        if (leftBag != null) return;
        leftBag = item;
        m_bagCount ++;
    }

    public ItemProperties ClearLeftBag() {
        if (leftBag == null) return null;
        ItemProperties clear = leftBag;
        leftBag = null;
        m_bagCount --;
        return clear;
    }

    public void StoreRightBag(ItemProperties item) {
        if (rightBag != null) return;
        rightBag = item;
        m_bagCount ++;
    }

    public ItemProperties ClearRightBag() {
        if (rightBag == null) return null;
        ItemProperties clear = rightBag;
        rightBag = null;
        m_bagCount --;
        return clear;
    }
    public int GetBagLenth() {
        return m_bagCount;
    }
}
