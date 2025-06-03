using UnityEngine;

namespace HomeByMarch {
    public interface IEntityFactory<T> where T : Entity {
        T Create(Transform spawnPoint);
    }
}