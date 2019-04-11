using System;
using System.Drawing;

namespace PaintDotNet.Effects
{
    /// <summary>
    /// Summary description for SharpenEffect.
    /// </summary>
    public unsafe class SharpenEffect
        : ConvolutionFilterEffect,
          IConfigurableEffect
    {
        private int[,] sharpenWeights = new int[3,3] { { -1, -1, -1 },
                                                       { -1, 20, -1 },
                                                       { -1, -1, -1 } };

        public override void Render(RenderArgs dstArgs, RenderArgs srcArgs, Rectangle roi)
        {           
        	base.RenderConvolutionFilter (sharpenWeights, 0, dstArgs, srcArgs, roi);
        }

        // http://www.photo.net/bboard/q-and-a-fetch-msg.tcl?msg_id=000Qi5
        // This guy (Alan Gibson) stated that sharpening is best done as "2*original - blur"

        // So I create a gaussian blur matrix, then negate all the elements except the center one
        // for the center one I compute this as the sum of all the elements in the matrix minus the
        // value at the center. So:
        // blurMatrix = ...;
        // sharpenMatrix = -blurMatrix;
        // sharpenMatrix[center,center] = sum(blurMatrix) - blurMatrix[center,center]

        void IConfigurableEffect.Render(EffectConfigToken properties, RenderArgs dstArgs, RenderArgs srcArgs, PdnRegion roi)
        {         
        	        	
        	AmountEffectConfigToken token = (AmountEffectConfigToken)properties;

                
            int[,] weights = BlurEffect.CreateGaussianBlurMatrix(1 << (token.Amount - 1));
            int sum = Utility.Sum(weights);
            int center = weights.GetLength(0) / 2;

            for (int i = weights.GetLowerBound(0); i <= weights.GetUpperBound(0); ++i)
            {
                for (int j = weights.GetLowerBound(1); j <= weights.GetUpperBound(1); ++j)
                {
                    if (i == center && j == center)
                    {
                        weights[i,j] = (2 * sum) - weights[i,j];
                    }
                    else
                    {
                        weights[i,j] = -weights[i,j];
                    }
                }
            }
			NormalizeWeightMatrix(weights);
            base.RenderConvolutionFilter (weights, 0, dstArgs, srcArgs, roi);     
        }

    }
}
