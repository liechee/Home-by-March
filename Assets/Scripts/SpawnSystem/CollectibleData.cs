using UnityEngine;

namespace HomeByMarch {
    [CreateAssetMenu(fileName = "CollectibleData", menuName = "HomeByMarch/Collectible Data")]
    public class CollectibleData : EntityData {
        public int score;
        // additional properties specific to collectibles
    }
}