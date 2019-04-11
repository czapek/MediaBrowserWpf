using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowserWPF.UserControls.Levels
{
    public class GammaRemap
    {
        private int[] remap;

        private double gamma;           //regular gamma, >= 0.0
        private int grayGamma;       //where gray (127) is mapped to. 0-255  This is what sets the Gamma
        public int[] Remap { get { return remap; } }
        public double PshopGamma { get { return 1 / gamma; } }  //actually 1/gamma (pShop gamma), valid values 0.10 - 9.99
        public double Gamma { get { return gamma; } }

        public double GrayGamma
        {
            set
            {
                ComputeRemap(value);
            }
        }

        public GammaRemap()
        {
            remap = new int[256];
            for (int i = 0; i <= 255; i++)
                remap[i] = i;
            gamma = 1.0;
            grayGamma = 127;
        }

        public static double GetGammFromGray(int grayGamma)
        {
            return (Math.Log10(grayGamma / 255.0D)) / (Math.Log10(127 / 255.0D));
        }

        public static int GetGrayFromGamma(double gamma)
        {
            return (int)Math.Round((Math.Pow(10, gamma * (Math.Log10(127 / 255.0D))) * 255.0D));
        }

        //given the value of the gray input slider, compute the remap array
        private void ComputeRemap(double gray_d)
        {
            //most common gray value, if its already been done, do nothing
            if ((int)Math.Floor(gray_d) == 127 && grayGamma == 127)
                return;
            grayGamma = (int)Math.Floor(gray_d);
            if (grayGamma <= 0) grayGamma = 1;
            gamma = GetGammFromGray(grayGamma);

            for (int i = 0 + 1; i < 256; i++)
            {
                //compute brightness, input adjusted to normalize over black to white
                double inputBrightness = i / 255.0D;
                double outputBrightness = Math.Pow(inputBrightness, gamma);
                remap[i] = (int)Math.Floor(outputBrightness * 255);
            }
        }

        public void GammaAdjustRemapArray(int[] srcRemap)
        {
            for (int i = 0; i < 256; i++)
                srcRemap[i] = remap[srcRemap[i]];
        }
    }
}
