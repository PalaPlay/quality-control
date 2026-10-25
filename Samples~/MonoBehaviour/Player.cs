using UnityEngine;
namespace Palapal.Game
{
    /// <summary>Test component.</summary>
    /// <seealso cref="MonoBehaviour"/>
    public class Player : MonoBehaviour
    {
        #region Fields
        [SerializeField] private int health;
        #endregion
        #region Properties
        public int Health => health;
        #endregion
        #region Methods
        private void Awake() { health = 1; }
        #endregion
    }
}
