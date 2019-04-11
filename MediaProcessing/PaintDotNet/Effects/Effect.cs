using System;
using System.Drawing;

namespace PaintDotNet.Effects
{
    /// <summary>
    /// Describes an interface for some sort of source->destination rendering effect.
    /// The pixels of the destination are the result of apply some function to the
    /// pixel values of the source that may be affect by some other state.
    /// </summary>
    public abstract class Effect
    {
        //private string name;
        //private string description;
        //private Image image;
		private PdnRegion effectSelection;
		//private bool singleThreaded;
	

		/// <summary>
		/// Gets whether an Effect wants to be rendered using only 1 thread.
		/// It is up to the caller of Render() to enforce this.
		/// </summary>





		public PdnRegion Selection
		{
			get
			{
				if (effectSelection == null || effectSelection.IsEmpty()) 
				{
					effectSelection = new PdnRegion();
					effectSelection.MakeInfinite();
				}
				return effectSelection;
			}

			set
			{
				effectSelection = value;
			}
		}
        /// <summary>
        /// Performs the effect's rendering. The source is to be treated as read-only,
        /// and only the destination pixels within the given rectangle-of-interest are
        /// to be written to. However, in order to compute the destination pixels,
        /// any pixels from the source may be utilized.
        /// </summary>
        /// <param name="dstArgs">Describes the destination surface.</param>
        /// <param name="srcArgs">Describes the source surface.</param>
        /// <param name="roi">The rectangle we want rendered in dstArgs.</param>
        public virtual void Render(RenderArgs dstArgs, RenderArgs srcArgs, Rectangle roi)
        {
            if (dstArgs.Surface.Size != srcArgs.Surface.Size)
            {
                throw new ArgumentException("Destination surface and Source surface sizes do not match", "dstArgs, srcArgs");
            }
            Rectangle checkRect = Rectangle.Intersect(dstArgs.Surface.Bounds, roi);
            if (checkRect != roi)
            {
                throw new ArgumentOutOfRangeException("roi", "Region of interest was out of bounds");
            }
        }

        /// <summary>
        /// This is a helper function. For every rectangle that makes up the requested
        /// region, the other form of Render will be called.
        /// </summary>
        /// <param name="dstArgs">Describes the destination surface.</param>
        /// <param name="srcArgs">Describes the source surface.</param>
        /// <param name="roi">The region we want rendered in dstArgs.</param>
        public void Render(RenderArgs dstArgs, RenderArgs srcArgs, PdnRegion roi)
        {
            Rectangle[] rects = roi.GetRegionScansReadOnlyInt();            

            foreach (Rectangle rect in rects)
            {                
            	Render(dstArgs, srcArgs, rect);
            }
        }

        /// <summary>
        /// This is a helper function. It allows you to render an effect "in place."
        /// That is, you don't need both a destination and a source Surface.
        /// </summary>
        private Surface renderSurface = null;
        public void RenderInPlace(RenderArgs srcAndDstArgs, PdnRegion roi)
        {
            if (renderSurface == null || renderSurface.Size != srcAndDstArgs.Surface.Size)
            {
                renderSurface = new Surface(srcAndDstArgs.Surface.Size);
            }

            using (PdnRegion simplifiedRegion = Utility.SimplifyAndInflateRegion(roi))
            {
                simplifiedRegion.Intersect(renderSurface.Bounds);

                using (RenderArgs renderArgs = new RenderArgs(renderSurface))
                {
                    Render(renderArgs, srcAndDstArgs, simplifiedRegion);
                    srcAndDstArgs.Surface.CopySurface(renderSurface, roi);
                }
            }
        }

        public void RenderInPlace(RenderArgs srcAndDstArgs, Rectangle roi)
        {
            PdnRegion region = new PdnRegion(roi);
            
            RenderInPlace(srcAndDstArgs, region);
            region.Dispose();
		}


	
    }
}
