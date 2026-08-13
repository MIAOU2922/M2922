namespace M2922.Core
{
    /// <summary>
    /// Base des composants qui doivent être TICKÉS chaque frame par Udon (Update).
    ///
    /// ⚠ N'héritez de cette classe QUE si le composant a réellement besoin d'un
    /// Update() par frame (régénération, animation, polling UI, IA…).
    ///
    /// Les composants purement ÉVÉNEMENTIELS (DamageReceiver, ArmorComponent,
    /// HitboxSystem, DeathHandler…) doivent hériter directement de M2922_Base :
    /// ils n'auront alors AUCUN coût par frame (pas d'event Update dans le programme Udon).
    ///
    /// Si vous surchargez Update(), appelez base.Update() pour conserver le
    /// compteur de frames (ShouldUpdate()).
    /// </summary>
    public class M2922_Tickable : M2922_Base
    {
        protected virtual void Update()
        {
            TickBase();
        }
    }
}
