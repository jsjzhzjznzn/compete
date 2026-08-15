 using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 public class PlayerEnemyDetectionData 
    {
        [field: SerializeField, Header("���˼��")] public float detectionRadius { get; private set; }
        [field: SerializeField] public float detectionLength { get; private set; }

        [field: SerializeField] public LayerMask WhatIsEnemy { get; private set; }

       
    }