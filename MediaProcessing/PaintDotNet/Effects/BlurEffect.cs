using System;
using System.Drawing;

namespace PaintDotNet.Effects
{
    /// <summary>
    /// Summary description for BlurEffect.
    /// </summary>
    public class BlurEffect
        : ConvolutionFilterEffect, IConfigurableEffect
    {

        private int[,] blurWeights = new int[3,3] { { 1, 2, 1 }, { 2, 4, 2 }, { 1, 2, 1 } };

        public override void Render(RenderArgs dstArgs, RenderArgs srcArgs, Rectangle roi)
        {
            this.RenderConvolutionFilter(blurWeights, 0, dstArgs, srcArgs, roi);
        }

        

        public static int[,] CreateGaussianBlurMatrix(int amount)
        {
            int size = 1 + (amount * 2);
            int center = size / 2;
            int[,] weights = new int[size, size];

            for (int i = 0; i < size; ++i)
            {
                for (int j = 0; j < size; ++j)
                {
                    weights[i,j] = (int)(16 * Math.Sqrt(((j - center) * (j - center)) + ((i - center) * (i - center))));
                }
            }

            int max = 0;
            for (int i = 0; i < size; ++i)
            {
                for (int j = 0; j < size; ++j)
                {
                    if (weights[i,j] > max)
                    {
                        max = weights[i,j];
                    }
                }
            }

            for (int i = 0; i < size; ++i)
            {
                for (int j = 0; j < size; ++j)
                {
                    weights[i,j] = max - weights[i,j];
                }
            }

            return weights;
        }

        void IConfigurableEffect.Render(EffectConfigToken configToken, RenderArgs dstArgs, RenderArgs srcArgs, PdnRegion roi)
        {
            AmountEffectConfigToken bect = (AmountEffectConfigToken)configToken;
            this.RenderConvolutionFilter(CreateGaussianBlurMatrix(bect.Amount), 0, dstArgs, srcArgs, roi);
        }
    }
}
