using UnityEngine;

namespace HomeByMarch {
    public interface ISpawnPointStrategy {
        Transform NextSpawnPoint();
    }
}