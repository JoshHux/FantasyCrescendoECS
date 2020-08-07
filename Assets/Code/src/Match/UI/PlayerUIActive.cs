﻿using HouraiTeahouse.FantasyCrescendo.Core;
using UnityEngine;

namespace HouraiTeahouse.FantasyCrescendo.Matches {

/// <summary>
/// A UI View implemenation that enables or disables objects based 
/// on the player's state.
/// </summary>
public class PlayerUIActive : MonoBehaviour, IView<PlayerUIData> {

#pragma warning disable 0649
  [SerializeField] Object[] _objects;
  [SerializeField] bool _invert;
#pragma warning restore 0649

  public void UpdateView(in PlayerUIData player) {
    bool isActive = player.PlayerData.IsActive;
    if (_invert) isActive = !isActive;
    foreach (var obj in _objects) {
      if (obj is Behaviour behaviour) {
        behaviour.enabled = isActive;
      } else if (obj is GameObject go) {
        go.SetActive(isActive);
      }
    }
  }


}

}