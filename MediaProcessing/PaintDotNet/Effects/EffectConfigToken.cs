using System;

namespace PaintDotNet.Effects
{
    /// <summary>
    /// Summary description for EffectConfigToken.
    /// </summary>
    [Serializable]
    public abstract class EffectConfigToken
        : ICloneable
    {
        #region ICloneable Members
        /// <summary>
        /// This should simply call "new myType(this)" ... do not call base class'
        /// implementation of Clone, as this is handled by the constrcutors..
        /// </summary>
        public abstract object Clone();
        #endregion
        
        public EffectConfigToken()
        {
        }

        protected EffectConfigToken(EffectConfigToken copyMe)
        {
        }
    }
}

